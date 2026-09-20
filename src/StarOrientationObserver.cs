using Brutal.Logging;
using HarmonyLib;
using KSA;
using System.Reflection;

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
        harmony.CreateClassProcessor(typeof(KittenActionPatch)).Patch();
        harmony.CreateClassProcessor(typeof(KittenMmuPatch)).Patch();
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
                // Every vehicle re-applies its own remembered star orientation. Nothing is gated on
                // Program.ControlledVehicle, so switching control neither transfers star orientation
                // to the newly controlled vehicle nor discards the state the previous one was left in.
                if(OhMyStarsWindow.TryGetOrientedStar(__instance, out int hip) &&
                   !OhMyStarsWindow.ConsumeReapplySuppression(__instance)) {
                    OhMyStarsWindow.ApplyStarOrientation(__instance, hip);
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

    // Kitten trim actions are the kitten-side equivalents of the game's attitude mode buttons.
    // ManualTrim writes FlightComputer.AttitudeMode directly, bypassing every patched method above,
    // so the kitten's star orientation must be dropped here or the mod would keep reapplying it.
    // (HandleKittenAction is protected, hence the string method name.)
    [HarmonyPatch(typeof(KittenEva), "HandleKittenAction")]
    private static class KittenActionPatch {
        static void Prefix(KittenEva __instance, KittenEvaAction action) {
            try {
                if(action is KittenEvaAction.ManualTrim or KittenEvaAction.RateTrim) {
                    OhMyStarsWindow.ClearOrientedStar(__instance.FlightComputer);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] KittenAction Prefix: {ex}");
            }
        }
    }

    // While a kitten's MMU is driving the flight computer (ApplyMmuAttitudeTarget runs every physics
    // step and stomps any mod-set Custom target), the kitten's star orientation is parked via
    // DetachOrientedStar, which keeps it remembered but stops the mod from fighting the MMU.
    // When the MMU releases (ResetMmuAttitudeTarget), PrepareWorker re-engages the star. ReadOnlyVehicle
    // returns the live Vehicle, so the patches below use it as the dictionary key; the exact instance
    // type does not matter because the key is only ever compared by reference. All three involved
    // members are non-public, hence the string method names.
    [HarmonyPatch]
    private static class KittenMmuPatch {
        private static IEnumerable<MethodBase> TargetMethods() {
            MethodBase? apply = AccessTools.Method(typeof(PhysicsBubble), "ApplyMmuAttitudeTarget");
            if(apply != null)
                yield return apply;

            MethodBase? reset = AccessTools.Method(typeof(PhysicsBubble), "ResetMmuAttitudeTarget");
            if(reset != null)
                yield return reset;
        }

        private static bool Prefix(object[] __args) {
            try {
                Vehicle? vehicle = (__args?[0] as VehicleUpdateState)?.ReadOnlyVehicle;
                if(vehicle != null) {
                    OhMyStarsWindow.DetachOrientedStar(vehicle);
                }
            } catch(Exception ex) {
                DefaultCategory.Log.Warning($"[OhMyStars] KittenMmu Prefix: {ex}");
            }

            return true;
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