using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace OhMyStars;

internal sealed class MultipleStarCatalog {
    internal sealed class Star(string[] fields, IReadOnlyDictionary<string, int> columns) {
        public string this[string name] => fields[columns[name]];
        public long Id => long.Parse(this["oms_id"], CultureInfo.InvariantCulture);
        public string Label => this["component_label"];
        public bool IsWdsPosition => this["radec_source"] == "WDS";
        public string ListLabel => IsWdsPosition ? $"*{Label}" : Label;
        public string Role => this["component"];
        public bool IsSystemCompanion => this["system_id"].Length > 0 &&
            this["primary_oms_id"] != this["oms_id"];
        public string RelationshipDisplay => Role switch {
            "V" => "(Visual)",
            "Q" => "(Visual ?)",
            "U" => "(Relationship Unknown)",
            _ => this["relationships"]
        };
        public double? Number(string name) => ParseNumber(this[name]);
    }

    internal sealed record Measure(string Reference, string Companion, double? Theta, double? Rho, double? Year, int SourceLine);
    internal sealed record System(Star Primary, IReadOnlyList<Star> Members, IReadOnlyList<Measure> Measures);
    internal readonly record struct Offset(double East, double North);
    internal readonly record struct Target(long Id, float X, float Y);
    internal readonly record struct PlotLabel(float X, float Y, float Radius, float Width, float Height);
    internal readonly record struct LabelPosition(float X, float Y);

    public static LabelPosition[] PlaceLabels(IReadOnlyList<PlotLabel> stars, float width, float height) {
        List<(float X, float Y, float Width, float Height)> occupied = new();
        LabelPosition[] positions = new LabelPosition[stars.Count];
        for(int i = 0; i < stars.Count; i++) {
            PlotLabel star = stars[i];
            var nearest = stars.Where((_, index) => index != i)
                .OrderBy(other => Math.Pow(other.X - star.X, 2) + Math.Pow(other.Y - star.Y, 2))
                .FirstOrDefault(star);
            double preferred = nearest.X == star.X && nearest.Y == star.Y
                ? i * Math.PI * (3 - Math.Sqrt(5))
                : Math.Atan2(star.Y - nearest.Y, star.X - nearest.X);
            LabelPosition best = default;
            double bestScore = double.PositiveInfinity;
            double bestOverlap = double.PositiveInfinity;
            void Consider(float x, float y) {
                x = Math.Clamp(x, 2, Math.Max(2, width - star.Width - 2));
                y = Math.Clamp(y, 2, Math.Max(2, height - star.Height - 2));
                double overlap = 0;
                foreach(var box in occupied)
                    overlap += Math.Max(0, Math.Min(x + star.Width + 2, box.X + box.Width) - Math.Max(x - 2, box.X)) *
                        Math.Max(0, Math.Min(y + star.Height + 2, box.Y + box.Height) - Math.Max(y - 2, box.Y));
                foreach(var other in stars)
                    overlap += Math.Max(0, Math.Min(x + star.Width, other.X + other.Radius + 2) - Math.Max(x, other.X - other.Radius - 2)) *
                        Math.Max(0, Math.Min(y + star.Height, other.Y + other.Radius + 2) - Math.Max(y, other.Y - other.Radius - 2));
                double dx = x + star.Width / 2 - star.X, dy = y + star.Height / 2 - star.Y;
                double score = dx * dx + dy * dy;
                if(overlap < bestOverlap || overlap == bestOverlap && score < bestScore) {
                    bestOverlap = overlap;
                    bestScore = score;
                    best = new(x, y);
                }
            }
            for(int direction = 0; direction < 8; direction++) {
                double angle = preferred + direction * Math.PI / 4;
                float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
                Consider(star.X + dx * (star.Radius + 5 + star.Width / 2) - star.Width / 2,
                    star.Y + dy * (star.Radius + 5 + star.Height / 2) - star.Height / 2);
            }
            // Search farther only if the nearby candidates still collide.
            if(bestOverlap > 0)
                for(float y = 2; y + star.Height <= height - 2; y += star.Height + 4)
                    for(float x = 2; x + star.Width <= width - 2; x += Math.Max(4, star.Width / 2))
                        Consider(x, y);
            positions[i] = best;
            occupied.Add((best.X, best.Y, star.Width + 2, star.Height + 2));
        }
        return positions;
    }

