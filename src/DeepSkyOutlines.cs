using System.Globalization;

namespace OhMyStars;

internal sealed class DeepSkyOutlines {
    internal readonly record struct Coordinate(double RaHours, double DecDegrees);
    internal sealed record Contour(DeepSkyCatalog.Entry Entry, string SourceName, Coordinate[] Points);

    public Contour[] Contours { get; }
    public string[] UnmatchedNames { get; }
    public int SourceContourCount { get; }
    public int SourcePointCount { get; }

    private DeepSkyOutlines(Contour[] contours, string[] unmatched, int contourCount, int pointCount) {
        Contours = contours;
        UnmatchedNames = unmatched;
        SourceContourCount = contourCount;
        SourcePointCount = pointCount;
    }

    public static DeepSkyOutlines Load(string path, DeepSkyCatalog catalog) {
        Dictionary<string, DeepSkyCatalog.Entry> primaryNames = new(StringComparer.Ordinal);
        Dictionary<string, DeepSkyCatalog.Entry?> aliases = new(StringComparer.Ordinal);
        foreach(var entry in catalog.Entries) {
            if(!entry.CanDraw)
                continue;
            primaryNames.Add(DeepSkyCatalog.Normalize(entry.Name), entry);
            foreach(string identifier in entry.Identifiers) {
                string key = DeepSkyCatalog.Normalize(identifier);
                if(aliases.TryGetValue(key, out var previous) && previous != entry)
                    aliases[key] = null;
                else
                    aliases[key] = entry;
            }
        }
        List<Contour> contours = [];
        HashSet<string> unmatched = new(StringComparer.Ordinal);
        List<Coordinate>? points = null;
        string? name = null;
        int lineNumber = 0, contourCount = 0, pointCount = 0;
        foreach(string raw in File.ReadLines(path)) {
            lineNumber++;
            if(string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith('#'))
                continue;
            string[] parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length < 3 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double ra) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double dec) ||
                !double.IsFinite(ra) || !double.IsFinite(dec) || ra is < 0 or >= 24 || dec is < -90 or > 90)
                throw new InvalidDataException($"{path}:{lineNumber}: invalid outline coordinate.");
            Coordinate point = new(ra, dec);
            switch(parts[2]) {
                case "start":
                    if(parts.Length != 4 || points is not null)
                        throw new InvalidDataException($"{path}:{lineNumber}: invalid or nested outline start.");
                    name = parts[3];
                    points = [point];
                    break;
                case "vertex":
                case "end":
                    if(parts.Length != 3 || points is null)
                        throw new InvalidDataException($"{path}:{lineNumber}: outline vertex/end without start.");
                    points.Add(point);
                    if(parts[2] == "end") {
                        if(points.Count < 4 || points[0] != points[^1] || points.Distinct().Count() < 3)
                            throw new InvalidDataException($"{path}:{lineNumber}: outline must be a closed polygon with at least three distinct points.");
                        contourCount++;
                        string key = DeepSkyCatalog.Normalize(name!);
                        if(!primaryNames.TryGetValue(key, out var entry))
                            entry = aliases.GetValueOrDefault(key);
                        if(entry is null) {
                            unmatched.Add(name!);
                        } else {
                            points.RemoveAt(points.Count - 1);
                            contours.Add(new(entry, name!, points.ToArray()));
                        }
                        points = null;
                        name = null;
                    }
                    break;
                default:
                    throw new InvalidDataException($"{path}:{lineNumber}: unknown outline command '{parts[2]}'.");
            }
            pointCount++;
        }
        if(points is not null || contourCount == 0)
            throw new InvalidDataException($"{path}: incomplete or empty outline catalog.");
        return new(contours.ToArray(), unmatched.Order(StringComparer.Ordinal).ToArray(), contourCount, pointCount);
    }
}
