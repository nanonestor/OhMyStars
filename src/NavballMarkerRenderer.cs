using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace OhMyStars;

// Draws a star-shaped marker on the game's navball at the orientation of the star currently
// selected in the Oh My Stars window's Information tab. Mirrors the game's own marker pipeline:
// the direction is converted ecliptic -> CCI -> navball control frame -> screen exactly like
// NavBallRenderer.ToScreenMarker, then rasterized with the same size, rim fade, and dark-outline
// styling as the markers drawn by Content/Core/Shaders/Common/NavballMarkers.glsl.
internal static class NavballMarkerRenderer {
    public static bool showNavballMarker = false;
    public static bool showMarkerLabel = true;

    // Warm amber fill; distinct from every built-in marker color and reads as a star.
    private static readonly float3 FillColor = new float3(1f, 0.78f, 0.25f);
    // Same near-black outline as the built-in markers (MARKER_COL_OUTLINE in NavballMarkers.glsl).
    private static readonly float3 OutlineColor = new float3(0.05f, 0.05f, 0.05f);

    // Constants mirrored from Content/Core/Shaders/Common/NavballMarkers.glsl.
    private const float MarkerSizeFrac = 0.22f;
    private const float MarkerBorderPx = 1.2f;
    private const float MarkerRim = 0.948683298f; // sqrt(0.9)
    private const float MarkerRimMargin = MarkerSizeFrac * 0.25f;
    private const float MarkerMaxReachUv = 0.17f;
    private const float MarkerStrokeFrac = 0.09f; // MARKER_THK
    private const float MarkerDotFrac = 0.05f;    // MARKER_DOT_FRAC

    // The glyph renders 30% smaller than the stock marker size, border thickness included.
    private const float GlyphScale = 0.7f;
    private const float GlyphBorderPx = MarkerBorderPx * GlyphScale;

    // T-shaped center indicator geometry from GaugeNavball.frag; it is drawn after the markers
    // there, so it occludes them (CROSS_LENGTH, CROSS_THICKNESS, BORDER_PX).
    private const float CenterIndicatorLength = 0.3f;
    private const float CenterIndicatorThickness = 0.02f;
    private const float CenterIndicatorBorderPx = 1.0f;

    // Star glyph proportions in units of glyph size s (the glsl glyphs reach up to ~0.64*s
    // from their center, e.g. the prograde ring plus prongs).
    private const float StarOuterRadiusFrac = 0.62f;
    private const float StarInnerRadiusFrac = 0.25f;
    private const int StarVertexCount = 10;
    private const float LabelGapPx = 3f;
    private static readonly float4 LabelColor = new float4(1f, 0.78f, 0.25f, 1f);

    // Same body-to-screen permutation as NavBallRenderer.Body2Screen.
    private static readonly double4x4 Body2Screen = new double4x4(
        0.0, 0.0, 1.0, 0.0,
        1.0, 0.0, 0.0, 0.0,
        0.0, 1.0, 0.0, 0.0,
        0.0, 0.0, 0.0, 1.0);

    public static void Draw() {
        if(!showNavballMarker)
            return;

        if(!IsNavballBallOnScreen(out float2 ballCenter, out float ballExtends, out GaugeCanvas navballCanvas))
            return;

        int hip = OhMyStarsWindow.SelectedStarHip;
        if(hip <= 0)
            return;

        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null)
            return;

        if(!SkyCulturesRenderer.TryGetStarDirection(hip, out double3 catalogDirection))
            return;

        if(!TryGetMarkerScreenDirection(vehicle, catalogDirection, out float3 screenDirection))
            return;

        // markerPlacement (NavballMarkers.glsl) drops markers on the back hemisphere.
        if(screenDirection.Z <= 0f)
            return;

        float lenXY = MathF.Sqrt(
            screenDirection.X * screenDirection.X +
            screenDirection.Y * screenDirection.Y);
        if(lenXY > MarkerRim)
            return;

        float fade = RimFade(lenXY);
        if(fade <= 0f)
            return;

        // The ball is rendered by a Vulkan draw callback inside the navball canvas window,
        // so the marker must go to the foreground draw list to land on top of it.
        ImDrawListPtr drawList = ImGui.GetForegroundDrawList();

        float2 markerCenter = ballCenter + new float2(screenDirection.X, screenDirection.Y) * ballExtends;
        float glyphSize = MarkerSizeFrac * ballExtends * GlyphScale;

        if(IsMarkerCovered(markerCenter, navballCanvas))
            return;

