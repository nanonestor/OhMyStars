using Brutal.Logging;
using HarmonyLib;
using KSA;

namespace OhMyStars;

internal static class SaveLoadObserver {
    /// <summary>
    /// Save id of the most recently loaded or written save.
    /// Empty when no save is loaded yet.
    /// </summary>
    public static string CurrentSaveId { get; private set; } = string.Empty;

    public static void Reset() {
        CurrentSaveId = string.Empty;
    }

    public static void ApplyPatches(Harmony harmony) {
        harmony.CreateClassProcessor(typeof(LoadPatch)).Patch();
        harmony.CreateClassProcessor(typeof(MakePatch)).Patch();
    }

    [HarmonyPatch(typeof(UncompressedSave), nameof(UncompressedSave.Load))]
    private static class LoadPatch {
        static void Postfix(UncompressedSave __instance) {
            try {
                CurrentSaveId = __instance.Id ?? string.Empty;

                OhMyStarsSettingsStore.LoadForSave(CurrentSaveId);
            } catch(Exception ex) {
                DefaultCategory.Log.Warning(
                    $"[OhMyStars] SaveLoadObserver Load Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(UncompressedSave), nameof(UncompressedSave.Make))]
    private static class MakePatch {
        static void Postfix(string name, GameSave __result) {
            try {
                string newSaveId = __result?.Id ?? name ?? string.Empty;

                if(string.IsNullOrEmpty(newSaveId))
                    return;

                CurrentSaveId = newSaveId;

                OhMyStarsSettingsStore.SaveForSave(newSaveId);
            } catch(Exception ex) {
                DefaultCategory.Log.Warning(
                    $"[OhMyStars] SaveLoadObserver Make Postfix: {ex}");
            }
        }
    }
}