using System.Security.Cryptography;

namespace CatalogBuilder;

internal static class Validation
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("Catalog validation: " + message);
    }

    public static void Run(string root, string output)
    {
        string source = Path.Combine(root, "AT-HYG_40", "athyg_40.csv");
        string catalog = Path.Combine(output, "oms_catalog.csv");
        string reviews = Path.Combine(output, "definitively-correct-systems.csv");
        int reviewedSystems = File.Exists(reviews)
            ? OhMyStars.MultipleStarCatalog.Load(catalog, reviews).DefinitivelyCorrectPrimaries.Count : 0;
        var orbitalCompanions = OrbitalClassification.Companions(catalog, Path.Combine(output, "orb6orbits.txt"),
            Path.Combine(root, "WDS", "wds_precise.txt"));
        var userReviews = new Dictionary<(string Wds, string Label), (long ComponentId, string Desired)>();
        string userReviewsPath = Path.Combine(output, "user-component-reviews.csv");
        if (File.Exists(userReviewsPath))
            foreach (string line in File.ReadLines(userReviewsPath).Skip(1))
            {
                string[] r = Csv.Parse(line);
                userReviews[(r[1], r[3])] = (long.Parse(r[2], Program.Invariant), r[5]);
            }
        string[] sourceHeader = Csv.Parse(File.ReadLines(source).First());
        string[] header = Csv.Parse(File.ReadLines(catalog).First());
        var columns = header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index);
        Require(header[0] == "oms_id", "first column must be oms_id");
        Require(header.Skip(1).Take(sourceHeader.Length).SequenceEqual(sourceHeader), "AT-HYG column order changed");
        foreach (string removed in new[] { "star_role", "catalog_status", "render_eligible", "position_method", "hd_crossrefs", "wds_ids", "wds_pairs",
            "hierarchy_path", "parent_component", "relative_to_component", "measurement_components", "wds_notes" })
            Require(!columns.ContainsKey(removed), "obsolete column " + removed);
        Require(columns.ContainsKey("bds"), "missing BDS column");
        Require(columns.ContainsKey("bu"), "missing BU column");
        var bdsReferences = new HashSet<(string Wds, string Id)>();
        foreach (string line in File.ReadLines(Path.Combine(root, "WDS", "wdsidsadsbds.txt")))
        {
            if (line.Length < 49 || line[49..].Trim().Length > 0 ||
                !System.Text.RegularExpressions.Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) continue;
            if (int.TryParse(line.Substring(33, 6).Trim(), out int n) && n > 0)
                bdsReferences.Add((line[..10], n.ToString(Program.Invariant)));
        }
        var additions = File.ReadLines(Path.Combine(output, "identifier-additions.csv")).Skip(1).Select(Csv.Parse)
            .ToDictionary(c => (c[0], c[1]), c => (Value: c[2], Evidence: c[3]));
        var selectedSource = new Dictionary<int, string[]>();
        var identities = new Dictionary<long, string[]>();
        var measures = new Dictionary<int, (long Owner, string Relation, double? Theta, double? Rho, int? Year)>();
        var systems = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        var roles = new Dictionary<string, long>(StringComparer.Ordinal);
        string Get(string[] row, string field) => row[columns[field]];
        long count = 0, wdsOnly = 0, missingPositions = 0, bdsSystems = 0;
        foreach (string line in File.ReadLines(catalog).Skip(1))
        {
            string[] row = Csv.Parse(line);
            Require(row.Length == header.Length, "incorrect field count");
            long id = long.Parse(Get(row, "oms_id"), Program.Invariant);
            long primary = long.Parse(Get(row, "primary_oms_id"), Program.Invariant);
            string role = Get(row, "component"), wds = Get(row, "wds_id");
            if (Get(row, "bds").Length > 0)
            {
                Require(primary == id && role is "P" or "U" && Get(row, "bds").Split(';').All(id => bdsReferences.Contains((wds, id))), "invalid BDS system alias");
                bdsSystems++;
            }
            if (Get(row, "bu").Length > 0)
                Require(primary == id && role is "P" or "U", "BU discovery alias must be on system anchor");
            Require(id > 0 && identities.TryAdd(id, row), "duplicate/invalid OMS ID");
            Require(role is "S" or "P" or "C" or "U" or "V" or "Q", "invalid classification");
            roles[role] = roles.GetValueOrDefault(role) + 1;
            Require(role is "C" or "V" or "Q" or "U" || primary == id, "non-component must reference self");
            Require(role is not ("C" or "V" or "Q") || primary != id, "component references itself");
            if (wds == "")
            {
                Require(role is "S" or "U" && primary == id, "ungrouped role/reference");
                Require(wds == "" && Get(row, "system_id") == "", "single/unknown grouped as resolved system");
            }
            else
            {
                Require(System.Text.RegularExpressions.Regex.IsMatch(wds, @"^\d{5}[+-]\d{4}$"), "WDS ID must be plain signed text");
                Require(Get(row, "system_id") == primary.ToString(Program.Invariant), "system primary mismatch");
                if (!systems.TryGetValue(wds, out var members)) systems[wds] = members = new(StringComparer.Ordinal);
                Require(members.TryAdd(Get(row, "component_label"), id), "duplicate leaf label");
            }
            if (Get(row, "id").Length > 0)
            {
                Require(Get(row, "radec_source") == "GIA", "AT-HYG provenance");
                Require(selectedSource.TryAdd(int.Parse(Get(row, "id"), Program.Invariant), row.Skip(1).Take(sourceHeader.Length).ToArray()), "duplicate AT-HYG ID");
            }
            else
            {
                wdsOnly++;
                Require(Get(row, "radec_source") == "WDS" && Get(row, "pos_src") == "WDS", "WDS provenance");
                bool blankPosition = Get(row, "ra") == "" && Get(row, "dec") == "";
                Require(blankPosition || (Program.Number(Get(row, "ra")) is >= 0 and < 24 && Program.Number(Get(row, "dec")) is >= -90 and <= 90), "WDS coordinate range");
                if (blankPosition) missingPositions++;
                Require(Get(row, "dist") == "" && Get(row, "absmag") == "" && Get(row, "gaia") == "", "invented WDS distance/magnitude/Gaia ID");
                Require(role is "P" or "C" or "V" or "Q" or "U", "orphan WDS-only row");
            }
            string[] lines = Split(Get(row, "wds_source_line"));
            string[][] data = new[] { "relationships", "theta_deg", "rho_arcsec", "measurement_year" }
                .Select(name => Get(row, name).Split('|')).ToArray();
            if (lines.Length == 0)
                Require(data.All(c => c.Length == 1 && c[0] == ""), "measurements without source lines");
            else
                Require(data.All(c => c.Length == lines.Length), "misaligned scalar measurement lists");
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = int.Parse(lines[i], Program.Invariant);
                Require(measures.TryAdd(lineNumber, (id, data[0][i], Program.Number(data[1][i]),
                    Program.Number(data[2][i]), (int?)Program.Number(data[3][i]))), "duplicate measurement ownership");
            }
            count++;
        }
        var parsedPairs = new Dictionary<string, List<Pair>>(StringComparer.Ordinal);
        var classificationPairs = new Dictionary<string, List<Pair>>(StringComparer.Ordinal);
        HashSet<(string Wds, string First, string Second)> excludedEndpoints = [];
        int sourceLine = 0;
        foreach (string line in File.ReadLines(Path.Combine(root, "WDS", "wds_precise.txt")))
        {
            sourceLine++;
            if (line.Length == 130 && systems.ContainsKey(line[..10]))
            {
                var ends = Pair.Endpoints(Program.Slice(line, 17, 5));
                if (Pair.Excluded(Program.Slice(line, 107, 4)))
                    excludedEndpoints.Add((line[..10], ends.First, ends.Second));
                else if (Hierarchy.Endpoint(ends.First) && Hierarchy.Endpoint(ends.Second) && ends.First != ends.Second)
                {
                    Pair evidence = Pair.Parse(line, sourceLine);
                    if (!Errata.Deleted(evidence.Wds, evidence.Discoverer))
                    {
                        evidence = evidence with { Discoverer = Errata.Rename(evidence.Wds, evidence.Discoverer) };
                        if (!classificationPairs.TryGetValue(evidence.Wds, out var evidenceList))
                            classificationPairs[evidence.Wds] = evidenceList = [];
                        evidenceList.Add(evidence);
                    }
                }
            }
            if (!measures.TryGetValue(sourceLine, out var measure)) continue;
            Pair p = Pair.Parse(line, sourceLine);
            string[] owner = identities[measure.Owner];
            Require(p.Wds == Get(owner, "wds_id"), "pair system mismatch");
            Require(!Pair.Excluded(p.Flags) && !Errata.Deleted(p.Wds, p.Discoverer), "excluded pair reintroduced");
            Require(measure.Theta == p.Theta && measure.Rho == p.Rho && measure.Year == p.LastYear, "WDS geometry overwritten");
            Require(measure.Relation == p.First + ">" + p.Second, "relationship lost");
            if (!parsedPairs.TryGetValue(p.Wds, out var list)) parsedPairs[p.Wds] = list = [];
            list.Add(p);
        }
        Require(parsedPairs.Values.Sum(l => l.Count) == measures.Count, "unresolved source measurement");
        long wdsSpectral = 0, filledAthygSpectral = 0;
        foreach (var row in identities.Values.Where(row => Get(row, "spect_src") == "WDS"))
        {
            Require(parsedPairs.TryGetValue(Get(row, "wds_id"), out var pairs), "WDS spectrum lacks source system");
            string[] types = pairs!.Select(p => p.SpectralFor(Get(row, "component_label"))).OfType<string>()
                .Distinct(StringComparer.Ordinal).ToArray();
            Require(types.Length == 1 && types[0] == Get(row, "spect"), "WDS spectrum lacks unambiguous individual-component evidence");
            wdsSpectral++;
        }
        foreach (var members in systems.Values)
        {
            string[] anchor = identities[long.Parse(Get(identities[members.Values.First()], "primary_oms_id"), Program.Invariant)];
            string wds = Get(anchor, "wds_id");
            var expected = classificationPairs[wds].Select(p => p.Discoverer)
                .Where(d => System.Text.RegularExpressions.Regex.IsMatch(d, @"^BU\d+$"))
                .Select(d => int.Parse(d[2..], Program.Invariant).ToString(Program.Invariant)).ToHashSet(StringComparer.Ordinal);
            Require(Get(anchor, "bu").Split(';', StringSplitOptions.RemoveEmptyEntries).All(expected.Contains),
                $"BU alias lacks source discovery evidence for {wds}: {Get(anchor, "bu")}");
        }
        var sourceNotes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? noteWds = null;
        foreach (string line in File.ReadLines(Path.Combine(root, "WDS", "wdsnewnotes_main.txt")))
        {
            if (line.Length < 22) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) noteWds = line[..10];
            if (noteWds is null) continue;
            if (!sourceNotes.TryGetValue(noteWds, out var list)) sourceNotes[noteWds] = list = [];
            list.Add(line.TrimEnd());
        }
        foreach (var (wds, members) in systems)
        {
            Require(parsedPairs.TryGetValue(wds, out var pairs), "system lacks relationships");
            if (wds == "14396-6050")
            {
                Require(members.Count == 3 &&
                    members.TryGetValue("A", out long a) && Get(identities[a], "id") == "1455072" &&
                    Get(identities[a], "component") == "P" &&
                    members.TryGetValue("B", out long b) && Get(identities[b], "id") == "1455030" &&
                    Get(identities[b], "component") == "C" &&
                    members.TryGetValue("C", out long c) && Get(identities[c], "id") == "1440825" &&
                    Get(identities[c], "component") == "C", "incorrect Alpha Centauri A/B/C identities");
            }
            // A retained star can have role evidence from a pair omitted because its other endpoint conflicts.
            Pair[] evidence = classificationPairs[wds]
                .Where(p => !excludedEndpoints.Contains((p.Wds, p.First, p.Second))).ToArray();
            Hierarchy hierarchy = new(evidence);
            var optical = Builder.OpticalNotes(sourceNotes.GetValueOrDefault(wds, []), evidence);
            HashSet<string> unknown = evidence.Where(p => p.Flags.Contains('I'))
                .SelectMany(p => hierarchy.Leaves(p.First).Concat(hierarchy.Leaves(p.Second))).ToHashSet(StringComparer.Ordinal);
            HashSet<string> visual = evidence.Where(p => optical.Pairs.Contains(p.Line) || Pair.Visual(p.Flags))
                .SelectMany(p => hierarchy.Leaves(p.Second)).ToHashSet(StringComparer.Ordinal);
            visual.UnionWith(optical.Labels);
            HashSet<string> questionable = evidence.Where(p => optical.QuestionablePairs.Contains(p.Line))
                .SelectMany(p => hierarchy.Leaves(p.Second)).ToHashSet(StringComparer.Ordinal);
            questionable.UnionWith(optical.QuestionableLabels);
            HashSet<string> physical = evidence.Where(p => optical.PhysicalPairs.Contains(p.Line))
                .SelectMany(p => hierarchy.Leaves(p.Second)).ToHashSet(StringComparer.Ordinal);
            physical.UnionWith(optical.PhysicalLabels);
            foreach (Pair p in pairs!)
            {
                string[] first = hierarchy.Leaves(p.First), second = hierarchy.Leaves(p.Second);
                Require(!first.Intersect(second).Any(), "overlapping endpoints");
                Require(first.Concat(second).All(members.ContainsKey), "missing individual-star leaf");
                Require(members[second[0]] == measures[p.Line].Owner, "incorrect measurement owner");
            }
            foreach (var (label, id) in members)
            {
                string[] row = identities[id];
                Require(Hierarchy.Atom(label), "invalid individual-star hierarchy label");
                string role = Get(row, "component");
                long primaryId = long.Parse(Get(row, "primary_oms_id"), Program.Invariant);
                Require(identities.TryGetValue(primaryId, out var primary), "missing primary");
                Require(Get(primary!, "primary_oms_id") == Get(primary!, "oms_id") &&
                    Get(primary!, "wds_id") == wds && Get(primary!, "component") is "P" or "U", "invalid system anchor");
                string expected;
                if (unknown.Contains(label)) expected = "U";
                else if (id == primaryId) expected = "P";
                else if (userReviews.TryGetValue((wds, label), out var userReview))
                {
                    Require(userReview.ComponentId == id, "user review identity mismatch for OMS " + id);
                    expected = userReview.Desired;
                }
                else if (Builder.IsCuratedVisualCompanion(wds, label)) expected = "V";
                else if (Builder.IsCuratedPhysicalCompanion(wds, label)) expected = "C";
                else
                {
                    var own = Builder.EffectiveMotion(Program.Number(Get(row, "pmra")), Program.Number(Get(row, "pmdec")), label, pairs!);
                    var main = Builder.EffectiveMotion(Program.Number(Get(primary!, "pmra")), Program.Number(Get(primary!, "pmdec")), Get(primary!, "component_label"), pairs!);
                    double? velocity = Builder.RelativeTangentialVelocityKms(own.Ra, own.Dec, main.Ra, main.Dec,
                        Program.Number(Get(primary!, "dist")), Program.Number(Get(row, "dist")));
                    if (velocity is > 75.0) expected = "V";
                    else if (Builder.SeparationFromPrimary(pairs!, hierarchy.Leaves,
                                 Get(primary!, "component_label"), label) is < 10.0) expected = "C";
                    else if (orbitalCompanions.Contains(id)) expected = "C";
                    else expected = questionable.Contains(label) ? "Q" :
                        visual.Contains(label) ? physical.Contains(label) ? "Q" : "V" :
                        Builder.IsExtremeMotionDifference(own.Ra, own.Dec, main.Ra, main.Dec) ? "Q" : "C";
                }
                Require(role == expected, "incorrect uncertainty/Visual role for OMS " + id);
            }
        }
        foreach (var (id, row) in identities)
        {
            long primaryId = long.Parse(Get(row, "primary_oms_id"), Program.Invariant);
            Require(identities.ContainsKey(primaryId), "missing primary");
        }
        long checkedSource = 0, baseRows = 0, addedSourceIds = 0;
        int magColumn = Array.IndexOf(sourceHeader, "mag");
        foreach (string line in File.ReadLines(source).Skip(1))
        {
            string[] original = Csv.Parse(line);
            int id = int.Parse(original[0], Program.Invariant);
            if (Builder.InBase(original[magColumn]))
            {
                Require(selectedSource.ContainsKey(id), "missing base star " + id);
                baseRows++;
            }
            if (!selectedSource.TryGetValue(id, out var copy)) continue;
            int spectColumn = Array.IndexOf(sourceHeader, "spect");
            int spectSourceColumn = Array.IndexOf(sourceHeader, "spect_src");
            bool filledSpectral = Pair.MissingSpectral(original[spectColumn]) &&
                !Pair.MissingSpectral(copy[spectColumn]) && copy[spectSourceColumn] == "WDS";
            if (filledSpectral) filledAthygSpectral++;
            for (int i = 0; i < original.Length; i++)
            {
                if (copy[i] == original[i]) continue;
                if (filledSpectral && (i == spectColumn || i == spectSourceColumn)) continue;
                Require(original[i] == "" && Builder.IdentifierColumns.Contains(sourceHeader[i]) &&
                    additions.TryGetValue(("AT40:" + id, sourceHeader[i]), out var audit) &&
                    audit.Value == copy[i] && audit.Evidence.Length > 0, "overwritten nonempty or unaudited AT-HYG field " + id + ":" + sourceHeader[i]);
                addedSourceIds++;
            }
            checkedSource++;
        }
        Require(checkedSource == selectedSource.Count, "missing source row");
        var registry = File.ReadLines(Path.Combine(output, "identity-registry.csv")).Skip(1).Select(Csv.Parse).ToDictionary(c => c[0], c => long.Parse(c[1], Program.Invariant));
        Require(registry.Values.Distinct().Count() == registry.Count, "duplicate registry IDs");
        foreach (var row in identities.Values)
        {
            string key = Get(row, "id").Length > 0 ? "AT40:" + Get(row, "id") : "WDS:" + Get(row, "wds_id") + ":" + Get(row, "component_label");
            Require(registry.TryGetValue(key, out long assigned) && assigned == long.Parse(Get(row, "oms_id"), Program.Invariant), "registry mismatch");
        }
        var byKey = identities.Values.ToDictionary(row => Get(row, "id").Length > 0 ? "AT40:" + Get(row, "id") :
            "WDS:" + Get(row, "wds_id") + ":" + Get(row, "component_label"), StringComparer.Ordinal);
        foreach (var (key, audit) in additions)
            Require(byKey.TryGetValue(key.Item1, out var row) && Get(row, key.Item2) == audit.Value &&
                audit.Evidence.Length > 0 && Builder.IdentifierColumns.Contains(key.Item2), "invalid identifier enrichment audit");
        string[] suppressed = ["shared_Gaia_identity", "excluded_S_U_X_Y", "unknown_theta_or_rho", "companion_epoch_drift_unknown",
            "no_safe_new_identity", "ambiguous_", "hierarchy_or_historical_identity", "proper_motion_mismatch_", "WDS_identification_uncertain_flag_I",
            "crossmatch_identity_uncertain", "conflicting_component_identity"];
        long reviewCount = 0;
        foreach (string line in File.ReadLines(Path.Combine(output, "match-review.csv")).Skip(1))
        {
            string[] r = Csv.Parse(line);
            Require(r.Length == 5 && !suppressed.Any(r[4].Contains), "obsolete review reason");
            if (r[4].StartsWith("narrative_status_requires_review", StringComparison.Ordinal))
                Require(r[4].Contains("wdsnewnotes_main.txt line ") && r[4].Contains("trigger=") &&
                    r[4].Contains("note=") && r[4].Contains("notes_context="), "missing narrative source evidence");
            reviewCount++;
        }
        using var stream = File.OpenRead(catalog);
        string hash = Convert.ToHexString(SHA256.HashData(stream));
        Program.WriteJson(Path.Combine(output, "validation.json"), new
        {
            status = "passed_structural_and_source_precedence_checks", rows = count,
            athyg_rows_verified = checkedSource, base_athyg_rows_verified = baseRows,
            original_nonempty_athyg_fields_preserved_except_missing_spectral_metadata = true,
            added_ids_on_athyg_rows = addedSourceIds,
            spectral_provenance_updated_only_for_missing_spectra = true,
            wds_spectral_classifications = wdsSpectral, wds_spectral_filled_athyg = filledAthygSpectral,
            identifier_additions = additions.Count, wds_only_rows = wdsOnly, unknown_position_rows = missingPositions,
            pair_measurements = measures.Count, bds_identified_systems = bdsSystems, roles, review_rows = reviewCount,
            catalog_sha256 = hash, shared_gaia_ids_allowed = true, scientific_identity_review_required = true,
            definitively_correct_systems = reviewedSystems
        });
        Console.WriteLine($"Verified {count:N0} rows, all {baseRows:N0} base stars, {checkedSource:N0} AT-HYG rows with known fields preserved, {wdsOnly:N0} WDS-only rows.");
        Console.WriteLine($"Verified {wdsSpectral:N0} component-specific WDS spectra, including {filledAthygSpectral:N0} AT-HYG gap fills.");
        Console.WriteLine($"Verified {measures.Count:N0} scalar relationships/measurements, hierarchy leaves, plain WDS IDs, Visual vector threshold, ID audit, registry and review policies.");
    }

    private static string[] Split(string value) => value.Length == 0 ? [] : value.Split('|');
}
