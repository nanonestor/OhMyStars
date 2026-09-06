using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;
using ModMenu;
using StarMap.API;

namespace OhMyStars;

[StarMapMod]
public class OhMyStarsEntryPoint {
    private static Harmony? _harmony;

    [StarMapAllModsLoaded]
    public static void OnFullyLoaded() {
        _harmony = new Harmony("nanonestor.ohmystars");

        OhMyStarsSettingsStore.Init();
        OhMyStarsSettingsStore.Load();
        SaveLoadObserver.ApplyPatches(_harmony);

        StellariumRenderer.Init();
        OhMyStarsWindow.LoadSettings();
    }

    [ModMenuEntry("Oh My Stars")]
    public static void DrawMenu() {
        if(ImGui.MenuItem("Open Window")) {
            OhMyStarsWindow.ToggleWindow();
        }
    }

    [StarMapBeforeGui]
    public static void OnBeforeGui(double dt) {
        OhMyStarsWindow.Draw();
    }

    [StarMapAfterGui]
    public static void OnAfterUi(double dt) {
        StellariumRenderer.Draw();
    }
}