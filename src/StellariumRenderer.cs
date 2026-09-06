using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System;
using System.Collections.Generic;
using System.Text;
using static KSA.Rendering.Lighting.CascadedShadowSystem;

namespace OhMyStars;

internal unsafe static class StellariumRenderer {
    private const double StarDistance = 19.5d;

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

    private static readonly List<CelestialOccluder> celestialOccluders = new();

    private readonly record struct CelestialOccluder(double3 Center, double Radius);

    public static double3 ApplyAlignment(double3 direction) {
        return StarDirectionConverter.ApplyFineAlignment(
            direction,
            alignmentRotationXDegrees,
            alignmentRotationYDegrees,
            alignmentRotationZDegrees);
    }

    public static float2 EgoToOverlayScreen(Camera camera, double3 position) {
        float2 screen = camera.EgoToScreen(position);
        ImGuiViewport* viewport = ImGui.GetMainViewport();

        return viewport == null
            ? screen
            : screen + viewport->Pos;
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
        Camera camera = Program.GetMainCamera();
        ImGuiViewport* viewport = ImGui.GetMainViewport();

        if(camera == null || viewport == null)
            return;

        ImDrawListPtr? draw_list = CreateWindow(viewport);
        if(draw_list == null)
            return;

        IParentBody? occluderRoot = GetOccluderRoot(camera.Following);
        if(occluderRoot is null) {
            celestialOccluders.Clear();
        } else {
            CollectCelestialOccluders(camera, occluderRoot);
        }

        // StarsEdit places stars at this fixed distance from the camera, so the overlays must use
        // the same camera-centered sphere to avoid a camera-offset parallax error.
        double3 center = double3.Zero;
        double radius = StarDistance;

        SkyMarkingsRenderer.Draw(draw_list.Value, camera, center, radius);
        SkyCulturesRenderer.Draw(draw_list.Value, camera, center, radius, showAsterisms, showAsterismNames, showStarNames);

        if(showIAUConstellations)
            IAUConstellationsRenderer.Draw(draw_list.Value, camera, center, radius);

        DrawStarPointer(draw_list.Value, camera, radius);

        ImGui.End();
    }

    private static void DrawStarPointer(ImDrawListPtr drawList, Camera camera, double radius) {
        if(!OhMyStarsWindow.TryGetStarPointer(out int hip, out float2 windowPosition, out float2 windowSize) ||
            !SkyCulturesRenderer.TryGetStarDirection(hip, out double3 direction)) {
            return;
        }

        double3 position = ApplyAlignment(direction) * radius;
        if(!TryGetStarPointerTarget(camera, position, out float2 target))
            return;

        float2 anchor = GetNearestWindowCorner(windowPosition, windowSize, target);
        ImDrawListExtensions.AddLine(drawList, anchor, target, new ImColor8(255, 255, 255, 255), 1.5f);
    }

    private static bool TryGetStarPointerTarget(Camera camera, double3 position, out float2 target) {
        if(camera.IsPointWithinFov(position)) {
            target = EgoToOverlayScreen(camera, position);
            return true;
        }

        float4 clip = camera.EgoToClip(position);
        if(System.MathF.Abs(clip.W) < float.Epsilon) {
            target = default;
            return false;
        }

        float x = clip.X / clip.W;
        float y = clip.Y / clip.W;
        if(clip.W < 0f) {
            x = -x;
            y = -y;
        }

        if(System.MathF.Abs(x) < float.Epsilon && System.MathF.Abs(y) < float.Epsilon) {
            y = -1f;
        }

        float maximum = System.MathF.Max(System.MathF.Abs(x), System.MathF.Abs(y));
        ImGuiViewport* viewport = ImGui.GetMainViewport();
        if(viewport == null || maximum < float.Epsilon) {
            target = default;
            return false;
        }

        float2 center = viewport->Pos + viewport->Size * 0.5f;
        target = center + new float2(
            x / maximum * viewport->Size.X * 0.5f,
            y / maximum * viewport->Size.Y * 0.5f);
        return true;
    }

    private static float2 GetNearestWindowCorner(float2 windowPosition, float2 windowSize, float2 target) {
        float2 topLeft = windowPosition;
        float2 topRight = new float2(windowPosition.X + windowSize.X, windowPosition.Y);
        float2 bottomLeft = new float2(windowPosition.X, windowPosition.Y + windowSize.Y);
        float2 bottomRight = windowPosition + windowSize;

        float2 nearest = topLeft;
        float nearestDistanceSquared = DistanceSquared(topLeft, target);
        foreach(float2 corner in new[] { topRight, bottomLeft, bottomRight }) {
            float distanceSquared = DistanceSquared(corner, target);
            if(distanceSquared < nearestDistanceSquared) {
                nearest = corner;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private static float DistanceSquared(float2 a, float2 b) {
        float x = a.X - b.X;
        float y = a.Y - b.Y;
        return x * x + y * y;
    }

    private static IParentBody? GetOccluderRoot(IFollowable? following) {
        return following switch {
            IParentBody parentBody => parentBody,
            IOrbiter orbiter => orbiter.Parent,
            _ => null
        };
    }

    public static bool IsVisibleFromCamera(double3 position) {
        double distance = VectorMath.Length(position);
        if(distance <= 0d)
            return false;

        double3 direction = position / distance;
        foreach(CelestialOccluder occluder in celestialOccluders) {
            double projection =
                direction.X * occluder.Center.X +
                direction.Y * occluder.Center.Y +
                direction.Z * occluder.Center.Z;

            // The rendered star sphere is camera-relative, but it represents an infinitely distant
            // background. Any occluder in front of the camera along this direction hides it.
            if(projection <= 0d)
                continue;

            double centerDistanceSquared =
                occluder.Center.X * occluder.Center.X +
                occluder.Center.Y * occluder.Center.Y +
                occluder.Center.Z * occluder.Center.Z;
            double closestDistanceSquared = centerDistanceSquared - projection * projection;

            if(closestDistanceSquared < occluder.Radius * occluder.Radius)
                return false;
        }

        return true;
    }

    private static void CollectCelestialOccluders(Camera camera, IParentBody parentBody) {
        celestialOccluders.Clear();

        IParentBody root = parentBody;
        while(root is IOrbiter orbiter && !ReferenceEquals(orbiter.Parent, root)) {
            root = orbiter.Parent;
        }

        CollectCelestialOccludersRecursive(camera, root);
    }

    private static void CollectCelestialOccludersRecursive(Camera camera, IParentBody body) {
        AddOccluder(camera, body, body.MeanRadius);

        foreach(IOrbiter child in body.Children) {
            if(child is IParentBody childBody) {
                CollectCelestialOccludersRecursive(camera, childBody);
            }
            else if(child is Vehicle vehicle) {
                AddOccluder(camera, vehicle, vehicle.BoundingSphereRadiusBody);
            }
        }
    }

    private static void AddOccluder(Camera camera, IPosition objectPosition, double radius) {
        double3 center = camera.GetPositionEgo(objectPosition);
        double centerDistance = VectorMath.Length(center);

        // A camera inside its current body or vehicle must not treat that enclosing sphere as an occluder.
        if(radius > 0d && centerDistance > radius) {
            celestialOccluders.Add(new CelestialOccluder(center, radius));
        }
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
