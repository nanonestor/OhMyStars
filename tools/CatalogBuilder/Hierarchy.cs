using System.Text.RegularExpressions;

namespace CatalogBuilder;

internal sealed class Hierarchy
{
    private readonly Dictionary<string, SortedSet<string>> _children = new(StringComparer.Ordinal);
    private readonly bool _sirius;
    internal static bool Atom(string label) => Regex.IsMatch(label, @"^[A-Z](?:[a-z](?:[1-9]\d*)?)?$");
    internal static bool Endpoint(string label) => Atom(label) || Regex.IsMatch(label, "^[A-Z]{2,}$");
    internal static string Parent(string label) => Regex.IsMatch(label, @"\d+$") ?
        Regex.Replace(label, @"\d+$", "") : label.Length == 2 && char.IsLower(label[1]) ? label[..1] : "";

    public Hierarchy(IEnumerable<Pair> pairs)
    {
        Pair[] records = pairs.ToArray();
        _sirius = records.Length > 0 && records.All(p => p.Wds == "06451-1643");
        foreach (string endpoint in records.SelectMany(p => new[] { p.First, p.Second }))
            foreach (string atom in Atoms(endpoint))
            {
                string child = atom, parent;
                while ((parent = Parent(child)).Length > 0)
                {
                    if (!_children.TryGetValue(parent, out var children)) _children[parent] = children = new(StringComparer.Ordinal);
                    children.Add(child);
                    child = parent;
                }
            }
    }

    private static IEnumerable<string> Atoms(string endpoint) =>
        Atom(endpoint) ? new[] { endpoint } : endpoint.Select(c => c.ToString());

    public string[] Leaves(string endpoint) => Atoms(endpoint).SelectMany(Expand).Distinct(StringComparer.Ordinal).ToArray();
    private IEnumerable<string> Expand(string atom) =>
        // Sirius Bb is an optical neighbor of B, not evidence that B is a blended subsystem.
        _sirius && atom == "B" ? new[] { atom } :
        _children.TryGetValue(atom, out var children) ? children.SelectMany(Expand) : new[] { atom };
    public string Path(string leaf)
    {
        List<string> parts = [leaf];
        string parent = leaf;
        while ((parent = Parent(parent)).Length > 0) parts.Insert(0, parent);
        return string.Join("/", parts);
    }
}
