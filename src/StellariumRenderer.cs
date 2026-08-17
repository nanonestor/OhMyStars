using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System;
using System.Collections.Generic;
using System.Text;
using static KSA.Rendering.Lighting.CascadedShadowSystem;

namespace StellariumCatalog;

internal unsafe static class StellariumRenderer {
    public static bool showIAUConstellations = false;
    public static bool showStarNames = false;
    public static bool showAsterisms = true;
    public static bool showAsterismNames = true;

    public static float iauLineOpacity = 1f;
    public static float3 iauLineColor = new float3(1f, 1f, 1f);

    public static float asterismLineOpacity = 1f;
    public static float3 asterismLineColor = new float3(1f, 1f, 1f);

    // Fine-alignment rotation (degrees) to compensate for any residual offset vs. the game's star field
    public static float alignmentRotationXDegrees = 0f;
    public static float alignmentRotationYDegrees = 0f;
    public static float alignmentRotationZDegrees = 0f;

    public static double3 ApplyAlignment(double3 direction) {
        return StarDirectionConverter.ApplyFineAlignment(
            direction,
            alignmentRotationXDegrees,
            alignmentRotationYDegrees,
            alignmentRotationZDegrees);
    }

    public static ImColor8 ToLineColor(float3 color, float opacity) {
        byte r = (byte)(System.Math.Clamp(color.X, 0f, 1f) * 255f);
        byte g = (byte)(System.Math.Clamp(color.Y, 0f, 1f) * 255f);
        byte b = (byte)(System.Math.Clamp(color.Z, 0f, 1f) * 255f);
        byte a = (byte)(System.Math.Clamp(opacity, 0f, 1f) * 255f);
        return new ImColor8(r, g, b, a);
    }

    public static void Init() {
        IAUConstellationsRenderer.Init();
        SkyCulturesRenderer.Init();
        SkyMarkingsRenderer.Init();
    }

    public static void Draw() {
        Vehicle? vehicle = Program.ControlledVehicle;
        Camera camera = Program.GetMainCamera();
        ImGuiViewport* viewport = ImGui.GetMainViewport();

        if(vehicle == null || camera == null || viewport == null)
            return;

        ImDrawListPtr? draw_list = CreateWindow(viewport);
        if(draw_list == null)
            return;

        IParentBody parentBody = vehicle.Orbit.Parent;
        EgoTransform.TryVehicleToEgo(vehicle, camera, parentBody, out double3 center);

        double radius = VectorMath.Length(center) * 10000d;

        SkyMarkingsRenderer.Draw(draw_list.Value, camera, center, radius);
        SkyCulturesRenderer.Draw(draw_list.Value, camera, center, radius, showAsterisms, showAsterismNames, showStarNames);

        if(showIAUConstellations)
            IAUConstellationsRenderer.Draw(draw_list.Value, camera, center, radius);

        ImGui.End();
    }

    public static ImDrawListPtr? CreateWindow(ImGuiViewport* viewport) {

        float2 window_size = viewport->Size;
        ImGui.SetNextWindowPos(viewport->Pos);
        ImGui.SetNextWindowSize(window_size);
        ImGui.SetNextWindowViewport(viewport->ID);
        ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoInputs |
            ImGuiWindowFlags.NoNavFocus;
        if(!ImGui.Begin("HUDFullscreenWindow", flags)) {
            ImGui.End();
            return null;
        }
        return ImGui.GetWindowDrawList();
    }

}
