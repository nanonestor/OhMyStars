using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;

namespace OhMyStars;

internal static class IAUConstellationsRenderer {
    private static readonly List<Segment> segments = new();

    public static void Init() {
        string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string lines_in_20_path = Path.Combine(
            userDocs,
            "My Games",
            "Kitten Space Agency",
            "mods",
            "OhMyStars",
            "lines_in_20.txt");

        LoadConstellationLines(lines_in_20_path);
    }

    public static void Draw(ImDrawListPtr draw_list, Camera camera, double3 center, double radius) {
        double3 centerD = new double3(center.X, center.Y, center.Z);
        ImColor8 lineColor = StellariumRenderer.ToLineColor(StellariumRenderer.iauLineColor, StellariumRenderer.iauLineOpacity);

        foreach(Segment segment in segments) {
            double3 a = centerD + StellariumRenderer.ApplyAlignment(segment.A) * radius;
            double3 b = centerD + StellariumRenderer.ApplyAlignment(segment.B) * radius;

            double3 midpoint = (a + b) * 0.5d;
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
                2f);
        }
    }

    private static void LoadConstellationLines(string path) {
        if(!File.Exists(path)) {
            return;
        }

        string? previousKey = null;
        double3 previousPoint = default;
        bool hasPreviousPoint = false;

        foreach(string rawLine in File.ReadLines(path)) {
            string line = rawLine.Trim();

            if(line.Length == 0) {
                continue;
            }

            string[] parts = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if(parts.Length < 3) {
                continue;
            }

            double raHours = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double decDegrees = double.Parse(parts[1], CultureInfo.InvariantCulture);
            string key = parts[2];

            double3 point = StarDirectionConverter.RaDecToDirection(raHours, decDegrees);

            if(hasPreviousPoint && key == previousKey) {
                segments.Add(new Segment(previousPoint, point));
            }

            previousPoint = point;
            previousKey = key;
            hasPreviousPoint = true;
        }
    }
    //public static float GameSkyRollDegrees = 90f;

    private readonly struct Segment {
        public readonly double3 A;
        public readonly double3 B;

        public Segment(double3 a, double3 b) {
            A = a;
            B = b;
        }
    }


}
