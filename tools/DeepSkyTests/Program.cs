using System.Globalization;
using System.Security.Cryptography;
using OhMyStars;

int checks = 0;
void Check(bool condition, string message) {
    checks++;
    if(!condition)
        throw new InvalidOperationException(message);
}
void Reject(Action action, string message) {
    try {
        action();
    } catch(InvalidDataException) {
        checks++;
        return;
    }
    throw new InvalidOperationException(message);
}

string root = Path.Combine(AppContext.BaseDirectory, "catalogs");
string ngc = Path.Combine(root, "NGC.csv"), addendum = Path.Combine(root, "addendum.csv");
Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ngc))) ==
    "840FE0C9EE1332E551B2E722A0E92726CD7B157914A3D2177602832AADD3AA9E", "Bundled NGC snapshot is unmodified.");
Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(addendum))) ==
    "1D8F0914E643ADA325A5A94D88D8FEFAD6A4937A2F77CC34F21483AF22B11983", "Bundled addendum snapshot is unmodified.");
var catalog = DeepSkyCatalog.Load(ngc, addendum);
OutlineTests.Run(catalog, root, Check, Reject);
Check(catalog.Entries.Count == 14033, "All 13,969 NGC/IC and 64 addendum entries load.");
Check(catalog.Entries.Count(entry => entry.IsAddendum) == 64, "Addendum provenance is retained.");
Check(catalog.Entries.Select(entry => entry.Id).Distinct().Count() == catalog.Entries.Count, "Picker IDs are unique.");
Check(catalog.Search("").Length == catalog.Entries.Count, "Empty search lists every entry without truncation.");
foreach(var (code, _) in DeepSkyCatalog.Types) {
    var entries = catalog.Search("", code);
    Check(entries.Length > 0 && entries.All(entry => entry.Type == code), $"Type filter {code} works.");
}
foreach(var kind in Enum.GetValues<DeepSkyCatalog.CatalogKind>()) {
    var entries = catalog.Search("", catalog: kind);
    Check(entries.Length > 0 && entries.All(entry => entry.InCatalog(kind)), $"Catalog filter {kind} works.");
}
var andromeda = catalog.Entries.Single(entry => entry.Name == "NGC0224");
var messierEntries = catalog.Search("", catalog: DeepSkyCatalog.CatalogKind.Messier);
Check(messierEntries.Select(entry => entry.MessierNumber).SequenceEqual(
    Enumerable.Range(1, 110).Select(number => (int?)number)), "Messier results run numerically from M1 through M110 without gaps or repeats.");
Check(messierEntries[101].Name == "M102" && messierEntries[101]["M"] == "101",
    "M102 sorts by its primary identity while preserving the original duplicate cross-reference.");
Check(messierEntries[30].MessierLabel.StartsWith("M31 - NGC0224", StringComparison.Ordinal) &&
    messierEntries[109].MessierLabel.StartsWith("M110 - NGC0205", StringComparison.Ordinal),
    "Messier dropdown labels put unpadded Messier IDs first.");
foreach(var (code, _) in DeepSkyCatalog.Types) {
    var filtered = catalog.Search("", code, DeepSkyCatalog.CatalogKind.Messier);
    Check(filtered.SequenceEqual(filtered.OrderBy(entry => entry.MessierNumber)),
        $"Messier type filter {code} preserves numeric order.");
}
var messierQuery = catalog.Search("M1", catalog: DeepSkyCatalog.CatalogKind.Messier);
Check(messierQuery.SequenceEqual(messierQuery.OrderBy(entry => entry.MessierNumber)),
    "Keyword-filtered Messier results retain numeric order.");
Check(catalog.Search("").SequenceEqual(catalog.Entries), "Other search modes retain the original catalog order.");
foreach(string query in new[] { "andromeda", "ANDROMEDA galaxy", "NGC0224", "NGC 224", "224", "M31", "M 031" })
    Check(catalog.Search(query).Contains(andromeda), $"Name/identifier search '{query}' finds M31.");