    public IReadOnlyList<System> Systems { get; }
    public IReadOnlyDictionary<long, Star> Stars { get; }

    private MultipleStarCatalog(IReadOnlyList<System> systems, IReadOnlyDictionary<long, Star> stars) {
        Systems = systems;
        Stars = stars;
    }

    public static MultipleStarCatalog Load(string path) {
        using TextFieldParser parser = new(path) {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");
        string[] header = parser.ReadFields() ?? throw new InvalidDataException("Empty curated catalog.");
        var columns = header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index, StringComparer.Ordinal);
        foreach(string required in new[] { "oms_id", "system_id", "primary_oms_id", "component", "component_label",
            "ra", "dec", "mag", "absmag", "ci", "dist", "hip", "hd", "h", "stf", "stt", "bds", "wds_id",
            "proper", "bayer", "con", "spect", "radec_source", "relationships", "theta_deg", "rho_arcsec",
            "measurement_year", "wds_source_line", "mag_src", "pmra", "pmdec",
            "id", "tyc", "gaia", "hyg", "hr", "gl", "flam", "bu" }) {
            if(!columns.ContainsKey(required))
                throw new InvalidDataException($"Curated catalog is missing '{required}'. Regenerate with the current CatalogBuilder.");
        }
        Dictionary<long, List<Star>> groups = new();
        HashSet<long> ids = new();
        Dictionary<long, Star> stars = new();
        while(!parser.EndOfData) {
            string[] row = parser.ReadFields() ?? throw new InvalidDataException("Missing catalog row.");
            if(row.Length != header.Length)
                throw new InvalidDataException($"Incorrect catalog field count at line {parser.LineNumber}.");
            if(!long.TryParse(row[columns["oms_id"]], out long id) || id <= 0 || !ids.Add(id))
                throw new InvalidDataException("Invalid or duplicate OMS ID.");
            string systemId = row[columns["system_id"]];
            Star star = new(row, columns);
            stars.Add(id, star);
            if(systemId.Length == 0)
                continue;
            if(!long.TryParse(systemId, out long system) || system <= 0 ||
                row[columns["primary_oms_id"]] != systemId)
                throw new InvalidDataException($"Invalid group for OMS {id}.");
            if(!groups.TryGetValue(system, out var members))
                groups[system] = members = new();
            foreach(string numeric in new[] { "ra", "dec", "mag", "absmag", "ci", "dist", "pmra", "pmdec" })
                star.Number(numeric);
            members.Add(star);
        }
        List<System> systems = new();
        foreach(var (id, members) in groups) {
            Star primary = members.SingleOrDefault(s => s.Id == id)
                ?? throw new InvalidDataException($"Missing primary OMS {id}.");
            if(primary.Role is not ("P" or "U") || members.Select(s => s.Label).Distinct(StringComparer.Ordinal).Count() != members.Count)
                throw new InvalidDataException($"Invalid primary/component labels in OMS system {id}.");
            List<Measure> measures = new();
            foreach(Star star in members) {
                if(star.Role is not ("P" or "C" or "V" or "Q" or "U"))
                    throw new InvalidDataException($"Invalid component role for OMS {star.Id}.");
                string[] relations = Split(star["relationships"]);
                string[][] values = new[] { "theta_deg", "rho_arcsec", "measurement_year", "wds_source_line" }
                    .Select(name => relations.Length == 0 ? Split(star[name]) : star[name].Split('|')).ToArray();
                if(values.Any(v => v.Length != relations.Length))
                    throw new InvalidDataException($"Misaligned measurements for OMS {star.Id}.");
                for(int i = 0; i < relations.Length; i++) {
                    string[] ends = relations[i].Split('>');
                    if(ends.Length != 2 || ends.Any(string.IsNullOrEmpty) ||
                        !int.TryParse(values[3][i], out int line) || line <= 0)
                        throw new InvalidDataException($"Invalid relationship for OMS {star.Id}.");
                    measures.Add(new Measure(ends[0], ends[1], ParseNumber(values[0][i]),
                        ParseNumber(values[1][i]), ParseNumber(values[2][i]), line));
                }
            }
            systems.Add(new System(primary, members.OrderBy(s => s.Id == id ? 0 : 1)
                .ThenBy(s => s.Label, StringComparer.Ordinal).ToArray(),
                measures.OrderByDescending(m => m.Year).ThenBy(m => m.SourceLine).ToArray()));
        }
        return new MultipleStarCatalog(systems.OrderBy(s => s.Primary.Id).ToArray(), stars);
    }

