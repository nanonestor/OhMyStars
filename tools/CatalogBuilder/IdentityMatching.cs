using System.Text.RegularExpressions;

namespace CatalogBuilder;

internal sealed partial class Builder
{
    private readonly Dictionary<string, HashSet<string>> _componentIdentities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _primaryAliases = new(StringComparer.Ordinal);
    private readonly HashSet<string> _matchingKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Candidate>> _identityCandidates = new(StringComparer.OrdinalIgnoreCase);

    private void LoadMatchingIdentities()
    {
        LoadHdReferences();
        void Add(Dictionary<string, HashSet<string>> references, string scope, string key)
        {
            if (!references.TryGetValue(scope, out var keys)) references[scope] = keys = new(StringComparer.OrdinalIgnoreCase);
            keys.Add(key);
            _matchingKeys.Add(key);
        }
        foreach (string hip in _hipReferences.Values)
            _matchingKeys.Add("hip:" + hip);
        foreach (var ids in _hdByDm.Values.Where(ids => ids.Count == 1))
            _matchingKeys.Add("hd:" + ids.Single());
        foreach (var (wds, notes) in _notes)
            foreach (string note in notes)
                foreach (Match match in Regex.Matches(note, @"\b([A-Z](?:[a-z](?:\d+)?)?)\s+(?:is|=)\s*(HD|HIP)\s*(\d+)\b"))
                    Add(_componentIdentities, wds + ":" + match.Groups[1].Value,
                        match.Groups[2].Value.ToLowerInvariant() + ":" + match.Groups[3].Value);

        foreach (string line in File.ReadLines(Input("WDS", "bf.txt")))
        {
            if (line.Length < 20 || !Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) continue;
            string scope = line[..10] + "|" + Program.Compact(line.Substring(11, 7));
            foreach (string alias in Regex.Split(line[20..].Trim(), @"\s{2,}").Where(s => s is not ("" or ".")))
            {
                if (BayerAlias(alias) is { } bayer)
                    Add(_primaryAliases, scope, "bayer:" + bayer.Bayer + " " + bayer.Constellation);
                else if (Regex.IsMatch(alias, @"^\d+ [A-Za-z]{3}$"))
                    Add(_primaryAliases, scope, "flam:" + alias);
                else
                    Add(_primaryAliases, scope, "proper:" + alias);
            }
        }
    }

    private void IndexMatchingIdentities(string[] fields, Candidate candidate)
    {
        foreach (string key in IdentityKeys(fields))
        {
            if (!_matchingKeys.Contains(key)) continue;
            if (!_identityCandidates.TryGetValue(key, out var candidates)) _identityCandidates[key] = candidates = [];
            candidates.Add(candidate);
        }
    }

    private Candidate? MatchComponentIdentity(SystemGroup system, Pair pair, string label,
        double? ra, double? dec, double? magnitude)
    {
        HashSet<string> strong = new(StringComparer.OrdinalIgnoreCase);
        if (_componentIdentities.TryGetValue(system.Wds + ":" + label, out var explicitKeys))
            strong.UnionWith(explicitKeys);
        // HDS references describe the first endpoint, not an arbitrary leaf of a blended group.
        if (pair.First == label && _hipReferences.TryGetValue(pair.Wds + "|" + pair.Discoverer, out var hip))
            strong.Add("hip:" + hip);
        if (pair.First == label && _wdsSource.TryGetValue(pair.Line, out var source))
        {
            string dm = WdsDm(source.Substring(98, 8));
            if (_hdByDm.TryGetValue(dm, out var ids) && ids.Count == 1)
            {
                string key = "hd:" + ids.Single();
                // DM references can describe the system rather than a resolved component.
                if (_identityCandidates.TryGetValue(key, out var candidates) && candidates.Count == 1 &&
                    ra is double a && dec is double d && magnitude is double expected &&
                    candidates[0].Mag is double actual && Math.Abs(actual - expected) <= 1 &&
                    Spatial.Distance(a, d, candidates[0].Ra, candidates[0].Dec) <= 6)
                    strong.Add(key);
            }
        }
        var exact = ResolveIdentityKeys(strong);
        if (exact.Conflict)
        {
            Review(pair, "identity_first_conflicting_or_shared_identifiers_" + label);
            Count("identity_first_ambiguous_identifiers");
            return null;
        }
        if (exact.Candidate is Candidate identified)
            return identified;

        // Name/Bayer/Flamsteed aliases may name a whole pair: only consider an unexpanded A.
        if (label != "A" || system.PrimaryLabel != "A" || pair.First != "A" ||
            system.Hierarchy!.Leaves("A").Length != 1 || ra is null || dec is null || magnitude is null)
            return null;
        if (!_primaryAliases.TryGetValue(pair.Wds + "|" + pair.Discoverer, out var aliases))
            return null;
        var named = ResolveIdentityKeys(aliases);
        if (!named.Conflict && named.Candidate is Candidate candidate && candidate.Mag is double mag &&
            Math.Abs(mag - magnitude.Value) <= 1 && Spatial.Distance(ra.Value, dec.Value, candidate.Ra, candidate.Dec) <= 6)
            return candidate;
        return null;
    }

    private (Candidate? Candidate, bool Conflict) ResolveIdentityKeys(IEnumerable<string> keys)
    {
        Candidate? result = null;
        foreach (string key in keys)
        {
            if (!_identityCandidates.TryGetValue(key, out var matches)) continue;
            if (matches.Count != 1) return (null, true);
            if (result is Candidate previous && previous.Id != matches[0].Id) return (null, true);
            result = matches[0];
        }
        return (result, false);
    }

    private (double? Ra, double? Dec) AnchoredCompanionPosition(SystemGroup system, Pair pair)
    {
        if (system.Members.TryGetValue(pair.First, out var reference) && reference.Position is Candidate anchor)
        {
            Count("athyg_anchored_companion_searches");
            return CompanionPosition(pair with { Ra = anchor.Ra, Dec = anchor.Dec });
        }
        return pair.First == system.PrimaryLabel ? CompanionPosition(pair) : (null, null);
    }
}
