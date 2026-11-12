using System.Globalization;
using OhMyStars;

static void Check(bool value, string message) {
    if(!value) throw new InvalidOperationException(message);
}

var skyLabels = new ScreenLabelLayout();
Check(MultipleStarCatalog.IsWithinCenterRadius(800, 400, 800, 400, 800, 0) &&
    !MultipleStarCatalog.IsWithinCenterRadius(801, 400, 800, 400, 800, 0),
    "Zero highlight radius includes only the screen center.");
Check(MultipleStarCatalog.IsWithinCenterRadius(1040, 720, 800, 400, 800, 50) &&
    !MultipleStarCatalog.IsWithinCenterRadius(1041, 720, 800, 400, 800, 50),
    "Highlight radius is circular, inclusive, and based on screen height.");
Check(MultipleStarCatalog.IsWithinCenterRadius(1100, 600, 900, 600, 800, 25) &&
    !MultipleStarCatalog.IsWithinCenterRadius(1101, 600, 900, 600, 800, 25),
    "Highlight filtering respects viewport origin offsets and the selected percentage.");
List<ScreenLabelLayout.Box> PlaceSkyLabels() {
    skyLabels.Begin(400, 240);
    ScreenLabelLayout.Box gridLabel = new(170, 100, 50, 16);
    skyLabels.Reserve(gridLabel);
    List<ScreenLabelLayout.Box> placed = [gridLabel];
    foreach(var preferred in Enumerable.Repeat(new ScreenLabelLayout.Box(175, 100, 70, 16), 60)
        .Concat(new[] { new ScreenLabelLayout.Box(-20, -10, 80, 20), new ScreenLabelLayout.Box(390, 235, 80, 20) })) {
        if(!skyLabels.TryPlace(preferred, out var box))
            continue;
        Check(box.X >= 2 && box.Y >= 2 && box.X + box.Width <= 398 && box.Y + box.Height <= 238,
            "Background labels stay inside viewport bounds.");
        Check(placed.All(other => box.X >= other.X + other.Width + 3 || box.X + box.Width + 3 <= other.X ||
            box.Y >= other.Y + other.Height + 3 || box.Y + box.Height + 3 <= other.Y),
            "Background names, spectra and reserved grid labels do not overlap.");
        placed.Add(box);
    }
    Check(placed.Count > 5 && placed.Count < 63, "Crowded background labels relocate or are suppressed.");
    return placed;
}
var firstSkyLayout = PlaceSkyLabels();
Check(firstSkyLayout.SequenceEqual(PlaceSkyLabels()), "Background label layout is deterministic and clears between frames.");
Check(!skyLabels.TryPlace(new(float.NaN, 0, 10, 10), out _) &&
    !skyLabels.TryPlace(new(0, 0, 500, 10), out _), "Invalid or oversized labels are not placed.");

Check(StarDesignations.Heading("Helvetios", "51", "Peg") == "Helvetios / 51 Pegasi", "Proper / Flamsteed heading.");
Check(StarDesignations.Heading("", "58", "Ori") == "58 Orionis", "Unnamed Flamsteed star.");
Check(StarDesignations.Heading("Albireo", "", "Cyg") == "Albireo", "No dangling slash without Flamsteed.");
Check(StarDesignations.Heading("", "", "Cas") == "", "Missing heading must remain blank.");
Check(StarDesignations.Bayer("Iot", "Cas") == "Iota Cassiopeiae", "Expanded Bayer and genitive.");
Check(StarDesignations.Bayer("Alp-2", "CVn") == "Alpha-2 Canum Venaticorum", "Numbered Bayer / multiword genitive.");
Check(StarDesignations.MatchesBayerSearch("Epsilon-1 Lyrae", " Epsilon Lyrae ") &&
    StarDesignations.MatchesBayerSearch("Epsilon-2 Lyrae", "epsilon lyr") &&
    StarDesignations.MatchesBayerSearch("Epsilon-1 Lyrae", "Epsilon-1") &&
    !StarDesignations.MatchesBayerSearch("Epsilon-2 Lyrae", "Epsilon-1") &&
    StarDesignations.UnnumberedBayer("Alpha-2 Canum Venaticorum") == "Alpha Canum Venaticorum",
    "Bayer search accepts unnumbered names without losing numbered search precision.");
Check(StarDesignations.Bayer("e", "UMa") == "e Ursae Majoris", "Roman Bayer letter preserved.");
Check(StarDesignations.Bayer("", "Cas") == "", "Missing Bayer must not invent a designation.");
Check(StarDesignations.Bayer("Iot", "") == "Iota", "Missing constellation.");
Check(StarDesignations.StarHeading("Vega", "Alp", "Lyr") == "Vega / Alpha Lyrae" &&
    StarDesignations.StarHeading("", "Iot", "Cas") == "Iota Cassiopeiae" &&
    StarDesignations.StarHeading("Vega", "", "Lyr") == "Vega" &&
    StarDesignations.StarHeading("", "", "Lyr") == "",
    "Stars heading uses proper / full Bayer consistently, omitting unavailable identifiers.");
Check(StarDesignations.ConstellationGenitive("???") == "???", "Unknown constellation preserved.");
Check(StarDesignations.CatalogIdentifier("bu", "Burnham", "1;1028") == "Burnham 1; Burnham 1028", "Burnham display names.");
Check(StarDesignations.CatalogIdentifier("stf", "F. Struve", "STF2382;STFA37;STFB12") ==
    "F. Struve 2382; F. Struve A 37; F. Struve B 12", "F. Struve display preserves appendix distinctions.");
Check(StarDesignations.CatalogIdentifier("stt", "O. Struve", "STT183;STTA37") ==
    "O. Struve 183; O. Struve A 37", "O. Struve display preserves appendix distinctions.");
Check(StarDesignations.CatalogIdentifier("stf", "F. Struve", "STF2382;STFA37", true) ==
    "F. Struve \u03a3 2382; F. Struve \u03a3 A 37" &&
    StarDesignations.CatalogIdentifier("stt", "", "STT183;STTA37", true) ==
    "\u039f\u03a3 183; \u039f\u03a3 A 37",
    "Struve symbols precede each identifier while retaining appendix distinctions.");
Check(StarDesignations.CatalogIdentifier("h", "Herschel", "H 2 5;H N 10") ==
    "Herschel 2 5; Herschel N 10", "Herschel display preserves classes.");
