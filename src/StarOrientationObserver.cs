using Brutal.Logging;
using HarmonyLib;
using KSA;

namespace OhMyStars;

internal static class StarOrientationObserver {
    public static void ApplyPatches(Harmony harmony) {
        harmony.CreateClassProcessor(typeof(PrepareWorkerPatch)).Patch();
        harmony.CreateClassProcessor(typeof(TrackTargetPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RateHoldPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SetNullRotPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SetStabilizationPatch)).Patch();
        harmony.CreateClassProcessor(typeof(SetEnumPatch)).Patch();
        harmony.CreateClassProcessor(typeof(ToggleEnumPatch)).Patch();
    }

    // Every standard mode button/keybind ultimately funnels through Vehicle.SetEnum/ToggleEnum
    // (see FlightComputerInputData.Apply). Some of those paths (e.g. plain Manual/Auto mode selection
    // via ToggleAttitudeMode, or a direct AttitudeMode assignment) never touch TrackTarget/RateHold/SetNullRot,
    // so they must be caught here too or star orientation keeps getting silently reapplied every physics tick.
    private static bool IsAttitudeModeChange(Enum? enumValue) {
        return enumValue is FlightComputerAttitudeMode ||
               enumValue is VehicleReferenceFrame ||
               (enumValue is FlightComputerAttitudeTrackTarget target && target != FlightComputerAttitudeTrackTarget.Custom);
    }

    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.PrepareWorker))]
    private static class PrepareWorkerPatch {
        static void Prefix(Vehicle __instance) {
            try {
                if(__instance == Program.ControlledVehicle && OhMyStarsWindow.OrientedStarHip > 0) {
                    OhMyStarsWindow.ApplyStarOrientation(__instance, OhMyStarsWindow.OrientedStarHip);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] PrepareWorker Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(FlightComputer), nameof(FlightComputer.TrackTarget))]
    private static class TrackTargetPatch {
        static void Prefix(FlightComputer __instance, FlightComputerAttitudeTrackTarget target) {
            try {
                if(target != FlightComputerAttitudeTrackTarget.Custom) {
                    OhMyStarsWindow.ClearOrientedStar(__instance);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] TrackTarget Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(FlightComputer), nameof(FlightComputer.RateHold))]
    private static class RateHoldPatch {
        static void Prefix(FlightComputer __instance) {
            try {
                OhMyStarsWindow.ClearOrientedStar(__instance);
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] RateHold Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(FlightComputer), nameof(FlightComputer.SetNullRot))]
    private static class SetNullRotPatch {
        static void Prefix(FlightComputer __instance) {
            try {
                OhMyStarsWindow.ClearOrientedStar(__instance);
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] SetNullRot Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.SetStabilization))]
    private static class SetStabilizationPatch {
        static void Prefix(Vehicle __instance, bool active) {
            try {
                if(!active) {
                    OhMyStarsWindow.ClearOrientedStar(__instance.FlightComputer);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] SetStabilization Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.SetEnum))]
    private static class SetEnumPatch {
        static void Prefix(Vehicle __instance, Enum? enumValue) {
            try {
                if(IsAttitudeModeChange(enumValue)) {
                    OhMyStarsWindow.ClearOrientedStar(__instance.FlightComputer);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] SetEnum Prefix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.ToggleEnum))]
    private static class ToggleEnumPatch {
        static void Prefix(Vehicle __instance, Enum? enumValue) {
            try {
                if(IsAttitudeModeChange(enumValue)) {
                    OhMyStarsWindow.ClearOrientedStar(__instance.FlightComputer);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] ToggleEnum Prefix: {ex}");
            }
        }
    }
}