    private static string[] Split(string value) => value.Length == 0 ? [] : value.Split('|');

    private static double? ParseNumber(string value) {
        if(value.Length == 0)
            return null;
        if(!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
            throw new InvalidDataException($"Invalid catalog numeric value '{value}'.");
        return number;
    }

    public static Dictionary<string, Offset> Layout(System system, bool approximateGroups = true,
        bool approximateCoordinates = false, ISet<string>? approximateLabels = null) {
        var labels = system.Members.Select(s => s.Label).ToHashSet(StringComparer.Ordinal);
        string? Representative(string endpoint) {
            if(labels.Contains(endpoint))
                return endpoint;
            if(!approximateGroups)
                return null;
            bool Contains(string leaf) => endpoint.All(char.IsUpper)
                ? endpoint.Contains(leaf[0])
                : leaf.StartsWith(endpoint, StringComparison.Ordinal) && leaf.Length > endpoint.Length &&
                    !char.IsDigit(endpoint[^1]);
            return labels.Where(Contains).OrderBy(label => label == system.Primary.Label ? 0 : 1)
                .ThenBy(label => label, StringComparer.Ordinal).FirstOrDefault();
        }
        Dictionary<string, Offset> positions = new(StringComparer.Ordinal) { [system.Primary.Label] = new(0, 0) };
        HashSet<string> approximated = new(StringComparer.Ordinal);
        bool changed;
        do {
            changed = false;
            foreach(Measure measure in system.Measures) {
                // Group locations use a representative leaf, not an inferred barycenter.
                string? reference = Representative(measure.Reference), companion = Representative(measure.Companion);
                if(reference is null || companion is null || reference == companion ||
                    measure.Theta is not double theta || theta < 0 || theta > 360 ||
                    measure.Rho is not double rho || rho <= 0)
                    continue;
                double angle = theta * Math.PI / 180;
                Offset delta = new(rho * Math.Sin(angle), rho * Math.Cos(angle));
                if(positions.TryGetValue(reference, out var first) && !positions.ContainsKey(companion)) {
                    positions[companion] = new(first.East + delta.East, first.North + delta.North);
                    if(approximated.Contains(reference))
                        approximated.Add(companion);
                    changed = true;
                } else if(positions.TryGetValue(companion, out var second) && !positions.ContainsKey(reference)) {
                    positions[reference] = new(second.East - delta.East, second.North - delta.North);
                    if(approximated.Contains(companion))
                        approximated.Add(reference);
                    changed = true;
                }
            }
            if(!changed && approximateCoordinates) {
                // Exhaust measured links before anchoring one disconnected group from RA/Dec.
                foreach(var star in system.Members) {
                    if(positions.ContainsKey(star.Label) || CoordinateOffset(system.Primary, star) is not Offset offset)
                        continue;
                    positions[star.Label] = offset;
                    approximated.Add(star.Label);
                    changed = true;
                    break;
                }
            }
        } while(changed);
        approximateLabels?.UnionWith(approximated);
        return positions;
    }

    private static Offset? CoordinateOffset(Star primary, Star star) {
        if(primary.Number("ra") is not double primaryRa || primary.Number("dec") is not double primaryDec ||
            star.Number("ra") is not double ra || star.Number("dec") is not double dec)
            return null;
        if(primaryRa is < 0 or >= 24 || ra is < 0 or >= 24 ||
            primaryDec is < -90 or > 90 || dec is < -90 or > 90)
            throw new InvalidDataException("Out-of-range RA/Dec in multiple-star plot.");
        double deltaRa = (ra - primaryRa) * Math.PI / 12;
        double latitude = dec * Math.PI / 180, originLatitude = primaryDec * Math.PI / 180;
        double east = Math.Cos(latitude) * Math.Sin(deltaRa);
        double north = Math.Cos(originLatitude) * Math.Sin(latitude) -
            Math.Sin(originLatitude) * Math.Cos(latitude) * Math.Cos(deltaRa);
        double forward = Math.Sin(originLatitude) * Math.Sin(latitude) +
            Math.Cos(originLatitude) * Math.Cos(latitude) * Math.Cos(deltaRa);
        double length = Math.Sqrt(east * east + north * north);
        // Spherical angular offset: RA is hours, Dec degrees, output east/north arcseconds.
        double scale = (length > 0 ? Math.Atan2(length, forward) / length : 1) * 180 / Math.PI * 3600;
        return new(east * scale, north * scale);
    }

    public static string MissingPositionText(IEnumerable<Star> members, IReadOnlyDictionary<string, Offset> layout) {
        string labels = string.Join(", ", members.Where(star => !layout.ContainsKey(star.Label)).Select(star => star.Label));
        return labels.Length == 0 ? "" : $"Not Shown - {labels}";
    }

    public static bool HasNoPositionData(Star star, System system) {
        if(star.Id == system.Primary.Id || !star.IsWdsPosition ||
            star.Number("ra").HasValue && star.Number("dec").HasValue)
            return false;
        bool Includes(string endpoint) => endpoint == star.Label ||
            (endpoint.All(char.IsUpper) ? endpoint.Contains(star.Label[0]) :
                star.Label.StartsWith(endpoint, StringComparison.Ordinal) &&
                star.Label.Length > endpoint.Length && !char.IsDigit(endpoint[^1]));
        return !system.Measures.Any(measure => measure.Rho is > 0 &&
            (Includes(measure.Reference) || Includes(measure.Companion)));
    }

    public static bool IsWithinMagnitudeCutoff(double? magnitude, double cutoff) =>
        magnitude is double value && double.IsFinite(value) && value <= cutoff;

    public static bool IsWithinCenterRadius(double x, double y, double centerX, double centerY,
        double screenHeight, double radiusPercent) {
        double radius = screenHeight * Math.Clamp(radiusPercent, 0, 50) / 100;
        double dx = x - centerX, dy = y - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    public static bool IsDimComponent(Star star, Star primary, double cutoff) =>
        star.Id != primary.Id && star.Number("mag") is double magnitude && magnitude > cutoff;

    public static bool IsEntryVisible(Star star, bool excludeWdsOnly, bool excludeVisual = false) =>
        (!excludeWdsOnly || !star.IsWdsPosition) && (!excludeVisual || star.Role != "V");

    public static bool HasEligibleCompanion(System system, double cutoff, bool excludeWdsOnly = false, bool excludeVisual = false) =>
        IsEntryVisible(system.Primary, excludeWdsOnly, excludeVisual) &&
        system.Members.Any(star => star.Id != system.Primary.Id && IsEntryVisible(star, excludeWdsOnly, excludeVisual) &&
            !IsDimComponent(star, system.Primary, cutoff));

    public static bool IsPlotVisible(Star star, Star primary, double cutoff, IReadOnlyDictionary<long, bool> overrides,
        bool excludeWdsOnly = false, bool excludeVisual = false) =>
        IsEntryVisible(star, excludeWdsOnly, excludeVisual) && (star.Id == primary.Id || (overrides.TryGetValue(star.Id, out bool visible)
            ? visible : !IsDimComponent(star, primary, cutoff)));

    public static Offset PlotPosition(Offset offset, double extent, double size, double zoom, Offset pan) {
        double scale = extent > 0 ? (size * 0.5 - 36) / extent * zoom : zoom;
        return new(size * 0.5 - (offset.East - pan.East) * scale,
            size * 0.5 - (offset.North - pan.North) * scale);
    }

    public static (double? Ra, double? Dec) RelativeProperMotion(Star star, Star primary) {
        if(star.Role is not ("V" or "Q"))
            return (null, null);
        return (star.Number("pmra") - primary.Number("pmra"),
            star.Number("pmdec") - primary.Number("pmdec"));
    }

    public static long? PickNearest(IEnumerable<Target> targets, float x, float y, float radius) {
        long? selected = null;
        float best = radius * radius;
        foreach(Target target in targets) {
            float dx = target.X - x, dy = target.Y - y;
            float distance = dx * dx + dy * dy;
            if(distance < best || distance == best && (!selected.HasValue || target.Id < selected.Value)) {
                best = distance;
                selected = target.Id;
            }
        }
        return selected;
    }
}
