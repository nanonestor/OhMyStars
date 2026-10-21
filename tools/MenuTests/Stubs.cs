using System.Runtime.CompilerServices;
using HarmonyLib;

// Keep menu tests independent of the game's graphics initialization and native ImGui.
namespace KSA {
    public class Program {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void DrawProgramMenusHook() { }
    }
}

namespace Brutal.ImGuiApi {
    internal static class ImGui {
        internal static List<string> Items { get; } = [];
        internal static string? Clicked { get; set; }
        internal static bool MenuItem(string label) {
            Items.Add(label);
            return Clicked == label;
        }
    }
}

namespace OhMyStars {
    internal static class OhMyStarsWindow {
        internal static bool IsOpen { get; set; }
        internal static void OpenWindow() => IsOpen = true;
        internal static void ToggleWindow() => IsOpen = !IsOpen;
        internal static void LoadSettings() { }
        internal static void Draw() { }
        internal static void PersistWindowOpenStates() { }
    }

    internal static class StarsEditWindow {
        internal static bool IsOpen { get; set; }
        internal static void ToggleWindow() => IsOpen = !IsOpen;
        internal static void OnAllModsLoaded() { }
        internal static void Update() { }
        internal static void Draw() { }
    }

    internal static class OhMyStarsSettingsStore {
        internal static void Init() { }
        internal static void Load() { }
    }

    internal static class SaveLoadObserver {
        internal static void ApplyPatches(Harmony harmony) { }
    }

    internal static class StarOrientationObserver {
        internal static void ApplyPatches(Harmony harmony) { }
    }

    internal static class StellariumRenderer {
        internal static void Init() { }
        internal static void Draw() { }
    }

    internal static class StarsEditPatcher {
        internal static void Patch() { }
    }
}