Check(StarDesignations.CatalogIdentifier("hd", "HD", "5005") == "HD 5005", "HD display uses space.");
Check(StarDesignations.CatalogIdentifier("bds", "BDS", "455") == "BDS:455", "Other identifier displays unchanged.");
MultipleStarCatalog.Star HeaderStar(string proper = "", string flam = "", string bayer = "",
    string bu = "", string hd = "", string stf = "", string stt = "", string hip = "", string oms = "",
    string gaia = "") {
    string[] columns = ["proper", "bayer", "con", .. CatalogIdentifiers.Catalogs.Select(c => c.Column)];
    string[] fields = columns.Select(column => column switch {
        "proper" => proper, "flam" => flam, "bayer" => bayer, "con" => "Lyr",
        "bu" => bu, "hd" => hd, "stf" => stf, "stt" => stt, "hip" => hip,
        "oms_id" => oms, "gaia" => gaia, _ => ""
    }).ToArray();
    return new(fields,
        columns.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
}
Check(StarDesignations.PrimaryHeading(HeaderStar(bu: "1", hd: "5005", stf: "STF1", stt: "STT2")) ==
    "Burnham 1 / HD 5005", "Burnham/HD tier takes precedence over Struve.");
Check(StarDesignations.PrimaryHeading(HeaderStar(bu: "1", stf: "STF1")) == "Burnham 1" &&
    StarDesignations.PrimaryHeading(HeaderStar(hd: "5005", stt: "STT2")) == "HD 5005 / O. Struve 2",
    "Burnham alone stays unchanged; HD alone is supplemented by O. Struve.");
Check(StarDesignations.PrimaryHeading(HeaderStar(hd: "5005", stf: "STFA37", stt: "STT2")) ==
    "HD 5005 / F. Struve A 37" &&
    StarDesignations.PrimaryHeading(HeaderStar(hd: "5005", stf: " ", stt: "STT2")) == "HD 5005 / O. Struve 2" &&
    StarDesignations.PrimaryHeading(HeaderStar(hd: "5005")) == "HD 5005",
    "HD-only heading prefers F. Struve, then O. Struve, and stays HD-only without either.");
Check(StarDesignations.PrimaryHeading(HeaderStar(stf: "STFA37", stt: "STT183")) ==
    "F. Struve A 37 / O. Struve 183", "Struve fallback uses shared readable formatting.");
Check(StarDesignations.PrimaryHeading(HeaderStar(hd: "5005", stf: "STFA37", stt: "STT183"), true) ==
    "HD 5005 / F. Struve \u03a3 A 37" &&
    StarDesignations.PrimaryHeading(HeaderStar(hd: "5005", stt: "STT183"), true) ==
    "HD 5005 / O. Struve \u039f\u03a3 183",
    "Symbol-enabled headings retain the existing HD/Struve priority.");
Check(StarDesignations.PrimaryHeading(HeaderStar(stf: "STF1")) == "F. Struve 1" &&
    StarDesignations.PrimaryHeading(HeaderStar(stt: "STT2")) == "O. Struve 2",
    "Either Struve identifier can appear alone.");
Check(StarDesignations.PrimaryHeading(HeaderStar(proper: "Vega", bu: "1")) == "Vega" &&
    StarDesignations.PrimaryHeading(HeaderStar(flam: "4", hd: "5005")) == "4 Lyrae" &&
    StarDesignations.PrimaryHeading(HeaderStar(bayer: "Eps-1", bu: "1")) == "",
    "Existing proper, Flamsteed and Bayer headings suppress catalog fallbacks.");
Check(StarDesignations.PrimaryHeading(HeaderStar(bu: " ", hd: " ", stt: "STT2")) == "O. Struve 2" &&
    StarDesignations.PrimaryHeading(HeaderStar()) == "",
    "Blank identifiers advance the hierarchy without inventing a heading.");
Check(StarDesignations.PrimaryHeading(HeaderStar(hip: "123", oms: "700")) == "Hipparcos:123" &&
    StarDesignations.PrimaryHeading(HeaderStar(gaia: "1234567890123456789", oms: "700")) == "Gaia:1234567890123456789" &&
    StarDesignations.PrimaryHeading(HeaderStar(oms: "700")) == "OMS 700",
    "Remaining real catalog identifiers take precedence over the last-resort OMS heading.");
Check(StarDesignations.PrimaryHeading(HeaderStar(proper: "Vega", flam: "3", bayer: "Alp", oms: "700")) ==
    "Vega / 3 Lyrae" && StarDesignations.Bayer("Alp", "Lyr") == "Alpha Lyrae",
    "Both tabs use proper/Flamsteed on the first line and full Bayer on the second.");
Check(StarDesignations.NavballName(HeaderStar(proper: "Vega", bayer: "Alp", hd: "1")) == "Vega" &&
    StarDesignations.NavballName(HeaderStar(bayer: "Eps-1", hd: "1")) == "Eps-1 Lyr" &&
    StarDesignations.NavballName(HeaderStar(hd: "1", flam: "4", stf: "STF2")) == "HD 1" &&
    StarDesignations.NavballName(HeaderStar(flam: "4", stf: "STF2")) == "4 Lyr" &&
    StarDesignations.NavballName(HeaderStar(stf: "STF2", stt: "STT3", hip: "4")) == "F. Struve 2" &&
    StarDesignations.NavballName(HeaderStar(stt: "STT3", hip: "4")) == "O. Struve 3" &&
    StarDesignations.NavballName(HeaderStar(hip: "4", oms: "700")) == "HIP:4" &&
    StarDesignations.NavballName(HeaderStar(gaia: "12345", oms: "700")) == "Gaia:12345" &&
    StarDesignations.NavballName(HeaderStar(oms: "700")) == "OMS 700",
    "Navball labels follow proper, abbreviated Bayer, HD, Flamsteed, Struve, HIP, other catalogs, then OMS.");
Check(StarDesignations.BackgroundName(HeaderStar(proper: "Vega", bayer: "Alp", flam: "3")) == "Vega" &&
    StarDesignations.BackgroundName(HeaderStar(bayer: "Gam", flam: "3", stf: "STF1")) == "Gamma Lyrae" &&
    StarDesignations.BackgroundName(HeaderStar(flam: "3", stf: "STF1")) == "3 Lyrae" &&
    StarDesignations.BackgroundName(HeaderStar(stf: "STFA37", stt: "STT2", bu: "1", hd: "5005")) == "F. Struve A 37" &&
    StarDesignations.BackgroundName(HeaderStar(stt: "STT2", bu: "1", hd: "5005")) == "O. Struve 2" &&
    StarDesignations.BackgroundName(HeaderStar(bu: "1", hd: "5005")) == "Burnham 1" &&
    StarDesignations.BackgroundName(HeaderStar(hd: "5005")) == "HD 5005" &&
    StarDesignations.BackgroundName(HeaderStar(oms: "700", hip: "123")) == "",
    "Background names use exactly one preferred designation and never fall back to OMS/HIP.");
string[] constellationCodes = ("And Ant Aps Aqr Aql Ara Ari Aur Boo Cae Cam Cnc CVn CMa CMi Cap Car Cas Cen Cep Cet Cha Cir Col Com CrA CrB Crv Crt Cru " +
    "Cyg Del Dor Dra Equ Eri For Gem Gru Her Hor Hya Hyi Ind Lac Leo LMi Lep Lib Lup Lyn Lyr Men Mic Mon Mus Nor Oct Oph Ori Pav Peg Per Phe Pic Psc PsA " +
    "Pup Pyx Ret Sge Sgr Sco Scl Sct Ser Sex Tau Tel Tri TrA Tuc UMa UMi Vel Vir Vol Vul").Split(' ');
Check(constellationCodes.Length == 88 && constellationCodes.All(c => StarDesignations.ConstellationGenitive(c) != c),
    "All 88 IAU constellations have a genitive.");

var circularOrbit = new MultipleStarOrbits.Orbit("00001+0000", "A", "B", 10, 0, 0, 0, 0, 1, "fixture");
foreach(double scaleExtent in new[] { 0.0001, 0.1, 1, 10, 100, 1000, 10000 })
    foreach(double plotSize in new[] { 100d, 240, 420 })
        foreach(double plotZoom in new[] { 1d, 2, 10 }) {
            var bar = MultipleStarCatalog.PlotScaleBar(scaleExtent, plotSize, plotZoom)!.Value;
            var first = MultipleStarCatalog.PlotPosition(new(0, 0), scaleExtent, plotSize, plotZoom, new(3, 4));
            var last = MultipleStarCatalog.PlotPosition(new(bar.Arcseconds, 0), scaleExtent, plotSize, plotZoom, new(3, 4));
            Check(Math.Abs(Math.Log10(bar.Arcseconds) - Math.Round(Math.Log10(bar.Arcseconds))) < 1e-9 &&
                bar.Pixels > (plotSize - 24) / 10 - 1e-9 &&
                bar.Pixels <= plotSize - 24 + 1e-9 &&
                Math.Abs(first.East - last.East - bar.Pixels) < 1e-6,
                "Scale bar uses a fitting power of ten and matches actual projected arcseconds across extents, sizes, zoom and pan.");
        }
foreach(double arcseconds in new[] { 1d, 10, 100 }) {
    var fittingBar = MultipleStarCatalog.PlotScaleBar(arcseconds, 420, 1)!.Value;
    Check(fittingBar.Arcseconds == arcseconds && Math.Abs(fittingBar.Pixels - 174) < 1e-9,
        "Use 1, 10 or 100 arcseconds when it fits the plot width, even beyond the old 120-pixel limit.");
}
Check(MultipleStarCatalog.PlotScaleBar(0, 420, 1) is null &&
    MultipleStarCatalog.PlotScaleBar(1, 72, 1) is null &&
    MultipleStarCatalog.PlotScaleBar(1, 420, 0) is null,
    "No misleading scale is shown without an angular extent or usable plot projection.");
var circularPoints = MultipleStarOrbits.Sample(circularOrbit, new(2, 3));
Check(circularPoints.Length == 129 && circularPoints.All(p => Math.Abs(Math.Sqrt(Math.Pow(p.East - 2, 2) + Math.Pow(p.North - 3, 2)) - 10) < 1e-9),
    "Face-on circular relative orbit must have constant angular radius and translated reference.");
Check(Math.Abs(circularPoints[0].North - 13) < 1e-9 && Math.Abs(circularPoints[32].East - 12) < 1e-9,
    "Orbital node convention must place zero angle north and positive angle east.");
var eccentricPoints = MultipleStarOrbits.Sample(circularOrbit with { Eccentricity = 0.5 }, new(0, 0));
Check(Math.Abs(eccentricPoints[0].North - 5) < 1e-9 && Math.Abs(eccentricPoints[64].North + 15) < 1e-9,
    "Eccentric orbit must have reference at focus, not ellipse center.");
var edgeOnPoints = MultipleStarOrbits.Sample(circularOrbit with { Inclination = 90 }, new(0, 0));
Check(edgeOnPoints.All(p => Math.Abs(p.East) < 1e-9), "Edge-on orbit must project to a line.");
var rotatedNode = MultipleStarOrbits.Sample(circularOrbit with { Node = 90 }, new(0, 0));
Check(Math.Abs(rotatedNode[0].East - 10) < 1e-9 && Math.Abs(rotatedNode[0].North) < 1e-9,
    "90-degree node rotates periastron from north to east.");
var rotatedPeriastron = MultipleStarOrbits.Sample(circularOrbit with { Periastron = 90, Inclination = 60 }, new(0, 0));
Check(Math.Abs(rotatedPeriastron[0].East - 5) < 1e-9 && Math.Abs(rotatedPeriastron[0].North) < 1e-9,
    "Periastron rotation must project through inclination.");
Check(Math.Abs(circularPoints[^1].East - circularPoints[0].East) < 1e-9 &&
    Math.Abs(circularPoints[^1].North - circularPoints[0].North) < 1e-9, "Sampled orbit closes.");

MultipleStarCatalog.Offset[] testLine = [new(0, 0), new(100, 0)];
Check(MultipleStarOrbits.TouchesCurve(new(50, 5.5), 5, testLine) &&
    !MultipleStarOrbits.TouchesCurve(new(50, 5.501), 5, testLine),
    "Orbit contact includes the star radius and half of the one-pixel line thickness.");
Check(MultipleStarOrbits.TouchesCurve(new(50, 0), 2, testLine),
    "Orbit contact checks segment interiors, not just sampled vertices.");
Check(MultipleStarOrbits.TouchesCurve(new(103, 4), 4.5, testLine) &&
    !MultipleStarOrbits.TouchesCurve(new(104, 4), 4.5, testLine),
    "Contact beyond a segment endpoint uses distance to the endpoint.");
Check(MultipleStarOrbits.TouchesCurve(new(0, 2), 2, [new(0, 0), new(0, 0)]) &&
    !MultipleStarOrbits.TouchesCurve(new(0, 0), 2, []),
    "Degenerate projected segments and empty curves are handled.");
var contactPoint = MultipleStarCatalog.PlotPosition(new(50, 3), 100, 272, 1, new(0, 0));
var contactCurve = testLine.Select(p => MultipleStarCatalog.PlotPosition(p, 100, 272, 1, new(0, 0))).ToArray();
var zoomPoint = MultipleStarCatalog.PlotPosition(new(50, 3), 100, 272, 2, new(7, 8));
var zoomCurve = testLine.Select(p => MultipleStarCatalog.PlotPosition(p, 100, 272, 2, new(7, 8))).ToArray();
Check(MultipleStarOrbits.TouchesCurve(contactPoint, 5, contactCurve) &&
    !MultipleStarOrbits.TouchesCurve(zoomPoint, 5, zoomCurve),
    "Contact is evaluated in screen pixels at the current plot zoom and pan.");

foreach(var points in new[] {
    new[] { (100f, 100f), (102f, 100f) },
    Enumerable.Repeat((100f, 100f), 14).ToArray(),
    new[] { (2f, 2f), (2f, 3f), (198f, 198f), (197f, 198f) }
}) {
    var stars = points.Select(p => new MultipleStarCatalog.PlotLabel(p.Item1, p.Item2, 5, 24, 14)).ToArray();
    var positions = MultipleStarCatalog.PlaceLabels(stars, 200, 200);
    Check(positions.SequenceEqual(MultipleStarCatalog.PlaceLabels(stars, 200, 200)), "Labels must be stable.");
    for(int i = 0; i < positions.Length; i++) {
        var a = positions[i];
        Check(a.X >= 0 && a.Y >= 0 && a.X + 24 <= 200 && a.Y + 14 <= 200, "Label outside plot.");
        for(int j = 0; j < i; j++) {
            var b = positions[j];
            Check(a.X + 24 <= b.X || b.X + 24 <= a.X || a.Y + 14 <= b.Y || b.Y + 14 <= a.Y,
                "Cluster labels overlap.");
        }
    }
}

string temp = Path.Combine(Path.GetTempPath(), $"oms-multi-{Guid.NewGuid():N}.csv");
string reviewTemp = temp + ".reviews.csv";
string approvalTemp = temp + ".approvals.csv";
string blockedApprovalPath = temp + ".blocked";
try {
    string[] header = ("oms_id,system_id,primary_oms_id,component,component_label,ra,dec,mag,absmag,ci,dist,hip,hd,h,stf,stt,bds,wds_id," +
        "proper,bayer,con,spect,radec_source,relationships,theta_deg,rho_arcsec,measurement_year,wds_source_line,mag_src,pmra,pmdec,id,tyc,gaia,hyg,hr,gl,flam,bu").Split(',');
    string Row(long id, string label, string relationships, string theta, string rho, string years, string lines) {
        string[] values = Enumerable.Repeat("", header.Length).ToArray();
        void Set(string name, string value) => values[Array.IndexOf(header, name)] = value;
        Set("oms_id", id.ToString(CultureInfo.InvariantCulture));
        Set("system_id", "1"); Set("primary_oms_id", "1"); Set("component", id == 1 ? "P" : id == 4 ? "U" : id == 5 ? "Q" : "V");
        Set("component_label", label); Set("relationships", relationships); Set("theta_deg", theta);
        Set("rho_arcsec", rho); Set("measurement_year", years); Set("wds_source_line", lines);
        Set("wds_id", "00001+0000"); Set("radec_source", id is 1 or 2 ? "WDS" : "GIA");
        Set("proper", id == 1 ? "\"A, \"\"quoted\"\" star\"" : "");
        Set("pmra", id == 1 ? "10" : "13");
        Set("pmdec", id == 1 ? "-4" : id == 3 ? "" : "-6");
        return string.Join(",", values);
    }
    string[] rows = [
        Row(1, "A", "", "", "", "", ""),
        Row(2, "B", "A>B|A>B", "90|90", "20|10", "2020|2000", "1|2"),
        Row(3, "C", "B>C", "0", "5", "2000", "3"),
        Row(4, "D", "AB>D", "180", "50", "2000", "4"),
        Row(5, "E", "A>E", "", "", "2000", "5"),
        Row(6, "F", "F>C", "270", "5", "2000", "6")
    ];
    File.WriteAllLines(temp, new[] { string.Join(",", header) }.Concat(rows));
    var catalog = MultipleStarCatalog.Load(temp);
    Check(catalog.DefinitivelyCorrectPrimaries.Count == 0, "Review metadata is opt-in for release catalog loading.");
    File.WriteAllLines(reviewTemp, ["primary_oms_id,wds_id,name", "1,00001+0000,Reviewed fixture"]);
    Check(MultipleStarCatalog.Load(temp, reviewTemp).DefinitivelyCorrectPrimaries.SetEquals([1L]),
        "Definitive review resolves the exact primary and WDS identity.");
    foreach(string invalid in new[] {
        "2,00001+0000,Companion", "999,00001+0000,Missing primary",
        "1,99999+9999,Wrong identity", "1,00001+0000,",
        "1,00001+0000,First\n1,00001+0000,Duplicate", "bad,00001+0000,Bad ID"
    }) {
        File.WriteAllText(reviewTemp, "primary_oms_id,wds_id,name\n" + invalid + "\n");
        bool rejected = false;
        try { MultipleStarCatalog.Load(temp, reviewTemp); }
        catch(InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid definitive review must fail explicitly: " + invalid);
    }
    File.WriteAllText(reviewTemp, "wrong_header\n");
    bool badReviewHeader = false;
    try { MultipleStarCatalog.Load(temp, reviewTemp); }
    catch(InvalidDataException) { badReviewHeader = true; }
    Check(badReviewHeader, "Malformed review header is rejected.");
#if DEBUG
    File.WriteAllLines(reviewTemp, ["primary_oms_id,wds_id,name", "1,00001+0000,Reviewed fixture"]);
    string originalReviews = File.ReadAllText(reviewTemp);
    var editableReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    editableReviews.SetDefinitiveApproval(1, false);
    Check(!editableReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        !MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).DefinitivelyCorrectPrimaries.Contains(1),
        "Undo approval overrides shipped approval immediately and after reload.");
    editableReviews.SetDefinitiveApproval(1, true);
    Check(editableReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).DefinitivelyCorrectPrimaries.Contains(1),
        "Approval is saved atomically and survives reload, including a quoted primary name.");
    Check(File.ReadAllText(reviewTemp) == originalReviews &&
        File.ReadAllLines(approvalTemp).Length == 2,
        "Runtime changes do not edit the shipped list; repeated toggles keep one override per primary.");
    Check(MultipleStarCatalog.Load(temp, approvalOverridesPath: approvalTemp).DefinitivelyCorrectPrimaries.Contains(1),
        "A local approval can add an unreviewed primary without a shipped baseline.");
    bool rejectedCompanionApproval = false;
    try { editableReviews.SetDefinitiveApproval(2, true); }
    catch(InvalidDataException) { rejectedCompanionApproval = true; }
    Check(rejectedCompanionApproval && File.ReadAllLines(approvalTemp).Length == 2,
        "Companions cannot be approved as primaries or change persisted overrides.");
    var blockedReviews = MultipleStarCatalog.Load(temp, reviewTemp, blockedApprovalPath);
    Directory.CreateDirectory(blockedApprovalPath);
    bool writeFailed = false;
    try { blockedReviews.SetDefinitiveApproval(1, false); }
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { writeFailed = true; }
    Check(writeFailed && blockedReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        !Directory.EnumerateFiles(Path.GetDirectoryName(temp)!, Path.GetFileName(blockedApprovalPath) + ".*.tmp").Any(),
        "Failed persistence leaves the displayed approval unchanged and removes temporary output.");
    const string componentReviewHeader = "primary_oms_id,wds_id,name,approved,component_oms_id,component_label,original_type,desired_type,notes";
    File.WriteAllText(approvalTemp, "primary_oms_id,wds_id,name,approved\n1,00001+0000,Legacy approval,false\n");
    var componentReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    string catalogBeforeReviews = File.ReadAllText(temp);
    string notes = "User review, \"keep identity\"\nAdditional explanation: caf\u00e9.";
    componentReviews.SetComponentReview(1, 2, "C", notes);
    var reloadedReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    Check(reloadedReviews.GetComponentReview(2) == new MultipleStarCatalog.ComponentReview("C", notes) &&
        !reloadedReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        File.ReadLines(approvalTemp).First() == componentReviewHeader,
        "Component reviews migrate the legacy CSV while preserving undo approvals and quoted/multiline Unicode notes.");
    Check(reloadedReviews.Stars[2].Role == "V" && reloadedReviews.Systems.Single().Members.Count == 6 &&
        File.ReadAllText(temp) == catalogBeforeReviews && File.ReadAllText(reviewTemp) == originalReviews,
        "Pending corrections leave live component types, membership and shipped catalog/reviews unchanged.");
    foreach(var (type, _) in MultipleStarCatalog.ComponentReviewTypes) {
        componentReviews.SetComponentReview(1, 2, type, notes);
        Check(MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).GetComponentReview(2)?.DesiredType == type,
            "Every component type, including unlink, survives reload: " + type);
    }
    componentReviews.SetComponentReview(1, 3, "unlink", "");
    componentReviews.SetComponentReview(1, 2, "C", notes);
    componentReviews.SetDefinitiveApproval(1, true);
    reloadedReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    Check(reloadedReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        reloadedReviews.GetComponentReview(2)?.Notes == notes && reloadedReviews.GetComponentReview(3)?.DesiredType == "unlink",
        "Approving a corrected system preserves every pending component correction.");
    componentReviews.SetDefinitiveApproval(1, false);
    reloadedReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    Check(!reloadedReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        reloadedReviews.GetComponentReview(2)?.Notes == notes && reloadedReviews.GetComponentReview(3)?.DesiredType == "unlink",
        "Undoing approval also preserves all pending component corrections.");
    componentReviews.SetDefinitiveApproval(1, true);
    componentReviews.SetComponentReview(1, 2, "C", "");
    Check(MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).GetComponentReview(2) ==
        new MultipleStarCatalog.ComponentReview("C", ""),
        "Clearing a note preserves a requested type change.");
    componentReviews.SetComponentReview(1, 2, "V", "Note without a type change");
    Check(MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).GetComponentReview(2)?.Notes == "Note without a type change",
        "Notes can be recorded without changing the original type.");
    componentReviews.SetComponentReview(1, 2, "V", "");
    reloadedReviews = MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp);
    Check(reloadedReviews.GetComponentReview(2) is null &&
        reloadedReviews.GetComponentReview(3)?.DesiredType == "unlink" &&
        reloadedReviews.DefinitivelyCorrectPrimaries.Contains(1),
        "Returning to the original type with no note cancels only that component's request.");
    componentReviews.SetComponentReview(1, 1, "P", "Primary note");
    Check(MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp).GetComponentReview(1)?.Notes == "Primary note",
        "The primary component can also carry a review note.");
    string savedComponents = File.ReadAllText(approvalTemp);
    foreach(var (primary, component, type) in new[] { (999L, 2L, "C"), (1L, 999L, "C"), (1L, 2L, "bad") }) {
        bool rejectedComponent = false;
        try { componentReviews.SetComponentReview(primary, component, type, ""); }
        catch(InvalidDataException) { rejectedComponent = true; }
        Check(rejectedComponent && File.ReadAllText(approvalTemp) == savedComponents,
            "Invalid component identity or type cannot mutate saved reviews.");
    }
    writeFailed = false;
    try { blockedReviews.SetComponentReview(1, 2, "C", "Unsaved"); }
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { writeFailed = true; }
    Check(writeFailed && blockedReviews.GetComponentReview(2) is null &&
        blockedReviews.DefinitivelyCorrectPrimaries.Contains(1) &&
        !Directory.EnumerateFiles(Path.GetDirectoryName(temp)!, Path.GetFileName(blockedApprovalPath) + ".*.tmp").Any(),
        "Failed component writes leave memory and approval unchanged and clean up temporary output.");
    foreach(string invalidComponent in new[] {
        "1,99999+9999,Wrong WDS,,2,B,V,C,",
        "1,00001+0000,Missing component,,999,B,V,C,",
        "1,00001+0000,Stale label,,2,Z,V,C,",
        "1,00001+0000,Stale type,,2,B,C,V,",
        "1,00001+0000,Invalid type,,2,B,V,bad,",
        "1,00001+0000,Mixed row,true,2,B,V,C,",
        "2,00001+0000,Wrong primary,,3,C,V,C,",
        "1,00001+0000,Duplicate,,2,B,V,C,\n1,00001+0000,Duplicate,,2,B,V,unlink,",
        "1,00001+0000,Incomplete approval,true,,,,,Unexpected note"
    }) {
        File.WriteAllText(approvalTemp, componentReviewHeader + "\n" + invalidComponent + "\n");
        bool rejectedComponent = false;
        try { MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp); }
        catch(InvalidDataException) { rejectedComponent = true; }
        Check(rejectedComponent, "Invalid component reviews fail explicitly: " + invalidComponent);
    }
    foreach(string invalidOverride in new[] {
        "1,99999+9999,Wrong WDS,true", "2,00001+0000,Companion,true",
        "1,00001+0000,Wrong status,maybe", "1,00001+0000,First,true\n1,00001+0000,Duplicate,false"
    }) {
        File.WriteAllText(approvalTemp, "primary_oms_id,wds_id,name,approved\n" + invalidOverride + "\n");
        bool rejectedOverride = false;
        try { MultipleStarCatalog.Load(temp, reviewTemp, approvalTemp); }
        catch(InvalidDataException) { rejectedOverride = true; }
        Check(rejectedOverride, "Invalid local overrides fail explicitly: " + invalidOverride);
    }
    Console.WriteLine("Developer review persistence passed: legacy migration, all types/unlink, notes, cancellation, approvals, invalid identities and failed writes.");
