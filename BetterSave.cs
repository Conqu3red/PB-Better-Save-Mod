using System;
using System.IO;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;


namespace BetterSave
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class BetterSave : PolyTechMod
    {
        public new const string
            PluginGuid = "PolyTech.BetterSave",
            PluginName = "Better Save",
            PluginVersion = "1.0.0";

        public static BetterSave instance;
        public static ConfigEntry<bool> modEnabled;
        public static MethodInfo
            SandboxLoad_LocalSlotClickedCallback,
            SandboxLoad_SlotHoverCallback,
            SandboxLoad_SlotDeleteCallback,
            SandboxLoad_SlotRenameCallback;
        
        public static GameObject FileLoaderPrefab;
        Harmony harmony;
        void Awake()
        {
            //this.repositoryUrl = "https://github.com/Conqu3red/PB-Better-Save-Mod/"; // repo to check for updates from
            if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;

            modEnabled = Config.Bind("Better Save Mod", "modEnabled", true, "Enable Mod");

            modEnabled.SettingChanged += onEnableDisable;

            harmony = new Harmony("PolyTech.BetterSave");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            this.authors = new string[] { "Conqu3red" };

            this.isEnabled = modEnabled.Value;

            PolyTechMain.registerMod(this);
        }

        public void Start()
        {
            // do something idk
        }


        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }
        public override void enableMod()
        {
            modEnabled.Value = true;
        }
        public override void disableMod()
        {
            modEnabled.Value = false;
        }

        public bool shouldRun()
        {
            return this.isEnabled && PolyTechMain.ptfInstance.isEnabled;
        }
        
        [HarmonyPatch(typeof(Main), "InstantiateGameUI")]
        public static class StartupPatch {
            public static void Postfix(){
                SandboxLoad_LocalSlotClickedCallback = typeof(Panel_LoadSandboxLayout).GetMethod("LocalSlotClickedCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxLoad_SlotHoverCallback = typeof(Panel_LoadSandboxLayout).GetMethod("SlotHoverCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxLoad_SlotDeleteCallback = typeof(Panel_LoadSandboxLayout).GetMethod("SlotDeleteCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxLoad_SlotRenameCallback = typeof(Panel_LoadSandboxLayout).GetMethod("SlotRenameCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                FileLoaderPrefab = GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader.gameObject; // maybe Instantiate?
            }
        }

        public class SlotFolder {
            public GameObject FileLoaderObj;
            public Panel_FileLoader m_FileLoader;
            public Panel_FileLoader m_ParentFileLoader = null;

            public SlotFolder()
            {
                FileLoaderObj = Instantiate(FileLoaderPrefab, FileLoaderPrefab.transform.parent);
                FileLoaderObj.transform.position = FileLoaderPrefab.transform.position;
                FileLoaderObj.transform.rotation = FileLoaderPrefab.transform.rotation;
                FileLoaderObj.SetActive(false);
                m_FileLoader = FileLoaderObj.GetComponent<Panel_FileLoader>();
                m_FileLoader.DestroySlots();
            }

            public SlotFolder(Panel_FileLoader parent) : this() {
                m_ParentFileLoader = parent;
            }
        }

        /*
            Some notes:
            at the top add:
                current folder
                <BACK>

            put at top via "\u200B"

            *MAYBE* instead of recursively indexing *all* folders straight away, only index depth one ane
                    index the more when you open a folder??
        */

        // TODO: create own FileLoader's for each folder
        public static class LoadHandler
        {
            public static DateTime FarFuture = new DateTime(9999, 1, 1);
            public static Dictionary<FileSlot, SlotFolder> m_FolderLookup = new Dictionary<FileSlot, SlotFolder>();
            public static FileSlot.OnHoverChangeDelegate hoverDelegate = new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover}));
            public static void setEditButtons(FileSlot slot, bool v)
            {
                slot.m_RenameButton.gameObject.SetActive(v);
                slot.m_DeleteButton.gameObject.SetActive(v);
            }
            public static void createHeadingSlots(SlotFolder folder, string folderPath)
            {
                FileSlot folderListing = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks,
                    "\u200B\u200B" + folderPath,
                    (slot) => {},
                    (slot, hover) => {}
                );
                
                setEditButtons(folderListing, false);

                FileSlot backButton = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks - 1,
                    "\u200B<BACK>",
                    (slot) => { OpenLoader(folder.m_ParentFileLoader, GameUI.m_Instance.m_LoadSandboxLayout); },
                    hoverDelegate
                );

                setEditButtons(backButton, false);
            }
            public static void LoadDir(DirectoryInfo directoryInfo, string folderPath, Panel_FileLoader parentFolder, int depthLeft = 1)
            {
                if (parentFolder == null)
                {
                    Debug.LogError("parentFolder is null");
                    return;
                }
                instance.Logger.LogInfo($"scanning dir '{directoryInfo.Name}'");
                //Debug.Log($"{directoryInfo == null} {parentFolder == null} {GameUI.m_Instance.m_LoadSandboxLayout == null}");
                
                var LocalSlotClickedCallback = new FileSlot.OnClickedDelegate(slot => {
                    if (!slot) return;
                    InterfaceAudio.Play("ui_menu_select");
                    LoadHandler.SetContents(slot, GameUI.m_Instance.m_LoadSandboxLayout); // "open" the folder
                });

                var SlotHoverCallback = new FileSlot.OnHoverChangeDelegate((slot, hover) => {
                    SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover});
                });

                var SlotDeleteCallback = new FileSlot.OnClickedDelegate(slot => {}); // TODO!!!
                var SlotRenameCallback = new FileSlot.OnClickedDelegate(slot => {}); // TODO!!!

                // make FolderSlot for this folder
                FileSlot folderSlot = parentFolder.AddSlot(
                    Path.Combine(folderPath, directoryInfo.Name),
                    directoryInfo.LastWriteTime.Ticks,
                    $"[{directoryInfo.Name}]",
                    LocalSlotClickedCallback,
                    SlotHoverCallback
                );

                folderPath = Path.Combine(folderPath, directoryInfo.Name);

                if (folderSlot)
                {
                    folderSlot.SetOnDeleteCallback(SlotDeleteCallback); // TODO
                    folderSlot.SetOnRenameCallback(SlotRenameCallback); // TODO

                    m_FolderLookup[folderSlot] = new SlotFolder(parentFolder); // construct folder object for storage
                    
                    //if (depthLeft == 0) return;

                    // create header info stuff
                    createHeadingSlots(m_FolderLookup[folderSlot], folderPath);

                    foreach (FileInfo fileInfo in directoryInfo.GetFiles("*" + SandboxLayout.SAVE_EXTENSION))
                    {
                        //instance.Logger.LogInfo($"found slot {fileInfo.Name}");
                        // standard processing for regular "slot" except it has to be put into the current folder
                        FileSlot fileSlot = m_FolderLookup[folderSlot].m_FileLoader.AddSlot(
                            Path.Combine(folderPath, fileInfo.Name),
                            fileInfo.LastWriteTime.Ticks,
                            Path.GetFileNameWithoutExtension(fileInfo.Name),
                            new FileSlot.OnClickedDelegate(slot => SandboxLoad_LocalSlotClickedCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot})),
                            new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover}))
                        );
                        
                        if (fileSlot)
                        {
                            fileSlot.SetOnDeleteCallback(new FileSlot.OnClickedDelegate(slot => SandboxLoad_SlotDeleteCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot}))); // TODO
                            fileSlot.SetOnRenameCallback(new FileSlot.OnClickedDelegate(slot => SandboxLoad_SlotRenameCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot}))); // TODO
                        }
                    }

                    foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
                    {
                        // recusively create other folders
                        LoadDir(dir, folderPath, m_FolderLookup[folderSlot].m_FileLoader); // recursively traverse file tree
                    }
                } else {
                    Debug.Log("folderSlot is null!");
                }
            }

            public static void SetContents(FileSlot slot, Panel_LoadSandboxLayout __instance)
            {
                __instance.m_FileLoader.gameObject.SetActive(false);
                __instance.m_FileLoader = m_FolderLookup[slot].m_FileLoader;
                __instance.m_FileLoader.gameObject.SetActive(true);
                instance.Logger.LogInfo($"Opened folder '{slot.m_FileName}' ({__instance.m_FileLoader.gameObject})");
            }

            public static void OpenLoader(Panel_FileLoader fileLoader, Panel_LoadSandboxLayout __instance)
            {
                __instance.m_FileLoader.gameObject.SetActive(false);
                __instance.m_FileLoader = fileLoader;
                __instance.m_FileLoader.gameObject.SetActive(true);
                instance.Logger.LogInfo($"returned to previous folder");
            }
        }

        [HarmonyPatch(typeof(Panel_LoadSandboxLayout), "PopulateLocalSandboxSlots")]
        public static class PopulatePatch
        {
            public static void Postfix(Panel_LoadSandboxLayout __instance)
            {
                if (!instance.shouldRun()) return;

                // load directory up
                string savePath = SandboxLayout.GetSavePath();
                if (!Directory.Exists(savePath))
                {
                    return;
                }
                DirectoryInfo directoryInfo = new DirectoryInfo(savePath);
                if (directoryInfo == null)
                {
                    return;
                }

                foreach (DirectoryInfo info in directoryInfo.GetDirectories())
                    LoadHandler.LoadDir(info, "", __instance.m_FileLoader);
            }
        }
    }
}