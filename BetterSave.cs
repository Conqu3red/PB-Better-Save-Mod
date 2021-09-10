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
            SandboxLoad_SlotRenameCallback,
            
            SandboxSave_SlotClickedCallback,
            SandboxSave_SlotHoverCallback,
            SandboxSave_SlotDeleteCallback,
            SandboxSave_SlotRenameCallback;
        
        public static GameObject FileLoaderPrefab, FileSaverPrefab;
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
                
                SandboxSave_SlotClickedCallback = typeof(Panel_SaveSandboxLayout).GetMethod("SlotClickedCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxSave_SlotHoverCallback = typeof(Panel_SaveSandboxLayout).GetMethod("SlotHoverCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxSave_SlotDeleteCallback = typeof(Panel_SaveSandboxLayout).GetMethod("SlotDeleteCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                SandboxSave_SlotRenameCallback = typeof(Panel_LoadSandboxLayout).GetMethod("SlotRenameCallback", BindingFlags.Instance | BindingFlags.NonPublic);
                

                FileLoaderPrefab = Instantiate(GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader.gameObject, GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader.gameObject.transform.parent); // maybe Instantiate?
                FileLoaderPrefab.transform.position = GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader.gameObject.transform.position;
                FileLoaderPrefab.transform.rotation = GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader.gameObject.transform.rotation;
                FileLoaderPrefab.SetActive(false);
                Panel_FileLoader fileLoader = FileLoaderPrefab.GetComponent<Panel_FileLoader>();
                fileLoader.DestroySlots();

                FileSaverPrefab = Instantiate(GameUI.m_Instance.m_SaveSandboxLayout.m_FileLoader.gameObject, GameUI.m_Instance.m_SaveSandboxLayout.m_FileLoader.gameObject.transform.parent); // maybe Instantiate?
                FileSaverPrefab.transform.position = GameUI.m_Instance.m_SaveSandboxLayout.m_FileLoader.gameObject.transform.position;
                FileSaverPrefab.transform.rotation = GameUI.m_Instance.m_SaveSandboxLayout.m_FileLoader.gameObject.transform.rotation;
                FileSaverPrefab.SetActive(false);
                Panel_FileLoader fileSaver = FileSaverPrefab.GetComponent<Panel_FileLoader>();
                fileSaver.DestroySlots();
            }
        }

        public class SlotFolder {
            public GameObject FileLoaderObj;
            public Panel_FileLoader m_FileLoader;
            public Panel_FileLoader m_ParentFileLoader = null;
            public DirectoryInfo directoryInfo;
            public string relativePath;
            public bool isPopulated = false;

            public SlotFolder(bool isSave = false)
            {
                var p = isSave ? FileSaverPrefab : FileLoaderPrefab;
                FileLoaderObj = Instantiate(p, p.transform.parent);
                FileLoaderObj.transform.position = p.transform.position;
                FileLoaderObj.transform.rotation = p.transform.rotation;
                FileLoaderObj.SetActive(false);
                m_FileLoader = FileLoaderObj.GetComponent<Panel_FileLoader>();
            }

            public SlotFolder(Panel_FileLoader parent, DirectoryInfo directoryInfo, string relativePath, bool isSave = false) : this(isSave) {
                this.m_ParentFileLoader = parent;
                this.directoryInfo = directoryInfo;
                this.relativePath = relativePath;
            }
        }


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
                    $"\u200B\u200B\u200B<b>{folderPath}</b>",
                    (slot) => {},
                    (slot, hover) => {}
                );
                
                setEditButtons(folderListing, false);

                FileSlot refreshButton = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks - 1,
                    "\u200B\u200B<color=yellow>Refresh</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        folder.m_FileLoader.DestroySlots();
                        PopulateFolder(folder);
                    },
                    hoverDelegate
                );
                
                setEditButtons(folderListing, false);

                FileSlot backButton = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks - 2,
                    "\u200B<color=yellow>Back</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        OpenLoader(folder.m_ParentFileLoader, GameUI.m_Instance.m_LoadSandboxLayout);
                    },
                    hoverDelegate
                );

                setEditButtons(backButton, false);
            }

            public static SlotFolder CreateFolder(DirectoryInfo directoryInfo, string folderPath, Panel_FileLoader parentFolder)
            {
                if (parentFolder == null)
                {
                    Debug.LogError("parentFolder is null");
                    return null;
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
                    $"<b>[{directoryInfo.Name}]</b>",
                    LocalSlotClickedCallback,
                    SlotHoverCallback
                );

                folderPath = Path.Combine(folderPath, directoryInfo.Name);

                if (folderSlot)
                {
                    folderSlot.SetOnDeleteCallback(SlotDeleteCallback); // TODO
                    folderSlot.SetOnRenameCallback(SlotRenameCallback); // TODO

                    setEditButtons(folderSlot, false);

                    m_FolderLookup[folderSlot] = new SlotFolder(parentFolder, directoryInfo, folderPath); // construct folder object for storage
                    return m_FolderLookup[folderSlot];
                }
                return null;
            }
            public static void PopulateFolder(SlotFolder slotFolder)
            {
                // create header info stuff
                createHeadingSlots(slotFolder, slotFolder.relativePath);

                foreach (FileInfo fileInfo in slotFolder.directoryInfo.GetFiles("*" + SandboxLayout.SAVE_EXTENSION))
                {
                    //instance.Logger.LogInfo($"found slot {fileInfo.Name}");
                    // standard processing for regular "slot" except it has to be put into the current folder
                    FileSlot fileSlot = slotFolder.m_FileLoader.AddSlot(
                        Path.Combine(slotFolder.relativePath, fileInfo.Name),
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

                foreach (DirectoryInfo dir in slotFolder.directoryInfo.GetDirectories())
                {
                    // create links to other folders
                    CreateFolder(dir, slotFolder.relativePath, slotFolder.m_FileLoader);
                }
                slotFolder.isPopulated = true;
            }



            public static void SetContents(FileSlot slot, Panel_LoadSandboxLayout __instance)
            {
                var slotFolder = m_FolderLookup[slot];
                __instance.m_FileLoader.gameObject.SetActive(false);
                __instance.m_FileLoader = slotFolder.m_FileLoader;
                __instance.m_FileLoader.gameObject.SetActive(true);
                if (!slotFolder.isPopulated)
                    PopulateFolder(slotFolder);
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
        public static class LoadPopulatePatch
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
                    LoadHandler.CreateFolder(info, "", __instance.m_FileLoader);
            }
        }

        public static class SaveHandler
        {
            public static DateTime FarFuture = new DateTime(9999, 1, 1);
            public static Dictionary<FileSlot, SlotFolder> m_FolderLookup = new Dictionary<FileSlot, SlotFolder>();
            public static FileSlot.OnHoverChangeDelegate hoverDelegate = new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover}));
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
                    $"\u200B\u200B\u200B<b>{folderPath}</b>",
                    (slot) => {},
                    (slot, hover) => {}
                );
                
                setEditButtons(folderListing, false);

                FileSlot refreshButton = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks - 1,
                    "\u200B\u200B<color=yellow>Refresh</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        folder.m_FileLoader.DestroySlots();
                        PopulateFolder(folder);
                    },
                    hoverDelegate
                );
                
                setEditButtons(folderListing, false);

                FileSlot backButton = folder.m_FileLoader.AddSlot(
                    folderPath,
                    FarFuture.Ticks - 2,
                    "\u200B<color=yellow>Back</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        OpenLoader(folder.m_ParentFileLoader, GameUI.m_Instance.m_SaveSandboxLayout);
                    },
                    hoverDelegate
                );

                setEditButtons(backButton, false);

                // TODO: add [new folder] button and add [new layout button]

                FileSlot newFolderBtn = folder.m_FileLoader.AddSlot(
                    "",
                    FarFuture.Ticks - 3,
                    "\u200B<color=yellow>New Folder</color>",
                    (slot) => {
                        PopupInputField.Display(
                            "Enter a folder name",
                            string.Empty,
                            (name) => SaveHandler.MakeNewFolder(name, folderPath, folder.m_FileLoader)
                        );
                        
                    },
                    hoverDelegate
                );

                setEditButtons(newFolderBtn, false);

                FileSlot newLayoutBtn = folder.m_FileLoader.AddSlot(
                    "",
                    FarFuture.Ticks - 3,
                    "\u200B<color=yellow>" + Localize.Get("UI_NEW_SANDBOX_LAYOUT") + "</color>",
                    (slot) => {
                        PopupInputField.Display(
                            Localize.Get("UI_INPUTFIELD_SANDBOX_LAYOUT_NAME"),
                            string.Empty,
                            (name) => GameUI.m_Instance.m_SaveSandboxLayout.SaveNew(Path.Combine(folderPath, name))
                        );
                        
                    },
                    hoverDelegate
                );

            }

            public static SlotFolder CreateFolder(DirectoryInfo directoryInfo, string folderPath, Panel_FileLoader parentFolder)
            {
                if (parentFolder == null)
                {
                    Debug.LogError("parentFolder is null");
                    return null;
                }
                instance.Logger.LogInfo($"scanning dir '{directoryInfo.Name}'");
                //Debug.Log($"{directoryInfo == null} {parentFolder == null} {GameUI.m_Instance.m_SaveSandboxLayout == null}");
                
                var LocalSlotClickedCallback = new FileSlot.OnClickedDelegate(slot => {
                    if (!slot) return;
                    InterfaceAudio.Play("ui_menu_select");
                    SetContents(slot, GameUI.m_Instance.m_SaveSandboxLayout); // "open" the folder
                });

                var SlotHoverCallback = new FileSlot.OnHoverChangeDelegate((slot, hover) => {
                    SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover});
                });

                var SlotDeleteCallback = new FileSlot.OnClickedDelegate(slot => {}); // TODO!!!
                var SlotRenameCallback = new FileSlot.OnClickedDelegate(slot => {}); // TODO!!!

                // make FolderSlot for this folder
                FileSlot folderSlot = parentFolder.AddSlot(
                    Path.Combine(folderPath, directoryInfo.Name),
                    directoryInfo.LastWriteTime.Ticks,
                    $"<b>[{directoryInfo.Name}]</b>",
                    LocalSlotClickedCallback,
                    SlotHoverCallback
                );

                folderPath = Path.Combine(folderPath, directoryInfo.Name);

                if (folderSlot)
                {
                    folderSlot.SetOnDeleteCallback(SlotDeleteCallback); // TODO
                    folderSlot.SetOnRenameCallback(SlotRenameCallback); // TODO

                    setEditButtons(folderSlot, false);

                    m_FolderLookup[folderSlot] = new SlotFolder(parentFolder, directoryInfo, folderPath, isSave: true); // construct folder object for storage
                    return m_FolderLookup[folderSlot];
                }
                return null;
            }
            public static void PopulateFolder(SlotFolder slotFolder)
            {
                // create header info stuff
                createHeadingSlots(slotFolder, slotFolder.relativePath);

                foreach (FileInfo fileInfo in slotFolder.directoryInfo.GetFiles("*" + SandboxLayout.SAVE_EXTENSION))
                {
                    //instance.Logger.LogInfo($"found slot {fileInfo.Name}");
                    // standard processing for regular "slot" except it has to be put into the current folder
                    FileSlot fileSlot = slotFolder.m_FileLoader.AddSlot(
                        Path.Combine(slotFolder.relativePath, fileInfo.Name),
                        fileInfo.LastWriteTime.Ticks,
                        Path.GetFileNameWithoutExtension(fileInfo.Name),
                        slot => {
                            slot.m_DisplayName.text = slot.m_FileName;
                            SandboxSave_SlotClickedCallback.Invoke(
                                GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot}
                            );
                        },
                        new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover}))
                    );
                    
                    if (fileSlot)
                    {
                        fileSlot.SetOnDeleteCallback(new FileSlot.OnClickedDelegate(slot => SandboxSave_SlotDeleteCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot}))); // TODO
                        fileSlot.SetOnRenameCallback(new FileSlot.OnClickedDelegate(slot => SandboxSave_SlotRenameCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot}))); // TODO
                    }
                }

                foreach (DirectoryInfo dir in slotFolder.directoryInfo.GetDirectories())
                {
                    // create links to other folders
                    CreateFolder(dir, slotFolder.relativePath, slotFolder.m_FileLoader);
                }
                slotFolder.isPopulated = true;
            }



            public static void SetContents(FileSlot slot, Panel_SaveSandboxLayout __instance)
            {
                var slotFolder = m_FolderLookup[slot];
                __instance.m_FileLoader.gameObject.SetActive(false);
                __instance.m_FileLoader = slotFolder.m_FileLoader;
                __instance.m_FileLoader.gameObject.SetActive(true);
                if (!slotFolder.isPopulated)
                    PopulateFolder(slotFolder);
                instance.Logger.LogInfo($"Opened folder '{slot.m_FileName}' ({__instance.m_FileLoader.gameObject})");
            }

            public static void OpenLoader(Panel_FileLoader fileLoader, Panel_SaveSandboxLayout __instance)
            {
                __instance.m_FileLoader.gameObject.SetActive(false);
                __instance.m_FileLoader = fileLoader;
                __instance.m_FileLoader.gameObject.SetActive(true);
                instance.Logger.LogInfo($"returned to previous folder");
            }

            public static void MakeNewFolder(string name, string path, Panel_FileLoader parentFolder){
                string loc = Path.Combine(SandboxLayout.GetSavePath(), path, name);
                if (!Directory.Exists(loc))
                {
                    var info = Directory.CreateDirectory(loc);
                    CreateFolder(info, path, parentFolder);
                }
            }
        }

        [HarmonyPatch(typeof(Panel_SaveSandboxLayout), "PopulateSlots")]
        public static class SavePopulatePatch
        {
            public static void Postfix(Panel_SaveSandboxLayout __instance)
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

                // TODO: fix ordering, folders and new folder btn
                //       are at the bottom of the list solve by doing a postfix maybe?

                List<FileSlot> actualSlots = __instance.m_FileLoader.m_Slots.GetRange(1, __instance.m_FileLoader.m_Slots.Count - 1);
                __instance.m_FileLoader.m_Slots.RemoveRange(1, __instance.m_FileLoader.m_Slots.Count - 1);

                __instance.m_FileLoader.m_Slots[0].m_DisplayName.text = "<color=yellow>" + Localize.Get("UI_NEW_SANDBOX_LAYOUT") + "</color>";
                
                // create [add folder] button
                string text = "<color=yellow>New Folder</color>";
                __instance.m_FileLoader.AddSlot(
                    text, 0L, text,
                    slot => {
                        PopupInputField.Display(
                            "Enter a folder name",
                            string.Empty,
                            (name) => SaveHandler.MakeNewFolder(name, "", __instance.m_FileLoader)
                        );
                    },
                    SaveHandler.hoverDelegate
                );
	            

                foreach (DirectoryInfo info in directoryInfo.GetDirectories())
                    SaveHandler.CreateFolder(info, "", __instance.m_FileLoader);
                
                // add back the actual files
                __instance.m_FileLoader.m_Slots.AddRange(actualSlots);
                // reorder
                for (int i = 0; i < __instance.m_FileLoader.m_Slots.Count; i++)
		        {
			        __instance.m_FileLoader.m_Slots[i].transform.SetSiblingIndex(i);
		        }
            }
        }
    }
}