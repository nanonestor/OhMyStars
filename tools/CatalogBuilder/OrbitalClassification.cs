using OhMyStars;

namespace CatalogBuilder;

internal static class OrbitalClassification
{
    internal static HashSet<long> Companions(string catalog, string orbits, string wdsSource)
    {
        var stars = MultipleStarCatalog.Load(catalog);
        var elements = MultipleStarOrbits.Load(orbits);
        var orbitalPairs = elements.Orbits.Select(o => (o.Wds, o.Reference, o.Companion)).ToHashSet();
        HashSet<int> orbitalLines = [];
        int lineNumber = 0;
        foreach (string line in File.ReadLines(wdsSource))
        {
            lineNumber++;
            if (line.Length != 130) continue;
            var endpoints = Pair.Endpoints(Program.Slice(line, 17, 5));
            if (!Pair.Excluded(Program.Slice(line, 107, 4)) &&
                orbitalPairs.Contains((line[..10], endpoints.First, endpoints.Second)))
                orbitalLines.Add(lineNumber);
        }
        HashSet<long> result = [];
        foreach (var system in stars.Systems)
        {
            var curves = elements.Curves(system, MultipleStarCatalog.Layout(system));
            var labels = curves.Select(curve => curve.Companion).ToHashSet(StringComparer.Ordinal);
            foreach (var member in system.Members.Where(member => labels.Contains(member.Label)))
                result.Add(member.Id);
            // WDS measurements of a parent endpoint belong to its first leaf (B -> Ba).
            // This is membership evidence for that representative, not an individual orbit to plot.
            foreach (var member in system.Members)
                if (member["wds_source_line"].Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => orbitalLines.Contains(int.Parse(line, Program.Invariant))))
                    result.Add(member.Id);
        }
        return result;
    }

    internal static Dictionary<string, long> Apply(string catalog, string orbits, string wdsSource)
    {
        var companions = Companions(catalog, orbits, wdsSource);
        string[] header = Csv.Parse(File.ReadLines(catalog).First());
        int id = Array.IndexOf(header, "oms_id"), role = Array.IndexOf(header, "component");
        Dictionary<string, long> promoted = [];
        string staging = catalog + ".tmp";
        using (var writer = new StreamWriter(staging))
        {
            writer.WriteLine(Csv.Line(header));
            foreach (string line in File.ReadLines(catalog).Skip(1))
            {
                string[] row = Csv.Parse(line);
                if (row[role] is "Q" or "V" && companions.Contains(long.Parse(row[id], Program.Invariant)))
                {
                    promoted[row[role]] = promoted.GetValueOrDefault(row[role]) + 1;
                    row[role] = "C";
                    writer.WriteLine(Csv.Line(row));
                }
                else writer.WriteLine(line);
            }
        }
        File.Move(staging, catalog, true);
        return promoted;
    }
}