Check(catalog.Search("M 031").SequenceEqual(catalog.Search("M31")) &&
    catalog.Search("NGC 224").SequenceEqual(catalog.Search("NGC0224")),
    "A spaced catalog identifier is one search term, not unrelated prefix and number matches.");
Check(catalog.Search("Andromeda", "PN").Length == 0, "Type and keyword filters intersect.");
Check(catalog.Search("Andromeda", catalog: DeepSkyCatalog.CatalogKind.IC).All(entry => entry.InCatalog(DeepSkyCatalog.CatalogKind.IC)),
    "Catalog and keyword filters intersect.");
Check(catalog.Search("no-such-object-xyz-999999").Length == 0, "No-match search is empty.");
Check(andromeda.Identifiers.Contains("NGC0224") && andromeda.Identifiers.Contains("M031") &&
    andromeda.Identifiers.Any(id => id.StartsWith("PGC", StringComparison.Ordinal)), "All cross-catalog aliases are retained.");
Check(catalog.Search("PGC 2557").Contains(andromeda), "Other-catalog cross-identifiers are searchable.");
var m40 = catalog.Search("M40", catalog: DeepSkyCatalog.CatalogKind.Messier).Single(entry => entry.Name == "M040");
var m45 = catalog.Search("M45", catalog: DeepSkyCatalog.CatalogKind.Messier).Single(entry => entry.Name == "Mel022");
Check(m40.IsAddendum && m45.IsAddendum, "M40 and M45 are found in the addendum through Messier search.");
Check(m45.Identifiers.Contains("M045") && m45["Common names"] == "Pleiades", "M45's proper name and Messier alias survive.");
Check(catalog.Search("Horsehead", catalog: DeepSkyCatalog.CatalogKind.Other).Any(entry => entry.Name == "B033"),
    "Other catalog search includes addendum objects.");
Check(Math.Abs(andromeda.RaHours!.Value - (42d / 60 + 44.35 / 3600)) < 1e-10 &&
    Math.Abs(andromeda.DecDegrees!.Value - (41 + 16d / 60 + 8.6 / 3600)) < 1e-10, "J2000 sexagesimal conversion.");
var orion = catalog.Entries.Single(entry => entry.Name == "NGC1976");
Check(Math.Abs(orion.DecDegrees!.Value - (-5 - 23d / 60 - 22.8 / 3600)) < 1e-10, "Southern declination conversion.");
Check(andromeda.Magnitude == 3.44 && andromeda.MagnitudeBand == "V", "V magnitude takes precedence over B.");
var blueOnly = catalog.Entries.Single(entry => entry.Name == "IC0002");
Check(blueOnly.Magnitude == 15.46 && blueOnly.MagnitudeBand == "B", "B magnitude is the fallback.");
var horsehead = catalog.Entries.Single(entry => entry.Name == "B033");
Check(horsehead.Magnitude is null, "Missing V/B does not turn into zero or use IR magnitude.");
Check(catalog.Entries.Where(entry => entry.Type is "Dup" or "NonEx").All(entry => !entry.CanDraw),
    "Duplicate and nonexistent entries are searchable but never get duplicate or fictitious rings.");
Check(catalog.Entries.All(entry => !entry.CanDraw || entry.RaHours is >= 0 and < 24 && entry.DecDegrees is >= -90 and <= 90),
    "All drawable coordinates lie in the valid range.");
Check(catalog.Entries.Any(entry => entry["NED notes"].Length > 0) &&
    catalog.Entries.Any(entry => entry["OpenNGC notes"].Length > 0), "Both note fields are retained.");
Check(ReferenceEquals(andromeda.DisplayDetails, andromeda.DisplayDetails), "Selected-object details are formatted once and reused.");
Check(andromeda.DisplayDetails.Type == "Galaxy (G)" &&
    andromeda.DisplayDetails.Identifiers == "Identifiers: " + string.Join(", ", andromeda.Identifiers) &&
    andromeda.DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 3.44" &&
    andromeda.DisplayDetails.FieldLines.Contains("Constellation: Andromeda"),
    "Cached details preserve type, aliases, magnitude and full constellation name.");
