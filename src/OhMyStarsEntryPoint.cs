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
        _harmony.Patch(
            AccessTools.Method(typeof(KSA.Program), nameof(KSA.Program.DrawProgramMenusHook)),
            postfix: new HarmonyMethod(typeof(OhMyStarsEntryPoint), nameof(DrawProgramMenu)));

        OhMyStarsSettingsStore.Init();
        OhMyStarsSettingsStore.Load();
        SaveLoadObserver.ApplyPatches(_harmony);
        StarOrientationObserver.ApplyPatches(_harmony);

        StellariumRenderer.Init();
        OhMyStarsWindow.LoadSettings();

        StarsEditWindow.OnAllModsLoaded();
    }

    // Star patches must be in place before the game builds its star technique and loads star binaries.
    [StarMapBeforeMain]
    public static void OnBeforeMain() {
        StarsEditPatcher.Patch();
    }

    [ModMenuEntry("Oh My Stars")]
    public static void DrawMenu() {
        if(ImGui.MenuItem("Oh My Stars Window")) {
            OhMyStarsWindow.ToggleWindow();
        }
        if(ImGui.MenuItem("Star Editor")) {
            StarsEditWindow.ToggleWindow();
        }
    }

    internal static void DrawProgramMenu() {
        if(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "ModMenu")) {
            return;
        }

        if(ImGui.MenuItem("Oh My Stars")) {
            OhMyStarsWindow.OpenWindow();
        }
        if(ImGui.MenuItem("Stars Editor")) {
            StarsEditWindow.IsOpen = true;
        }
    }

    [StarMapBeforeGui]
    public static void OnBeforeGui(double dt) {
        TelescopeView.Update();
        StarsEditWindow.Update();
        OhMyStarsWindow.Draw();
        StarsEditWindow.Draw();
        OhMyStarsWindow.PersistWindowOpenStates();
    }

    [StarMapAfterGui]
    public static void OnAfterUi(double dt) {
        StellariumRenderer.Draw();
        TelescopeView.DrawViewportOverlay();
    }
}