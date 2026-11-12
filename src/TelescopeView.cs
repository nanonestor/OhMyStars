using Brutal.ImGuiApi;
using Brutal.Numerics;
using Core;
using KSA;

namespace OhMyStars;

// Telescope tab + secondary-viewport camera for the Hubble tube part
// (HubbleTube_Prefab_MainTube, see parts/hubble). Activation drives the
// part's stock PowerConsumer (LightSwitch semantics, exactly like the game's
// light parts): the game's own update loop bills the 2.1 kW draw against the
// vehicle's batteries and reports the consumption state back. The view uses
// the stock secondary-viewport lease (the docking-camera mechanism) with the
// camera aimed along the fore connector = telescope boresight.
internal static class TelescopeView {
    private const string PartTemplateId = "HubbleTube_Prefab_MainTube";
    private const string BoresightConnectorId = "_connector1";

    private sealed class ViewportOwner : IViewportOwner {
    }

    private static readonly ViewportOwner Owner = new();

    // Zoom presets: label + field of view. ~50 degrees is the default game
    // camera, so 20 degrees reads as roughly 2.5x and so on.
    private static readonly (string Label, float FovDegrees)[] ZoomLevels = {
        ("1x (50\u00b0)", 50f),
        ("2.5x (20\u00b0)", 20f),
        ("5x (10\u00b0)", 10f),
        ("10x (5\u00b0)", 5f),
        ("25x (2\u00b0)", 2f),
        ("50x (1\u00b0)", 1f),
        ("100x (0.5\u00b0)", 0.5f),
        ("200x (0.25\u00b0)", 0.25f),
        ("300x (0.167\u00b0)", 0.167f),
        ("400x (0.125\u00b0)", 0.125f)
    };

    private static int _zoomIndex = 3;
    private static bool _viewportClaimFailed;

    internal static bool TelescopePresent { get; private set; }

    private static Vehicle? _vehicle;
    private static Part? _part;
    private static PowerConsumer? _consumer;
    private static Part.Connector? _boresight;
    private static KeyframeAnimationModule? _doorAnimation;

    internal static bool IsActive => _consumer is { LightIsActive: true };

    private static bool IsPowered => _consumer?.IsSwitchedOn() ?? false;

    private static KeyframeAnimationModule.DeploymentState DoorState {
        get {
            if(_doorAnimation == null || _part?.Tree == null ||
               !_part.Tree.States.TryGetTypeList(out ModuleStateful<KeyframeAnimationModule,
                   KeyframeAnimationModule.State, EmptyStruct, EmptyStruct>.StateList? states)) {
                return KeyframeAnimationModule.DeploymentState.Deployed; // no door animation: act as always open
            }
            return states.GetModuleAndState(_doorAnimation).State.DeploymentState;
        }
    }

    // Runs every frame (also while the mod window is closed) to keep the
    // viewport in sync with the part and vehicle state.
    internal static void Update() {
        FindTelescope();

        if(!TelescopePresent) {
            ReleaseViewport();
            return;
        }

        // The aperture door follows the telescope state: open while active,
        // shut otherwise. The game's animation module does the movement.
        if(_doorAnimation != null) {
            _doorAnimation.TimeGoal = IsActive ? _doorAnimation.Shared.Duration : 0f;
        }

        if(!IsActive) {
            ReleaseViewport();
            return;
        }

        if(ViewportRegistry.TryGetOwned(Owner, out IGameViewport? viewport)) {
            if(!viewport.Visible) {
                // The user closed the viewport window: treat it as deactivation.
                Deactivate();
                return;
            }
            viewport.FixedController.DockingConnector = _boresight;
            viewport.GetCamera().SetFieldOfView(ZoomLevels[_zoomIndex].FovDegrees);
        }
        else if(DoorState == KeyframeAnimationModule.DeploymentState.Deployed) {
            // The view opens once the aperture door has finished opening.
            ClaimViewport();
        }
    }

    private static void FindTelescope() {
        _vehicle = null;
        _part = null;
        _consumer = null;
        _boresight = null;
        _doorAnimation = null;
        TelescopePresent = false;

        if(Program.EditorFlag || !Program.IsControlledVehicleActive) {
            return;
        }
        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null) {
            return;
        }