Check(orion.DisplayDetails.FieldLines.Contains("Constellation: Orion"), "Constellation display uses the nominative full name, not a genitive.");
var parallaxEntry = catalog.Entries.Single(entry => entry.Name == "IC0166");
Check(Math.Abs(parallaxEntry.DistanceLightYears!.Value - 18637.50729685714) < 1e-8 &&
    parallaxEntry.DisplayDetails.Distance == "Distance: 18,637.507 light years (from catalog parallax)",
    "Catalog milliarcseconds convert to light years with explicit inverse-parallax provenance.");
Check(orion.DistanceLightYears is null && orion.DisplayDetails.Distance is null,
    "No distance line is invented when a catalog parallax is absent.");
Check(catalog.Entries.Count(entry => entry.DistanceLightYears.HasValue) == 737,
    "All 737 positive bundled parallaxes provide distances.");
string[] hiddenPrefixes = ["Major axis:", "Minor axis:", "Major axis (", "Minor axis (", "Parallax",
    "RA proper motion", "Dec proper motion", "Radial velocity", "Redshift", "Source codes:", "Catalog data sources:"];
foreach(var item in catalog.Entries) {
    var lines = item.DisplayDetails.FieldLines;
    Check(lines.All(line => !hiddenPrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))),
        $"Hidden fields are absent from cached details for {item.Name}.");
    if(item["Const"].Length > 0)
        Check(lines.Any(line => line.StartsWith("Constellation: ", StringComparison.Ordinal)),
            $"Constellation {item["Const"]} resolves for {item.Name}.");
}
Check(catalog.Entries.Where(item => item["Const"] == "Se1")
    .All(item => item.DisplayDetails.FieldLines.Contains("Constellation: Serpens Caput")) &&
    catalog.Entries.Where(item => item["Const"] == "Se2")
    .All(item => item.DisplayDetails.FieldLines.Contains("Constellation: Serpens Cauda")),
    "OpenNGC's split Serpens abbreviations expand to the correct full names.");
Check(andromeda.SelectableLabel == andromeda.Label + "##DeepSky" + andromeda.Id, "Cached dropdown labels retain stable picker identity.");
Check(blueOnly.DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 15.46 (B-band; visual unavailable)",
    "Without V, the selected-object magnitude uses a clearly identified B-band fallback.");
Check(horsehead.DisplayDetails.ApparentMagnitude == "Apparent Magnitude: N/A",
    "Objects without integrated magnitudes explicitly display N/A.");

DeepSkySettings settings = new();
Check(!settings.AlwaysShow && !settings.ShowNebulaOutlines && settings.IncludeUnknownMagnitude && settings.Catalogs.Values.All(value => value),
    "Default scope is tab-only, outlines off, unknown magnitudes included, all catalogs allowed.");
Check(settings.Styles.Values.All(style => !style.Enabled && style.MagnitudeCutoff == 12), "All types start off with cutoff 12.");
Check(!settings.AllTypesEnabled, "Master type toggle starts off.");
settings.AllTypesEnabled = true;
Check(settings.AllTypesEnabled && settings.Styles.Where(pair => pair.Key is not ("Dup" or "NonEx")).All(pair => pair.Value.Enabled),
    "Master toggle enables every drawable type.");
Check(!settings.Styles["Dup"].Enabled && !settings.Styles["NonEx"].Enabled,
    "Master toggle does not enable duplicate/nonexistent records.");
settings.Styles["G"].Enabled = false;
Check(!settings.AllTypesEnabled, "Individual deselection updates the master toggle.");
settings.Styles["G"].Enabled = true;
Check(settings.AllTypesEnabled, "Selecting the last individual type checks the master toggle.");
settings.Styles["G"].MagnitudeCutoff = 9;
settings.Styles["G"].Red = 0.125f;
settings.AllTypesEnabled = false;
Check(!settings.AllTypesEnabled && settings.Styles.Values.All(style => !style.Enabled),
    "Master toggle disables every drawable type.");
Check(settings.Styles["G"].MagnitudeCutoff == 9 && settings.Styles["G"].Red == 0.125f,
    "Master toggle preserves individual magnitude and color preferences.");
