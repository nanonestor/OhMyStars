using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;
using ModMenu;
using StarMap.API;

namespace StellariumCatalog;

[StarMapMod]
public class StellariumCatalogEntryPoint {
    private static Harmony? _harmony;

    [StarMapAllModsLoaded]
    public static void OnFullyLoaded() {
        _harmony = new Harmony("dejvid.stellariumcatalog");

        StellariumCatalogSettingsStore.Init();
        StellariumCatalogSettingsStore.Load();
        SaveLoadObserver.ApplyPatches(_harmony);

        StellariumRenderer.Init();
        StellariumCatalogWindow.LoadSettings();
    }

    [ModMenuEntry("StellariumCat...")]
    public static void DrawMenu() {
        if(ImGui.MenuItem("Open Window")) {
            StellariumCatalogWindow.ToggleWindow();
        }
    }

    [StarMapBeforeGui]
    public static void OnBeforeGui(double dt) {
        StellariumCatalogWindow.Draw();
    }

    [StarMapAfterGui]
    public static void OnAfterUi(double dt) {
        StellariumRenderer.Draw();
    }
}