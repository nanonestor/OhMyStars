using System.Globalization;
using System.Text.RegularExpressions;

namespace OhMyStars;

internal sealed class MultipleStarOrbits {
    internal sealed record Orbit(string Wds, string Reference, string Companion, double Axis, double Eccentricity,
        double Inclination, double Node, double Periastron, int Grade, string Citation);
    internal sealed record Curve(string Companion, MultipleStarCatalog.Offset[] Points);
    public IReadOnlyList<Orbit> Orbits { get; }
    private readonly IReadOnlyDictionary<string, Orbit[]> _byWds;

    private MultipleStarOrbits(IReadOnlyList<Orbit> orbits) {
        Orbits = orbits;
        _byWds = orbits.GroupBy(o => o.Wds).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
    }

    public static MultipleStarOrbits Load(string path) {
        List<Orbit> orbits = new();
        int records = 0;
        foreach(string line in File.ReadLines(path)) {
            if(line.Length < 29 || !Regex.IsMatch(line.Substring(19, 10), @"^\d{5}[+-]\d{4}$"))
                continue;
            records++;
            if(line.Length < 245)
                throw new InvalidDataException($"Truncated ORB6 record {records}.");
            string Field(int start, int length) => line.Substring(start, length).Trim();
            double? Number(int start, int length) =>
                double.TryParse(Field(start, length), NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && double.IsFinite(n) ? n : null;
            // Astrometric/visibility-only or incomplete solutions do not describe a relative visual orbit.
            if(Number(233, 1) is not (>= 1 and <= 5) ||
                Number(223, 4) is double equinox && equinox != 2000)
                continue;
            Match designation = Regex.Match(Field(30, 14), @"^[A-Za-z]{1,4}\s*\d+\s*(.*)$");
            if(!designation.Success)
                continue;
            string pair = designation.Groups[1].Value.Trim();
            if(pair.Length == 0)
                pair = "AB";
            string first, second;
            if(pair == "AB") {
                first = "A";
                second = "B";
            } else if(Regex.IsMatch(pair, @"^[A-Z]{2}$")) {
                first = pair[..1];
                second = pair[1..];
            } else {
                string[] ends = pair.Split([',', '-']);
                if(ends.Length != 2)
                    continue;
                first = ends[0];
                second = ends[1];
                if(Regex.IsMatch(first, @"^[A-Z][a-z][1-9]\d*$") && Regex.IsMatch(second, @"^[1-9]\d*$"))
                    second = Regex.Replace(first, @"\d+$", "") + second;
            }
            if(!Regex.IsMatch(first, @"^[A-Z](?:[a-z](?:[1-9]\d*)?)?$") ||
                !Regex.IsMatch(second, @"^[A-Z](?:[a-z](?:[1-9]\d*)?)?$") || first == second)
                continue;
            double factor = Field(114, 1) switch { "a" => 1, "m" => 0.001, "M" => 60, "u" => 0.000001, _ => 0 };
            if(factor == 0 || Number(105, 9) is not double axis || axis <= 0 ||
                Number(187, 8) is not double eccentricity || eccentricity < 0 || eccentricity >= 1 ||
                Number(125, 8) is not double inclination || inclination < 0 || inclination > 180 ||
                Number(143, 8) is not double node || Number(205, 8) is not double periastron ||
                Number(80, 12) is not > 0 || Number(162, 12) is null)
                continue;
            orbits.Add(new(Field(19, 10), first, second, axis * factor, eccentricity, inclination,
                node, periastron, (int)Number(233, 1)!.Value, Field(237, 8)));
        }
        if(records == 0)
            throw new InvalidDataException("ORB6 catalog contains no recognizable records.");
        // Multiple solutions for one pair are deliberately not resolved by guessing a preferred orbit.
        var unique = orbits.GroupBy(o => (o.Wds, o.Reference, o.Companion)).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
        Console.WriteLine($"OhMyStars: ORB6 loaded {unique.Length} unique supported visual orbits from {records} records.");
        return new(unique);
    }

    public Curve[] Curves(MultipleStarCatalog.System system, IReadOnlyDictionary<string, MultipleStarCatalog.Offset> layout) {
        if(!_byWds.TryGetValue(system.Primary["wds_id"], out var candidates))
            return [];
        var labels = system.Members.Select(s => s.Label).ToHashSet(StringComparer.Ordinal);
        return candidates.Where(o => labels.Contains(o.Reference) && labels.Contains(o.Companion) &&
            layout.ContainsKey(o.Reference) &&
            system.Measures.Any(m => m.Reference == o.Reference && m.Companion == o.Companion))
            .Select(o => new Curve(o.Companion, Sample(o, layout[o.Reference]))).ToArray();
    }

    public static MultipleStarCatalog.Offset[] Sample(Orbit orbit, MultipleStarCatalog.Offset origin) {
        double radians = Math.PI / 180;
        double node = orbit.Node * radians, periastron = orbit.Periastron * radians;
        double cosI = Math.Cos(orbit.Inclination * radians);
        double a = Math.Cos(node) * Math.Cos(periastron) - Math.Sin(node) * Math.Sin(periastron) * cosI;
        double b = -Math.Cos(node) * Math.Sin(periastron) - Math.Sin(node) * Math.Cos(periastron) * cosI;
        double f = Math.Sin(node) * Math.Cos(periastron) + Math.Cos(node) * Math.Sin(periastron) * cosI;
        double g = -Math.Sin(node) * Math.Sin(periastron) + Math.Cos(node) * Math.Cos(periastron) * cosI;
        MultipleStarCatalog.Offset[] points = new MultipleStarCatalog.Offset[129];
        for(int i = 0; i < points.Length; i++) {
            double anomaly = 2 * Math.PI * i / (points.Length - 1);
            double x = orbit.Axis * (Math.Cos(anomaly) - orbit.Eccentricity);
            double y = orbit.Axis * Math.Sqrt(1 - orbit.Eccentricity * orbit.Eccentricity) * Math.Sin(anomaly);
            points[i] = new(origin.East + f * x + g * y, origin.North + a * x + b * y);
        }
        return points;
    }
}
