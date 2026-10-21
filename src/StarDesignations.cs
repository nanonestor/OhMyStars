namespace OhMyStars;

internal static class StarDesignations {
    // IAU constellation abbreviations and Latin genitives, independent of the active sky culture.
    private static readonly IReadOnlyDictionary<string, string> Genitives = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["And"] = "Andromedae", ["Ant"] = "Antliae", ["Aps"] = "Apodis", ["Aqr"] = "Aquarii",
        ["Aql"] = "Aquilae", ["Ara"] = "Arae", ["Ari"] = "Arietis", ["Aur"] = "Aurigae",
        ["Boo"] = "Bootis", ["Cae"] = "Caeli", ["Cam"] = "Camelopardalis", ["Cnc"] = "Cancri",
        ["CVn"] = "Canum Venaticorum", ["CMa"] = "Canis Majoris", ["CMi"] = "Canis Minoris",
        ["Cap"] = "Capricorni", ["Car"] = "Carinae", ["Cas"] = "Cassiopeiae", ["Cen"] = "Centauri",
        ["Cep"] = "Cephei", ["Cet"] = "Ceti", ["Cha"] = "Chamaeleontis", ["Cir"] = "Circini",
        ["Col"] = "Columbae", ["Com"] = "Comae Berenices", ["CrA"] = "Coronae Australis",
        ["CrB"] = "Coronae Borealis", ["Crv"] = "Corvi", ["Crt"] = "Crateris", ["Cru"] = "Crucis",
        ["Cyg"] = "Cygni", ["Del"] = "Delphini", ["Dor"] = "Doradus", ["Dra"] = "Draconis",
        ["Equ"] = "Equulei", ["Eri"] = "Eridani", ["For"] = "Fornacis", ["Gem"] = "Geminorum",
        ["Gru"] = "Gruis", ["Her"] = "Herculis", ["Hor"] = "Horologii", ["Hya"] = "Hydrae",
        ["Hyi"] = "Hydri", ["Ind"] = "Indi", ["Lac"] = "Lacertae", ["Leo"] = "Leonis",
        ["LMi"] = "Leonis Minoris", ["Lep"] = "Leporis", ["Lib"] = "Librae", ["Lup"] = "Lupi",
        ["Lyn"] = "Lyncis", ["Lyr"] = "Lyrae", ["Men"] = "Mensae", ["Mic"] = "Microscopii",
        ["Mon"] = "Monocerotis", ["Mus"] = "Muscae", ["Nor"] = "Normae", ["Oct"] = "Octantis",
        ["Oph"] = "Ophiuchi", ["Ori"] = "Orionis", ["Pav"] = "Pavonis", ["Peg"] = "Pegasi",
        ["Per"] = "Persei", ["Phe"] = "Phoenicis", ["Pic"] = "Pictoris", ["Psc"] = "Piscium",
        ["PsA"] = "Piscis Austrini", ["Pup"] = "Puppis", ["Pyx"] = "Pyxidis", ["Ret"] = "Reticuli",
        ["Sge"] = "Sagittae", ["Sgr"] = "Sagittarii", ["Sco"] = "Scorpii", ["Scl"] = "Sculptoris",
        ["Sct"] = "Scuti", ["Ser"] = "Serpentis", ["Sex"] = "Sextantis", ["Tau"] = "Tauri",
        ["Tel"] = "Telescopii", ["Tri"] = "Trianguli", ["TrA"] = "Trianguli Australis",
        ["Tuc"] = "Tucanae", ["UMa"] = "Ursae Majoris", ["UMi"] = "Ursae Minoris",
        ["Vel"] = "Velorum", ["Vir"] = "Virginis", ["Vol"] = "Volantis", ["Vul"] = "Vulpeculae"
    };

    public static string ConstellationGenitive(string abbreviation) =>
        Genitives.GetValueOrDefault(abbreviation.Trim(), abbreviation.Trim());

    public static string Heading(string proper, string flamsteed, string constellation) =>
        string.Join(" / ", new[] { proper.Trim(), Designation(flamsteed.Trim(), constellation) }.Where(s => s.Length > 0));

    public static string PrimaryHeading(MultipleStarCatalog.Star primary, bool struveSymbols = false) {
        string heading = Heading(primary["proper"], primary["flam"], primary["con"]);
        if(heading.Length > 0 || Bayer(primary["bayer"], primary["con"]).Length > 0)
            return heading;
        foreach(var tier in new[] {
            new[] { ("bu", "Burnham"), ("hd", "HD") },
            new[] { ("bu", "Burnham") },
            new[] { ("hd", "HD"), ("stf", "F. Struve") },
            new[] { ("hd", "HD"), ("stt", "O. Struve") },
            new[] { ("hd", "HD") },
            new[] { ("stf", "F. Struve"), ("stt", "O. Struve") },
            new[] { ("stf", "F. Struve") },
            new[] { ("stt", "O. Struve") }
        }) {
            if(tier.Any(id => string.IsNullOrWhiteSpace(primary[id.Item1])))
                continue;
            heading = string.Join(" / ", tier
                .Select(id => CatalogIdentifier(id.Item1, id.Item2, primary[id.Item1].Trim(), struveSymbols)));
            return heading;
        }
        foreach(var (name, column) in CatalogIdentifiers.Catalogs.Where(c => c.Column != "oms_id"))
            if(!string.IsNullOrWhiteSpace(primary[column]))
                return CatalogIdentifier(column, name, primary[column].Trim(), struveSymbols);
        return string.IsNullOrWhiteSpace(primary["oms_id"]) ? "" : $"OMS {primary["oms_id"]}";
    }

    public static string Bayer(string bayer, string constellation) =>
        Designation(ExpandBayer(bayer.Trim()), constellation);

    public static string UnnumberedBayer(string designation) =>
        designation.Contains('-')
            ? System.Text.RegularExpressions.Regex.Replace(designation, @"^(\S+)-[0-9]+(?= |$)", "$1")
            : designation;

    public static bool MatchesBayerSearch(string designation, string query) =>
        designation.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) ||
        UnnumberedBayer(designation).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string BackgroundName(MultipleStarCatalog.Star star) {
        if(!string.IsNullOrWhiteSpace(star["proper"]))
            return star["proper"].Trim();
        string bayer = Bayer(star["bayer"], star["con"]);
        if(bayer.Length > 0)
            return bayer;
        string flamsteed = Heading("", star["flam"], star["con"]);
        if(flamsteed.Length > 0)
            return flamsteed;
        foreach(var (column, name) in new[] {
            ("stf", "F. Struve"), ("stt", "O. Struve"), ("bu", "Burnham"), ("hd", "HD")
        })
            if(!string.IsNullOrWhiteSpace(star[column]))
                return CatalogIdentifier(column, name, star[column].Trim());
        return "";
    }

    public static string NavballName(MultipleStarCatalog.Star star) {
        if(!string.IsNullOrWhiteSpace(star["proper"]))
            return star["proper"].Trim();
        string constellation = star["con"].Trim();
        if(!string.IsNullOrWhiteSpace(star["bayer"]))
            return string.Join(" ", new[] { star["bayer"].Trim(), constellation }.Where(s => s.Length > 0));
        if(!string.IsNullOrWhiteSpace(star["hd"]))
            return CatalogIdentifier("hd", "HD", star["hd"].Trim());
        if(!string.IsNullOrWhiteSpace(star["flam"]))
            return string.Join(" ", new[] { star["flam"].Trim(), constellation }.Where(s => s.Length > 0));
        foreach(var (column, name) in new[] { ("stf", "F. Struve"), ("stt", "O. Struve"), ("hip", "HIP") })
            if(!string.IsNullOrWhiteSpace(star[column]))
                return CatalogIdentifier(column, name, star[column].Trim());
        foreach(var (name, column) in CatalogIdentifiers.Catalogs.Where(c => c.Column != "oms_id"))
            if(!string.IsNullOrWhiteSpace(star[column]))
                return CatalogIdentifier(column, name, star[column].Trim());
        return $"OMS {star.Id}";
    }

    public static string StarHeading(string proper, string bayer, string constellation) =>
        string.Join(" / ", new[] { proper.Trim(), Bayer(bayer, constellation) }.Where(s => s.Length > 0));

    public static string CatalogIdentifier(string column, string catalog, string value, bool struveSymbols = false) {
        string prefix = column switch {
            "bu" => "BU", "stf" => "STF", "stt" => "STT", "h" => "H", _ => ""
        };
        if(prefix.Length == 0 && column != "hd")
            return catalog.Length > 0 ? $"{catalog}:{value}" : value;
        return string.Join("; ", value.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(alias => {
            alias = alias.Trim();
            if(prefix.Length > 0 && alias.StartsWith(prefix, StringComparison.Ordinal))
                alias = alias[prefix.Length..].TrimStart();
            int number = 0;
            while(number < alias.Length && char.IsLetter(alias[number]))
                number++;
            if(number > 0 && number < alias.Length && char.IsDigit(alias[number]))
                alias = alias.Insert(number, " ");
            string symbol = struveSymbols ? column switch { "stf" => "\u03a3 ", "stt" => "\u039f\u03a3 ", _ => "" } : "";
            return catalog.Length > 0 ? $"{catalog} {symbol}{alias}" : $"{symbol}{alias}";
        }));
    }

    private static string Designation(string identifier, string constellation) =>
        identifier.Length == 0 ? "" : string.Join(" ", new[] { identifier, ConstellationGenitive(constellation) }.Where(s => s.Length > 0));

    public static string ExpandBayer(string bayer) {
        if(string.IsNullOrWhiteSpace(bayer))
            return bayer;
        string[] parts = bayer.Split('-', 2);
        string letter = parts[0] switch {
            "Alp" => "Alpha", "Bet" => "Beta", "Gam" => "Gamma", "Del" => "Delta",
            "Eps" => "Epsilon", "Zet" => "Zeta", "Eta" => "Eta", "The" => "Theta",
            "Iot" => "Iota", "Kap" => "Kappa", "Lam" => "Lambda", "Mu" => "Mu",
            "Nu" => "Nu", "Xi" => "Xi", "Omi" => "Omicron", "Pi" => "Pi", "Rho" => "Rho",
            "Sig" => "Sigma", "Tau" => "Tau", "Ups" => "Upsilon", "Phi" => "Phi",
            "Chi" => "Chi", "Psi" => "Psi", "Ome" => "Omega", _ => parts[0]
        };
        return parts.Length == 1 ? letter : $"{letter}-{parts[1]}";
    }
}
