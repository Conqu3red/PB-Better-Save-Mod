using System;
using System.IO;
using BepInEx;
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
            PluginVersion = "1.0.1";

        public static BetterSave instance;
        public static ConfigEntry<bool> modEnabled, openLastFolder;
        public static ConfigEntry<string> sandboxLocation;
        public static MethodInfo
            SandboxLoad_LocalSlotClickedCallback,
            SandboxLoad_SlotHoverCallback,
            SandboxLoad_SlotDeleteCallback,
            SandboxLoad_SlotRenameCallback,
            
            SandboxSave_SlotClickedCallback,
            SandboxSave_SlotHoverCallback,
            SandboxSave_SlotDeleteCallback,
            SandboxSave_SlotRenameCallback;
        Harmony harmony;
        void Awake()
        {
            //this.repositoryUrl = "https://github.com/Conqu3red/PB-Better-Save-Mod/"; // repo to check for updates from
            if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;

            modEnabled = Config.Bind("Better Save Mod", "Mod Enabled", true, "Enable Mod");
            openLastFolder = Config.Bind("Better Save Mod", "Open Last Folder", true);
            sandboxLocation = Config.Bind("Better Save Mod", "Custom Sandbox Location", "");
            
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
            }
        }

        public static string GetInitialSavePath() {
            if (
                instance.shouldRun()
                && !string.IsNullOrEmpty(sandboxLocation.Value)
                && Directory.Exists(sandboxLocation.Value)
            ) {
                return sandboxLocation.Value;
            }
            return SandboxLayout.GetSavePath();
        }

        public class SlotFolder {
            public bool isSave = false;
            public static string displayFormat = "<b>[{0}]</b>";
            public DirectoryInfo directoryInfo;
            public string absolutePath => directoryInfo.FullName;
            public string Name => directoryInfo.Name;
            public SlotFolder Parent => GetParent();
            public SlotFolder(DirectoryInfo directoryInfo, bool isSave = false) {
                this.isSave = isSave;
                this.directoryInfo = directoryInfo;
            }
            public SlotFolder(string directory, bool isSave = false) {
                this.isSave = isSave;
                this.directoryInfo = new DirectoryInfo(directory);
            }

            private SlotFolder GetParent() {
                var f = directoryInfo.Parent;
                return new SlotFolder(f, isSave);
            }
        }

        public static void RenameSlot(FileSlot slot) {
            PopupInputField.Display(
                Localize.Get("POPUP_RENAME_SLOT", slot.m_DisplayName.text),
                slot.m_DisplayName.text,
                name => {
                    if (string.IsNullOrEmpty(name)) return;
                    string oldPath = slot.m_FileName;
                    string newPath = Path.Combine(
                        Path.GetDirectoryName(slot.m_FileName),
                        (Path.GetExtension(name) == SandboxLayout.SAVE_EXTENSION)
                        ? name
                        : (name + SandboxLayout.SAVE_EXTENSION)
                    );

                    Utils.RenameFile(oldPath, newPath);

                    slot.m_DisplayName.text = name;
                    
                } 
            );
        }
        public static void DeleteSlot(FileSlot slot, Panel_FileLoader fileLoader) {
            PopUpMessage.Display(
                Localize.Get("POPUP_DELETE_SLOT", slot.m_DisplayName.text),
                () => {
                    Utils.DeleteFile(slot.m_FileName);
                    fileLoader.DeleteSlot(slot);
                }
            );
        }

        public static void RenameFolder(FileSlot slot) {
            var dirInfo = new DirectoryInfo(slot.m_FileName);

            PopupInputField.Display(
                Localize.Get("POPUP_RENAME_SLOT", slot.m_DisplayName.text),
                dirInfo.Name,
                name => {
                    if (string.IsNullOrEmpty(name)) return;
                    string newPath = Path.Combine(
                        dirInfo.Parent.FullName,
                        name
                    );

                    instance.Logger.LogInfo($"Old: {dirInfo.FullName}");
                    instance.Logger.LogInfo($"New: {newPath}");

                    if (Directory.Exists(newPath)) {
                        PopUpWarning.Display("That folder name already exists.");
                        return;
                    }

                    dirInfo.MoveTo(newPath);

                    slot.m_FileName = newPath;
                    slot.m_DisplayName.text = string.Format(SlotFolder.displayFormat, name);
                } 
            );
        }

        public static void DeleteFolder(FileSlot slot, Panel_FileLoader fileLoader) {
            var dirInfo = new DirectoryInfo(slot.m_FileName);
            PopUpMessage.Display(
                Localize.Get("POPUP_DELETE_SLOT", slot.m_DisplayName.text),
                () => {
                    dirInfo.Delete();
                    fileLoader.DeleteSlot(slot);
                }
            );
        }

        public static void setEditButtons(FileSlot slot, bool v)
        {
            slot.m_RenameButton.gameObject.SetActive(v);
            slot.m_DeleteButton.gameObject.SetActive(v);
        }

        public static void changePos(FileSlot slot, Panel_FileLoader fileLoader, int index) {
            fileLoader.m_Slots.Remove(slot);
            fileLoader.m_Slots.Insert(index, slot);
            for (int i = 0; i < fileLoader.m_Slots.Count; i++)
            {
                fileLoader.m_Slots[i].transform.SetSiblingIndex(i);
            }
        }

        public static class LoadHandler
        {
            public static DateTime FarFuture = new DateTime(9999, 1, 1);
            public static SlotFolder m_OpenFolder = null;
            public static FileSlot.OnHoverChangeDelegate hoverDelegate = new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover}));
            public static Panel_FileLoader m_FileLoader => GameUI.m_Instance.m_LoadSandboxLayout.m_FileLoader;
            public static void createHeadingSlots(SlotFolder folder)
            {
                FileSlot folderListing = m_FileLoader.AddSlot(
                    folder.absolutePath,
                    FarFuture.Ticks,
                    $"\u200B\u200B\u200B<b>{folder.Name}</b>",
                    (slot) => {},
                    (slot, hover) => {}
                );

                changePos(folderListing, m_FileLoader, 0);
                setEditButtons(folderListing, false);

                FileSlot refreshButton = m_FileLoader.AddSlot(
                    folder.absolutePath,
                    FarFuture.Ticks - 1,
                    "\u200B\u200B<color=yellow>Refresh</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        Populate(folder);
                    },
                    hoverDelegate
                );
                
                changePos(refreshButton, m_FileLoader, 1);
                setEditButtons(folderListing, false);

                FileSlot backButton = m_FileLoader.AddSlot(
                    folder.absolutePath,
                    FarFuture.Ticks - 2,
                    "\u200B<color=yellow>Back</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        // TODO: OpenFolder(*parent path*)
                        if (folder.Parent.directoryInfo != null)
                            Populate(folder.Parent);
                    },
                    hoverDelegate
                );

                changePos(backButton, m_FileLoader, 2);
                setEditButtons(backButton, false);
            }

            public static FileSlot CreateFileSlotFromFolder(SlotFolder slotFolder)
            {
                if (slotFolder == null)
                {
                    Debug.LogError("slotFolder is null");
                    return null;
                }
                
                var FolderOpenedCallback = new FileSlot.OnClickedDelegate(slot => {
                    if (!slot) return;
                    InterfaceAudio.Play("ui_menu_select");
                    // FIXME
                    var folder = new SlotFolder(new DirectoryInfo(slot.m_FileName));
                    Populate(folder);
                    m_OpenFolder = slotFolder;
                });

                var FolderHoverCallback = new FileSlot.OnHoverChangeDelegate((slot, hover) => {
                    SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover});
                });

                // make FolderSlot for this folder
                FileSlot folderSlot = m_FileLoader.AddSlot(
                    slotFolder.absolutePath,
                    slotFolder.directoryInfo.LastWriteTime.Ticks,
                    string.Format(SlotFolder.displayFormat, slotFolder.Name),
                    FolderOpenedCallback,
                    FolderHoverCallback
                );

                if (folderSlot)
                {
                    folderSlot.SetOnDeleteCallback(slot => DeleteFolder(slot, m_FileLoader));
                    folderSlot.SetOnRenameCallback(slot => RenameFolder(slot));

                    setEditButtons(folderSlot, true);
                    return folderSlot;
                }
                return null;
            }
            public static void Populate(SlotFolder slotFolder)
            {
                m_OpenFolder = slotFolder;
                m_FileLoader.DestroySlots();

                foreach (FileInfo fileInfo in slotFolder.directoryInfo.EnumerateFiles("*" + SandboxLayout.SAVE_EXTENSION))
                {
                    //instance.Logger.LogInfo($"found slot {fileInfo.Name}");
                    // standard processing for regular "slot" except it has to be put into the current folder
                    FileSlot fileSlot = m_FileLoader.AddSlot(
                        Path.Combine(slotFolder.absolutePath, fileInfo.Name),
                        fileInfo.LastWriteTime.Ticks,
                        Path.GetFileNameWithoutExtension(fileInfo.Name),
                        new FileSlot.OnClickedDelegate(slot => SandboxLoad_LocalSlotClickedCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot})),
                        new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxLoad_SlotHoverCallback.Invoke(GameUI.m_Instance.m_LoadSandboxLayout, new object[] {slot, hover}))
                    );
                    
                    if (fileSlot)
                    {
                        fileSlot.SetOnDeleteCallback(slot => DeleteSlot(slot, m_FileLoader));
                        fileSlot.SetOnRenameCallback(slot => RenameSlot(slot));
                    }
                }

                foreach (DirectoryInfo dir in slotFolder.directoryInfo.EnumerateDirectories())
                {
                    // create links to other folders
                    CreateFileSlotFromFolder(new SlotFolder(dir));
                }

                if (Profile.m_SortSandboxLayoutsByDate)
                {
                    m_FileLoader.SortByDate();
                }
                else {
                    m_FileLoader.SortAlphabetically();
                }

                // create header info stuff
                createHeadingSlots(slotFolder);
                m_FileLoader.SelectSlotIndex(1);
            }
        }

        [HarmonyPatch(typeof(Panel_LoadSandboxLayout), "PopulateLocalSandboxSlots")]
        public static class LoadPopulatePatch
        {
            public static void Postfix(Panel_LoadSandboxLayout __instance)
            {
                if (!instance.shouldRun()) return;

                if (LoadHandler.m_OpenFolder != null && openLastFolder.Value) {
                    LoadHandler.Populate(LoadHandler.m_OpenFolder); // "open" previous folder
                    return;
                }

                // default folder
                string savePath = GetInitialSavePath();
                if (!Directory.Exists(savePath))
                {
                    return;
                }
                DirectoryInfo directoryInfo = new DirectoryInfo(savePath);
                if (directoryInfo == null)
                {
                    return;
                }
                
                LoadHandler.Populate(new SlotFolder(directoryInfo));
                
            }
        }

        [HarmonyPatch(typeof(Panel_LoadSandboxLayout), "Sort")]
        public static class SortPatch {
            public static bool Prefix() {
                if (instance.shouldRun()) {
                    if (LoadHandler.m_OpenFolder != null)
                        LoadHandler.Populate(LoadHandler.m_OpenFolder);
                    return false;
                }
                return true;
            } 
        }

        [HarmonyPatch(typeof(Panel_LoadSandboxLayout), "LocalSlotClickedCallback")]
        public static class ClickedViaReturnKeyPatch {
            private static bool flag = false;
            public static bool Prefix(FileSlot slot) {
                //instance.Logger.LogInfo($"LocalSlotClickedCallback called {flag}");
                if (instance.shouldRun()) {
                    if (flag) {
                        flag = false;
                        return true;
                    }
                    flag = true;
                    slot.m_FileSlotButton.onClick.Invoke();
                    flag = false;
                    return false;
                }

                return true;
            }
        }

        public static class SaveHandler
        {
            public static DateTime FarFuture = new DateTime(9999, 1, 1);
            public static SlotFolder m_OpenFolder = null;
            public static FileSlot.OnHoverChangeDelegate hoverDelegate = new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover}));
            public static Panel_FileLoader m_FileLoader => GameUI.m_Instance.m_SaveSandboxLayout.m_FileLoader;
            public static void createHeadingSlots(SlotFolder folder)
            {
                FileSlot folderListing = m_FileLoader.AddSlot(
                    folder.absolutePath,
                    FarFuture.Ticks,
                    $"\u200B\u200B\u200B<b>{folder.Name}</b>",
                    (slot) => {},
                    (slot, hover) => {}
                );
                
                changePos(folderListing, m_FileLoader, 0);
                setEditButtons(folderListing, false);

                FileSlot refreshButton = m_FileLoader.AddSlot(
                    folder.absolutePath,
                    FarFuture.Ticks - 1,
                    "\u200B\u200B<color=yellow>Refresh</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        Populate(folder);
                    },
                    hoverDelegate
                );
                
                changePos(refreshButton, m_FileLoader, 1);
                setEditButtons(refreshButton, false);

                FileSlot backButton = m_FileLoader.AddSlot(
                    folder.Name,
                    FarFuture.Ticks - 2,
                    "\u200B<color=yellow>Back</color>",
                    (slot) => {
                        InterfaceAudio.Play("ui_menu_select");
                        if (folder.Parent.directoryInfo != null)
                            Populate(folder.Parent);
                    },
                    hoverDelegate
                );

                changePos(backButton, m_FileLoader, 2);
                setEditButtons(backButton, false);

                FileSlot newFolderBtn = m_FileLoader.AddSlot(
                    "",
                    FarFuture.Ticks - 3,
                    "\u200B<color=yellow>New Folder</color>",
                    (slot) => {
                        PopupInputField.Display(
                            "Enter a folder name",
                            string.Empty,
                            (name) => SaveHandler.MakeNewFolder(name, folder.absolutePath, m_FileLoader)
                        );
                        
                    },
                    hoverDelegate
                );

                changePos(newFolderBtn, m_FileLoader, 3);
                setEditButtons(newFolderBtn, false);

                FileSlot newLayoutBtn = m_FileLoader.AddSlot(
                    "",
                    FarFuture.Ticks - 3,
                    "\u200B<color=yellow>" + Localize.Get("UI_NEW_SANDBOX_LAYOUT") + "</color>",
                    (slot) => {
                        PopupInputField.Display(
                            Localize.Get("UI_INPUTFIELD_SANDBOX_LAYOUT_NAME"),
                            string.Empty,
                            (name) => SaveNewLayout(Path.Combine(folder.absolutePath, name))
                        );
                        
                    },
                    hoverDelegate
                );

                changePos(newLayoutBtn, m_FileLoader, 4);
                setEditButtons(newLayoutBtn, false);

            }

            public static FileSlot CreateFileSlotFromFolder(SlotFolder slotFolder)
            {
                if (slotFolder == null)
                {
                    Debug.LogError("slotFolder is null");
                    return null;
                }
                
                var FolderOpenedCallback = new FileSlot.OnClickedDelegate(slot => {
                    if (!slot) return;
                    InterfaceAudio.Play("ui_menu_select");
                    var folder = new SlotFolder(slot.m_FileName);
                    Populate(folder); // "open" the folder
                    m_OpenFolder = folder;
                });

                var FolderHoverCallback = new FileSlot.OnHoverChangeDelegate((slot, hover) => {
                    SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover});
                });

                // make FolderSlot for this folder
                FileSlot folderSlot = m_FileLoader.AddSlot(
                    slotFolder.absolutePath,
                    slotFolder.directoryInfo.LastWriteTime.Ticks,
                    string.Format(SlotFolder.displayFormat, slotFolder.Name),
                    FolderOpenedCallback,
                    FolderHoverCallback
                );

                if (folderSlot)
                {
                    folderSlot.SetOnDeleteCallback(slot => DeleteFolder(slot, m_FileLoader));
                    folderSlot.SetOnRenameCallback(slot => RenameFolder(slot));

                    setEditButtons(folderSlot, true);

                    return folderSlot;
                }
                return null;
            }
            public static void Populate(SlotFolder slotFolder)
            {
                m_OpenFolder = slotFolder;
                m_FileLoader.DestroySlots();

                foreach (FileInfo fileInfo in slotFolder.directoryInfo.EnumerateFiles("*" + SandboxLayout.SAVE_EXTENSION))
                {
                    //instance.Logger.LogInfo($"found slot {fileInfo.Name}");
                    // standard processing for regular "slot" except it has to be put into the current folder
                    FileSlot fileSlot = m_FileLoader.AddSlot(
                        Path.Combine(slotFolder.absolutePath, fileInfo.Name),
                        fileInfo.LastWriteTime.Ticks,
                        Path.GetFileNameWithoutExtension(fileInfo.Name),
                        slot => SaveNewLayout(slot.m_FileName),
                        new FileSlot.OnHoverChangeDelegate((slot, hover) => SandboxSave_SlotHoverCallback.Invoke(GameUI.m_Instance.m_SaveSandboxLayout, new object[] {slot, hover}))
                    );
                    
                    if (fileSlot)
                    {
                        fileSlot.SetOnDeleteCallback(slot => DeleteSlot(slot, m_FileLoader));
                        fileSlot.SetOnRenameCallback(RenameSlot);
                    }
                }

                foreach (DirectoryInfo dir in slotFolder.directoryInfo.EnumerateDirectories())
                {
                    // create links to other folders
                    CreateFileSlotFromFolder(new SlotFolder(dir));
                }

                m_FileLoader.SortByDate();

                // create header info stuff
                createHeadingSlots(slotFolder);
                m_FileLoader.SelectSlotIndex(1);
            }

            public static void MakeNewFolder(string name, string path, Panel_FileLoader parentFolder){
                string loc = Path.Combine(path, name);
                if (!Directory.Exists(loc))
                {
                    var info = Directory.CreateDirectory(loc);
                    CreateFileSlotFromFolder(new SlotFolder(info));
                }
            }

            public static void SaveNewLayout(string path) {
                var name = Path.GetFileNameWithoutExtension(path);
                if (File.Exists(SandboxLayout.AddFileExtension(path)))
                {
                    PopUpMessage.Display(
                        Localize.Get("POPUP_OVERWRITE_SLOT", name),
                        () => {
                            Save(path, name);
                            GameUI.m_Instance.m_SaveSandboxLayout.gameObject.SetActive(false);
                        }
                    );
                    return;
                }
                Save(path, name);
                GameUI.m_Instance.m_SaveSandboxLayout.gameObject.SetActive(false);
            }

            public static void Save(string path, string name) {
                if (GameUI.m_Instance.m_SandboxEditCustomShapeTools.gameObject.activeInHierarchy)
                {
                    GameUI.m_Instance.m_SandboxEditCustomShapeTools.ExitCustomShapeEditToolsMode();
                }
                Profile.m_LastLoadedSandbox = path;
                Profile.Save();
                SandboxLayout.Save(path);
                Sandbox.m_CurrentLayoutName = name;
                Sandbox.m_UnsavedChanges = false;
                GameUI.ShowMessage(ScreenMessageLocation.TOP_LEFT, string.Format("Saving {0}...", name), 1f);
            }
        }

        [HarmonyPatch(typeof(Panel_SaveSandboxLayout), "PopulateSlots")]
        public static class SavePopulatePatch
        {
            public static void Postfix(Panel_SaveSandboxLayout __instance)
            {
                if (!instance.shouldRun()) return;

                if (SaveHandler.m_OpenFolder != null && openLastFolder.Value) {
                    SaveHandler.Populate(SaveHandler.m_OpenFolder); // "open" previous folder
                    return;
                }

                // default folder
                string savePath = GetInitialSavePath();
                if (!Directory.Exists(savePath))
                {
                    return;
                }
                DirectoryInfo directoryInfo = new DirectoryInfo(savePath);
                if (directoryInfo == null)
                {
                    return;
                }
                
                SaveHandler.Populate(new SlotFolder(directoryInfo));
            }
        }

        [HarmonyPatch(typeof(Panel_SaveSandboxLayout), "SlotClickedCallback")]
        public static class SaveViaReturnKeyPatch {
            private static bool flag = false;
            public static bool Prefix(FileSlot slot) {
                if (instance.shouldRun()) {
                    if (flag) {
                        flag = false;
                        return true;
                    }
                    flag = true;
                    slot.m_FileSlotButton.onClick.Invoke();
                    flag = false;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Panel_TopBar), "OnQuickSave")]
        public static class QuickSavePatch {
            public static bool Prefix() {
                if (instance.shouldRun()) {
                    if (GameManager.GetGameMode() == GameMode.SANDBOX)
                    {
                        if (string.IsNullOrEmpty(Sandbox.m_CurrentLayoutName))
                        {
                            GameUI.m_Instance.m_SaveSandboxLayout.gameObject.SetActive(true);
                        }
                        else {
                            SaveHandler.Save(Profile.m_LastLoadedSandbox, Sandbox.m_CurrentLayoutName);
                        }
                    }
                    return false;
                }
                return true;
            }
        }
    }
}