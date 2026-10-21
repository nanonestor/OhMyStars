using System.Text.RegularExpressions;

namespace CatalogBuilder;

internal sealed partial class Builder
{
    internal static readonly string[] IdentifierColumns = ["tyc", "hyg", "hip", "hd", "hr", "gl", "bayer", "flam", "con", "proper"];
    private readonly List<string[]> _identifierAdditions = [];
    private readonly Dictionary<string, HashSet<string>> _hdByDm = new(StringComparer.Ordinal);

    private void LoadHdReferences()
    {
        foreach (string sourceLine in File.ReadLines(Input("Henry Draper", "catalog.dat")))
        {
            if (sourceLine.Length is < 28 or > 48) throw new InvalidDataException("Unexpected HD record width.");
            string line = sourceLine.PadRight(48);
            string hd = line[..6].Trim();
            if (line[47] == 'D' || hd == "194648") continue;
            string dm = NormalizeDm(line.Substring(6, 10), line[16], line[17]);
            if (dm.Length == 0) continue;
            if (!_hdByDm.TryGetValue(dm, out var ids)) _hdByDm[dm] = ids = new(StringComparer.Ordinal);
            ids.Add(hd);
        }
    }

    internal static (string Bayer, string Constellation)? BayerAlias(string alias)
    {
        Match match = Regex.Match(alias, @"^([a-z]{2,3})(?:[- ](\d+))?\s+([a-z]{3})$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        string letter = match.Groups[1].Value.ToLowerInvariant();
        if (!new[] { "alp", "bet", "gam", "del", "eps", "zet", "eta", "the", "iot", "kap", "lam", "mu", "nu", "xi",
            "omi", "pi", "rho", "sig", "tau", "ups", "phi", "chi", "psi", "ome" }.Contains(letter)) return null;
        string con = match.Groups[3].Value.ToLowerInvariant();
        con = con switch { "cvn" => "CVn", "cma" => "CMa", "cmi" => "CMi", "cra" => "CrA", "crb" => "CrB",
            "lmi" => "LMi", "psa" => "PsA", "tra" => "TrA", "uma" => "UMa", "umi" => "UMi",
            _ => char.ToUpperInvariant(con[0]) + con[1..] };
        return (char.ToUpperInvariant(letter[0]) + letter[1..] +
            (match.Groups[2].Success ? "-" + match.Groups[2].Value : ""), con);
    }

    private void EnrichIdentifiers()
    {
        void Fill(Star star, string column, string value, string evidence)
        {
            if (value.Length == 0 || Field(star.Fields, column).Length > 0) return;
            star.Fields[_columns[column]] = value;
            _identifierAdditions.Add([star.Key, column, value, evidence]);
            Count("filled_" + column);
        }

        foreach (SystemGroup system in _systems)
        {
            foreach (Pair pair in system.Pairs)
            {
                if (!system.Members.TryGetValue(pair.First, out var first)) continue;
                if (_hipReferences.TryGetValue(pair.Wds + "|" + pair.Discoverer, out string? hip))
                    Fill(first, "hip", hip, $"hds.txt:{pair.Wds}:{pair.Discoverer}; individual endpoint {pair.First}");
                string raw = _wdsSource[pair.Line];
                string dm = WdsDm(raw.Substring(98, 8));
                if (dm.Length > 0 && _hdByDm.TryGetValue(dm, out var ids) && ids.Count == 1)
                    Fill(first, "hd", ids.Single(), $"WDS DM {dm} -> III/135A exact unique DM");
            }
            foreach (string note in system.Notes)
            {
                foreach (Match match in Regex.Matches(note, @"\b([A-Z](?:[a-z](?:\d+)?)?)\s+(?:is|=)\s*(HD|HIP)\s*(\d+)\b"))
                    if (system.Members.TryGetValue(match.Groups[1].Value, out var member))
                        Fill(member, match.Groups[2].Value.ToLowerInvariant(), match.Groups[3].Value, "explicit component-scoped WDS note: " + note.Trim());
            }
        }

        var wanted = new Dictionary<string, List<Star>>(StringComparer.OrdinalIgnoreCase);
        void Want(Star star, string key)
        {
            if (!wanted.TryGetValue(key, out var list)) wanted[key] = list = [];
            if (!list.Contains(star)) list.Add(star);
        }
        foreach (Star star in AllStars())
            foreach (string key in IdentityKeys(star.Fields))
                Want(star, key);
        var byWds = _systems.ToDictionary(s => s.Wds, StringComparer.Ordinal);
        Dictionary<Star, List<(string Column, string Value, string Evidence)>> scopedAliases = new();
        foreach (string line in File.ReadLines(Input("WDS", "bf.txt")))
        {
            if (line.Length < 20 || !byWds.TryGetValue(line[..10], out var system)) continue;
            string discoverer = Program.Compact(line.Substring(11, 7));
            string[] endpoints = system.Pairs.Where(p => p.Discoverer == discoverer).Select(p => p.First).Distinct().ToArray();
            // A discoverer can cover several unrelated endpoints; never guess an individual identity.
            if (endpoints.Length != 1 || !system.Members.TryGetValue(endpoints[0], out var target)) continue;
            if (!scopedAliases.TryGetValue(target, out var aliases)) scopedAliases[target] = aliases = [];
            foreach (string alias in Regex.Split(line[20..].Trim(), @"\s{2,}").Where(s => s is not ("" or ".")))
            {
                var bayer = BayerAlias(alias);
                Match flam = Regex.Match(alias, @"^(\d+) ([A-Za-z]{3})$");
                string evidence = $"bf.txt:{system.Wds}:{discoverer}; individual endpoint {endpoints[0]}; alias {alias}";
                if (bayer is { } designation)
                {
                    aliases.Add(("bayer", designation.Bayer, evidence));
                    aliases.Add(("con", designation.Constellation, evidence));
                    // A Bayer alias can describe a blended source: store the scoped alias, not a false row identity.
                }
                else if (flam.Success)
                {
                    aliases.Add(("flam", flam.Groups[1].Value, evidence));
                    aliases.Add(("con", flam.Groups[2].Value, evidence));
                }
                else Want(target, "proper:" + alias);
            }
        }
        // Query the full source by exact aliases; a shared catalog ID is not an individual-star match.
        var matches = new Dictionary<Star, Dictionary<string, string[]>>();
        using (var reader = File.OpenText(Input("AT-HYG_40", "athyg_40.csv")))
        {
            reader.ReadLine();
            while (reader.ReadLine() is string line)
            {
                string[] fields = Csv.Parse(line);
                foreach (string key in IdentityKeys(fields))
                {
                    if (!wanted.TryGetValue(key, out var stars)) continue;
                    foreach (Star star in stars)
                    {
                        if (!matches.TryGetValue(star, out var rows)) matches[star] = rows = new(StringComparer.Ordinal);
                        rows[Field(fields, "id")] = fields;
                    }
                }
            }
        }
        foreach (var (matchedStar, rows) in matches)
        {
            if (rows.Count != 1) continue;
            string[] original = rows.Values.Single();
            Star star = matchedStar;
            if (star.AthygId is null && !_wdsStars.ContainsKey(star.Key)) continue;
            int matchedId = int.Parse(Field(original, "id"), Program.Invariant);
            if (star.AthygId is int sourceIdentity && sourceIdentity != matchedId)
            {
                if (star.System is not null)
                    Review(star.System.Pairs.First(), $"identifier_link_conflicting_identity_{star.Component}: AT40:{sourceIdentity} versus AT40:{matchedId}");
                continue;
            }
            if (star.AthygId is null)
            {
                int sourceId = matchedId;
                Star target = Keep(sourceId);
                if (target.System is not null && (target.System != star.System || target.Component != star.Component))
                {
                    OmitDuplicateRelationships(star);
                    Count("wds_duplicate_identifier_relationships_omitted");
                    continue;
                }
                target.Fields = original.ToArray();
                target.System = star.System;
                target.Component = star.Component;
                target.H.UnionWith(star.H); target.Stf.UnionWith(star.Stf); target.Stt.UnionWith(star.Stt); target.Bds.UnionWith(star.Bds); target.Bu.UnionWith(star.Bu);
                if (scopedAliases.Remove(star, out var aliases)) scopedAliases[target] = aliases;
                star.System!.Members[star.Component] = target;
                _wdsStars.Remove(star.Key);
                string[][] pending = _identifierAdditions.Where(a => a[0] == star.Key).ToArray();
                _identifierAdditions.RemoveAll(a => a[0] == star.Key);
                foreach (string[] addition in pending) Fill(target, addition[1], addition[2], addition[3]);
                star = target;
                Count("wds_id_links_reused_full_athyg_rows");
            }
            foreach (string column in IdentifierColumns)
                Fill(star, column, Field(original, column), "unique exact identifier link to AT40:" + Field(original, "id"));
        }
        foreach (var (star, aliases) in scopedAliases)
        {
            if (star.System is null) continue;
            string existingCon = Field(star.Fields, "con");
            if (existingCon.Length > 0 && aliases.Any(a => a.Column == "con" &&
                !a.Value.Equals(existingCon, StringComparison.OrdinalIgnoreCase))) continue;
            foreach (var group in aliases.GroupBy(a => a.Column))
            {
                var candidates = group.ToArray();
                if (group.Key == "bayer" && candidates.Any(a => a.Value.Contains('-')))
                    candidates = candidates.Where(a => a.Value.Contains('-')).ToArray();
                if (candidates.Select(a => a.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                    Fill(star, group.Key, candidates[0].Value, candidates[0].Evidence);
            }
        }
        HashSet<string> retainedKeys = AllStars().Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        _identifierAdditions.RemoveAll(a => !retainedKeys.Contains(a[0]));
        foreach (string column in IdentifierColumns)
        {
            _counts.Remove("filled_" + column);
            long n = _identifierAdditions.Count(a => a[1] == column);
            if (n > 0) _counts["filled_" + column] = n;
        }
        using var writer = new StreamWriter(Path.Combine(output, "identifier-additions.csv"), false);
        writer.WriteLine("source_key,column,value,evidence");
        foreach (string[] addition in _identifierAdditions) writer.WriteLine(Csv.Line(addition));
    }

    private void OmitDuplicateRelationships(Star duplicate)
    {
        SystemGroup system = duplicate.System ?? throw new InvalidDataException("Duplicate WDS star lacks a source group.");
        system.Pairs.RemoveAll(p => system.Hierarchy!.Leaves(p.First).Concat(system.Hierarchy.Leaves(p.Second)).Contains(duplicate.Component));
        HashSet<string> participating = system.Pairs
            .SelectMany(p => system.Hierarchy!.Leaves(p.First).Concat(system.Hierarchy.Leaves(p.Second)))
            .ToHashSet(StringComparer.Ordinal);
        if (!participating.Contains(system.PrimaryLabel)) participating.Clear();
        foreach (string label in system.Members.Keys.Where(k => !participating.Contains(k)).ToArray())
        {
            Star star = system.Members[label];
            star.System = null;
            star.Component = "";
            star.H.Clear(); star.Stf.Clear(); star.Stt.Clear(); star.Bds.Clear(); star.Bu.Clear();
            if (star.AthygId is null) _wdsStars.Remove(star.Key);
            else if (!_baseIds.Contains(star.AthygId.Value)) _athyg.Remove(star.AthygId.Value);
            system.Members.Remove(label);
        }
        if (participating.Count == 0) _systems.Remove(system);
    }

    private IEnumerable<string> IdentityKeys(string[] fields)
    {
        foreach (string name in new[] { "tyc", "hip", "hd", "hr", "gl", "hyg", "proper" })
        {
            string value = Field(fields, name);
            if (value.Length > 0) yield return name + ":" + value;
        }
        string con = Field(fields, "con");
        if (con.Length > 0)
            foreach (string name in new[] { "bayer", "flam" })
                if (Field(fields, name).Length > 0) yield return name + ":" + Field(fields, name) + " " + con;
    }

    internal static string NormalizeDm(string core, char supplement = ' ', char component = ' ')
    {
        Match m = Regex.Match(core, @"^(BD|CD|CP|AG)\s*([+-])(\d{1,2})\s*(\d+)$");
        if (!m.Success || supplement != ' ' || component != ' ') return "";
        return m.Groups[1].Value + m.Groups[2].Value +
            int.Parse(m.Groups[3].Value, Program.Invariant).ToString(Program.Invariant) + ":" +
            int.Parse(m.Groups[4].Value, Program.Invariant).ToString(Program.Invariant);
    }

    internal static string WdsDm(string raw)
    {
        Match m = Regex.Match(raw.Trim(), @"^([+-])(\d{2})\s*(\d+)$");
        if (!m.Success) return "";
        int zone = int.Parse(m.Groups[2].Value, Program.Invariant);
        string prefix = m.Groups[1].Value == "+" || zone <= 22 ? "BD" : zone <= 51 ? "CD" : "CP";
        return NormalizeDm(prefix + m.Groups[1].Value + m.Groups[2].Value + " " + m.Groups[3].Value);
    }
}
