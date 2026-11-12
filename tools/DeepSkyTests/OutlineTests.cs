using System.Security.Cryptography;
using OhMyStars;
using static OhMyStars.DeepSkyOutlineGeometry;

internal static class OutlineTests {
    public static void Run(DeepSkyCatalog catalog, string root, Action<bool, string> check, Action<Action, string> reject) {
        string path = Path.Combine(root, "outlines_stellarium.dat");
        check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) ==
            "5C74C716951C387E5D086CB51A9807BA8C844C71F54E058EC2021B797BF2EF50", "Bundled outline snapshot is unmodified.");
        var outlines = DeepSkyOutlines.Load(path, catalog);
        check(outlines.SourceContourCount == 513 && outlines.SourcePointCount == 28879, "All upstream contours and points are parsed.");
        Console.WriteLine($"Outline catalog: {outlines.Contours.Length} matched contours, " +
            $"{outlines.Contours.Select(contour => contour.Entry.Id).Distinct().Count()} catalog objects; " +
            $"omitted names: {string.Join(", ", outlines.UnmatchedNames)}.");
        check(outlines.Contours.Length == 441 &&
            outlines.Contours.Select(contour => contour.Entry.Id).Distinct().Count() == 182 &&
            outlines.UnmatchedNames.Length == 11, "Snapshot resolves 441 contours to 182 objects, with 11 explicitly unmatched region names.");
        check(outlines.Contours.Any(contour => contour.SourceName == "C9" && contour.Entry.Name == "C009"),
            "Unpadded outline names resolve to the original catalog's padded identifiers.");
        check(outlines.Contours.Any(contour => contour.SourceName == "M045" && contour.Entry.Name == "Mel022"),
            "Messier aliases connect Pleiades outlines to the correct addendum object.");
        check(outlines.Contours.Any(contour => contour.Entry.Name == "NGC1976") &&
            outlines.Contours.Any(contour => contour.Entry.Name == "NGC7000"),
            "Orion and North America nebulae have catalog-linked outlines.");
        check(outlines.Contours.All(contour => contour.Entry.CanDraw && contour.Points.Length >= 3 &&
            contour.Points[0] != contour.Points[^1]), "Geometry retains each closed contour with an implicit closing edge.");
        check(outlines.UnmatchedNames.Contains("Antlia") && outlines.UnmatchedNames.Contains("LMC") &&
            !outlines.UnmatchedNames.Contains("M045") && !outlines.UnmatchedNames.Contains("C9"),
            "Unmatched regions are reported rather than assigned to unrelated catalog entries.");

        List<ClipPoint> workA = [], workB = [];
        ClipPoint[] Square(double half, double x = 0, double y = 0) => [
            new(x - half, y - half, 1), new(x + half, y - half, 1),
            new(x + half, y + half, 1), new(x - half, y + half, 1)
        ];
        ClipPoint[] square = Square(0.5);
        check(Contains(square, new(0, 0), workA, workB, out double area) && area == 1, "Polygon interior is selectable.");
        check(Contains(square, new(0.5, 0), workA, workB, out _) &&
            Contains(square, new(-0.5, -0.5), workA, workB, out _), "Edges and vertices count as inside.");
        check(!Contains(square, new(0.51, 0), workA, workB, out _), "Click outside a contour is not selected.");
        check(Contains(square.Reverse().ToArray(), new(0, 0), workA, workB, out _), "Contour winding does not affect picking.");
        ClipPoint[] concave = [new(-0.8, -0.8, 1), new(0.8, -0.8, 1), new(0.8, -0.3, 1),
            new(-0.3, -0.3, 1), new(-0.3, 0.8, 1), new(-0.8, 0.8, 1)];
        check(Contains(concave, new(-0.6, 0.5), workA, workB, out _) &&
            !Contains(concave, new(0.5, 0.5), workA, workB, out _), "Concave silhouettes use polygon interiors, not bounding rectangles.");
        check(Contains(Square(2), new(0.9, 0.9), workA, workB, out area) && Math.Abs(area - 4) < 1e-10,
            "A contour surrounding the viewport remains selectable even when all original edges are offscreen.");
        check(Contains(Square(0.5, 0.9), new(0.95, 0), workA, workB, out _) &&
            !Contains(Square(0.5, 0.9), new(1.1, 0), workA, workB, out _),
            "Partially offscreen contours clip to the viewport and cannot pick outside it.");
        check(!Contains(Square(0.2, 3), new(0, 0), workA, workB, out _) &&
            !Contains([new(-0.5, -0.5, -1), new(0.5, -0.5, -1), new(0, 0.5, -1)], new(0, 0), workA, workB, out _),
            "Fully offscreen and behind-camera contours are not clickable.");
        check(!Contains([new(0, 0, 1), new(0.1, 0, 1), new(0.2, 0, 1)], new(0.1, 0), workA, workB, out _) &&
            !Contains([new(double.NaN, 0, 1), new(0, 0, 1), new(0, 1, 1)], new(0, 0), workA, workB, out _),
            "Degenerate and nonfinite projected geometry never produces a hit.");
        check(ClipSegment(new(-2, 0, 1), new(2, 0, 1), out double start, out double end) &&
            start == 0.25 && end == 0.75, "Visible portions of edges crossing the viewport are retained.");
        check(!ClipSegment(new(2, 0, 1), new(3, 1, 1), out _, out _) &&
            !ClipSegment(new(0, 0, -1), new(0, 0, -2), out _, out _), "Invisible edges are clipped away.");
        check(ClipSegment(new(0, 0, -1), new(0, 0, 1), out start, out end) &&
            start > 0.5 && end == 1, "Near-camera-plane crossings clip before projection instead of drawing mirrored lines.");
        check(!ClipSegment(new(double.PositiveInfinity, 0, 1), new(0, 0, 1), out _, out _),
            "Nonfinite segments are not drawn.");

        Target[] targets = [new(10, square), new(20, Square(0.25))];
        check(Pick(99, targets, new(0, 0), workA, workB) == 99,
            "A clicked ring of a different object wins over a containing nebula.");
        check(Pick(null, targets, new(0, 0), workA, workB) == 20,
            "Overlapping contours select the smallest containing footprint.");
        check(Pick(null, targets, new(0.4, 0), workA, workB) == 10 &&
            Pick(null, targets, new(0.8, 0), workA, workB) is null,
            "Interior picking falls back to larger enclosing contours or no selection.");
        check(Pick(null, [new(20, square), new(10, square)], new(0, 0), workA, workB) == 10,
            "Equal-area overlaps break ties by catalog identity, not draw order.");
        check(Pick(null, [], new(0, 0), workA, workB) is null, "Cleared/hidden outline targets cannot be selected.");
        check(Pick(null, [new(10, Square(0.2, -0.6)), new(10, Square(0.2, 0.6))], new(0, 0), workA, workB) is null,
            "Disconnected contours do not fill the space between them.");

        // A view centered on RA 0: tangent-screen X is equatorial Y and camera depth is equatorial X.
        ClipPoint Equatorial(double ra, double dec) {
            double r = ra * Math.PI / 12, d = dec * Math.PI / 180;
            return new(Math.Cos(d) * Math.Sin(r), Math.Sin(d), Math.Cos(d) * Math.Cos(r));
        }
        ClipPoint[] wrap = [Equatorial(23.9, -1), Equatorial(0.1, -1), Equatorial(0.1, 1), Equatorial(23.9, 1)];
        check(Contains(wrap, new(0, 0), workA, workB, out _), "RA 24/0 seam uses continuous sky geometry, not an RA bounding box.");
        var orion = catalog.Entries.Single(entry => entry.Name == "NGC1976");
        double raCenter = orion.RaHours!.Value * Math.PI / 12;
        double decCenter = orion.DecDegrees!.Value * Math.PI / 180;
        ClipPoint Tangent(DeepSkyOutlines.Coordinate point) {
            double ra = point.RaHours * Math.PI / 12 - raCenter;
            double dec = point.DecDegrees * Math.PI / 180;
            return new(Math.Cos(dec) * Math.Sin(ra),
                Math.Sin(dec) * Math.Cos(decCenter) - Math.Cos(dec) * Math.Cos(ra) * Math.Sin(decCenter),
                Math.Sin(dec) * Math.Sin(decCenter) + Math.Cos(dec) * Math.Cos(ra) * Math.Cos(decCenter));
        }
        var orionTargets = outlines.Contours.Where(contour => contour.Entry == orion)
            .Select(contour => new Target(orion.Id, contour.Points.Select(Tangent).ToArray())).ToArray();
        check(Pick(null, orionTargets, new(0, 0), workA, workB) == orion.Id,
            "The actual Orion Nebula catalog center selects M42 through the real upstream silhouette.");

        for(int i = 0; i < 1000; i++)
            Pick(null, targets, new(0, 0), workA, workB);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        bool stable = true;
        for(int i = 0; i < 10000; i++)
            stable &= Pick(null, targets, new(0, 0), workA, workB) == 20;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        check(stable && allocated == 0, $"Repeated outline picking reuses buffers without managed allocations; got {allocated} bytes.");
        Console.WriteLine($"Outline picking: 10,000 repeated overlap tests, {allocated} managed bytes.");

        string temp = Path.Combine(Path.GetTempPath(), "OhMyStars-OutlineTests-" + Guid.NewGuid().ToString("N") + ".dat");
        try {
            void Write(string text) => File.WriteAllText(temp, text);
            void Read() => DeepSkyOutlines.Load(temp, catalog);
            string triangle = "00.7 +40 start NGC0224\n00.8 +40 vertex\n00.8 +41 vertex\n00.7 +40 end\n";
            Write(triangle);
            check(DeepSkyOutlines.Load(temp, catalog).Contours.Single().Points.Length == 3,
                "Minimal valid closed polygon loads without changing RA units.");
            Write(triangle.Replace("NGC0224", "NotAnObject", StringComparison.Ordinal));
            check(DeepSkyOutlines.Load(temp, catalog).UnmatchedNames.SequenceEqual(new[] { "NotAnObject" }),
                "Unmatched primary names remain explicit diagnostics.");
            Write(triangle.Replace("00.7 +40 end", "00.9 +40 end", StringComparison.Ordinal));
            reject(Read, "Unclosed outline must fail explicitly.");
            Write("00.7 +40 vertex\n"); reject(Read, "Vertex without start must fail.");
            Write("00.7 +40 start NGC0224\n"); reject(Read, "Unterminated outline must fail.");
            Write("00.7 +40 start NGC0224\n00.8 +40 start NGC0224\n"); reject(Read, "Nested starts must fail.");
            Write(triangle.Replace("+41", "+91", StringComparison.Ordinal)); reject(Read, "Invalid Dec must fail.");
            Write(triangle.Replace("00.8", "24.0", StringComparison.Ordinal)); reject(Read, "Invalid RA must fail.");
            Write(triangle.Replace("vertex", "bogus", StringComparison.Ordinal)); reject(Read, "Unknown commands must fail.");
            Write(triangle.Replace("00.8", "NaN", StringComparison.Ordinal)); reject(Read, "Nonfinite coordinates must fail.");
            Write(""); reject(Read, "Empty outline data must fail.");
        } finally {
            File.Delete(temp);
        }
    }
}