settings = new();
Check(!settings.IsVisible(andromeda), "Disabled types have no rings or click targets.");
var galaxy = settings.Styles["G"];
galaxy.Enabled = true;
settings.Styles[orion.Type].Enabled = true;
settings.Styles[orion.Type].MagnitudeCutoff = 4;
Check(settings.IsVisible(orion), "Magnitude cutoff includes the exact boundary.");
settings.Styles[orion.Type].MagnitudeCutoff = 3.99f;
Check(!settings.IsVisible(orion), "Fainter objects do not receive rings or targets.");
galaxy.MagnitudeCutoff = 4;
Check(settings.IsVisible(andromeda) && !settings.IsVisible(blueOnly), "Magnitude eligibility follows astronomical brightness ordering.");
galaxy.MagnitudeCutoff = 16;
Check(settings.IsVisible(blueOnly), "B-only entry passes its correct cutoff.");
settings.Styles["DrkN"].Enabled = true;
Check(settings.IsVisible(horsehead), "Unknown magnitude is included by default for enabled types.");
settings.IncludeUnknownMagnitude = false;
Check(!settings.IsVisible(horsehead), "Unknown magnitude can be excluded separately.");
settings.IncludeUnknownMagnitude = true;
foreach(var kind in settings.Catalogs.Keys.ToArray())
    settings.Catalogs[kind] = false;
Check(!settings.IsVisible(andromeda) && !settings.IsVisible(horsehead), "All catalogs disabled means no rings or targets.");
settings.Catalogs[DeepSkyCatalog.CatalogKind.Messier] = true;
Check(settings.IsVisible(andromeda) && !settings.IsVisible(blueOnly), "Messier-only overlay includes M31 but not unrelated galaxies.");
settings.Catalogs[DeepSkyCatalog.CatalogKind.Other] = true;
Check(settings.IsVisible(horsehead), "Other overlay includes the addendum.");
foreach(var entry in catalog.Entries.Where(entry => entry.Type is "Dup" or "NonEx")) {
    settings.Styles[entry.Type].Enabled = true;
    Check(!settings.IsVisible(entry), $"Non-drawable {entry.Name} stays excluded even when enabled.");
}

settings.AlwaysShow = true;
settings.ShowNebulaOutlines = true;
galaxy.Red = 0.125f;
galaxy.Green = 0.375f;
galaxy.Blue = 0.75f;
CultureInfo original = CultureInfo.CurrentCulture;
try {
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
    Dictionary<string, string> saved = settings.Serialize().Select(line => line.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts[1]);
    var restored = DeepSkySettings.Read(saved);
    Check(restored.Serialize().SequenceEqual(settings.Serialize()), "Every setting round-trips independently of system culture.");
    Check(restored.AlwaysShow && restored.ShowNebulaOutlines && restored.Styles["G"].Enabled && restored.Styles["G"].Red == 0.125f,
        "Always-show, enabled types, magnitude cutoffs, catalog toggles and colors persist.");
} finally {
    CultureInfo.CurrentCulture = original;
}
var invalid = DeepSkySettings.Read(new Dictionary<string, string> {
    ["DeepSkyType_G_Magnitude"] = "NaN", ["DeepSkyType_PN_Magnitude"] = "999",
    ["DeepSkyType_Nova_Magnitude"] = "-999", ["DeepSkyType_G_R"] = "Infinity",
    ["DeepSkyType_G_G"] = "2", ["DeepSkyType_G_B"] = "-2", ["DeepSkyAlwaysShow"] = "garbage"
});
Check(invalid.Styles["G"].MagnitudeCutoff == 12 && invalid.Styles["PN"].MagnitudeCutoff == 25 &&
    invalid.Styles["Nova"].MagnitudeCutoff == -5 && !invalid.AlwaysShow, "Invalid values fall back and finite outliers are clamped.");
Check(float.IsFinite(invalid.Styles["G"].Red) && invalid.Styles["G"].Green == 1 && invalid.Styles["G"].Blue == 0,
    "Restored colors remain finite and within 0..1.");