        foreach(Part part in vehicle.Parts.Parts) {
            if(part.Template?.Id != PartTemplateId) {
                continue;
            }
            Span<PowerConsumer> consumers = part.Modules.Get<PowerConsumer>();
            if(consumers.IsEmpty) {
                return; // part predates the telescope game data
            }
            _vehicle = vehicle;
            _part = part;
            _consumer = consumers[0];
            Span<KeyframeAnimationModule> animations = part.Modules.Get<KeyframeAnimationModule>();
            if(!animations.IsEmpty) {
                _doorAnimation = animations[0];
            }
            foreach(Part.Connector connector in part.Connectors) {
                if(connector.Id == BoresightConnectorId) {
                    _boresight = connector;
                    break;
                }
            }
            TelescopePresent = true;
            return;
        }
    }

    internal static void Activate() {
        if(_consumer == null) {
            return;
        }
        _consumer.LightIsActive = true;
        // The viewport is claimed by Update() once the aperture door opens.
    }

    internal static void Deactivate() {
        if(_consumer != null) {
            _consumer.LightIsActive = false;
        }
        ReleaseViewport();
    }

    private static void ClaimViewport() {
        if(_vehicle == null) {
            return;
        }
        if(!ViewportRegistry.TryClaimSecondaryViewport(Owner, out IGameViewport? viewport)) {
            _viewportClaimFailed = true;
            return;
        }
        _viewportClaimFailed = false;
        viewport.SetVisible(inValue: true);
        viewport.RequestResize(new int2(560, 560));
        viewport.SetCameraMode(CameraMode.Fixed);
        viewport.FixedController.Camera.SetFollow(_vehicle, tidalLocking: true, changeControl: false);
        viewport.FixedController.DockingConnector = _boresight;
        viewport.GetCamera().SetFieldOfView(ZoomLevels[_zoomIndex].FovDegrees);
        viewport.SetName("Telescope##OhMyStarsTelescope");
    }

    private static void ReleaseViewport() {
        if(ViewportRegistry.TryGetOwned(Owner, out IGameViewport? viewport)) {
            viewport.FixedController.DockingConnector = null;
            ViewportRegistry.ReleaseSecondaryViewport(Owner);
        }
        _viewportClaimFailed = false;
    }

    // Sums every battery on the controlled vehicle using the game's module
    // state lists (the same data the stock part info window shows).
    internal static bool TryGetElectrical(out double chargeJoules, out double capacityJoules, out double drawWatts) {
        chargeJoules = 0.0;
        capacityJoules = 0.0;
        drawWatts = 0.0;
        if(_vehicle == null) {
            return false;
        }
        PartTree tree = _vehicle.Parts;
        foreach(Part part in tree.Parts) {
            foreach(Battery battery in part.Modules.Get<Battery>()) {
                capacityJoules += battery.MaximumCapacity.Value();
                chargeJoules += tree.Batteries.GetState(battery).Charge.Value();
            }
        }
        if(_consumer != null) {
            drawWatts = tree.PowerConsumers.GetState(_consumer).Consumed.Value();
        }
        return capacityJoules > 0.0;
    }

    internal static string FormatEnergy(double joules) {
        return $"{joules / 3.6e6:0.00} kWh";
    }

    internal static string FormatPower(double watts) {
        return watts >= 1000.0 ? $"{watts / 1000.0:0.00} kW" : $"{watts:0} W";
    }

    // Right-aligned electrical readout for the top of the mod window.
    internal static void DrawWindowStatus() {
        if(!TelescopePresent || !TryGetElectrical(out double charge, out double capacity, out double draw)) {
            return;
        }
        string text = $"Charge {FormatEnergy(charge)} / {FormatEnergy(capacity)}";
        if(IsActive) {
            text += $"   Draw {FormatPower(draw)}";
        }
        float width = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(),
            ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - width));
        ImGui.TextColored(in ConsoleStyle.TextMuted, text);
    }

    internal static void Draw() {
        if(!TelescopePresent) {
            ImGui.TextColored(in ConsoleStyle.TextMuted,
                "No telescope part on the controlled vehicle.");
            return;
        }

        bool active = IsActive;
        if(ImGui.Button(active ? "Deactivate Telescope" : "Activate Telescope")) {
            if(active) {
                Deactivate();
            }
            else {
                Activate();
            }
        }

        if(active && !IsPowered) {
            ImGui.TextColored(in ConsoleStyle.Pending,
                "Insufficient electrical power - telescope is not operating.");
        }
        if(active && _viewportClaimFailed) {
            ImGui.TextColored(in ConsoleStyle.Pending,
                "No camera viewport is available (close a docking camera and reactivate).");
        }
        if(_doorAnimation != null) {
            string doorText = DoorState switch {
                KeyframeAnimationModule.DeploymentState.Deployed => "Aperture door: open",
                KeyframeAnimationModule.DeploymentState.Deploying => "Aperture door: opening...",
                KeyframeAnimationModule.DeploymentState.Retracting => "Aperture door: closing...",
                _ => "Aperture door: closed",
            };
            ImGui.TextColored(in ConsoleStyle.TextMuted, doorText);
        }

        OhMyStarsWindow.DrawThickSeparator();

        int zoomIndex = _zoomIndex;
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("100x (0.5\u00b0)").X * 2f);
        if(ImGui.BeginCombo("Zoom", ZoomLevels[zoomIndex].Label)) {
            for(int i = 0; i < ZoomLevels.Length; i++) {
                if(ImGui.Selectable(ZoomLevels[i].Label, i == zoomIndex)) {
                    _zoomIndex = i;
                }
            }
            ImGui.EndCombo();
        }

        if(TryGetElectrical(out double charge, out double capacity, out double draw)) {
            ImGui.Text($"Electrical storage: {FormatEnergy(charge)} / {FormatEnergy(capacity)}");
            if(active) {
                ImGui.Text($"Consumption: {FormatPower(draw)}");
            }
        }

        ImGui.Separator();
        ImGui.TextColored(in ConsoleStyle.TextMuted, "Telescope controls coming soon.");
    }

    // --- telescope viewport overlay -------------------------------------

    private static List<GameStarSpace.LoadedStar>? _starCache;
    private static int _starCacheCount = -1;

    // Draws the mod's sky overlays plus light-gathering boosted stars over
    // the telescope viewport window. Called after the main overlay pass.
    internal static void DrawViewportOverlay() {
        if(StarsEditWindow.VanillaMode || !TelescopePresent || !IsActive || !IsPowered) {
            return;
        }
        if(!ViewportRegistry.TryGetOwned(Owner, out IGameViewport? viewport) || !viewport.Visible) {
            return;
        }
        Camera camera = viewport.GetCamera();
        float2 origin = viewport.Position;
        float2 size = new float2(viewport.Size.X, viewport.Size.Y);
        if(size.X <= 0f || size.Y <= 0f) {
            return;
        }

        ImDrawListPtr drawList = ImGui.GetForegroundDrawList();
        float2 clipMax = origin + size;
        ImDrawListExtensions.PushClipRect(drawList, in origin, in clipMax, true);
        StellariumRenderer.OverlayOffsetOverride = origin;
        try {
            DrawBoostedStars(drawList, camera, origin);
            StellariumRenderer.DrawOverlaySet(drawList, camera, origin, size, interactive: false);
        }
        finally {
            StellariumRenderer.OverlayOffsetOverride = null;
            ImDrawListExtensions.PopClipRect(drawList);
        }
    }

    // Light gathering: redraw the loaded star field brighter inside the
    // telescope view. Every star the game renders sub-pixel becomes a
    // visible dot, and bright stars get a soft glow.
    private static void DrawBoostedStars(ImDrawListPtr drawList, Camera camera, float2 offset) {
        InstancedStarTechnique? technique = GameStarSpace.GetTechnique();
        if(technique == null) {
            return;
        }
        int count = GameStarSpace.GetLoadedCount(technique);
        if(_starCache == null || count != _starCacheCount) {
            _starCache = GameStarSpace.GetLoadedStars(technique);
            _starCacheCount = count;
        }

        double3 observerPc = GameStarSpace.GetObserverPc(camera);
        double3 forward = camera.GetForwardEcl();
        // Cheap cone pre-filter: the full FOV as half-angle leaves margin for
        // the viewport aspect while skipping ~99% of the sky at high zoom.
        double cosLimit = Math.Cos(ZoomLevels[_zoomIndex].FovDegrees * (Math.PI / 180.0));
        double radius = StellariumRenderer.StarDistance;

        List<GameStarSpace.LoadedStar> stars = _starCache;
        for(int i = 0; i < stars.Count; i++) {
            GameStarSpace.LoadedStar star = stars[i];
            double3 direction = GameStarSpace.Shift(
                new double3(star.Direction.X, star.Direction.Y, star.Direction.Z),
                star.InverseParsecs, observerPc, out bool hidden);
            if(hidden || double3.Dot(direction, forward) < cosLimit) {
                continue;
            }
            float2 screen = camera.EgoToScreen(direction * radius);
            if(float.IsNaN(screen.X)) {
                continue;
            }
            float2 point = screen + offset;
            float dotRadius = 0.9f + star.Scale * 0.055f;
            if(star.Scale > 24) {
                ImDrawListExtensions.AddCircleFilled(drawList, in point, dotRadius * 2.4f,
                    new ImColor8(star.R, star.G, star.B, 36), 0);
            }
            ImDrawListExtensions.AddCircleFilled(drawList, in point, dotRadius,
                new ImColor8(star.R, star.G, star.B, 255), 0);
        }
    }
}