        DrawStarGlyph(drawList, markerCenter, glyphSize, fade);
        if(showMarkerLabel && SkyCulturesRenderer.TryGetStarDisplayName(hip, out string displayName)) {
            DrawMarkerLabel(drawList, markerCenter, glyphSize, fade, displayName);
        }
        DrawCenterIndicatorOverMarker(drawList, markerCenter, glyphSize, ballCenter, ballExtends);
    }

    private static bool TryGetMarkerScreenDirection(Vehicle vehicle, double3 starDirectionCce, out float3 screenDirection) {
        screenDirection = default;

        double3 dirEcl = StellariumRenderer.ApplyAlignment(starDirectionCce);
        if(VectorMath.IsZero(dirEcl))
            return false;

        // The catalog works in ecliptic axes (CCE); navball marker directions are CCI, so convert
        // through the parent body's CCE->CCI rotation, then into the navball's control frame.
        doubleQuat cce2Cci = vehicle.Parent.GetCce2Cci();
        double3 dirCci = dirEcl.Transform(cce2Cci);

        doubleQuat cci2Ctrl = vehicle.GetCtrl2Cci().Inverse();
        double3 dirScreen = dirCci.Transform(cci2Ctrl).Transform(Body2Screen).NormalizeOrZero();
        if(VectorMath.IsZero(dirScreen))
            return false;

        screenDirection = float3.Pack(dirScreen);
        return true;
    }

    // The marker is only valid while the game is actually painting the navball ball, which is
    // the same set of conditions that gate the built-in markers:
    //  - RenderGame + CanvasesRender (Program.Render): not the editor, DrawUI on, a controlled
    //    vehicle exists; canvases with no controlled vehicle render nothing at all.
    //  - GaugeCanvas.OnDrawUi: the canvas window is only submitted (and its ball draw callback
    //    only executes) when the canvas is enabled, context-visible, and the vehicle is active
    //    (canvas windows are regular ImGui windows, so an inactive vehicle leaves an empty frame).
    //  - the canvas window must not be fully clipped off-screen.
    private static bool IsNavballBallOnScreen(out float2 ballCenter, out float ballExtends, out GaugeCanvas navballCanvasOut) {
        ballCenter = default;
        ballExtends = 0f;
        navballCanvasOut = null!;

        if(Program.EditorFlag || !Program.DrawUI || Program.ControlledVehicle == null ||
           !Program.IsControlledVehicleActive)
            return false;

        GaugeCanvas? navballCanvas = null;
        foreach(GaugeCanvas canvas in GaugeCanvas.AllCanvases) {
            if(canvas.Id == "Navball") {
                navballCanvas = canvas;
                break;
            }
        }

        if(navballCanvas == null || !navballCanvas.Enabled || !navballCanvas.IsContextVisible())
            return false;

        foreach(GaugeComponent component in navballCanvas.Components) {
            if(component.Name != "Ball" || component.Canvas == null)
                continue;

            CanvasRect rect = new CanvasRect(component);
            if(rect.Size.X <= 0f || rect.Size.Y <= 0f)
                return false;

            ballCenter = rect.TopLeft + rect.Size * 0.5f;
            ballExtends = rect.Size.X * 0.5f;

            // When the canvas window is entirely off-screen ImGui skips it, so the ball (and
            // therefore this marker) is not drawn.
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            float2 min = viewport.Pos;
            float2 max = viewport.Pos + viewport.Size;
            if(ballCenter.X + ballExtends < min.X || ballCenter.X - ballExtends > max.X ||
               ballCenter.Y + ballExtends < min.Y || ballCenter.Y - ballExtends > max.Y)
                return false;

            navballCanvasOut = navballCanvas;
            return true;
        }

        return false;
    }

    // This marker draws to the foreground list, so it sits above the navball gauge and would
    // also sit above any gauge canvas or window layered over that spot. The game keeps its UI
    // coverage mask on the GPU with no CPU query, so this detects overlap directly against the
    // gauge canvases' screen rects and the mod's own window instead.
    private static bool IsMarkerCovered(float2 markerCenter, GaugeCanvas navballCanvas) {
        foreach(GaugeCanvas canvas in GaugeCanvas.AllCanvases) {
            if(ReferenceEquals(canvas, navballCanvas) || !canvas.Enabled || !canvas.IsContextVisible())
                continue;

            if(PointInRect(markerCenter, canvas.GetPixelsMin(), canvas.GetPixelsSize()))
                return true;
        }

        if(OhMyStarsWindow.TryGetWindowRect(out float2 windowPosition, out float2 windowSize) &&
           PointInRect(markerCenter, windowPosition, windowSize)) {
            return true;
        }

        return false;
    }

    private static bool PointInRect(float2 point, float2 rectMin, float2 rectSize) {
        return rectSize.X > 0f && rectSize.Y > 0f &&
               point.X >= rectMin.X && point.X <= rectMin.X + rectSize.X &&
               point.Y >= rectMin.Y && point.Y <= rectMin.Y + rectSize.Y;
    }

    // Replicates smoothstep(MARKER_RIM, MARKER_RIM - MARKER_RIM_MARGIN, lenXY + MARKER_MAX_REACH_UV)
    // from the glsl (note the inverted edges: fade is 1 well inside the ball and 0 at the rim).
    private static float RimFade(float lenXY) {
        float x = lenXY + MarkerMaxReachUv;
        float t = Math.Clamp((x - MarkerRim) / -MarkerRimMargin, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void DrawStarGlyph(ImDrawListPtr drawList, float2 center, float glyphSize, float fade) {
        float outerRadius = glyphSize * StarOuterRadiusFrac;
        float innerRadius = glyphSize * StarInnerRadiusFrac;
        float stroke = glyphSize * MarkerStrokeFrac;

        // Five-pointed star outline with one vertex straight up.
        Span<float2> points = stackalloc float2[StarVertexCount];
        for(int i = 0; i < StarVertexCount; i++) {
            float radius = (i & 1) == 0 ? outerRadius : innerRadius;
            float angle = -MathF.PI * 0.5f + i * (MathF.PI / (StarVertexCount / 2));
            float2 direction = new float2(MathF.Cos(angle), MathF.Sin(angle));

            points[i] = center + direction * radius;
        }

        // Same layering as the glsl markers: a dilated dark outline band underneath,
        // the fill-colored stroke on top of it.
        ImDrawListExtensions.AddPolyline(drawList, points, StellariumRenderer.ToLineColor(OutlineColor, fade), ImDrawFlags.Closed, stroke + 2f * GlyphBorderPx);
        ImDrawListExtensions.AddPolyline(drawList, points, StellariumRenderer.ToLineColor(FillColor, fade), ImDrawFlags.Closed, stroke);

        // Small center dot, like the prograde/normal/radial glyphs.
        float dotRadius = glyphSize * MarkerDotFrac;
        ImDrawListExtensions.AddCircleFilled(drawList, center, dotRadius + GlyphBorderPx, StellariumRenderer.ToLineColor(OutlineColor, fade), 0);
        ImDrawListExtensions.AddCircleFilled(drawList, center, dotRadius, StellariumRenderer.ToLineColor(FillColor, fade), 0);
    }

    // Small centered label below the glyph, at the game's interface text size, with the same
    // drop shadow the game uses for on-screen text (ImGuiHelper.DrawText).
    private static void DrawMarkerLabel(ImDrawListPtr drawList, float2 center, float glyphSize, float fade, string name) {
        float2 position = center + new float2(0f, glyphSize * StarOuterRadiusFrac + GlyphBorderPx + LabelGapPx);
        byte4 color = byte4.Pack(LabelColor, Pack.Float.Normalize).Transparency(fade);
        ImGuiHelper.DrawText(drawList, position, name, color, TextAlignment.Center);
    }

    // The shader draws the white T-shaped center indicator after the markers, so it occludes
    // them. This overlay renders on top of the whole gauge, so the occlusion must be replicated
    // by repainting the T over the marker whenever they overlap.
    private static void DrawCenterIndicatorOverMarker(ImDrawListPtr drawList, float2 markerCenter, float glyphSize, float2 ballCenter, float ballExtends) {
        float halfLen = 0.5f * CenterIndicatorLength * ballExtends;
        float halfThk = 0.5f * CenterIndicatorThickness * ballExtends;
        float border = CenterIndicatorBorderPx;

        float markerRadius = glyphSize * (StarOuterRadiusFrac + 0.5f * MarkerStrokeFrac) + GlyphBorderPx;
        bool overlaps =
            markerCenter.X + markerRadius >= ballCenter.X - halfLen - border &&
            markerCenter.X - markerRadius <= ballCenter.X + halfLen + border &&
            markerCenter.Y + markerRadius >= ballCenter.Y - halfThk - border &&
            markerCenter.Y - markerRadius <= ballCenter.Y + halfLen + border;
        if(!overlaps)
            return;

        ImColor8 indicatorOutline = new ImColor8(0, 0, 0, 255);
        ImColor8 indicatorFill = new ImColor8(255, 255, 255, 255);
        float2 borderVec = new float2(border);

        // Horizontal bar centered on the ball center, vertical stem running downward from it.
        float2 barMin = ballCenter + new float2(-halfLen, -halfThk);
        float2 barMax = ballCenter + new float2(halfLen, halfThk);
        float2 stemMin = ballCenter + new float2(-halfThk, 0f);
        float2 stemMax = ballCenter + new float2(halfThk, halfLen);

        ImDrawListExtensions.AddRectFilled(drawList, barMin - borderVec, barMax + borderVec, indicatorOutline, 0f, ImDrawFlags.None);
        ImDrawListExtensions.AddRectFilled(drawList, stemMin - borderVec, stemMax + borderVec, indicatorOutline, 0f, ImDrawFlags.None);
        ImDrawListExtensions.AddRectFilled(drawList, barMin, barMax, indicatorFill, 0f, ImDrawFlags.None);
        ImDrawListExtensions.AddRectFilled(drawList, stemMin, stemMax, indicatorFill, 0f, ImDrawFlags.None);
    }
}
