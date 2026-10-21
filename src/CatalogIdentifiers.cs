namespace OhMyStars;

internal static class CatalogIdentifiers {
    internal static readonly (string Name, string Column)[] Catalogs = [
        ("OMS", "oms_id"), ("AT-HYG", "id"), ("Tycho", "tyc"), ("Gaia", "gaia"),
        ("HYG", "hyg"), ("Hipparcos", "hip"), ("HD", "hd"), ("HR", "hr"),
        ("Gliese", "gl"), ("Flamsteed", "flam"), ("Herschel", "h"),
        ("F. Struve", "stf"), ("O. Struve", "stt"), ("Burnham", "bu"),
        ("BDS", "bds"), ("WDS", "wds_id")
    ];

    internal static IEnumerable<(string Name, string Value)> Entries(MultipleStarCatalog.Star star,
        bool includeCatalogNames = true, bool struveSymbols = false) {
        if(!string.IsNullOrWhiteSpace(star["proper"]))
            yield return ("Proper", star["proper"]);
        string bayer = StarDesignations.Bayer(star["bayer"], star["con"]);
        if(bayer.Length > 0)
            yield return ("Bayer", bayer);
        foreach(var (name, column) in Catalogs)
            if(!string.IsNullOrWhiteSpace(star[column]))
                yield return (name, column == "flam"
                    ? StarDesignations.Heading("", star[column], star["con"])
                    : StarDesignations.CatalogIdentifier(column,
                        includeCatalogNames ? column == "hip" ? "HIP" : name : "", star[column], struveSymbols));
    }

    private static readonly string[] SearchColumns = ["proper", "bayer", .. Catalogs.Select(c => c.Column)];
    private static readonly IReadOnlyDictionary<string, string> CatalogNames =
        Catalogs.ToDictionary(c => c.Column, c => c.Name);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MultipleStarCatalog,
        (long Id, string Label, string Raw)[]> BayerAliases = new();

    private static (long Id, string Label, string Raw)[] SystemBayerAliases(MultipleStarCatalog catalog) =>
        BayerAliases.GetValue(catalog, source => source.Systems.SelectMany(system =>
            system.Members.Where(star => !string.IsNullOrWhiteSpace(star["con"]))
                .Select(star => (Label: StarDesignations.Bayer(star["bayer"], star["con"]), Raw: star["bayer"]))
                .Where(alias => StarDesignations.UnnumberedBayer(alias.Raw) != alias.Raw)
                .Select(alias => (Id: system.Primary.Id, Label: StarDesignations.UnnumberedBayer(alias.Label),
                    Raw: StarDesignations.UnnumberedBayer(alias.Raw)))
                .Distinct()).OrderBy(alias => alias.Label, StringComparer.Ordinal).ThenBy(alias => alias.Id).ToArray());

    internal static IEnumerable<(long Id, string Label)> SearchSystemBayerAliases(MultipleStarCatalog catalog, string query) =>
        SystemBayerAliases(catalog).Where(alias => alias.Label.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
            alias.Raw.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).Select(alias => (alias.Id, alias.Label));

    internal static string SearchLabel(MultipleStarCatalog.Star star, string column) {
        if(column.Length == 0)
            return !string.IsNullOrWhiteSpace(star["proper"]) ? star["proper"] :
                !string.IsNullOrWhiteSpace(star["bayer"]) ? StarDesignations.Bayer(star["bayer"], star["con"]) :
                $"OMS {star.Id}";
        string value = star[column];
        if(string.IsNullOrWhiteSpace(value))
            return "";
        return column switch {
            "proper" => value,
            "bayer" => StarDesignations.Bayer(value, star["con"]),
            "flam" => $"Flamsteed {StarDesignations.Heading("", value, star["con"])}",
            _ => StarDesignations.CatalogIdentifier(column, CatalogNames[column], value)
        };
    }

    internal static IEnumerable<(long Id, string Label)> Search(MultipleStarCatalog catalog, string column, string query,
        CancellationToken cancellationToken = default, int partialLimit = int.MaxValue) {
        query = query.Trim();
        if(column is "" or "bayer")
            foreach(var alias in SearchSystemBayerAliases(catalog, query)) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return alias;
            }
        string[] columns = column.Length == 0 ? SearchColumns : [column];
        List<(long Id, string Label)> partialMatches = [];
        foreach(var star in catalog.Stars.Values) {
            cancellationToken.ThrowIfCancellationRequested();
            string? partialLabel = null;
            bool exact = false;
            foreach(string candidate in columns) {
                if(string.IsNullOrWhiteSpace(star[candidate]))
                    continue;
                string value = star[candidate];
                bool rawMatch = query.Length == 0 || value.Contains(query, StringComparison.OrdinalIgnoreCase);
                // Plain labels add only "Catalog:"; avoid constructing them unless that prefix can match.
                if(!rawMatch && candidate is not ("bayer" or "flam" or "stf" or "stt" or "bu" or "h" or "hd")) {
                    if(candidate == "proper")
                        continue;
                    string name = CatalogNames[candidate];
                    if(!query.Contains(':') && !name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                string label = SearchLabel(star, candidate);
                string bayerAlias = candidate == "bayer" ? StarDesignations.UnnumberedBayer(label) : "";
                string rawBayerAlias = candidate == "bayer" ? StarDesignations.UnnumberedBayer(value) : "";
                if(query.Length > 0 && (IsExactAlias(star[candidate], query) || IsExactAlias(label, query) ||
                    candidate == "bayer" && (IsExactAlias(bayerAlias, query) || IsExactAlias(rawBayerAlias, query)))) {
                    yield return (star.Id, label);
                    exact = true;
                    break;
                }
                if(rawMatch ||
                    label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    candidate == "bayer" && (bayerAlias.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        rawBayerAlias.Contains(query, StringComparison.OrdinalIgnoreCase))) {
                    if(query.Length == 0) {
                        yield return (star.Id, label);
                        break;
                    }
                    partialLabel ??= label;
                }
            }
            if(!exact && partialLabel != null && partialMatches.Count < partialLimit)
                partialMatches.Add((star.Id, partialLabel));
        }
        foreach(var match in partialMatches)
            yield return match;
    }

    private static bool IsExactAlias(string value, string query) =>
        value.Trim().Equals(query, StringComparison.OrdinalIgnoreCase) ||
        value.Contains(';') && value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(alias => alias.Equals(query, StringComparison.OrdinalIgnoreCase));
}
