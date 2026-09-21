using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace OhMyStars;

// Celestial right-ascension/declination coordinate grid overlay. The lines live on the same
// camera-centered star sphere as the constellation overlays and are built through
// StarDirectionConverter.RaDecToDirection, so the grid inherits the J2000 equatorial inclination
// (obliquity rotation) plus the user's fine-alignment - which is what sits it at the correct tilt
// against the game's natively ecliptic-oriented axes and star field.
internal static class SkyMarkingsRenderer {
    // Arc length of one drawn segment; 1 degree keeps the circles smooth on screen.
    private const double SegmentStepDegrees = 1.0;

    // Fraction beyond the exact view frustum (in clip space) that segment points are still
    // considered on screen, so grid lines do not pop in right at the screen edge while panning.
    private const float FovMargin = 0.15f;

    private static readonly float3 gridLineColor = new float3(0.75f, 0.85f, 1f);
    private const float gridLineOpacity = 0.35f;
    private const float gridLabelOpacity = 0.8f;

    private static readonly List<Segment> segments = new();
    private static readonly List<GridLabel> labels = new();
    private static readonly List<float4> drawnLabelRects = new();
    private static int builtDecSpacingDegrees = -1;
    private static bool builtShowRaMinuteBands;

    public static void Init() {
    }

    public static void Draw(ImDrawListPtr draw_list, Camera camera, double3 center, double radius) {
        if(!StellariumRenderer.showRaDecGrid)
            return;

        int decSpacing = StellariumRenderer.decSpacingDegrees;
        bool showRaMinuteBands = StellariumRenderer.showRaMinuteBands;
        if(decSpacing != builtDecSpacingDegrees || showRaMinuteBands != builtShowRaMinuteBands) {
            Rebuild(decSpacing, showRaMinuteBands);
        }

        ImColor8 lineColor = StellariumRenderer.ToLineColor(gridLineColor, gridLineOpacity);

        foreach(Segment segment in segments) {
            double3 a = center + StellariumRenderer.ApplyAlignment(segment.A) * radius;
            double3 b = center + StellariumRenderer.ApplyAlignment(segment.B) * radius;
            double3 midpoint = (a + b) * 0.5d;

            if(!IsNearFov(camera, a) && !IsNearFov(camera, midpoint) && !IsNearFov(camera, b))
                continue;

            if(!StellariumRenderer.IsVisibleFromCamera(a) ||
                !StellariumRenderer.IsVisibleFromCamera(midpoint) ||
                !StellariumRenderer.IsVisibleFromCamera(b)) {
                continue;
            }

            ImDrawListExtensions.AddLine(
                draw_list,
                StellariumRenderer.EgoToOverlayScreen(camera, a),
                StellariumRenderer.EgoToOverlayScreen(camera, b),
                lineColor,
                StellariumRenderer.gridLineThickness);
        }

        DrawLabels(draw_list, camera, center, radius);
    }

    // Sky-map style coordinate labels: RA labels centered on the celestial equator of each
    // labeled meridian, Dec labels along each labeled parallel. The text is centered on its
    // anchor point so it sits on the line crossing like a chart grid label; a prefixed label
    // ("RA"/"Dec") prints the prefix above the anchor and the number below it.
    private static void DrawLabels(ImDrawListPtr draw_list, Camera camera, double3 center, double radius) {
        ImColor8 labelColor = StellariumRenderer.ToLineColor(gridLineColor, gridLabelOpacity);
        drawnLabelRects.Clear();

        foreach(GridLabel label in labels) {
            double3 position = center + StellariumRenderer.ApplyAlignment(label.Direction) * radius;

            if(!camera.IsPointWithinFov(position))
                continue;
            if(!StellariumRenderer.IsVisibleFromCamera(position))
                continue;

            float2 anchor = StellariumRenderer.EgoToOverlayScreen(camera, position);

            if(label.Prefix != null) {
                float2 prefixSize = ImGui.CalcTextSize(label.Prefix);
                float2 numberSize = ImGui.CalcTextSize(label.Text);
                float2 prefixScreen = anchor - new float2(prefixSize.X * 0.5f, prefixSize.Y);
                float2 numberScreen = anchor - new float2(numberSize.X * 0.5f, 0f);

                if(OverlapsDrawnLabel(prefixScreen, prefixSize) ||
                    OverlapsDrawnLabel(numberScreen, numberSize)) {
                    continue;
                }

                drawnLabelRects.Add(new float4(prefixScreen.X, prefixScreen.Y, prefixScreen.X + prefixSize.X, prefixScreen.Y + prefixSize.Y));
                drawnLabelRects.Add(new float4(numberScreen.X, numberScreen.Y, numberScreen.X + numberSize.X, numberScreen.Y + numberSize.Y));

                ImDrawListExtensions.AddText(draw_list, prefixScreen, labelColor, label.Prefix);
                ImDrawListExtensions.AddText(draw_list, numberScreen, labelColor, label.Text);
                continue;
            }

            float2 size = ImGui.CalcTextSize(label.Text);
            float2 screen = anchor - size * 0.5f;

            // Wherever two labels would print on top of each other - an RA number sharing an
            // anchor with a Dec number at a line crossing, or meridian labels crowding together
            // near the poles - only the first is drawn. RA labels come first in the list, so an
            // RA number always survives a tie.
            if(OverlapsDrawnLabel(screen, size))
                continue;

            drawnLabelRects.Add(new float4(screen.X, screen.Y, screen.X + size.X, screen.Y + size.Y));

            ImDrawListExtensions.AddText(
                draw_list,
                screen,
                labelColor,
                label.Text);
        }
    }