DeepSkyVisibilityCache visibility = new(catalog);
DeepSkySettings cachedSettings = new();
cachedSettings.Styles["G"].Enabled = true;
var eligible = visibility.GetEntries(cachedSettings);
Check(eligible.Length > 0 && eligible.SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
    "Cached eligibility matches the original visibility rule.");
Check(ReferenceEquals(eligible, visibility.GetEntries(cachedSettings)), "Unchanged frames reuse the eligible array.");
cachedSettings.AlwaysShow = true;
cachedSettings.ShowNebulaOutlines = true;
cachedSettings.Styles["G"].Red = 0.125f;
cachedSettings.Styles["G"].Green = 0.25f;
cachedSettings.Styles["G"].Blue = 0.5f;
Check(ReferenceEquals(eligible, visibility.GetEntries(cachedSettings)),
    "Scope and color changes do not rescan object eligibility.");
cachedSettings.Styles["PN"].MagnitudeCutoff = 5;
Check(ReferenceEquals(eligible, visibility.GetEntries(cachedSettings)), "Disabled-type cutoffs do not rescan eligibility.");
cachedSettings.Styles["G"].MagnitudeCutoff = 12.001f;
Check(ReferenceEquals(eligible, visibility.GetEntries(cachedSettings)), "Sub-display-precision slider movement reuses eligibility.");
cachedSettings.Styles["G"].MagnitudeCutoff = 12.01f;
var changedEligible = visibility.GetEntries(cachedSettings);
Check(!ReferenceEquals(eligible, changedEligible) && changedEligible.SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
    "A displayed magnitude change rebuilds eligibility immediately.");
foreach(var (code, _) in DeepSkyCatalog.Types) {
    var style = cachedSettings.Styles[code];
    style.Enabled = !style.Enabled;
    Check(visibility.GetEntries(cachedSettings).SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
        $"Changing {code} visibility invalidates cached eligibility.");
    style.MagnitudeCutoff = 8.9f;
    Check(visibility.GetEntries(cachedSettings).SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
        $"Changing {code} magnitude preserves exact eligibility.");
}
cachedSettings.IncludeUnknownMagnitude = false;
Check(visibility.GetEntries(cachedSettings).SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
    "Changing unknown-magnitude inclusion invalidates eligibility.");
foreach(var kind in cachedSettings.Catalogs.Keys.ToArray()) {
    cachedSettings.Catalogs[kind] = false;
    Check(visibility.GetEntries(cachedSettings).SequenceEqual(catalog.Entries.Where(cachedSettings.IsVisible)),
        $"Changing {kind} catalog visibility invalidates eligibility.");
}
Check(visibility.GetEntries(cachedSettings).Length == 0, "All catalog toggles off clears cached eligibility.");
cachedSettings.Catalogs[DeepSkyCatalog.CatalogKind.Messier] = true;
eligible = visibility.GetEntries(cachedSettings);
Check(eligible.Length > 0 && eligible.All(entry => entry.InCatalog(DeepSkyCatalog.CatalogKind.Messier)),
    "Re-enabling a catalog repopulates eligibility without stale objects.");
cachedSettings = DeepSkySettings.Read(cachedSettings.Serialize().Select(line => line.Split('=', 2))
    .ToDictionary(parts => parts[0], parts => parts[1]));
Check(ReferenceEquals(eligible, visibility.GetEntries(cachedSettings)), "Equivalent replacement settings reuse cached eligibility.");
Check(visibility.GetEntries(new()).Length == 0, "Reset/replacement settings invalidate the old enabled types.");
eligible = visibility.GetEntries(cachedSettings);
var cachedDetails = andromeda.DisplayDetails;
for(int i = 0; i < 1000; i++) {
    visibility.GetEntries(cachedSettings);
    _ = andromeda.DisplayDetails;
}
var timer = System.Diagnostics.Stopwatch.StartNew();
long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
bool stable = true;
for(int i = 0; i < 10000; i++) {
    stable &= ReferenceEquals(eligible, visibility.GetEntries(cachedSettings));
    stable &= ReferenceEquals(cachedDetails, andromeda.DisplayDetails);
}
long cacheAllocations = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
timer.Stop();
Check(stable, "10,000 unchanged frames reuse eligibility and formatted details.");
Check(cacheAllocations == 0, $"Steady-state eligibility/details cache must allocate zero managed bytes, got {cacheAllocations}.");
Console.WriteLine($"Cache check: 10,000 unchanged eligibility/details reads, {cacheAllocations} managed bytes, {timer.Elapsed.TotalMilliseconds:F2} ms.");