#else
    File.WriteAllText(approvalTemp, "Malformed developer reviews must not be read in Release.\n");
    Check(MultipleStarCatalog.Load(temp, approvalOverridesPath: approvalTemp).DefinitivelyCorrectPrimaries.Count == 0,
        "Release ignores local developer review CSVs entirely, including malformed ones.");
#endif
    var system = catalog.Systems.Single();
    Check(!system.Primary.IsSystemCompanion &&
        system.Members.Where(s => s.Id != system.Primary.Id).All(s => s.IsSystemCompanion),
        "Stars picking distinguishes primary from every companion role, including U/Q/V.");
    string[] single = Enumerable.Repeat("", header.Length).ToArray();
    void SingleField(string name, string value) => single[Array.IndexOf(header, name)] = value;
    foreach(var (_, column) in CatalogIdentifiers.Catalogs)
        SingleField(column, column switch {
            "oms_id" => "700", "gaia" => "1234567890123456789", "bu" => "1;2",
            "stf" => "STFA37;STF2382", "stt" => "STT183", "h" => "H 2 5",
            _ => "123"
        });
    SingleField("component", "S");
    SingleField("primary_oms_id", "700");
    SingleField("proper", "Example");
    SingleField("bayer", "Iot");
    SingleField("con", "Cas");
    File.AppendAllLines(temp, [string.Join(",", single)]);
    var allStars = MultipleStarCatalog.Load(temp);
    Check(allStars.Stars.Count == 7 && allStars.Stars[700].Role == "S" && allStars.Systems.Count == 1,
        "OMS catalog retains singles and systems together without altering group membership.");
    var numberedBayer = (string[])single.Clone();
    numberedBayer[Array.IndexOf(header, "oms_id")] = "701";
    numberedBayer[Array.IndexOf(header, "primary_oms_id")] = "701";
    numberedBayer[Array.IndexOf(header, "bayer")] = "Eps-1";
    numberedBayer[Array.IndexOf(header, "con")] = "Lyr";
    File.AppendAllLines(temp, [string.Join(",", numberedBayer)]);
    var numberedCatalog = MultipleStarCatalog.Load(temp);
    string systemAliasPath = temp + ".bayer";
    try {
        string AliasRow(int id, int primary, string bayer, string label) {
            var values = Enumerable.Repeat("", header.Length).ToArray();
            foreach(var (name, value) in new[] { ("oms_id", $"{id}"), ("system_id", $"{primary}"),
                ("primary_oms_id", $"{primary}"), ("component", id == primary ? "P" : "C"),
                ("component_label", label), ("bayer", bayer), ("con", "Lyr") })
                values[Array.IndexOf(header, name)] = value;
            return string.Join(",", values);
        }
        File.WriteAllLines(systemAliasPath, [string.Join(",", header),
            AliasRow(800, 800, "Eps-1", "A"), AliasRow(801, 800, "Eps-2", "B"),
            AliasRow(900, 900, "", "A"), AliasRow(901, 900, "Eps-3", "B")]);
        var aliasCatalog = MultipleStarCatalog.Load(systemAliasPath);
        var aliases = CatalogIdentifiers.SearchSystemBayerAliases(aliasCatalog, "Epsilon Lyrae").ToArray();
        Check(aliases.SequenceEqual(new[] { (800L, "Epsilon Lyrae"), (900L, "Epsilon Lyrae") }),
            "One unnumbered entry per system selects its primary, even if only a companion carries the Bayer name.");
        Check(CatalogIdentifiers.Search(aliasCatalog, "", "Epsilon Lyrae").Count() == 5 &&
            CatalogIdentifiers.Search(aliasCatalog, "bayer", "Epsilon-2 Lyrae").Single().Id == 801 &&
            !CatalogIdentifiers.SearchSystemBayerAliases(aliasCatalog, "Epsilon-2").Any() &&
            CatalogIdentifiers.Search(aliasCatalog, "hd", "Epsilon Lyrae").Count() == 0,
            "System aliases supplement numbered results without changing numbered or other-catalog searches.");
    } finally {
        File.Delete(systemAliasPath);
    }
    Check(CatalogIdentifiers.Search(numberedCatalog, "", "Epsilon Lyrae", partialLimit: 0).Single().Id == 701 &&
        CatalogIdentifiers.Search(numberedCatalog, "bayer", "Eps", partialLimit: 0).Single().Id == 701 &&
        CatalogIdentifiers.Search(numberedCatalog, "bayer", "Epsilon-1 Lyrae").Single().Id == 701 &&
        !CatalogIdentifiers.Search(numberedCatalog, "bayer", "Epsilon-2 Lyrae").Any(),
        "All/catalog searches rank unnumbered full Bayer aliases as exact and preserve specific numbers.");
    Check(!allStars.Stars[700].IsSystemCompanion, "Single stars remain eligible for Stars picking.");
    foreach(var (_, column) in CatalogIdentifiers.Catalogs)
        Check(CatalogIdentifiers.Search(allStars, column, single[Array.IndexOf(header, column)]).Any(s => s.Id == 700),
            "Catalog search resolves recorded value for " + column);
    Check(CatalogIdentifiers.Search(allStars, "bu", "Burnham 2").Single().Id == 700 &&
        CatalogIdentifiers.Search(allStars, "stf", "F. Struve A 37").Single().Id == 700 &&
        CatalogIdentifiers.Search(allStars, "flam", "123 Cassiopeiae").Single().Id == 700 &&
        !CatalogIdentifiers.Search(allStars, "hd", "not present").Any(),
        "Readable aliases resolve and unmatched input returns no unrelated star.");
    foreach(var (_, column) in CatalogIdentifiers.Catalogs)
        Check(CatalogIdentifiers.Search(allStars, "", single[Array.IndexOf(header, column)]).Any(s => s.Id == 700),
            "All search resolves recorded value for " + column);
    foreach(string query in new[] { "example", "Iot", "Iota Cassiopeiae", "Burnham 2", "F. Struve A 37", "123 Cassiopeiae" })
        Check(CatalogIdentifiers.Search(allStars, "", query).Single().Id == 700,
            "All search resolves proper names, raw identifiers and readable designations: " + query);
    var allMatches = CatalogIdentifiers.Search(allStars, "", "123").ToArray();
    Check(allMatches.Count(s => s.Id == 700) == 1 &&
        !CatalogIdentifiers.Search(allStars, "", "not present").Any() &&
        CatalogIdentifiers.Search(allStars, "", "").Select(s => s.Id).Distinct().Count() == allStars.Stars.Count,
        "All search deduplicates matching identifiers, supports empty queries and rejects unmatched input.");
    Check(CatalogIdentifiers.Search(allStars, "hd", "123").Single().Label == "HD 123" &&
        !CatalogIdentifiers.Search(allStars, "hd", "Example").Any() &&
        CatalogIdentifiers.SearchLabel(allStars.Stars[700], "gaia") == "Gaia:1234567890123456789" &&
        CatalogIdentifiers.SearchLabel(system.Members.Single(s => s.Label == "B"), "bayer") == "",
        "Specific-method rows and previews contain only that identifier, preserving precision and missing values.");
    var identifiers = CatalogIdentifiers.Entries(allStars.Stars[700]).ToArray();
    Check(identifiers.Length == CatalogIdentifiers.Catalogs.Length + 2 &&
        identifiers.Contains(("Proper", "Example")) && identifiers.Contains(("Bayer", "Iota Cassiopeiae")) &&
        identifiers.Any(i => i.Value.Contains("1234567890123456789", StringComparison.Ordinal)),
        "Disclosure includes proper, Bayer and every recorded catalog ID without numeric truncation.");
    var alignedIdentifiers = CatalogIdentifiers.Entries(allStars.Stars[700], includeCatalogNames: false).ToArray();
    Check(alignedIdentifiers.Contains(("Burnham", "1; 2")) &&
        alignedIdentifiers.Contains(("F. Struve", "A 37; 2382")) &&
        alignedIdentifiers.Contains(("HD", "123")) &&
        alignedIdentifiers.Contains(("Bayer", "Iota Cassiopeiae")) &&
        alignedIdentifiers.Contains(("Gaia", "1234567890123456789")),
        "Aligned disclosure separates labels from values while preserving aliases and full identifiers.");
    Check(!CatalogIdentifiers.Entries(system.Members.Single(s => s.Label == "B")).Any(i => i.Name == "Bayer"),
        "Disclosure omits absent identifiers.");
    var partialRow = (string[])single.Clone();
    partialRow[Array.IndexOf(header, "oms_id")] = "699";
    partialRow[Array.IndexOf(header, "bu")] = "10;100";
    partialRow[Array.IndexOf(header, "proper")] = "Burnham 1 candidate";
    var exactRow = (string[])single.Clone();
    exactRow[Array.IndexOf(header, "proper")] = "Burnham 1 candidate";
    File.WriteAllLines(temp, new[] { string.Join(",", header), string.Join(",", partialRow), string.Join(",", exactRow) });
    var rankedCatalog = MultipleStarCatalog.Load(temp);
    foreach(string column in new[] { "bu", "" }) {
        var ranked = CatalogIdentifiers.Search(rankedCatalog, column, "  bUrNhAm 1  ").ToArray();
        Check(ranked.Select(s => s.Id).SequenceEqual(new long[] { 700, 699 }) &&
            ranked[0].Label == "Burnham 1; Burnham 2",
            "Exact alias precedes earlier partial rows and partial fields in " + column);
        Check(CatalogIdentifiers.Search(rankedCatalog, column, "Burnham 2").Single().Id == 700,
            "Second alias is recognized as an exact match.");
    }
    Check(CatalogIdentifiers.Search(rankedCatalog, "bu", "1").First().Id == 700,
        "Raw exact identifier precedes longer identifiers.");
    Check(CatalogIdentifiers.Search(rankedCatalog, "", "Burnham 1", partialLimit: 0).Single().Id == 700,
        "Limiting retained partial results cannot discard a later exact match.");
    using(var cancelledSearch = new CancellationTokenSource()) {
        cancelledSearch.Cancel();
        try {
            _ = CatalogIdentifiers.Search(rankedCatalog, "", "Burnham 1", cancelledSearch.Token).ToArray();
            throw new InvalidOperationException("Cancelled catalog search was not stopped.");
        } catch(OperationCanceledException) { }
    }
    string OrbitRow(string wds = "00001+0000", string designation = "TST   1AB", string units = "a",
        string grade = "2", string axis = "10", string eccentricity = "0.5", string equinox = "2000") {
        char[] line = new string(' ', 264).ToCharArray();
        void Put(int index, int length, string value) => value.PadLeft(length).CopyTo(0, line, index, length);
        Put(19, 10, wds); Put(30, 14, designation); Put(80, 12, "50"); Put(92, 1, "y");
        Put(105, 9, axis); Put(114, 1, units); Put(125, 8, "60"); Put(143, 8, "45");
        Put(162, 12, "2000"); Put(174, 1, "y"); Put(187, 8, eccentricity); Put(205, 8, "30");
        Put(223, 4, equinox); Put(233, 1, grade); Put(237, 8, "Test2026");
        return new string(line);
    }
    string orbitTemp = temp + ".orbits";
    try {
        File.WriteAllLines(orbitTemp, [
            "header",
            OrbitRow(),
            OrbitRow("00002+0000", units: "m", axis: "100"),
            OrbitRow("00003+0000", units: "M", axis: "1"),
            OrbitRow("00004+0000", units: "u", axis: "100"),
            OrbitRow("00005+0000", grade: "9"), OrbitRow("00006+0000", grade: "8"),
            OrbitRow("00007+0000", grade: "7"), OrbitRow("00008+0000", eccentricity: "1"),
            OrbitRow("00009+0000", equinox: "1950"),
            OrbitRow("00010+0000"), OrbitRow("00010+0000"),
            OrbitRow("00011+0000", designation: "TST   1AB-CD"),
            OrbitRow("00012+0000", designation: "TST   1Aa1,2")
        ]);
        var orbitFixtures = MultipleStarOrbits.Load(orbitTemp);
        Check(orbitFixtures.Orbits.Count == 5, "Reject incomplete/nonvisual/non-2000, ambiguous and group orbits.");
        Check(orbitFixtures.Orbits.Single(o => o.Wds == "00002+0000").Axis == 0.1 &&
            orbitFixtures.Orbits.Single(o => o.Wds == "00003+0000").Axis == 60 &&
            Math.Abs(orbitFixtures.Orbits.Single(o => o.Wds == "00004+0000").Axis - 0.0001) < 1e-12,
            "ORB6 angular-axis units normalized to arcseconds.");
        Check(orbitFixtures.Orbits.Single(o => o.Wds == "00012+0000").Companion == "Aa2", "Abbreviated numeric orbit endpoint.");
        var fixtureCurves = orbitFixtures.Curves(system, MultipleStarCatalog.Layout(system));
        Check(fixtureCurves.Length == 1 && fixtureCurves[0].Companion == "B" &&
            fixtureCurves[0].Points.All(p => double.IsFinite(p.East) && double.IsFinite(p.North)),
            "Orbit must match exact WDS and directional individual endpoints.");
        var translatedLayout = MultipleStarCatalog.Layout(system);
        var referenceShift = new MultipleStarCatalog.Offset(12.5, -7.25);
        var originalReference = translatedLayout["A"];
        translatedLayout["A"] = new(originalReference.East + referenceShift.East,
            originalReference.North + referenceShift.North);
        var translatedCurve = orbitFixtures.Curves(system, translatedLayout).Single();
        Check(translatedCurve.Points.Zip(fixtureCurves[0].Points).All(pair =>
            Math.Abs(pair.First.East - pair.Second.East - referenceShift.East) < 1e-9 &&
            Math.Abs(pair.First.North - pair.Second.North - referenceShift.North) < 1e-9),
            "Every orbit point follows the OMS parent position exactly, independently of ORB6 sky coordinates.");
        var beforeOrbitLayout = MultipleStarCatalog.Layout(system);
        var originalMeasuredPoint = beforeOrbitLayout["B"];
        orbitFixtures.Curves(system, beforeOrbitLayout);
        Check(beforeOrbitLayout["B"] == originalMeasuredPoint, "Orbit overlay must never move measured component points.");
        var faceOn = new MultipleStarOrbits.Orbit("00001+0000", "A", "B", 10, 0.5, 0, 0, 0, 2, "test");
        var faceOnPoints = MultipleStarOrbits.Sample(faceOn, new(2, 3));
        Check(Math.Abs(faceOnPoints[0].East - 2) < 1e-9 && Math.Abs(faceOnPoints[0].North - 8) < 1e-9 &&
            Math.Abs(faceOnPoints[32].East - (2 + 10 * Math.Sqrt(0.75))) < 1e-9 &&
            Math.Abs(faceOnPoints[32].North + 2) < 1e-9 &&
            Math.Abs(faceOnPoints[64].North + 12) < 1e-9,
            "Relative orbit projection preserves north/east orientation, focus and eccentricity.");
        Check(orbitFixtures.Curves(system with { Measures = [] }, MultipleStarCatalog.Layout(system)).Length == 0,
            "Do not associate orbit without matching catalog relationship.");
        File.WriteAllText(orbitTemp, "not a catalog");
        try {
            MultipleStarOrbits.Load(orbitTemp);
            throw new InvalidOperationException("Invalid orbit catalog was accepted.");
        } catch(InvalidDataException) { }
    } finally {
        File.Delete(orbitTemp);
    }
    Check(system.Primary["proper"] == "A, \"quoted\" star", "CSV quoted text not preserved.");
    Check(system.Primary.ListLabel == "*A" && system.Members.Single(s => s.Label == "B").ListLabel == "*B",
        "WDS-only position sources must mark primary and companion list entries.");
    Check(system.Members.Any(s => s.IsWdsPosition) &&
        !system.Members.Where(s => s.Label is not ("A" or "B")).Any(s => s.IsWdsPosition),
        "Conditional WDS legend must follow the same provenance predicate as the asterisks.");
    Check(system.Members.Single(s => s.Label == "C").ListLabel == "C",
        "Non-WDS positions must not receive a provenance asterisk.");
    Check(system.Primary.Label == "A", "Provenance marker must not alter geometry or component identifiers.");
    Check(MultipleStarCatalog.RelativeProperMotion(system.Primary, system.Primary) == (null, null),
        "Primary proper motion must not be displayed.");
    Check(MultipleStarCatalog.RelativeProperMotion(system.Members.Single(s => s.Label == "B"), system.Primary) == (3d, -2d),
        "Visual motion must be component minus primary.");
    Check(MultipleStarCatalog.RelativeProperMotion(system.Members.Single(s => s.Label == "C"), system.Primary) == (3d, null),
        "Missing motion axis must not be treated as zero.");
    Check(MultipleStarCatalog.RelativeProperMotionMagnitude(system.Primary, system.Primary) is null &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(system.Members.Single(s => s.Label == "B"), system.Primary)
            is double combinedMotion && Math.Abs(combinedMotion - Math.Sqrt(13)) < 1e-9 &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(system.Members.Single(s => s.Label == "C"), system.Primary) == 3,
        "Relative motion magnitude combines both recorded axes, uses a lone axis, and omits unavailable motion.");
    MultipleStarCatalog.Star MotionStar(string role, string ra, string dec) {
        string[] names = ["component", "pmra", "pmdec"];
        return new([role, ra, dec], names.Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index));
    }
    var motionPrimary = MotionStar("P", "0", "0");
    Check(MultipleStarCatalog.RelativeProperMotionMagnitude(MotionStar("V", "-6", ""), motionPrimary) == 6 &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(MotionStar("Q", "", "-8"), motionPrimary) == 8 &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(MotionStar("V", "0", "0"), motionPrimary) == 0 &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(MotionStar("C", "6", "8"), motionPrimary) == 10 &&
        MultipleStarCatalog.RelativeProperMotionMagnitude(MotionStar("U", "", ""), motionPrimary) is null,
        "Relative motion is available regardless of component category, with lone axes and zero handled.");
    Check(system.Members.Single(s => s.Label == "E").Role == "Q" &&
        MultipleStarCatalog.RelativeProperMotion(system.Members.Single(s => s.Label == "E"), system.Primary) == (3d, -2d),
        "Q accepted and retains Visual relative-motion behavior.");
    Check(system.Members.Single(s => s.Label == "E").RelationshipDisplay == "(Visual ?)" &&
        system.Members.Single(s => s.Label == "B").RelationshipDisplay == "(Visual)",
        "Q and V must display Visual ? and Visual respectively.");
    MultipleStarCatalog.Star MagnitudeStar(long id, string role, string magnitude) {
        var fields = Enumerable.Repeat("", header.Length).ToArray();
        fields[Array.IndexOf(header, "oms_id")] = id.ToString(CultureInfo.InvariantCulture);
        fields[Array.IndexOf(header, "component")] = role;
        fields[Array.IndexOf(header, "mag")] = magnitude;
        fields[Array.IndexOf(header, "absmag")] = "-10";
        return new(fields, header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    }
    Dictionary<long, bool> hidden = new();
    var nonWdsPrimary = MagnitudeStar(100, "P", "5");
    var wdsCompanion = system.Members.Single(s => s.Label == "B");
    var wdsOnlyCompanions = new MultipleStarCatalog.System(nonWdsPrimary, [nonWdsPrimary, wdsCompanion], []);
    Check(!MultipleStarCatalog.IsEntryVisible(system.Primary, true) &&
        !MultipleStarCatalog.IsEntryVisible(wdsCompanion, true) &&
        MultipleStarCatalog.IsEntryVisible(nonWdsPrimary, true),
        "WDS-only filter applies equally to primary and companion provenance.");
    Check(!MultipleStarCatalog.HasEligibleCompanion(wdsOnlyCompanions, 20, true) &&
        MultipleStarCatalog.HasEligibleCompanion(wdsOnlyCompanions, 20, false),
        "Toggling WDS-only exclusion removes/restores systems whose only companions are WDS-only.");
    Check(!MultipleStarCatalog.HasEligibleCompanion(system, 20, true),
        "WDS-only anchor never gets a ring while excluded.");
    hidden[wdsCompanion.Id] = true;
    Check(!MultipleStarCatalog.IsPlotVisible(wdsCompanion, nonWdsPrimary, 20, hidden, true) &&
        MultipleStarCatalog.IsPlotVisible(wdsCompanion, nonWdsPrimary, 20, hidden, false),
        "Plot override cannot bypass WDS-only filter, but remains available after disabling it.");
    hidden.Clear();
    var visual = MagnitudeStar(110, "V", "6");
    var questionableVisual = MagnitudeStar(111, "Q", "6");
    var visualOnly = new MultipleStarCatalog.System(nonWdsPrimary, [nonWdsPrimary, visual], []);
    Check(MultipleStarCatalog.IsEntryVisible(visual, false) &&
        !MultipleStarCatalog.IsEntryVisible(visual, false, true) &&
        MultipleStarCatalog.IsEntryVisible(questionableVisual, false, true) &&
        MultipleStarCatalog.IsEntryVisible(nonWdsPrimary, false, true),
        "Visual exclusion defaults off and excludes only V, not Q or primary.");
    Check(MultipleStarCatalog.HasEligibleCompanion(visualOnly, 20) &&
        !MultipleStarCatalog.HasEligibleCompanion(visualOnly, 20, excludeVisual: true) &&
        MultipleStarCatalog.HasEligibleCompanion(visualOnly with {
            Members = [nonWdsPrimary, visual, questionableVisual] }, 20, excludeVisual: true),
        "Visual-only systems lose ring eligibility; an eligible Q companion retains it.");
    Check(!MultipleStarCatalog.IsPlotVisible(visual, nonWdsPrimary, 20,
        new Dictionary<long, bool> { [visual.Id] = true }, excludeVisual: true) &&
        !MultipleStarCatalog.IsEntryVisible(wdsCompanion, true, true),
        "Visual exclusion cannot be bypassed by a plot override and combines with WDS exclusion.");
    var mixedSources = new MultipleStarCatalog.System(nonWdsPrimary,
        [nonWdsPrimary, wdsCompanion, MagnitudeStar(101, "Q", "6")], []);
    Check(MultipleStarCatalog.HasEligibleCompanion(mixedSources, 6, true) &&
        !MultipleStarCatalog.HasEligibleCompanion(mixedSources, 5, true),
        "Mixed-source ring eligibility combines WDS exclusion and apparent-magnitude cutoff.");
    foreach(string role in new[] { "C", "U", "V", "Q" }) {
        var dim = MagnitudeStar(50, role, "10.01");
        var boundary = MagnitudeStar(51, role, "10");
        var unknown = MagnitudeStar(52, role, "");
        Check(MultipleStarCatalog.IsDimComponent(dim, system.Primary, 10) &&
            !MultipleStarCatalog.IsPlotVisible(dim, system.Primary, 10, hidden),
            $"Faint {role} must be separated and not plotted, regardless of absolute magnitude.");
        Check(!MultipleStarCatalog.IsDimComponent(boundary, system.Primary, 10) &&
            MultipleStarCatalog.IsPlotVisible(boundary, system.Primary, 10, hidden), "Cutoff equality stays visible.");
        Check(MultipleStarCatalog.IsPlotVisible(unknown, system.Primary, 0, hidden), "Unknown magnitude stays visible.");
        hidden[dim.Id] = true;
        Check(MultipleStarCatalog.IsPlotVisible(dim, system.Primary, 10, hidden),
            $"Checking faint {role} must override cutoff and show its plot point.");
        Check(MultipleStarCatalog.IsDimComponent(dim, system.Primary, 10), "Override does not move faint star out of bottom section.");
        hidden[boundary.Id] = false;
        Check(!MultipleStarCatalog.IsPlotVisible(boundary, system.Primary, 10, hidden), "Unchecked star remains hidden.");
        Check(!MultipleStarCatalog.IsPlotVisible(boundary, system.Primary, 20, hidden), "Changing cutoff must not recheck star.");
        hidden.Clear();
        Check(MultipleStarCatalog.IsPlotVisible(boundary, system.Primary, 10, hidden), "Rechecked star restored.");
        Check(!MultipleStarCatalog.IsPlotVisible(dim, system.Primary, 10, hidden), "Selecting a system resets faint stars to unchecked.");
    }
    Check(!MultipleStarCatalog.IsDimComponent(MagnitudeStar(1, "U", "21"), system.Primary, 0),
        "System anchor remains visible even if its magnitude exceeds component cutoff.");
    Check(MultipleStarCatalog.IsPlotVisible(MagnitudeStar(50, "C", "-1"), system.Primary, 0, hidden),
        "Negative magnitude is brighter than minimum cutoff.");
    Check(!MultipleStarCatalog.IsPlotVisible(MagnitudeStar(50, "C", "20.01"), system.Primary, 20, hidden),
        "Upper cutoff boundary enforced.");
    var partition = new[] { MagnitudeStar(50, "Q", "11"), system.Primary, MagnitudeStar(51, "V", "9"), MagnitudeStar(52, "U", "") }
        .OrderBy(s => MultipleStarCatalog.IsDimComponent(s, system.Primary, 10)).Select(s => s.Id).ToArray();
    Check(partition.SequenceEqual(new long[] { 1, 51, 52, 50 }), "Stable dim partition preserves main and dim order.");
    foreach(string role in new[] { "C", "U", "V", "Q" }) {
        var dimSystem = new MultipleStarCatalog.System(system.Primary,
            [system.Primary, MagnitudeStar(50, role, "10.01"), MagnitudeStar(51, role, "15")], []);
        Check(!MultipleStarCatalog.HasEligibleCompanion(dimSystem, 10), $"All-faint {role} system must not get a ring.");
        Check(MultipleStarCatalog.HasEligibleCompanion(dimSystem, 10.01), "Companion at cutoff restores system ring.");
        Check(!MultipleStarCatalog.HasEligibleCompanion(dimSystem, 0), "Lowering cutoff removes system ring again.");
    }
    Check(!MultipleStarCatalog.HasEligibleCompanion(new(system.Primary, [system.Primary], []), 20),
        "Primary alone must not qualify as its own eligible companion.");
    Check(MultipleStarCatalog.HasEligibleCompanion(new(system.Primary,
        [system.Primary, MagnitudeStar(50, "Q", "")], []), 0), "Unknown companion magnitude retains system ring.");
    Check(MultipleStarCatalog.HasEligibleCompanion(new(system.Primary,
        [system.Primary, MagnitudeStar(50, "C", "-1"), MagnitudeStar(51, "V", "21")], []), 0),
        "One bright companion keeps ring even when another is too faint and geometry is unavailable.");
    var point1 = MultipleStarCatalog.PlotPosition(new(10, 5), 100, 420, 1, new(0, 0));
    var point4 = MultipleStarCatalog.PlotPosition(new(10, 5), 100, 420, 4, new(0, 0));
    Check(Math.Abs((210 - point4.East) - 4 * (210 - point1.East)) < 1e-9 &&
        Math.Abs((210 - point4.North) - 4 * (210 - point1.North)) < 1e-9,
        "Zoom scales separation fourfold within fixed plot dimensions.");
    Check(MultipleStarCatalog.PlotPosition(new(10, 5), 100, 420, 10, new(10, 5)) == new MultipleStarCatalog.Offset(210, 210),
        "Pan can center any offset at maximum zoom.");
    Check(MultipleStarCatalog.PlotPosition(new(10, 5), 100, 420, 0, new(30, -5)) == new MultipleStarCatalog.Offset(210, 210),
        "Zero zoom collapses offsets to center without invalid positions.");
    Check(MultipleStarCatalog.PlotPosition(new(0, 0), 0, 420, 1, new(0, 0)) == new MultipleStarCatalog.Offset(210, 210),
        "Primary-only extent remains finite and centered.");
    var companionFields = Enumerable.Repeat("", header.Length).ToArray();
    companionFields[Array.IndexOf(header, "component")] = "C";
    var companion = new MultipleStarCatalog.Star(companionFields,
        header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    Check(MultipleStarCatalog.RelativeProperMotion(companion, system.Primary) == (null, null),
        "True companion proper motion must not be displayed.");
    var layout = MultipleStarCatalog.Layout(system, approximateGroups: false);
    Check(layout["A"] == new MultipleStarCatalog.Offset(0, 0), "Primary not centered.");
    Check(Math.Abs(layout["B"].East - 20) < 1e-9 && Math.Abs(layout["B"].North) < 1e-9, "Theta orientation/latest measurement.");
    Check(Math.Abs(layout["C"].East - 20) < 1e-9 && Math.Abs(layout["C"].North - 5) < 1e-9, "Chained geometry.");
    Check(Math.Abs(layout["F"].East - 25) < 1e-9 && Math.Abs(layout["F"].North - 5) < 1e-9, "Reverse-edge geometry.");
    Check(!layout.ContainsKey("D") && !layout.ContainsKey("E"), "Unknown/group geometry invented.");
    var approximateLayout = MultipleStarCatalog.Layout(system);
    Check(approximateLayout.ContainsKey("D") && !approximateLayout.ContainsKey("E"),
        "U group-reference star should plot, but missing geometry must remain unplotted.");
    Check(Math.Abs(approximateLayout["D"].North + 50) < 1e-9, "Group representative orientation.");
    Check(approximateLayout.ContainsKey("B"), "V member excluded from plot.");
    var questionableFields = Enumerable.Repeat("", header.Length).ToArray();
    questionableFields[Array.IndexOf(header, "component")] = "Q";
    questionableFields[Array.IndexOf(header, "component_label")] = "Q";
    var questionable = new MultipleStarCatalog.Star(questionableFields,
        header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    var questionableSystem = new MultipleStarCatalog.System(system.Primary, [system.Primary, questionable],
        [new("A", "Q", 90, 7, 2020, 1)]);
    Check(Math.Abs(MultipleStarCatalog.Layout(questionableSystem)["Q"].East - 7) < 1e-9,
        "Q member with geometry must plot like V.");
    var nestedFields = (string label) => {
        string[] values = Enumerable.Repeat("", header.Length).ToArray();
        values[Array.IndexOf(header, "component_label")] = label;
        return new MultipleStarCatalog.Star(values,
            header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    };
    var nestedPrimary = nestedFields("Aa");
    var nestedSystem = new MultipleStarCatalog.System(nestedPrimary, [nestedPrimary, nestedFields("Ab"), nestedFields("B")],
        [new("Aa", "Ab", 0, 0.3, 2024, 1), new("A", "B", 54, 34.25, 2025, 2)]);
    var nestedLayout = MultipleStarCatalog.Layout(nestedSystem);
    Check(nestedLayout.Count == 3 && Math.Abs(nestedLayout["B"].East - 34.25 * Math.Sin(54 * Math.PI / 180)) < 1e-9,
        "Albireo-like A group must use Aa anchor without losing B.");
    var disconnectedSystem = new MultipleStarCatalog.System(nestedFields("A"),
        [nestedFields("A"), nestedFields("B"), nestedFields("Ca"), nestedFields("Cb")],
        [new("A", "B", 39.6, 0.083, 2022, 1), new("Ca", "Cb", 150.1, 0.69, 2019, 2)]);
    var disconnectedLayout = MultipleStarCatalog.Layout(disconnectedSystem);
    Check(MultipleStarCatalog.MissingPositionText(disconnectedSystem.Members, disconnectedLayout) == "Not Shown - Ca, Cb",
        "Disconnected subsystem reports both components in the exact in-plot notice.");
    Check(MultipleStarCatalog.MissingPositionText(disconnectedSystem.Members.Take(2), disconnectedLayout) == "" &&
        MultipleStarCatalog.MissingPositionText(nestedSystem.Members, nestedLayout) == "",
        "Complete geometry and intentionally filtered-out components do not produce a notice.");
    MultipleStarCatalog.Star PositionlessStar(long id, string label, string source = "WDS", string ra = "", string dec = "") {
        var values = Enumerable.Repeat("", header.Length).ToArray();
        foreach(var (name, value) in new[] { ("oms_id", id.ToString(CultureInfo.InvariantCulture)),
            ("component_label", label), ("radec_source", source), ("ra", ra), ("dec", dec) })
            values[Array.IndexOf(header, name)] = value;
        return new(values, header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    }
    var noDataSystem = new MultipleStarCatalog.System(system.Primary,
        [system.Primary, PositionlessStar(20, "Ca"), PositionlessStar(21, "Cb"), PositionlessStar(22, "D")],
        [new("Ca", "Cb", 150, 0.69, 2019, 1)]);
    Check(!MultipleStarCatalog.HasNoPositionData(noDataSystem.Primary, noDataSystem) &&
        !MultipleStarCatalog.HasNoPositionData(noDataSystem.Members[1], noDataSystem) &&
        !MultipleStarCatalog.HasNoPositionData(noDataSystem.Members[2], noDataSystem) &&
        MultipleStarCatalog.HasNoPositionData(noDataSystem.Members[3], noDataSystem),
        "Only WDS-only companions with neither coordinates nor any incoming/outgoing rho link are summarized.");
    Check(!MultipleStarCatalog.HasNoPositionData(PositionlessStar(23, "E", ra: "1", dec: "2"), noDataSystem) &&
        !MultipleStarCatalog.HasNoPositionData(PositionlessStar(24, "F", "GIA"), noDataSystem) &&
        MultipleStarCatalog.HasNoPositionData(PositionlessStar(25, "G", ra: "1"), noDataSystem),
        "Complete coordinates and non-WDS entries remain listed; partial coordinates do not locate a star.");
    Check(!MultipleStarCatalog.HasNoPositionData(noDataSystem.Members[1],
        noDataSystem with { Measures = [new("A", "C", null, 2, 2020, 1)] }) &&
        MultipleStarCatalog.HasNoPositionData(noDataSystem.Members[3],
        noDataSystem with { Measures = [new("A", "D", 90, 0, 2020, 1)] }),
        "Group rho links retain their leaves; zero separation is not a usable rho link.");
    MultipleStarCatalog.Star CoordinateStar(string label, string ra, string dec) {
        var values = Enumerable.Repeat("", header.Length).ToArray();
        values[Array.IndexOf(header, "component_label")] = label;
        values[Array.IndexOf(header, "ra")] = ra;
        values[Array.IndexOf(header, "dec")] = dec;
        return new(values, header.Select((name, index) => (name, index)).ToDictionary(p => p.name, p => p.index));
    }
    var coordinatePrimary = CoordinateStar("A", "23.999", "60");
    var coordinateSystem = new MultipleStarCatalog.System(coordinatePrimary,
        [coordinatePrimary, CoordinateStar("B", "0.001", "60"), CoordinateStar("Ca", "0.001", "60"),
            CoordinateStar("Cb", "12", "-60"), CoordinateStar("D", "0.001", "")],
        [new("A", "B", 0, 2, 2022, 1), new("Ca", "Cb", 90, 0.69, 2019, 2)]);
    HashSet<string> approximateLabels = new();
    var coordinateLayout = MultipleStarCatalog.Layout(coordinateSystem, approximateCoordinates: true,
        approximateLabels: approximateLabels);
    Check(coordinateLayout["B"] == new MultipleStarCatalog.Offset(0, 2),
        "Measured position takes precedence over conflicting RA/Dec.");
    Check(Math.Abs(coordinateLayout["Ca"].East - 54) < 0.00001 &&
        Math.Abs(coordinateLayout["Ca"].North) < 0.02,
        "RA hours wrap through midnight and scale by cosine of declination.");
    Check(Math.Abs(coordinateLayout["Cb"].East - coordinateLayout["Ca"].East - 0.69) < 1e-9 &&
        Math.Abs(coordinateLayout["Cb"].North - coordinateLayout["Ca"].North) < 1e-9 &&
        approximateLabels.SetEquals(["Ca", "Cb"]),
        "Ca is anchored from coordinates, then Cb uses measured rho/theta, not its conflicting coordinates.");
    Check(MultipleStarCatalog.MissingPositionText(coordinateSystem.Members, coordinateLayout) == "Not Shown - D" &&
        MultipleStarCatalog.Layout(coordinateSystem).Keys.Order().SequenceEqual(new[] { "A", "B" }),
        "Incomplete coordinates stay unplotted and catalog classification geometry remains measurement-only.");
    var reverseCoordinateSystem = coordinateSystem with {
        Members = [coordinatePrimary, CoordinateStar("Ca", "", ""), CoordinateStar("Cb", "23.999", "60.001")],
        Measures = [new("Ca", "Cb", 0, 0.69, 2019, 2)]
    };
    var reverseCoordinateLayout = MultipleStarCatalog.Layout(reverseCoordinateSystem, approximateCoordinates: true);
    Check(Math.Abs(reverseCoordinateLayout["Cb"].North - 3.6) < 1e-8 &&
        Math.Abs(reverseCoordinateLayout["Ca"].North - 2.91) < 1e-8,
        "Coordinate fallback also supports declination offsets and reverse measured chains.");
    var missingPrimaryCoordinates = coordinateSystem with { Primary = CoordinateStar("A", "", "") };
    Check(MultipleStarCatalog.Layout(missingPrimaryCoordinates, approximateCoordinates: true).Count == 2,
        "Without primary coordinates only measured geometry is used.");
    Check(MultipleStarCatalog.IsWithinMagnitudeCutoff(8.9, 8.9), "Magnitude cutoff must include its boundary.");
    Check(MultipleStarCatalog.IsWithinMagnitudeCutoff(-1.46, 8.9), "Bright primary excluded.");
    Check(!MultipleStarCatalog.IsWithinMagnitudeCutoff(8.9001, 8.9), "Faint primary included.");
    Check(!MultipleStarCatalog.IsWithinMagnitudeCutoff(null, 8.9), "Unknown magnitude included.");
    Check(!MultipleStarCatalog.IsWithinMagnitudeCutoff(double.NaN, 8.9), "Invalid magnitude included.");
    Check(MultipleStarCatalog.IsWithinMagnitudeCutoff(9.5, 10) &&
        !MultipleStarCatalog.IsWithinMagnitudeCutoff(9.5, 8.9), "Adjusting cutoff did not change eligibility.");
    MultipleStarCatalog.Target[] targets = [new(2, 2, 0), new(1, -2, 0), new(3, 4, 0)];
    Check(MultipleStarCatalog.PickNearest(targets, 0, 0, 8) == 1, "Overlap tie not stable.");
    Check(MultipleStarCatalog.PickNearest(targets, 3, 0, 8) == 2, "Nearest screen-center pick.");
    Check(MultipleStarCatalog.PickNearest(targets, 4, 0, 8) == 3, "Closest projected center not selected.");
    Check(MultipleStarCatalog.PickNearest(targets, 20, 0, 8) is null, "Outside-ring click selected a star.");
    Check(MultipleStarCatalog.PickNearest(targets, 12, 0, 8) == 3, "Ring boundary excluded.");
    File.WriteAllLines(temp, new[] { string.Join(",", header) }.Concat(rows.Take(1)).Append(Row(2, "B", "A>B|A>C", "90", "10|20", "2000|2000", "1|2")));
    try {
        MultipleStarCatalog.Load(temp);
        throw new InvalidOperationException("Misaligned relationships were accepted.");
    } catch(InvalidDataException) { }
    File.WriteAllLines(temp, new[] { string.Join(",", header), rows[0], rows[0] });
    try {
        MultipleStarCatalog.Load(temp);
        throw new InvalidOperationException("Duplicate OMS identity was accepted.");
    } catch(InvalidDataException) { }
    Console.WriteLine("Multi fixtures passed: quoted CSV, latest measurements, north/east geometry, chaining, reverse edges, unknown/group geometry, overlap picks, boundaries and invalid lists.");
    if(args.Length > 0) {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var real = MultipleStarCatalog.Load(args[0],
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "definitively-correct-systems.csv"));
        Check(real.DefinitivelyCorrectPrimaries.IsSupersetOf(
            [104412L, 84638L, 316359L, 54185L, 182054L, 27142L, 228978L, 57654L, 181816L, 160137L]),
            "The original ten user-reviewed primaries remain marked (alongside bulk-imported approvals), with corrected HD 98800 identity.");
        foreach(var (primaryId, physical) in new (long, string[])[] {
            (104412, ["B"]), (84638, ["B"]), (316359, ["C"]),
            (54185, []), (182054, []), (57654, ["Ab", "H", "L"])
        }) {
            var reviewed = real.Systems.Single(s => s.Primary.Id == primaryId);
            Check(reviewed.Primary.Role == "P" &&
                reviewed.Members.Where(s => s.Id != primaryId)
                    .All(s => s.Role == (physical.Contains(s.Label) ? "C" : "V")) &&
                physical.All(label => reviewed.Members.Any(s => s.Label == label)),
                $"Reviewed OMS {primaryId} keeps P and exactly the requested physical companions; others are Visual.");
        }
        foreach(var (primaryId, visualLabels) in new (long, string[])[] {
            (27142, ["C", "D"]), (228978, ["D"]),
            (181816, ["D", "E", "F", "G", "H"]), (160137, ["C"])
        }) {
            var reviewed = real.Systems.Single(s => s.Primary.Id == primaryId);
            Check(visualLabels.All(label => reviewed.Members.Single(s => s.Label == label).Role == "V"),
                $"Reviewed OMS {primaryId} has the requested Visual components.");
            Check(reviewed.Members.Where(s => s.Id != primaryId && !visualLabels.Contains(s.Label)).All(s => s.Role == "C"),
                $"Other physical companions of OMS {primaryId} are preserved.");
        }
        var capella = real.Systems.Single(s => s.Primary.Id == 57654);
        Check(!real.Stars.ContainsKey(373780) && !real.Stars.ContainsKey(373781) &&
            !capella.Members.Any(s => s.Label is "O" or "P") &&
            !capella.Measures.Any(m => m.Reference is "O" or "P" || m.Companion is "O" or "P"),
            "Suppressed Capella O/P have no catalog rows, plot markers or dangling measurements.");
        var registry = File.ReadLines(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "identity-registry.csv"));
        Check(registry.Contains("WDS:05167+4600:O,373780") && registry.Contains("WDS:05167+4600:P,373781"),
            "Suppressed Capella O/P identities stay reserved, not recycled.");
        Check(real.Systems.Single(s => s.Primary.Id == 160139).Members.Single(s => s.Label == "B").Role == "C",
            "Unrelated OMS 160139 is unchanged.");
        Console.WriteLine("Definitive review checks passed: ten primary identities, exact component roles, Capella suppression and reserved IDs.");
        if(args.Contains("--reviewed-systems-only"))
            return;
        if(args.Contains("--benchmark-search")) {
            foreach(string query in new[] { "sirius", "burnham 1", "123", "not-a-recorded-identifier" }) {
                var searchTimer = System.Diagnostics.Stopwatch.StartNew();
                var matches = CatalogIdentifiers.Search(real, "", query).Take(201).ToArray();
                Console.WriteLine($"All search '{query}': {searchTimer.Elapsed.TotalMilliseconds:F0} ms, {matches.Length} results.");
            }
        }
        string orbitPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "orb6orbits.txt");
        using(var source = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(orbitPath)!, "orb6-source.json")))) {
            foreach(var input in source.RootElement.GetProperty("sources").EnumerateArray()) {
                string inputPath = Path.Combine(Path.GetDirectoryName(orbitPath)!, input.GetProperty("file").GetString()!);
                using var stream = File.OpenRead(inputPath);
                Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)) ==
                    input.GetProperty("sha256").GetString(), "ORB6 snapshot hash must match its source manifest.");
            }
        }
        var realOrbits = MultipleStarOrbits.Load(orbitPath);
        var alphaCen = real.Systems.Single(s => s.Primary["wds_id"] == "14396-6050");
        var alphaLayout = MultipleStarCatalog.Layout(alphaCen);
        Check(alphaCen.Primary.Id == 195683 && alphaCen.Primary.Label == "A" &&
            alphaCen.Primary["proper"] == "Rigil Kentaurus" && alphaCen.Members.Count == 3 &&
            alphaCen.Members.Single(s => s.Label == "B").Id == 195677 &&
            alphaCen.Members.Single(s => s.Label == "C").Id == 374371 &&
            alphaCen.Members.Where(s => s.Label != "A").All(s => s.Role == "C") &&
            alphaCen.Measures.Any(m => m.Reference == "A" && m.Companion == "B" && m.Rho == 8.086) &&
            alphaCen.Measures.Any(m => m.Reference == "A" && m.Companion == "C" && m.Rho == 7960.200) &&
            alphaLayout.Count == 3 &&
            Math.Abs(Math.Sqrt(Math.Pow(alphaLayout["C"].East, 2) + Math.Pow(alphaLayout["C"].North, 2)) - 7960.200) < 1e-8 &&
            realOrbits.Curves(alphaCen, alphaLayout).Any(c => c.Companion == "B"),
            "Alpha Centauri uses Rigil Kentaurus A, Toliman B, single Proxima C, full wide separation and the A-B orbit.");
        var iotaCas = real.Systems.Single(s => s.Primary["wds_id"] == "02291+6724");
        Check(iotaCas.Primary.Id == 26669 && iotaCas.Primary.Label == "Aa" &&
            iotaCas.Members.Single(s => s.Label == "Ab").Id == 369442 &&
            iotaCas.Members.Single(s => s.Label == "B").Id == 369443 &&
            iotaCas.Members.Where(s => s.Label is "Ab" or "B" or "Ca" or "Cb").All(s => s.Role == "C") &&
            iotaCas.Members.Single(s => s.Label == "D").Id == 379241 &&
            iotaCas.Members.Single(s => s.Label == "D").Role == "V" &&
            iotaCas.Measures.Any(m => m.Reference == "A" && m.Companion == "B" && m.Rho == 2.980) &&
            iotaCas.Measures.Any(m => m.Reference == "Aa" && m.Companion == "Ab" && m.Rho == 0.601) &&
            MultipleStarCatalog.Layout(iotaCas).Count == iotaCas.Members.Count,
            "Iota Cas restores reserved B separately from close Ab, preserves its quintuple hierarchy and plots every component.");
        Check(realOrbits.Orbits.Count > 1000, "Real ORB6 snapshot must contain supported visual orbits.");
        Check(real.Systems.Count > 0, "Real catalog has no systems.");
        var oms24185 = real.Systems.Single(s => s.Primary.Id == 24185);
        var oms24185Layout = MultipleStarCatalog.Layout(oms24185);
        Check(MultipleStarCatalog.MissingPositionText(oms24185.Members, oms24185Layout) == "Not Shown - Ca, Cb",
            "OMS 24185 has an unanchored Ca-Cb pair, not invented absolute geometry.");
        Check(MultipleStarCatalog.MissingPositionText(oms24185.Members,
            MultipleStarCatalog.Layout(oms24185, approximateCoordinates: true)) == "Not Shown - Ca, Cb",
            "OMS 24185 has no recorded Ca/Cb RA/Dec to supply the missing anchor.");
        var oms24185Curve = realOrbits.Curves(oms24185, oms24185Layout).Single(c => c.Companion == "B");
        var oms24185B = oms24185Layout["B"];
        double residual = oms24185Curve.Points.Min(p =>
            Math.Sqrt(Math.Pow(p.East - oms24185B.East, 2) + Math.Pow(p.North - oms24185B.North, 2)));
        Check(Math.Abs(oms24185B.East - 0.083 * Math.Sin(39.6 * Math.PI / 180)) < 1e-9 &&
            Math.Abs(oms24185B.North - 0.083 * Math.Cos(39.6 * Math.PI / 180)) < 1e-9 &&
            residual is > 0.035 and < 0.037,
            "OMS 24185 preserves the 2022 WDS observation despite its disagreement with the supplied orbit.");
        var epsilonLyrae = real.Systems.Single(s => s.Primary["wds_id"] == "18443+3940");
        Check(epsilonLyrae.Primary.Id == 253698 && epsilonLyrae.Primary["bayer"] == "Eps-1" &&
            epsilonLyrae.Primary["flam"] == "4" &&
            StarDesignations.Bayer(epsilonLyrae.Primary["bayer"], epsilonLyrae.Primary["con"]) == "Epsilon-1 Lyrae",
            "Epsilon Lyrae primary must expose its normalized numbered Bayer and Flamsteed aliases.");
        Check(epsilonLyrae.Members.Any(s => s.Id == 253697 && s.Label == "B"),
            "Epsilon-1 AT-HYG row just beyond old radius must now belong to system B.");
        Check(epsilonLyrae.Members.Single(s => s.Label == "Ca").Id == 253708 &&
            epsilonLyrae.Members.Single(s => s.Label == "D").Id == 253709 &&
            !epsilonLyrae.Members.Single(s => s.Label == "D").IsWdsPosition &&
            epsilonLyrae.Members.Single(s => s.Label == "Cb").IsWdsPosition &&
            epsilonLyrae.Members.Single(s => s.Label == "Cb").Number("mag") is null &&
            !real.Stars.ContainsKey(372209) &&
            epsilonLyrae.Members.Where(s => s.Label is "B" or "Ca" or "Cb" or "D").All(s => s.Role == "C"),
            "Epsilon Lyrae retains the true Ca/Cb subsystem, assigns the real D identity once and classifies the main physical companions.");
        Check(epsilonLyrae.Measures.Any(m => m.Reference == "C" && m.Companion == "D" && m.Rho is > 2.4 and < 2.5) &&
            epsilonLyrae.Measures.Any(m => m.Reference == "Ca" && m.Companion == "Cb" && m.Rho is > 0.05 and < 0.06) &&
            realOrbits.Curves(epsilonLyrae, MultipleStarCatalog.Layout(epsilonLyrae)).Any(c => c.Companion == "Cb"),
            "Wide C-D and tight Ca-Cb measurements stay distinct with the exact inner orbit preserved.");
        var burnhamOne = real.Systems.Single(s => s.Primary["hd"] == "5005");
        foreach(string column in new[] { "bu", "" }) {
            var matches = CatalogIdentifiers.Search(real, column, "burnham 1").Take(201).ToArray();
            Check(matches.Length > 1 && matches[0].Label == "Burnham 1" &&
                matches.Any(s => s.Id == burnhamOne.Primary.Id),
                "Real Burnham 1 entries precede partial matches within the displayed result limit.");
        }
        var bharani = real.Systems.Single(s => s.Primary["proper"] == "Bharani");
        foreach(string name in new[] { "Aldebaran", "Elnath" }) {
            var filtered = real.Systems.Single(s => s.Primary["proper"] == name);
            Check(!MultipleStarCatalog.HasEligibleCompanion(filtered, 20, true) &&
                MultipleStarCatalog.HasEligibleCompanion(filtered, 20, false),
                name + " has only WDS-only companions and must have no Multi information under default provenance filtering.");
        }
        Check(bharani.Primary.Id == 30379 && bharani.Primary.Number("mag") is > 3 and < 4 &&
            bharani.Members.Any(s => s.Id == 373737 && s.Label == "Ab" && s.Number("mag") is null) &&
            bharani.Measures.Any(m => m.Reference == "Aa" && m.Companion == "Ab" && m.Rho is > 0 and < 0.2),
            "Bharani is distinct from its subarcsecond unknown-magnitude WDS companion.");
        Check(burnhamOne.Primary["bu"] == "1" && burnhamOne.Primary["bds"] == "455" &&
            new[] { "A", "B", "C", "D" }.All(label => burnhamOne.Members.Any(s => s.Label == label)),
            "HD 5005 must retain main quadruple with separate BU 1 and BDS 455 identifiers.");
        long members = 0, measures = 0, plotted = 0, coordinateFallbacks = 0;
        long orbitCurves = 0;
        foreach(var s in real.Systems) {
            Check(s.Members.All(m => long.Parse(m["primary_oms_id"], CultureInfo.InvariantCulture) == s.Primary.Id), "Real catalog primary reference.");
            var positions = MultipleStarCatalog.Layout(s);
            Check(positions.Values.All(p => double.IsFinite(p.East) && double.IsFinite(p.North)), "Non-finite plotted point.");
            var approximatePositions = MultipleStarCatalog.Layout(s, approximateCoordinates: true);
            Check(approximatePositions.Values.All(p => double.IsFinite(p.East) && double.IsFinite(p.North)) &&
                positions.All(p => approximatePositions[p.Key] == p.Value),
                "Real coordinate fallbacks are finite and never move existing measured positions.");
            coordinateFallbacks += approximatePositions.Count - positions.Count;
            var curves = realOrbits.Curves(s, positions);
            Check(curves.All(c => s.Members.Single(m => m.Label == c.Companion).Role is "C" or "P" or "U"),
                "Every plot-matched ORB6 companion must be physical C unless primary or identification-uncertain.");
            Check(curves.All(c => c.Points.Length == 129 &&
                c.Points.All(p => double.IsFinite(p.East) && double.IsFinite(p.North))), "Real orbit points must be finite and closed-size.");
            orbitCurves += curves.Length;
            members += s.Members.Count; measures += s.Measures.Count; plotted += positions.Count;
        }
        Console.WriteLine($"Coordinate fallback verified across {real.Systems.Count:N0} systems: {coordinateFallbacks:N0} additional plot-ready members; existing measured positions unchanged.");
        var albireo = real.Systems.Single(s => s.Primary["proper"] == "Albireo");
        Check(orbitCurves > 100, "Real catalog must match more than 100 supported orbit curves.");
        var sirius = real.Systems.Single(s => s.Primary["proper"] == "Sirius");
        Check(sirius.Members.Single(s => s.Label == "B").Role == "C" &&
            sirius.Members.Single(s => s.Label == "B").Id == 84637 &&
            sirius.Members.Single(s => s.Label == "B")["proper"] == "Sirius B" &&
            !sirius.Members.Single(s => s.Label == "B").IsWdsPosition &&
            sirius.Members.Single(s => s.Label == "B").IsSystemCompanion &&
            MultipleStarCatalog.HasEligibleCompanion(sirius, 20, true) &&
            !real.Stars.ContainsKey(376056) && !sirius.Members.Any(s => s.Label == "Ba") &&
            sirius.Members.Single(s => s.Label == "Bb").Role == "V" &&
            sirius.Measures.Any(m => m.Reference == "A" && m.Companion == "B" && m.Rho is > 11 and < 11.2),
            "Real Sirius B replaces the duplicate WDS Ba as physical B; Bb remains an optical neighbor.");
        Check(realOrbits.Orbits.Any(o => o.Wds == sirius.Primary["wds_id"] && o.Companion == "B") &&
            realOrbits.Curves(sirius, MultipleStarCatalog.Layout(sirius)).Any(c => c.Companion == "B"),
            "Sirius AB orbit now matches the real individual B rather than a blended Ba/Bb endpoint.");
        Console.WriteLine($"ORB6 verified: {realOrbits.Orbits.Count:N0} supported unique solutions, {orbitCurves:N0} exact individual-pair curves matched.");
        Check(StarDesignations.Heading(albireo.Primary["proper"], albireo.Primary["flam"], albireo.Primary["con"]) == "Albireo / 6 Cygni",
            "Real primary must use its own Flamsteed and constellation in heading.");
        Check(StarDesignations.Bayer(albireo.Primary["bayer"], albireo.Primary["con"]) == "Beta-1 Cygni",
            "Real primary must use expanded Bayer and genitive on second line.");
        var albireoB = albireo.Members.Single(s => s["proper"] == "Albireo B");
        Check(albireoB.Id == 267899 && albireoB.Role == "V" && albireoB.Label == "B",
            "Albireo B must retain its identity and join Albireo as Visual B.");
        Check(MultipleStarCatalog.Layout(albireo).Count == albireo.Members.Count,
            "All Albireo members with geometry, including Visual B, must plot.");
        Console.WriteLine($"Albireo verified: OMS {albireoB.Id}, role {albireoB.Role}, label {albireoB.Label}; all {albireo.Members.Count} members plotted.");
        Console.WriteLine($"Real catalog: {real.Systems.Count:N0} groups, {members:N0} members, {measures:N0} measures, {plotted:N0} plot-ready members; {timer.Elapsed.TotalSeconds:0.00}s.");
    }
} finally {
    File.Delete(temp);
    File.Delete(reviewTemp);
    File.Delete(approvalTemp);
    if(Directory.Exists(blockedApprovalPath))
        Directory.Delete(blockedApprovalPath);
}