    private static bool OverlapsDrawnLabel(float2 screen, float2 size) {
        foreach(float4 rect in drawnLabelRects) {
            if(screen.X < rect.Z && screen.X + size.X > rect.X &&
               screen.Y < rect.W && screen.Y + size.Y > rect.Y) {
                return true;
            }
        }

        return false;
    }

    // Rebuilds the grid segments: right ascension meridians always sit on whole hours (15 degrees),
    // with optional ten-minute sub-bands in between when minute bands are enabled; declination
    // parallels follow the selected degree spacing. Everything is chopped into
    // SegmentStepDegrees-long arcs.
    private static void Rebuild(int decSpacingDegrees, bool showRaMinuteBands) {
        segments.Clear();
        labels.Clear();
        builtDecSpacingDegrees = decSpacingDegrees;
        builtShowRaMinuteBands = showRaMinuteBands;

        for(double dec = -90.0 + decSpacingDegrees; dec <= 90.0 - decSpacingDegrees + 1e-9; dec += decSpacingDegrees) {
            for(double ra = 0.0; ra < 360.0; ra += SegmentStepDegrees) {
                segments.Add(new Segment(
                    StarDirectionConverter.RaDecToDirection(ra / 15.0, dec),
                    StarDirectionConverter.RaDecToDirection((ra + SegmentStepDegrees) / 15.0, dec)));
            }
        }

        // RA meridians: one per hour; with minute bands on, also at 10/20/30/40/50 minutes past
        // every hour.
        const double hourStepDegrees = 15.0;
        for(double ra = 0.0; ra < 360.0; ra += hourStepDegrees) {
            AddMeridian(ra);
            if(showRaMinuteBands) {
                foreach(double minuteOffsetDegrees in new[] { 2.5, 5.0, 7.5, 10.0, 12.5 }) {
                    AddMeridian(ra + minuteOffsetDegrees);
                }
            }
        }

        // Hour meridians: "RA" above "12h" on the equator, plain "12h" on the other label parallels.
        foreach(double labelDec in new[] { -75.0, -45.0, 0.0, 45.0, 75.0 }) {
            bool prefixed = labelDec == 0.0;
            for(int hour = 0; hour < 24; hour++) {
                labels.Add(new GridLabel(
                    string.Create(CultureInfo.InvariantCulture, $"{hour}h"),
                    prefixed ? "RA" : null,
                    StarDirectionConverter.RaDecToDirection(hour, labelDec)));
            }
        }

        // Minute bands (10/20/30/40/50 minutes past each hour): plain "40m" style labels on the
        // equator only, as a reminder of which band is which.
        if(showRaMinuteBands) {
            foreach(int minute in new[] { 10, 20, 30, 40, 50 }) {
                for(int hour = 0; hour < 24; hour++) {
                    labels.Add(new GridLabel(
                        string.Create(CultureInfo.InvariantCulture, $"{minute}m"),
                        null,
                        StarDirectionConverter.RaDecToDirection(hour + minute / 60.0, 0.0)));
                }
            }
        }

        // Dec labels: at 1 degree spacing only every fifth parallel (the ones that coincide with
        // the 5 degree grid) gets a label, or the screen drowns in text. Each labeled parallel is
        // marked every 45 degrees of RA as a plain "+n°" number; on the prime meridian "Dec"
        // prints above the number.
        double decLabelInterval = decSpacingDegrees == 1 ? 5.0 : decSpacingDegrees;
        for(double dec = -90.0 + decLabelInterval; dec <= 90.0 - decLabelInterval + 1e-9; dec += decLabelInterval) {
            for(double ra = 0.0; ra < 360.0; ra += 45.0) {
                labels.Add(new GridLabel(
                    string.Create(CultureInfo.InvariantCulture, $"{dec:+0;-0;0}°"),
                    ra == 0.0 ? "Dec" : null,
                    StarDirectionConverter.RaDecToDirection(ra / 15.0, dec)));
            }
        }
    }

    private static void AddMeridian(double raDegrees) {
        for(double dec = -90.0; dec < 90.0; dec += SegmentStepDegrees) {
            segments.Add(new Segment(
                StarDirectionConverter.RaDecToDirection(raDegrees / 15.0, dec),
                StarDirectionConverter.RaDecToDirection(raDegrees / 15.0, dec + SegmentStepDegrees)));
        }
    }

    // Cheap clip-space test: in front of the camera and inside the view frustum inflated by
    // FovMargin, so off-screen segments are rejected before the costlier occluder checks.
    private static bool IsNearFov(Camera camera, double3 position) {
        float4 clip = camera.EgoToClip(position);
        if(clip.W <= 0f)
            return false;

        float limit = clip.W * (1f + FovMargin);
        return MathF.Abs(clip.X) <= limit && MathF.Abs(clip.Y) <= limit;
    }

    private readonly struct Segment {
        public readonly double3 A;
        public readonly double3 B;

        public Segment(double3 a, double3 b) {
            A = a;
            B = b;
        }
    }

    // A grid label: the number text and, when set, a short prefix ("RA"/"Dec") that prints on the
    // line above the number instead of inline with it.
    private readonly record struct GridLabel(string Text, string? Prefix, double3 Direction);
}