string temp = Path.Combine(Path.GetTempPath(), "OhMyStars-DeepSkyTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try {
    string header = File.ReadLines(ngc).First();
    string[] columns = header.Split(';');
    string Record(Dictionary<string, string> fields) => string.Join(";", columns.Select(column => {
        string value = fields.GetValueOrDefault(column, "");
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }));
    string main = Path.Combine(temp, "NGC.csv"), extra = Path.Combine(temp, "addendum.csv");
    File.WriteAllText(extra, header + Environment.NewLine);
    Dictionary<string, string> fields = new() {
        ["Name"] = "NGC0001", ["Type"] = "PN", ["RA"] = "23:59:59.99", ["Dec"] = "-00:30:00",
        ["V-Mag"] = "6.5", ["B-Mag"] = "7.5", ["Cstar V-Mag"] = "1",
        ["NED notes"] = "A \"quoted\" note; with a semicolon\nand another line",
        ["OpenNGC notes"] = "Second source note", ["Identifiers"] = "TEST 001,TEST 002", ["Sources"] = "Type:2|RA:1"
    };
    void Write() => File.WriteAllText(main, header + Environment.NewLine + Record(fields) + Environment.NewLine);
    DeepSkyCatalog Read() => DeepSkyCatalog.Load(main, extra);
    Write();
    var entry = Read().Entries.Single();
    Check(entry["NED notes"] == fields["NED notes"] && entry["OpenNGC notes"] == fields["OpenNGC notes"],
        "Quoted semicolons, escaped quotes and multiline notes are preserved.");
    Check(entry.DisplayDetails.NoteLines.SequenceEqual(new[] {
        "NED notes: " + fields["NED notes"], "OpenNGC notes: " + fields["OpenNGC notes"]
    }), "Cached notes preserve both sources, quoting and line breaks.");
    Check(entry.DecDegrees == -0.5 && entry.RaHours is > 23.99 and < 24, "Negative-zero declination and near-24-hour RA are valid.");
    fields["Pax"] = "1000"; Write();
    Check(Read().Entries.Single().DistanceLightYears == 3.26156377695,
        "1000 milliarcseconds is one parsec, converted to light years.");
    foreach(string parallax in new[] { "", "0", "-1" }) {
        fields["Pax"] = parallax; Write();
        var noDistance = Read().Entries.Single();
        Check(noDistance.DistanceLightYears is null && noDistance.DisplayDetails.Distance is null,
            "Unavailable or nonpositive parallax does not produce a distance.");
    }
    foreach(string parallax in new[] { "NaN", "Infinity", "invalid", "1e-320" }) {
        fields["Pax"] = parallax; Write();
        Reject(() => Read(), "Invalid or overflow-producing parallax fails explicitly.");
    }
    fields["Pax"] = ""; Write();
    Check(entry.Magnitude == 6.5, "Central-star magnitude is not substituted for nebula magnitude.");
    Check(entry.DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 6.50" &&
        entry.DisplayDetails.FieldLines.All(line => !line.Contains(" magnitude:", StringComparison.Ordinal)),
        "Only one integrated magnitude is displayed when V exists, without separate band or central-star magnitude lines.");
    fields["V-Mag"] = ""; fields["J-Mag"] = "4.5"; fields["H-Mag"] = "4"; fields["K-Mag"] = "3.5"; Write();
    Check(Read().Entries.Single().DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 7.50 (B-band; visual unavailable)",
        "B takes precedence over infrared bands, without inventing a visual magnitude.");
    fields["B-Mag"] = ""; Write();
    Check(Read().Entries.Single().DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 4.50 (J-band; visual unavailable)",
        "J is the first infrared fallback for display.");
    Check(Read().Entries.Single().Magnitude is null, "Infrared display fallback does not change ring magnitude eligibility.");
    fields["J-Mag"] = ""; Write();
    Check(Read().Entries.Single().DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 4.00 (H-band; visual unavailable)",
        "H is used when V, B and J are absent.");
    fields["H-Mag"] = ""; Write();
    Check(Read().Entries.Single().DisplayDetails.ApparentMagnitude == "Apparent Magnitude: 3.50 (K-band; visual unavailable)",
        "K is the last integrated-magnitude fallback.");
    fields["K-Mag"] = ""; Write();
    Check(Read().Entries.Single().DisplayDetails.ApparentMagnitude == "Apparent Magnitude: N/A",
        "Central-star brightness never substitutes for the object's missing integrated magnitude.");
    fields["B-Mag"] = "7.5";
    Check(entry.Identifiers.Contains("TEST 001") && entry.Identifiers.Contains("TEST 002"), "Multiple alternate identifiers survive.");
    fields["V-Mag"] = "8.90"; Write();
    var boundary = Read().Entries.Single();
    DeepSkySettings boundarySettings = new();
    boundarySettings.Styles["PN"].Enabled = true;
    boundarySettings.Styles["PN"].MagnitudeCutoff = 8.9f;
    Check(boundarySettings.IsVisible(boundary), "Decimal magnitude at the displayed cutoff is included despite float-slider precision.");
    boundarySettings.Styles["PN"].MagnitudeCutoff = 8.89f;
    Check(!boundarySettings.IsVisible(boundary), "Objects fainter than the displayed two-decimal cutoff are excluded.");
    fields["RA"] = "24:00:00"; Write(); Reject(() => Read(), "RA >= 24 must fail explicitly.");
    fields["RA"] = "00:60:00"; Write(); Reject(() => Read(), "Minutes >= 60 must fail explicitly.");
    fields["RA"] = "00:00:60"; Write(); Reject(() => Read(), "Seconds >= 60 must fail explicitly.");
    fields["RA"] = "00:00:00"; fields["Dec"] = "+90:00:01"; Write(); Reject(() => Read(), "Out-of-range Dec must fail explicitly.");
    fields["Dec"] = "+90:00:00"; Write();
    Check(Read().Entries.Single().DecDegrees == 90, "Exactly polar coordinates are valid.");
    fields["V-Mag"] = "Infinity"; Write(); Reject(() => Read(), "Nonfinite magnitude must fail explicitly.");
    fields["V-Mag"] = "not-a-number"; Write(); Reject(() => Read(), "Malformed magnitude must fail explicitly.");
    fields["V-Mag"] = ""; fields["B-Mag"] = ""; fields["RA"] = ""; Write();
    Check(!Read().Entries.Single().CanDraw, "Missing coordinates keep an entry searchable without drawing it.");
    fields["Type"] = "UnknownType"; Write(); Reject(() => Read(), "New unknown object types must not be silently omitted.");
    fields["Type"] = "G"; Write();
    File.AppendAllText(main, Record(fields) + Environment.NewLine);
    Reject(() => Read(), "Duplicate primary names fail explicitly.");
    File.WriteAllText(main, "Name;WrongHeader\n");
    Reject(() => Read(), "Unexpected schema fails explicitly.");
    File.WriteAllText(main, header + "\nNGC0001;G\n");
    Reject(() => Read(), "Short records fail explicitly.");
    File.Delete(main);
    try {
        Read();
        throw new InvalidOperationException("Missing catalog should fail.");
    } catch(FileNotFoundException) {
        checks++;
    }
} finally {
    File.Delete(Path.Combine(temp, "NGC.csv"));
    File.Delete(Path.Combine(temp, "addendum.csv"));
    Directory.Delete(temp);
}
Console.WriteLine($"PASS: {checks} Deep Sky checks; {catalog.Entries.Count:N0} bundled objects loaded.");
