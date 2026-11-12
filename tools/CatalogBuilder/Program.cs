using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CatalogBuilder;

internal static class Program
{
    internal static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--self-test" }))
            {
                SelfTest();
                return 0;
            }
            if (args.Length == 3 && args[0] == "--validate")
            {
                Validation.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            }
            if (args.Length != 2)
                throw new ArgumentException("Usage: CatalogBuilder <source root> <output directory>, --validate <source root> <output directory>, or --self-test");
            new Builder(Path.GetFullPath(args[0]), Path.GetFullPath(args[1])).Run();
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    internal static double? Number(string value) =>
        double.TryParse(value, NumberStyles.Float, Invariant, out double n) && double.IsFinite(n) ? n : null;
    internal static string F(double value) => value.ToString("G17", Invariant);
    internal static string Slice(string line, int start, int length) => line.Substring(start, length).Trim();
    internal static string Compact(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static void SelfTest()
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("Self-test failed: " + message);
        }
        string[] cells = ["plain", "a,b", "quoted \"value\"", "", "{\"labels\":[\"A\",\"B\"]}"];
        Check(Csv.Parse(Csv.Line(cells)).SequenceEqual(cells), "CSV round trip");
        Check(Csv.Parse("a,\"b,b\",").SequenceEqual(new[] { "a", "b,b", "" }), "empty trailing cell");
        bool rejected = false;
        try { Csv.Parse("\"unfinished"); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "reject malformed CSV");
        Check(Number("NaN") is null && Number("Infinity") is null, "finite numbers");
        Check(Builder.InBase("10") && !Builder.InBase("10.0001") && !Builder.InBase(""), "inclusive magnitude boundary");
        foreach (char flag in "X")
            Check(Pair.Excluded(" " + flag), "excluded flag " + flag);
        foreach (char flag in "SUY")
            Check(!Pair.Excluded(" " + flag) && Pair.Visual(" " + flag), "visual flag " + flag);
        Check(!Pair.Excluded("TW"), "W is not excluded");
        string sample = "00000+7530A  1248      1904 1982    5 235.30    0.590     10.27 11.5  A7IV      +034+005          +74 1056      000006.64+752859.8";
        Pair p = Pair.Parse(sample, 1);
        Check(p.Theta == 235.3 && p.Rho == 0.590 && p.Mag1 == 10.27 && p.Mag2 == 11.5, "precise-last offsets");
        Check(p.First == "A" && p.Second == "B", "implicit AB");
        Check(p.Spectral == "A7IV" && p.SpectralFor("A") == "A7IV" && p.SpectralFor("B") is null,
            "precise WDS spectral field belongs to first component when unsplit");
        Check((p with { Spectral = "G8/K1III" }).SpectralFor("A") == "G8/K1III" &&
            (p with { Spectral = "+M3V" }).SpectralFor("A") is null &&
            (p with { Spectral = "+M3V" }).SpectralFor("B") == "M3V" &&
            (p with { Spectral = "G0V+..." }).SpectralFor("B") is null,
            "spectral qualifiers, missing first type and unknown second type");
        Check((p with { Spectral = "(DA+M)+M:" }).SpectralFor("A") is null &&
            (p with { Spectral = "F+G+K" }).SpectralFor("B") is null,
            "complex composite spectra are not guessed");
        Check(Pair.Endpoints("BC") == ("B", "C"), "BC endpoints");
        Check(Pair.Endpoints("AB,C") == ("AB", "C"), "blended endpoint retained");
        Check(Pair.Endpoints("Aa,Ab") == ("Aa", "Ab"), "case-sensitive endpoints");
        Check(Pair.Endpoints("AB-CD") == ("AB", "CD"), "AB-CD endpoints");
        Check(Pair.Endpoints("Aa1,2") == ("Aa1", "Aa2"), "abbreviated numeric subcomponents");
        Pair[] hierarchyPairs = [p with { First = "Aa1", Second = "Aa2" }, p with { First = "Aa", Second = "Ab" },
            p with { First = "Ba", Second = "Bb" }, p with { First = "AB", Second = "CD" }];
        Hierarchy hierarchy = new(hierarchyPairs);
        Check(hierarchy.Leaves("AB").SequenceEqual(new[] { "Aa1", "Aa2", "Ab", "Ba", "Bb" }), "nested leaves without blended stars");
        Check(hierarchy.Path("Aa1") == "A/Aa/Aa1", "hierarchy path");
        Check(Builder.MotionDifference(6, 8, 0, 0) == 10 &&
            Builder.MotionDifference(6, 8.01, 0, 0) is > 10 && Builder.MotionDifference(null, 8, 0, 0) is null, "Visual vector threshold");
        Check(!Builder.IsExtremeMotionDifference(100, 0, 0, 0) &&
            Builder.IsExtremeMotionDifference(100.01, 0, 0, 0), "strict absolute motion boundary");
        Check(!Builder.IsExtremeMotionDifference(400, 0, 200, 0) &&
            Builder.IsExtremeMotionDifference(400.01, 0, 200, 0), "strict relative motion boundary");
        Check(!Builder.IsExtremeMotionDifference(16.64, -47.75, 42.32, -49.3) &&
            !Builder.IsExtremeMotionDifference(null, 0, 0, 0) &&
            !Builder.IsExtremeMotionDifference(110, 0, 200, 0),
            "Almach, incomplete motion and absolute-only discrepancies do not trigger Q");
        Check(Builder.WdsDm("+00  123") == "BD+0:123" && Builder.WdsDm("-23  123") == "CD-23:123" &&
            Builder.WdsDm("-52  123") == "CP-52:123", "DM catalog zones and signed zero");
        Check(Builder.NormalizeDm("BD+00  123", component: 'B') == "", "avoid unresolved DM component");
        Check(Spatial.Distance(23.99999, 0, 0.00001, 0) < 2, "RA wrap");
        Check(Spatial.Distance(0, 90, 12, 90) < 0.001, "pole");
        var north = Spatial.Offset(0, 0, 0, 10);
        var east = Spatial.Offset(0, 0, 90, 10);
        Check(north.Dec > 0 && Math.Abs(north.Ra) < 1e-9 && east.Ra > 0 && Math.Abs(east.Dec) < 1e-9, "theta orientation");
        Check(Errata.Deleted("06122+5014", "TDS227") && !Errata.Deleted("06122+5014", "OTHER1"), "scoped deletion");
        Check(Errata.Rename("22060+3724", "LDS4940") == "LDS4943", "scoped rename");
        Check(Errata.Rename("22060+3724", "LDS4943") == "LDS4943", "idempotent rename");
        Check(Errata.Deleted("05167+4600", "HEL1") &&
            !Errata.Deleted("05167+4600", "ST3") && !Errata.Deleted("05167+4601", "HEL1"),
            "Capella O/P suppression is scoped, preserving H-L and unrelated HEL1 pairs");
        Check(Pair.NormalizeMotion(12, "P") == 120 && Pair.NormalizeMotion(-3, "P") == -30 &&
            Pair.NormalizeMotion(null, "P") is null && Pair.NormalizeMotion(12, "") == 12, "century motion to mas/year");
        Check(Builder.CompanionPosition(p with { Theta = null }) == (null, null), "missing theta remains unknown");
        Check(Builder.CompanionPosition(p with { Rho = null }) == (null, null), "missing rho remains unknown");
        Check(Builder.CompanionPosition(p with { LastYear = 2020, PmRa2 = null }) == (null, null), "unknown epoch drift");
        Check(Builder.CompanionPosition(p with { Rho = 0.001, LastYear = 2000 }).Ra.HasValue, "tiny separation retained");
        Check(Builder.CompanionPosition(p with { Rho = 1200, LastYear = 2000 }).Ra.HasValue, "wide separation retained");
        CatalogTests.Run();
        Console.WriteLine("CatalogBuilder self-tests passed.");
    }

    internal static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
}

internal static class Csv
{
    public static string Line(IEnumerable<string> values) => string.Join(",", values.Select(v =>
        v.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v));

    public static string[] Parse(string line)
    {
        List<string> result = [];
        StringBuilder cell = new();
        bool quoted = false, closed = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else { quoted = false; closed = true; }
            }
            else if (c == ',') { result.Add(cell.ToString()); cell.Clear(); closed = false; }
            else if (c == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (c == '"' || closed) throw new InvalidDataException("Malformed CSV field.");
            else cell.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unterminated CSV string.");
        result.Add(cell.ToString());
        return result.ToArray();
    }
}

internal sealed record Pair(string Wds, string Discoverer, string Components, string First, string Second,
    int? FirstYear, int? LastYear, double? Theta, double? Rho, double? Mag1, double? Mag2,
    string Flags, double? Ra, double? Dec, double? PmRa1, double? PmDec1, double? PmRa2, double? PmDec2, int Line)
{
    public string Spectral { get; init; } = "";

    internal static bool MissingSpectral(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() is "?" or "." or "..." ||
        value.Equals("unknown", StringComparison.OrdinalIgnoreCase) || value == "U";

    public string? SpectralFor(string label)
    {
        // '+' separates component spectra; '/' can instead qualify a single spectral type.
        string[] types = Spectral.Split('+');
        if (types.Length > 2 || Spectral.IndexOfAny(['(', ')']) >= 0) return null;
        string? value = label == First ? types[0] : label == Second && types.Length == 2 ? types[1] : null;
        return value is null || MissingSpectral(value) ? null : value.Trim();
    }

    public static bool Excluded(string flags) => flags.Contains('X');
    public static bool Visual(string flags) => flags.IndexOfAny(['S', 'U', 'Y']) >= 0;
    internal static double? NormalizeMotion(double? value, string flags) => value * (flags.Contains('P') ? 10 : 1);

    public static (string First, string Second) Endpoints(string raw)
    {
        if (raw.Length == 0) return ("A", "B");
        string[] sides = raw.Split([',', '-']);
        if (sides.Length == 2 && sides.All(s => s.Length > 0))
        {
            if (Regex.IsMatch(sides[1], @"^\d+$") && Regex.IsMatch(sides[0], @"^[A-Z][a-z]\d+$"))
                sides[1] = Regex.Replace(sides[0], @"\d+$", "") + sides[1];
            else if (Regex.IsMatch(sides[1], "^[a-z]$") && Regex.IsMatch(sides[0], "^[A-Z][a-z]$"))
                sides[1] = sides[0][..1] + sides[1];
            return (sides[0], sides[1]);
        }
        if (Regex.IsMatch(raw, "^[A-Z]{2}$")) return (raw[..1], raw[1..]);
        return (raw, "");
    }

    public static Pair Parse(string text, int line)
    {
        if (text.Length != 130) throw new InvalidDataException($"Precise WDS line {line} has width {text.Length}, expected 130.");
        string wds = Program.Slice(text, 0, 10);
        if (!Regex.IsMatch(wds, @"^\d{5}[+-]\d{4}$")) throw new InvalidDataException($"Invalid WDS ID at line {line}.");
        string component = Program.Slice(text, 17, 5);
        var (first, second) = Endpoints(component);
        if (wds == "06451-1643")
        {
            if (first == "Ba") first = "B";
            if (second == "Ba") second = "B";
        }
        string coords = text.Substring(112, 18);
        double? ra = null, dec = null;
        if (!coords.All(c => c is ' ' or '.'))
        {
            ra = Required(coords[..2]) + Required(coords.Substring(2, 2)) / 60 + Required(coords.Substring(4, 5)) / 3600;
            dec = Required(coords.Substring(10, 2)) + Required(coords.Substring(12, 2)) / 60 + Required(coords.Substring(14, 4)) / 3600;
            if (coords[9] == '-') dec = -dec;
            else if (coords[9] != '+') throw new InvalidDataException($"Invalid declination sign at line {line}.");
        }
        if (ra < 0 || ra >= 24 || dec is < -90 or > 90) throw new InvalidDataException($"Invalid WDS position at line {line}.");
        string flags = Program.Slice(text, 107, 4);
        // Alpha Centauri's wide AC measure needs the leading digit; broader parser changes await review.
        double? theta = Program.Number(Program.Slice(text, 38, 6)),
            rho = Program.Number(wds == "14396-6050" ? Program.Slice(text, 45, 8) : Program.Slice(text, 46, 7));
        return new Pair(wds, Program.Compact(Program.Slice(text, 10, 7)), component, first, second,
            (int?)Program.Number(Program.Slice(text, 23, 4)), (int?)Program.Number(Program.Slice(text, 28, 4)),
            theta is >= 0 and <= 360 ? theta : null, rho is > 0 ? rho : null,
            Program.Number(Program.Slice(text, 58, 5)), Program.Number(Program.Slice(text, 64, 5)),
            flags, ra, dec,
            NormalizeMotion(Program.Number(Program.Slice(text, 80, 4)), flags),
            NormalizeMotion(Program.Number(Program.Slice(text, 84, 4)), flags),
            NormalizeMotion(Program.Number(Program.Slice(text, 89, 4)), flags),
            NormalizeMotion(Program.Number(Program.Slice(text, 93, 4)), flags), line)
            { Spectral = Program.Slice(text, 70, 9) };
    }
    private static double Required(string s) => Program.Number(s) ?? throw new InvalidDataException("Missing required WDS numeric value: " + s);
}

internal readonly record struct Candidate(int Id, double Ra, double Dec, double? Mag, double? PmRa, double? PmDec, string Gaia);

internal sealed class Spatial
{
    private readonly Dictionary<(int, int, int), List<int>> _cells = [];
    public readonly List<Candidate> Stars = [];
    private const double Cell = 0.01;

    private static (double X, double Y, double Z) Vector(double ra, double dec)
    {
        double a = ra * Math.PI / 12, d = dec * Math.PI / 180;
        return (Math.Cos(d) * Math.Cos(a), Math.Cos(d) * Math.Sin(a), Math.Sin(d));
    }
    private static (int, int, int) Key(double x, double y, double z) => ((int)Math.Floor(x / Cell), (int)Math.Floor(y / Cell), (int)Math.Floor(z / Cell));
    public void Add(Candidate c)
    {
        var v = Vector(c.Ra, c.Dec);
        var key = Key(v.X, v.Y, v.Z);
        if (!_cells.TryGetValue(key, out var list)) _cells.Add(key, list = []);
        list.Add(Stars.Count);
        Stars.Add(c);
    }
    public List<Candidate> Near(double ra, double dec, double radius)
    {
        var v = Vector(ra, dec);
        var (x, y, z) = Key(v.X, v.Y, v.Z);
        int span = (int)Math.Ceiling(radius * Math.PI / (180 * 3600) / Cell);
        List<Candidate> result = [];
        for (int i = x - span; i <= x + span; i++)
            for (int j = y - span; j <= y + span; j++)
                for (int k = z - span; k <= z + span; k++)
                    if (_cells.TryGetValue((i, j, k), out var entries))
                        foreach (int n in entries)
                            if (Distance(ra, dec, Stars[n].Ra, Stars[n].Dec) <= radius) result.Add(Stars[n]);
        return result;
    }
    public static double Distance(double ra1, double dec1, double ra2, double dec2)
    {
        double d1 = dec1 * Math.PI / 180, d2 = dec2 * Math.PI / 180;
        double sd = Math.Sin((d1 - d2) / 2), sa = Math.Sin((ra1 - ra2) * Math.PI / 24);
        return 2 * Math.Asin(Math.Sqrt(Math.Clamp(sd * sd + Math.Cos(d1) * Math.Cos(d2) * sa * sa, 0, 1))) * 180 * 3600 / Math.PI;
    }
    public static (double Ra, double Dec) Offset(double ra, double dec, double theta, double rho)
    {
        double a = ra * Math.PI / 12, d = dec * Math.PI / 180;
        double angle = theta * Math.PI / 180, arc = rho * Math.PI / (180 * 3600);
        double d2 = Math.Asin(Math.Clamp(Math.Sin(d) * Math.Cos(arc) + Math.Cos(d) * Math.Sin(arc) * Math.Cos(angle), -1, 1));
        double a2 = a + Math.Atan2(Math.Sin(angle) * Math.Sin(arc) * Math.Cos(d), Math.Cos(arc) - Math.Sin(d) * Math.Sin(d2));
        return (((a2 * 12 / Math.PI) % 24 + 24) % 24, d2 * 180 / Math.PI);
    }
}

internal static class Errata
{
    private static readonly HashSet<string> DeletedPairs = new(StringComparer.Ordinal)
    {
        "04048-0302|LDS6163", "12493-0450|LDS6268", "10350+5402|LDS2869",
        "01444+6037|TDS60", "03588+0332|TDS123", "04191-0409|TDS133",
        "05372-0635|TDS188", "06122+5014|TDS227", "07386-2830|TDS391",
        "08455-2812|TDS491", "08557-0908|TDS502", "09476+1126|TDS555",
        "16293-4428|TDS815", "18440+3216|TDS940", "22103+3845|TDS1161",
        "23353+1813|TDS1216",
        // WDS notes explicitly reject the historical Proxima Ca-Cb detection: "Not a binary!"
        "14396-6050|SCZ1",
        // User-requested suppression of Capella O/P; IDs remain reserved.
        "05167+4600|HEL1"
    };
    // These aliases describe historical corrections; current positions still pass normal identity checks.
    internal static readonly HashSet<string> HistoricalDesignations = new(StringComparer.Ordinal)
    {
        "COU2387","HDS2046","STF2736","HJ1593","ES50","TDS94","OL233","COU332",
        "BU1348","STF418","PRZ4","STF621","AG83","STF664","CHE79","STF698","WZ9",
        "STT140","CHE82","ES214","J3317","ROE146","COU2209","KU97","KU144",
        "COU2686","HDS1312","BU156","HDS2994","LDS1431","LDS5852","MLB390",
        "STI405","KR32","LDS2294","OL224","MLB435","TDS1229","HJ607","HDS2895"
    };
    public static bool Deleted(string wds, string discoverer) => DeletedPairs.Contains(wds + "|" + discoverer);
    public static string Rename(string wds, string discoverer) => (wds, discoverer) switch
    {
        ("22060+3724", "LDS4940") => "LDS4943",
        ("14579+2953", "LDS6298") => "LDS6299",
        _ => discoverer
    };
}

internal sealed class Star(string key, int? athygId)
{
    public string Key { get; } = key;
    public int? AthygId { get; } = athygId;
    public string[] Fields = [];
    public long OmsId;
    public SystemGroup? System;
    public string Component = "";
    public Candidate? Position;
    public bool ClassificationUnknown;
    public readonly SortedSet<string> H = new(StringComparer.Ordinal);
    public readonly SortedSet<string> Stf = new(StringComparer.Ordinal);
    public readonly SortedSet<string> Stt = new(StringComparer.Ordinal);
    public readonly SortedSet<string> Bds = new(StringComparer.Ordinal);
    public readonly SortedSet<string> Bu = new(StringComparer.Ordinal);
}

internal sealed class SystemGroup(string wds)
{
    public string Wds { get; } = wds;
    public readonly SortedDictionary<string, Star> Members = new(StringComparer.Ordinal);
    public readonly List<Pair> Pairs = [];
    public readonly List<string> Notes = [];
    public string PrimaryLabel = "A";
    public Hierarchy? Hierarchy;
    public readonly HashSet<string> UnknownLabels = new(StringComparer.Ordinal);
    public readonly HashSet<string> VisualLabels = new(StringComparer.Ordinal);
    public readonly HashSet<string> QuestionableLabels = new(StringComparer.Ordinal);
    public readonly HashSet<string> PhysicalLabels = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Candidate> IdentifiedMembers = new(StringComparer.Ordinal);
    public Star Primary => Members[PrimaryLabel];
}

internal sealed partial class Builder(string root, string output)
{
    private string Input(string dir, string file) => Path.Combine(root, dir, file);
    private readonly Spatial _index = new();
    private readonly Dictionary<int, Star> _athyg = [];
    private readonly HashSet<int> _baseIds = [];
    private readonly Dictionary<string, Star> _wdsStars = new(StringComparer.Ordinal);
    private readonly List<SystemGroup> _systems = [];
    private readonly Dictionary<string, List<string>> _notes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(int Line, string Text)>> _noteEvidence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _bds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _componentAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Star> _resolvedAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _herschel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _discovererAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _hipReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _wdsSource = [];
    private readonly List<string[]> _review = [];
    private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _registry = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Wds, string Label), (long ComponentId, string Desired)> _userReviews = new();
    private readonly HashSet<(string Wds, string Label)> _userReviewsApplied = [];
    private readonly HashSet<long> _lockedRoles = [];
    private string[] _header = [];
    private Dictionary<string, int> _columns = new(StringComparer.Ordinal);
    private long _nextId = 1;
    private static readonly string[] Extra =
    [
        "system_id","component","primary_oms_id","component_label",
        "radec_source","h","stf","stt","bds","bu","wds_id",
        "relationships","theta_deg","rho_arcsec","measurement_year",
        "wds_source_line"
    ];

    private void Count(string name) => _counts[name] = _counts.GetValueOrDefault(name) + 1;
    internal static bool InBase(string mag) => Program.Number(mag) is double n && n <= 10;
    private string Field(string[] row, string name) => row[_columns[name]];
    private Star Keep(int id)
    {
        if (!_athyg.TryGetValue(id, out Star? star)) _athyg.Add(id, star = new Star("AT40:" + id, id));
        return star;
    }
    private void Review(Pair p, string reason) =>
        _review.Add([p.Wds, p.Discoverer, p.Components, p.Line.ToString(Program.Invariant), reason]);

    public void Run()
    {
        Directory.CreateDirectory(output);
        LoadRegistry();
        LoadUserReviews();
        LoadNotesAndAliases();
        LoadMatchingIdentities();
        Console.WriteLine("Indexing full AT-HYG 4.0 and selecting magnitude <= 10...");
        ReadAthyg(false);
        Console.WriteLine($"Indexed {_index.Stars.Count:N0} positions; {_athyg.Count:N0} base rows.");
        MergeWds();
        Console.WriteLine("Loading selected AT-HYG rows unchanged...");
        ReadAthyg(true);
        if (_systems.SingleOrDefault(s => s.Wds == "06451-1643") is { } sirius &&
            (!sirius.Members.TryGetValue("B", out var siriusB) || siriusB.AthygId != 586689 ||
                Field(siriusB.Fields, "proper") != "Sirius B"))
            throw new InvalidDataException("Curated Sirius B association failed its source-identity check.");
        if (_systems.SingleOrDefault(s => s.Wds == "18443+3940") is { } epsilon &&
            (!epsilon.Members.TryGetValue("A", out var epsilonA) || epsilonA.AthygId != 1915855 ||
                !epsilon.Members.TryGetValue("B", out var epsilonB) || epsilonB.AthygId != 1915854 ||
                !epsilon.Members.TryGetValue("Ca", out var ca) || Field(ca.Fields, "hd") != "173608" ||
                !epsilon.Members.TryGetValue("D", out var d) || Field(d.Fields, "hd") != "173607" ||
                !epsilon.Members.TryGetValue("Cb", out var cb) || cb.AthygId.HasValue))
            throw new InvalidDataException("Curated Epsilon Lyrae association failed its source-identity check.");
        if (_systems.SingleOrDefault(s => s.Wds == "14450+2704") is { } izar &&
            (!izar.Members.TryGetValue("A", out var izarA) || izarA.AthygId != 1462701 ||
                Field(izarA.Fields, "proper") != "Izar" || Field(izarA.Fields, "hd") != "129989" ||
                !izar.Members.TryGetValue("B", out var izarB) || izarB.AthygId != 1462698 ||
                Field(izarB.Fields, "hd") != "129988"))
            throw new InvalidDataException("Curated Izar association failed its source-identity check.");
        if (_systems.SingleOrDefault(s => s.Wds == "02039+4220") is { } almach &&
            (!almach.Members.TryGetValue("A", out var almachA) || almachA.AthygId != 156575 ||
                !almach.Members.TryGetValue("B", out var almachB) || almachB.AthygId != 156597 ||
                Field(almachB.Fields, "hd") != "12534" ||
                !almach.Members.TryGetValue("C", out var almachC) || almachC.AthygId.HasValue))
            throw new InvalidDataException("Curated Almach representative association failed its source-identity check.");
        if (_systems.SingleOrDefault(s => s.Wds == "02291+6724") is { } iotaCas &&
            (!iotaCas.Members.TryGetValue("Aa", out var iotaAa) || iotaAa.AthygId != 188568 ||
                !iotaCas.Members.ContainsKey("Ab") || !iotaCas.Members.TryGetValue("B", out var iotaB) ||
                iotaB.AthygId.HasValue || Field(iotaB.Fields, "hd").Length > 0 ||
                !iotaCas.Members.ContainsKey("Ca") || !iotaCas.Members.ContainsKey("Cb")))
            throw new InvalidDataException("Curated Iota Cassiopeiae component identities failed their source checks.");
        EnrichIdentifiers();
        if (_systems.SingleOrDefault(s => s.Wds == "14396-6050") is { } alphaCen &&
            (alphaCen.Members.Count != 3 ||
                !alphaCen.Members.TryGetValue("A", out var alphaA) || alphaA.AthygId != 1455072 ||
                Field(alphaA.Fields, "proper") != "Rigil Kentaurus" ||
                !alphaCen.Members.TryGetValue("B", out var alphaB) || alphaB.AthygId != 1455030 ||
                Field(alphaB.Fields, "proper") != "Toliman" ||
                !alphaCen.Members.TryGetValue("C", out var alphaC) || alphaC.AthygId != 1440825 ||
                Field(alphaC.Fields, "proper") != "Proxima Centauri"))
            throw new InvalidDataException("Curated Alpha Centauri component identities failed their source checks.");
        EnrichSpectralTypes();
        Validate();
        WriteOutputs();
        Console.WriteLine(Program.Json(_counts));
    }

    private void EnrichSpectralTypes()
    {
        foreach (var system in _systems)
            foreach (var (label, star) in system.Members)
            {
                if (!Pair.MissingSpectral(Field(star.Fields, "spect"))) continue;
                string[] types = system.Pairs.Select(p => p.SpectralFor(label)).OfType<string>()
                    .Distinct(StringComparer.Ordinal).ToArray();
                if (types.Length > 1)
                {
                    Count("wds_spectral_conflicts_skipped");
                    continue;
                }
                if (types.Length == 0) continue;
                star.Fields[_columns["spect"]] = types[0];
                star.Fields[_columns["spect_src"]] = "WDS";
                Count(star.AthygId.HasValue ? "wds_spectral_filled_athyg" : "wds_spectral_filled_wds_only");
            }
    }

    private void LoadRegistry()
    {
        string path = Path.Combine(output, "identity-registry.csv");
        if (!File.Exists(path)) return;
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            string[] c = Csv.Parse(line);
            if (c.Length != 2 || !long.TryParse(c[1], out long id) || id < 1 || !_registry.TryAdd(c[0], id))
                throw new InvalidDataException("Invalid identity registry row: " + line);
        }
        if (_registry.Values.Distinct().Count() != _registry.Count) throw new InvalidDataException("Duplicate OMS ID in registry.");
        _nextId = _registry.Values.DefaultIfEmpty(0).Max() + 1;
    }

    private void ReadAthyg(bool selectedOnly)
    {
        using var reader = File.OpenText(Input("AT-HYG_40", "athyg_40.csv"));
        string[] header = Csv.Parse(reader.ReadLine() ?? throw new InvalidDataException("Empty AT-HYG."));
        if (!selectedOnly)
        {
            _header = header;
            _columns = header.Select((h, i) => (h, i)).ToDictionary(p => p.h, p => p.i, StringComparer.Ordinal);
            foreach (string required in new[] { "id","ra","dec","mag","gaia","pmra","pmdec","hd","pos_src","dist","ci","spect","spect_src" })
                if (!_columns.ContainsKey(required)) throw new InvalidDataException("Missing AT-HYG column " + required);
        }
        else if (!header.SequenceEqual(_header)) throw new InvalidDataException("AT-HYG header changed during build.");
        HashSet<int> seen = [];
        while (reader.ReadLine() is string line)
        {
            string[] c = Csv.Parse(line);
            if (c.Length != _header.Length) throw new InvalidDataException("Unexpected AT-HYG field count.");
            int id = int.Parse(Field(c, "id"), Program.Invariant);
            if (!seen.Add(id)) throw new InvalidDataException("Duplicate AT-HYG ID " + id);
            if (selectedOnly)
            {
                if (_athyg.TryGetValue(id, out var star)) star.Fields = c;
                continue;
            }
            Count("athyg_input_rows");
            if (InBase(Field(c, "mag"))) { Keep(id); _baseIds.Add(id); Count("athyg_base_rows"); }
            double? ra = Program.Number(Field(c, "ra")), dec = Program.Number(Field(c, "dec"));
            if (ra is null || dec is null) { Count("athyg_without_position"); continue; }
            if (ra < 0 || ra >= 24 || Math.Abs(dec.Value) > 90) throw new InvalidDataException("Invalid AT-HYG coordinates " + id);
            Candidate candidate = new(id, ra.Value, dec.Value, Program.Number(Field(c, "mag")),
                Program.Number(Field(c, "pmra")), Program.Number(Field(c, "pmdec")), Field(c, "gaia"));
            _index.Add(candidate);
            IndexMatchingIdentities(c, candidate);
        }
    }

    private void LoadNotesAndAliases()
    {
        string? wds = null;
        int noteLine = 0;
        foreach (string line in File.ReadLines(Input("WDS", "wdsnewnotes_main.txt")))
        {
            noteLine++;
            if (line.Length < 22) continue;
            string id = line[..10].Trim();
            if (Regex.IsMatch(id, @"^\d{5}[+-]\d{4}$")) wds = id;
            if (wds is null) continue;
            if (!_notes.TryGetValue(wds, out var list)) _notes[wds] = list = [];
            list.Add(line.TrimEnd());
            if (!_noteEvidence.TryGetValue(wds, out var evidence)) _noteEvidence[wds] = evidence = [];
            evidence.Add((noteLine, line.TrimEnd()));
        }
        foreach (var (id, notes) in _notes)
            foreach (string note in notes)
                foreach (Match match in Regex.Matches(note,
                    @"\b(?:The\s+)?([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+component is the\s+([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+component of\s+(\d{5}[+-]\d{4})"))
                {
                    string a = AliasRoot(id + ":" + match.Groups[1].Value);
                    string b = AliasRoot(match.Groups[3].Value + ":" + match.Groups[2].Value);
                    if (a != b)
                    {
                        _componentAliases[string.CompareOrdinal(a, b) > 0 ? a : b] = string.CompareOrdinal(a, b) > 0 ? b : a;
                        Count("wds_explicit_component_alias_links");
                    }
                }
        foreach (string line in File.ReadLines(Input("WDS", "wdsidsadsbds.txt")))
        {
            if (line.Length < 49 || !Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$") ||
                line[49..].Trim().Length > 0) continue;
            string number = line.Substring(33, 6).Trim();
            if (!int.TryParse(number, out int n) || n <= 0) continue;
            string key = line[..10] + "|" + Program.Compact(line.Substring(40, 9));
            if (!_bds.TryGetValue(key, out var aliases)) _bds[key] = aliases = new(StringComparer.Ordinal);
            aliases.Add(n.ToString(Program.Invariant));
        }
        foreach (string line in File.ReadLines(Input("WDS", "h.txt")))
        {
            if (line.Length < 30 || !Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) continue;
            Match m = Regex.Match(line, @"=\s*(H\s+[1-6N]\s+\d+)");
            if (!m.Success) continue;
            string key = line[..10] + "|" + Program.Compact(line.Substring(10, 7));
            if (!_herschel.TryGetValue(key, out var aliases)) _herschel[key] = aliases = new(StringComparer.Ordinal);
            aliases.Add(Regex.Replace(m.Groups[1].Value, @"\s+", " "));
        }
        foreach (string filename in new[] { "stf.txt", "stt.txt" })
            foreach (string line in File.ReadLines(Input("WDS", filename)))
            {
                if (line.Length < 17 || !Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) continue;
                string discoverer = Program.Compact(line.Substring(10, 7));
                string key = line[..10] + "|" + discoverer;
                if (!_discovererAliases.TryGetValue(key, out var aliases))
                    _discovererAliases[key] = aliases = new(StringComparer.Ordinal);
                aliases.Add(discoverer);
                foreach (Match match in Regex.Matches(line[17..], @"=\s*((?:STF[AB]?|STTA?)\s*\d+)"))
                    aliases.Add(Program.Compact(match.Groups[1].Value));
            }
        foreach (string line in File.ReadLines(Input("WDS", "hds.txt")))
        {
            if (line.Length < 23 || !Regex.IsMatch(line[..10], @"^\d{5}[+-]\d{4}$")) continue;
            string hip = line[22..].Trim();
            if (!int.TryParse(hip, out int n) || n <= 0) continue;
            string key = line[..10] + "|" + Program.Compact(line.Substring(10, 7));
            if (_hipReferences.TryGetValue(key, out var existing) && existing != hip)
                throw new InvalidDataException("Conflicting HDS/HIP reference " + key);
            _hipReferences[key] = hip;
        }
    }

    private void MergeWds()
    {
        List<Pair> accepted = [];
        HashSet<string> rejectedEndpoints = new(StringComparer.Ordinal);
        int lineNo = 0;
        foreach (string text in File.ReadLines(Input("WDS", "wds_precise.txt")))
        {
            lineNo++;
            Count("wds_input_pairs");
            if (text.Length == 130 && Pair.Excluded(Program.Slice(text, 107, 4)))
            {
                var (first, second) = Pair.Endpoints(Program.Slice(text, 17, 5));
                rejectedEndpoints.Add(EndpointKey(text[..10], first, second));
                Count("wds_excluded_flags");
                continue;
            }
            Pair p;
            try { p = Pair.Parse(text, lineNo); }
            catch (InvalidDataException ex)
            {
                _review.Add([text.Length >= 10 ? text[..10] : "", "", "", lineNo.ToString(Program.Invariant), "invalid_source_row: " + ex.Message]);
                Count("wds_invalid_rows");
                continue;
            }
            if (Errata.Deleted(p.Wds, p.Discoverer)) { Count("wds_errata_deletions"); continue; }
            string renamed = Errata.Rename(p.Wds, p.Discoverer);
            if (renamed != p.Discoverer) { p = p with { Discoverer = renamed }; Count("wds_errata_renames"); }
            if (p.Flags.Contains('P')) Count("wds_century_motion_pairs_normalized");
            _wdsSource[p.Line] = text;
            accepted.Add(p);
        }
        foreach (Pair p in accepted.Where(p => rejectedEndpoints.Contains(EndpointKey(p))))
            Review(p, "conflicting_excluded_pair_identity");
        foreach (var group in accepted.Where(p => !rejectedEndpoints.Contains(EndpointKey(p)))
            .GroupBy(p => p.Wds).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            List<Pair> pairs = group.ToList();
            if (!pairs.Any(p => p.First.StartsWith('A') && p.Mag1 is <= 10)) continue;
            Count("wds_eligible_groups");
            List<Pair> supported = [];
            foreach (Pair p in pairs)
            {
                string? issue = !Hierarchy.Endpoint(p.First) || !Hierarchy.Endpoint(p.Second) ||
                    p.First == p.Second ? "unsupported_component_hierarchy" : null;
                if (issue is null) supported.Add(p);
                else Count("wds_unsupported_hierarchy_pairs_excluded");
                if (Errata.HistoricalDesignations.Contains(p.Discoverer)) Count("wds_historical_designations_accepted");
            }
            if (!supported.Any(p => p.First.StartsWith('A') && p.Mag1 is <= 10)) continue;
            SystemGroup system = new(group.Key);
            Hierarchy hierarchy = new(supported);
            system.Hierarchy = hierarchy;
            if (_notes.TryGetValue(group.Key, out var notes)) system.Notes.AddRange(notes);
            var optical = OpticalNotes(system.Notes, supported);
            system.VisualLabels.UnionWith(optical.Labels);
            system.QuestionableLabels.UnionWith(optical.QuestionableLabels);
            system.PhysicalLabels.UnionWith(optical.PhysicalLabels);
            foreach (Pair p in supported)
            {
                if (p.Flags.Contains('I'))
                    system.UnknownLabels.UnionWith(hierarchy.Leaves(p.First).Concat(hierarchy.Leaves(p.Second)));
                if (optical.Pairs.Contains(p.Line) || Pair.Visual(p.Flags))
                    system.VisualLabels.UnionWith(hierarchy.Leaves(p.Second));
                if (optical.QuestionablePairs.Contains(p.Line))
                    system.QuestionableLabels.UnionWith(hierarchy.Leaves(p.Second));
                if (optical.PhysicalPairs.Contains(p.Line))
                    system.PhysicalLabels.UnionWith(hierarchy.Leaves(p.Second));
            }
            // Narrative status contradictions are reviewed instead of overriding structured flags.
            for (int i = 0; i < system.Notes.Count; i++)
                if (Regex.IsMatch(system.Notes[i], @"\b(spurious|ghost pair)\b", RegexOptions.IgnoreCase) &&
                    InformationalNarrative(system.Notes, i)) Count("wds_informational_measurement_notes_accepted");
            string[] narrativeIssues = _noteEvidence.GetValueOrDefault(group.Key, [])
                .Where((n, index) => !optical.Notes.Contains(index) && !InformationalNarrative(system.Notes, index))
                .Select(n => (n.Line, n.Text, Match: Regex.Match(n.Text,
                    @"\b(optical|non-physical|spurious|ghost pair|not physical)\b", RegexOptions.IgnoreCase)))
                .Where(n => n.Match.Success)
                .Select(n => $"wdsnewnotes_main.txt line {n.Line}; trigger={n.Match.Value}; note={n.Text.Trim()}")
                .ToArray();
            if (narrativeIssues.Length > 0)
            {
                Count("wds_uncertain_existence_groups_excluded");
                foreach (Pair p in pairs) Count("wds_uncertain_existence_pairs_excluded");
                continue;
            }
            system.PrimaryLabel = hierarchy.Leaves("A")[0];
            string[] leaves = supported.SelectMany(p => hierarchy.Leaves(p.First).Concat(hierarchy.Leaves(p.Second)))
                .Distinct(StringComparer.Ordinal).OrderBy(s => s == system.PrimaryLabel ? 0 : 1).ThenBy(s => s, StringComparer.Ordinal).ToArray();
            foreach (string leaf in leaves)
            {
                Pair? direct = supported.Where(p => p.First == leaf).OrderByDescending(p => p.Ra.HasValue).FirstOrDefault();
                Pair? secondary = supported.Where(p => p.Second == leaf).OrderByDescending(p => CompanionPosition(p).Ra.HasValue).FirstOrDefault();
                Pair evidence = direct ?? secondary ?? supported.First(p => hierarchy.Leaves(p.First).Contains(leaf) || hierarchy.Leaves(p.Second).Contains(leaf));
                var pos = direct is not null && leaf == system.PrimaryLabel ? (evidence.Ra, evidence.Dec) :
                    secondary is not null ? CompanionPosition(secondary) : (null, null);
                if (MatchComponentIdentity(system, evidence, leaf, pos.Item1, pos.Item2, direct?.Mag1 ?? secondary?.Mag2) is Candidate identity)
                    system.IdentifiedMembers.Add(leaf, identity);
            }
            foreach (var shared in system.IdentifiedMembers.GroupBy(entry => entry.Value.Id).Where(g => g.Count() > 1).ToArray())
            {
                foreach (var entry in shared.ToArray()) system.IdentifiedMembers.Remove(entry.Key);
                Review(supported[0], "identity_first_shared_component_reference_AT40:" + shared.Key);
                Count("identity_first_ambiguous_identifiers");
            }
            foreach (string leaf in leaves)
            {
                Pair? direct = supported.Where(p => p.First == leaf).OrderByDescending(p => p.Ra.HasValue).FirstOrDefault();
                Pair? secondary = supported.Where(p => p.Second == leaf).OrderByDescending(p => CompanionPosition(p).Ra.HasValue).FirstOrDefault();
                Pair evidence = direct ?? secondary ?? supported.First(p => hierarchy.Leaves(p.First).Contains(leaf) || hierarchy.Leaves(p.Second).Contains(leaf));
                bool first = direct is not null;
                // The precise coordinate is the system anchor, not necessarily this pair's first endpoint.
                var pos = first && leaf == system.PrimaryLabel ? (evidence.Ra, evidence.Dec) :
                    secondary is not null ? AnchoredCompanionPosition(system, secondary) : (null, null);
                // A blended endpoint is not an extra star or a measured position of one of its leaves.
                double? mag = direct is not null ? direct.Mag1 : secondary?.Mag2;
                Resolve(system, evidence, leaf, pos.Item1, pos.Item2, mag, first);
            }
            foreach (Pair p in supported)
            {
                string[] firstLeaves = hierarchy.Leaves(p.First), secondLeaves = hierarchy.Leaves(p.Second);
                if (firstLeaves.Intersect(secondLeaves).Any())
                { Review(p, "overlapping_hierarchy_endpoints"); continue; }
                if (firstLeaves.Concat(secondLeaves).All(system.Members.ContainsKey))
                    system.Pairs.Add(p);
            }
            if (!system.Members.ContainsKey(system.PrimaryLabel) || system.Pairs.Count == 0)
            {
                foreach (Star star in system.Members.Values)
                {
                    star.System = null; star.Component = "";
                    if (star.AthygId is null) _wdsStars.Remove(star.Key);
                }
                Count("wds_groups_without_resolved_pairs");
                continue;
            }
            HashSet<string> participating = system.Pairs.SelectMany(p => hierarchy.Leaves(p.First).Concat(hierarchy.Leaves(p.Second))).ToHashSet(StringComparer.Ordinal);
            foreach (string orphan in system.Members.Keys.Where(k => !participating.Contains(k)).ToArray())
            {
                Star star = system.Members[orphan];
                star.System = null; star.Component = "";
                if (star.AthygId is null) _wdsStars.Remove(star.Key);
                system.Members.Remove(orphan);
            }
            if (!system.Members.ContainsKey(system.PrimaryLabel))
                throw new InvalidDataException("System primary lost during hierarchy resolution: " + system.Wds);
            foreach (Pair p in system.Pairs) AddAliases(system.Primary, p);
            _systems.Add(system);
            if (pairs.Any(p => p.Flags.Contains('W'))) Count("systems_missing_wdss");
        }
        // Components tentatively read from AT-HYG are only retained below 10 or in a resolved group.
        foreach (int id in _athyg.Keys.ToArray())
            if (_athyg[id].System is null && !_baseIds.Contains(id)) _athyg.Remove(id);
    }

    internal static (double? Ra, double? Dec) CompanionPosition(Pair p)
    {
        if (p.Ra is not double ra || p.Dec is not double dec || p.Theta is not double theta ||
            p.Rho is not double rho || p.LastYear is not int year) return (null, null);
        var pos = Spatial.Offset(ra, dec, theta, rho);
        double years = year - 2000;
        if (p.PmRa1.HasValue && p.PmDec1.HasValue && p.PmRa2.HasValue && p.PmDec2.HasValue)
        {
            double east = -(p.PmRa2.Value - p.PmRa1.Value) * years / 1000;
            double north = -(p.PmDec2.Value - p.PmDec1.Value) * years / 1000;
            return Spatial.Offset(pos.Ra, pos.Dec, Math.Atan2(east, north) * 180 / Math.PI, Math.Sqrt(east * east + north * north));
        }
        return Math.Abs(years) <= 5 ? pos : (null, null);
    }

    private void MarkUnknown(double? rightAscension, double? declination)
    {
        if (rightAscension is not double ra || declination is not double dec) return;
        foreach (Candidate c in _index.Near(ra, dec, 2))
            if (_athyg.TryGetValue(c.Id, out var star)) star.ClassificationUnknown = true;
    }

    private Star? Resolve(SystemGroup system, Pair pair, string label, double? ra, double? dec, double? mag, bool first)
    {
        if (system.Members.TryGetValue(label, out var known))
            return known;
        string alias = AliasRoot(system.Wds + ":" + label);
        if (_resolvedAliases.TryGetValue(alias, out var shared) && shared.System is not null &&
            (shared.System != system || shared.Component != label))
        {
            Count("wds_explicit_component_alias_assignments_omitted");
            return null;
        }
        double radius = 6;
        List<Candidate> nearby = ra is double a && dec is double d ? _index.Near(a, d, radius) : [];
        nearby.RemoveAll(c => system.Members.Values.Any(s => s.AthygId == c.Id));
        nearby.RemoveAll(c => system.IdentifiedMembers.Any(entry => entry.Key != label && entry.Value.Id == c.Id));
        // Preserve an established close match before using the expanded historical-position tolerance.
        List<Candidate> close = nearby.Where(c => Spatial.Distance(ra!.Value, dec!.Value, c.Ra, c.Dec) <= (first ? 2 : 3)).ToList();
        if (close.Count > 0) nearby = close;
        else if (mag.HasValue)
            nearby.RemoveAll(c => c.Mag.HasValue && Math.Abs(c.Mag.Value - mag.Value) > 2);
        List<Candidate> matches = nearby;
        if (system.IdentifiedMembers.TryGetValue(label, out var identified))
        {
            matches = [identified];
            Count("identity_first_component_matches");
        }
        if (system.Wds == "14396-6050" && label is "A" or "B" or "C")
        {
            // LDS 494's Proxima alias names C, not its first endpoint A.
            matches = [_index.Stars.Single(c => c.Id == (label switch {
                "A" => 1455072, "B" => 1455030, _ => 1440825
            }))];
            Count("curated_alpha_centauri_identity_matches");
        }
        if (system.Wds == "02039+4220" && label is "A" or "B" or "C")
        {
            // HD 12534 is the user-approved BC representative, not proof of resolved B photometry.
            matches = label == "C" ? [] :
                [_index.Stars.Single(c => c.Id == (label == "A" ? 156575 : 156597))];
            Count("curated_almach_representative_matches");
        }
        if (system.Wds == "14450+2704" && label is "A" or "B")
        {
            // WDS's J2000 position is closer to B; the named bright primary is AT-HYG 1462701.
            Candidate izar = _index.Stars.Single(c => c.Id == (label == "A" ? 1462701 : 1462698));
            double expectedMagnitude = label == "A" ? 2.578 : 4.808;
            if (izar.Mag is not double magnitude || Math.Abs(magnitude - expectedMagnitude) > 0.01)
                throw new InvalidDataException("Curated Izar identity no longer matches the expected AT-HYG photometry.");
            matches = [izar];
            Count("curated_izar_identity_matches");
        }
        if (system.Wds == "06451-1643" && label == "B")
        {
            Candidate siriusB = _index.Stars.Single(c => c.Id == 586689);
            if (siriusB.Mag is not double siriusMagnitude || Math.Abs(siriusMagnitude - 8.44) > 0.01)
                throw new InvalidDataException("Curated Sirius B identity no longer matches the expected AT-HYG photometry.");
            matches = [siriusB];
            Count("curated_sirius_b_identity_matches");
        }
        if (system.Wds == "18443+3940" && label is "A" or "B" or "Ca" or "Cb" or "D")
        {
            // AT-HYG's equal C/D coordinates cannot resolve the 0.058" CHR 77 subsystem.
            // Preserve the known main-star identities and leave the faint inner Cb source-only.
            matches = label == "Cb" ? [] :
                [_index.Stars.Single(c => c.Id == (label switch {
                    "A" => 1915855, "B" => 1915854, "Ca" => 1915959, _ => 1915960
                }))];
            Count("curated_epsilon_lyrae_identity_matches");
        }
        if (matches.Count > 1)
        {
            matches = matches.OrderBy(c => Spatial.Distance(ra!.Value, dec!.Value, c.Ra, c.Dec)).ThenBy(c => c.Id).ToList();
            double nearest = Spatial.Distance(ra!.Value, dec!.Value, matches[0].Ra, matches[0].Dec);
            List<Candidate> tied = matches.Where(c => Spatial.Distance(ra.Value, dec.Value, c.Ra, c.Dec) - nearest < 0.1).ToList();
            if (tied.Count > 1)
            {
                tied = tied.OrderBy(c => mag.HasValue && c.Mag.HasValue ? Math.Abs(mag.Value - c.Mag.Value) : double.PositiveInfinity)
                    .ThenBy(c => c.Id).ToList();
                matches = [tied[0]];
            }
            else matches = [matches[0]];
        }
        Star star;
        if (matches.Count == 1)
        {
            Candidate c = matches[0];
            if (_athyg.TryGetValue(c.Id, out var existing) && existing.System is not null &&
                (existing.System != system || existing.Component != label))
            { Count("wds_later_conflicting_assignments_omitted"); return null; }
            star = Keep(c.Id);
            star.Position = c;
        }
        else
        {
            string key = "WDS:" + system.Wds + ":" + label;
            if (!_wdsStars.TryGetValue(key, out star!))
            {
                star = new Star(key, null) { Fields = Enumerable.Repeat("", _header.Length).ToArray() };
                star.Fields[_columns["ra"]] = ra.HasValue ? Program.F(ra.Value) : "";
                star.Fields[_columns["dec"]] = dec.HasValue ? Program.F(dec.Value) : "";
                star.Fields[_columns["pos_src"]] = "WDS";
                star.Fields[_columns["mag"]] = mag is double m ? Program.F(m) : "";
                star.Fields[_columns["mag_src"]] = "WDS";
                bool individualMeasurement = (first ? pair.First : pair.Second) == label;
                double? pmRa = individualMeasurement ? first ? pair.PmRa1 : pair.PmRa2 : null;
                double? pmDec = individualMeasurement ? first ? pair.PmDec1 : pair.PmDec2 : null;
                star.Fields[_columns["pmra"]] = pmRa.HasValue ? Program.F(pmRa.Value) : "";
                star.Fields[_columns["pmdec"]] = pmDec.HasValue ? Program.F(pmDec.Value) : "";
                star.Fields[_columns["pm_src"]] = pmRa.HasValue || pmDec.HasValue ? "WDS" : "";
                _wdsStars.Add(key, star);
            }
        }
        if (system.Members.TryGetValue(label, out var previous) && previous.Key != star.Key)
        { Review(pair, "inconsistent_repeated_component_" + label); return null; }
        star.System = system;
        star.Component = label;
        system.Members[label] = star;
        _resolvedAliases[alias] = star;
        return star;
    }

    private string AliasRoot(string key)
    {
        while (_componentAliases.TryGetValue(key, out string? parent)) key = parent;
        return key;
    }

    internal static bool DeletedHistoricalMeasure(IReadOnlyList<string> notes, int index)
    {
        if (!Regex.IsMatch(notes[index], @"\bspurious preliminary\b", RegexOptions.IgnoreCase)) return false;
        string context = string.Join(" ", notes.Skip(index).Take(3).Select(n => n.Length > 21 ? n[21..].Trim() : n.Trim()));
        return Regex.IsMatch(context, @"\bspurious preliminary\b.{0,180}\bmeasure\b.{0,180}\bhas been deleted\b",
            RegexOptions.IgnoreCase);
    }

    internal static bool InformationalNarrative(IReadOnlyList<string> notes, int index)
    {
        if (DeletedHistoricalMeasure(notes, index)) return true;
        int first = index, last = index;
        bool StartsSection(int n) => notes[n].Length >= 18 && Program.Compact(notes[n].Substring(11, 7)).Length > 0;
        while (first > 0 && !StartsSection(first)) first--;
        while (last + 1 < notes.Count && !StartsSection(last + 1)) last++;
        string context = string.Join(" ", notes.Skip(first).Take(last - first + 1)
            .Select(n => n.Length > 21 ? n[21..].Trim() : n.Trim()));
        string local = string.Join(" ", notes.Skip(Math.Max(first, index - 2)).Take(index - Math.Max(first, index - 2) + 2)
            .Select(n => n.Length > 21 ? n[21..].Trim() : n.Trim()));
        if (Regex.IsMatch(context, @"\b(?:been restored|has been restored|restored\.).{0,300}\b(?:confirmation|duplicity)\b",
            RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(context, @"\bReinspection of the 1985 data\b.{0,200}\bgood agreement\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(local, @"\b(?:ghost pair|spurious pair|pair is spurious|spurious detection|Could it be spurious|considered spurious)\b",
            RegexOptions.IgnoreCase)) return false;
        return Regex.IsMatch(local,
            @"\b(?:measur(?:e|es|ement|ements)|acceleration|HIP solution|Hipparcos.{0,30}solution|proper motion|Astrographic Catalog|spectroscopic|SB1|double lines|negative result|plates)\b",
            RegexOptions.IgnoreCase);
    }

    private static string EndpointKey(Pair p) => EndpointKey(p.Wds, p.First, p.Second);
    private static string EndpointKey(string wds, string first, string second) =>
        wds + "|" + string.Join("|", new[] { first, second }.Order(StringComparer.Ordinal));

    private void AddAliases(Star star, Pair pair)
    {
        Match burnham = Regex.Match(pair.Discoverer, @"^BU(\d+)$");
        if (burnham.Success) star.Bu.Add(int.Parse(burnham.Groups[1].Value, Program.Invariant).ToString(Program.Invariant));
        if (_bds.TryGetValue(pair.Wds + "|" + pair.Discoverer, out var bds)) star.Bds.UnionWith(bds);
        if (_herschel.TryGetValue(pair.Wds + "|" + pair.Discoverer, out var h)) star.H.UnionWith(h);
        if (Regex.IsMatch(pair.Discoverer, @"^H[1-6N]\d+$"))
        {
            Match m = Regex.Match(pair.Discoverer, @"^H([1-6N])(\d+)$");
            star.H.Add("H " + m.Groups[1].Value + " " + m.Groups[2].Value);
        }
        if (Regex.IsMatch(pair.Discoverer, @"^STF[AB]?\d+$")) star.Stf.Add(pair.Discoverer);
        if (Regex.IsMatch(pair.Discoverer, @"^STTA?\d+$")) star.Stt.Add(pair.Discoverer);
        if (_discovererAliases.TryGetValue(pair.Wds + "|" + pair.Discoverer, out var aliases))
            foreach (string alias in aliases)
            {
                if (Regex.IsMatch(alias, @"^STF[AB]?\d+$")) star.Stf.Add(alias);
                if (Regex.IsMatch(alias, @"^STTA?\d+$")) star.Stt.Add(alias);
            }
    }

    private IEnumerable<Star> AllStars() => _athyg.Values.Concat(_wdsStars.Values);

    internal static double? MotionDifference(double? ra, double? dec, double? primaryRa, double? primaryDec) =>
        ra.HasValue && dec.HasValue && primaryRa.HasValue && primaryDec.HasValue ?
        Math.Sqrt(Math.Pow(ra.Value - primaryRa.Value, 2) + Math.Pow(dec.Value - primaryDec.Value, 2)) : null;

    internal static bool IsExtremeMotionDifference(double? ra, double? dec, double? primaryRa, double? primaryDec) =>
        primaryRa is double a && primaryDec is double d &&
        MotionDifference(ra, dec, a, d) is double difference &&
        difference > 100 && difference > Math.Sqrt(a * a + d * d);

    internal static (double? Ra, double? Dec) EffectiveMotion(double? ra, double? dec, string label, IEnumerable<Pair> pairs)
    {
        foreach (Pair p in pairs.OrderByDescending(p => p.LastYear).ThenBy(p => p.Line))
        {
            if (p.First == label) { ra ??= p.PmRa1; dec ??= p.PmDec1; }
            else if (p.Second == label) { ra ??= p.PmRa2; dec ??= p.PmDec2; }
            if (ra.HasValue && dec.HasValue) break;
        }
        return (ra, dec);
    }

    internal sealed record OpticalEvidence(HashSet<int> Pairs, HashSet<int> Notes, HashSet<string> Labels,
        HashSet<int> QuestionablePairs, HashSet<string> QuestionableLabels,
        HashSet<int> PhysicalPairs, HashSet<string> PhysicalLabels);

    internal static OpticalEvidence OpticalNotes(IReadOnlyList<string> notes, IReadOnlyList<Pair> pairs)
    {
        HashSet<int> matchedPairs = [], matchedNotes = [];
        HashSet<string> labels = new(StringComparer.Ordinal);
        HashSet<int> questionablePairs = [], physicalPairs = [];
        HashSet<string> questionableLabels = new(StringComparer.Ordinal), physicalLabels = new(StringComparer.Ordinal);
        string discoverer = "";
        for (int i = 0; i < notes.Count; i++)
        {
            string line = notes[i];
            if (line.Length < 21) continue;
            string designation = Program.Compact(line.Substring(11, 7));
            if (designation.Length > 0) discoverer = designation;
            string body = line[21..].Trim();
            const string status = @"(?:not optical|non-physical|not physical|optical|physical|bound)";
            Match optical = Regex.Match(body, @"\b(?:not optical|non-physical|not physical|optical)\b", RegexOptions.IgnoreCase);
            if (!optical.Success)
                optical = Regex.Match(body, @"\b(?:physical|bound)\b", RegexOptions.IgnoreCase);
            if (!optical.Success || Regex.IsMatch(body, @"\b(spurious|ghost pair)\b", RegexOptions.IgnoreCase)) continue;
            bool physical = optical.Value.Equals("physical", StringComparison.OrdinalIgnoreCase) ||
                optical.Value.Equals("bound", StringComparison.OrdinalIgnoreCase) ||
                optical.Value.Equals("not optical", StringComparison.OrdinalIgnoreCase);
            if (physical && !Regex.IsMatch(body, @"\b(?:is|are|pair|components?)\s+(?:(?:probably|likely|possibly|a|gravitationally)\s+)*(?:physical|bound|not optical)\b",
                RegexOptions.IgnoreCase)) continue;
            bool tentative = Regex.IsMatch(body,
                @"\b(probably|likely|unlikely|possibly|perhaps|may|might|appears?|uncertain|unclear|unknown|undefined|disputed|questionable|inconclusive|not certain|not clear|not necessarily)\b|(?:optical/physical|physical/optical)",
                RegexOptions.IgnoreCase);
            HashSet<int> targetPairs = tentative ? questionablePairs : physical ? physicalPairs : matchedPairs;
            HashSet<string> targetLabels = tentative ? questionableLabels : physical ? physicalLabels : labels;
            MatchCollection named = Regex.Matches(body,
                @"\b([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+(?:component\s+)?(?:is|are)\s+(?:(?:probably|likely|possibly|perhaps|a|gravitationally)\s+)*" + status + @"\b");
            Match namedList = Regex.Match(body,
                @"\b([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+and\s+([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+components\b.{0,60}\b(?:non-physical|not physical)\b");
            if (namedList.Success)
            {
                Hierarchy hierarchy = new(pairs);
                targetLabels.UnionWith(hierarchy.Leaves(namedList.Groups[1].Value).Concat(hierarchy.Leaves(namedList.Groups[2].Value)));
                matchedNotes.Add(i);
                continue;
            }
            if (named.Count > 0)
            {
                Hierarchy hierarchy = new(pairs);
                foreach (Match match in named)
                {
                    bool uncertainAssertion = named.Count == 1 && tentative ||
                        Regex.IsMatch(match.Value, @"\b(probably|likely|possibly|perhaps)\b", RegexOptions.IgnoreCase);
                    bool physicalAssertion = Regex.IsMatch(match.Value, @"\b(?:not optical|(?<!not )(?<!non-)(?:physical|bound))\b", RegexOptions.IgnoreCase);
                    var assertionLabels = uncertainAssertion ? questionableLabels : physicalAssertion ? physicalLabels : labels;
                    assertionLabels.UnionWith(hierarchy.Leaves(match.Groups[1].Value));
                }
                matchedNotes.Add(i);
                continue;
            }
            Match uncertainComponent = Regex.Match(body, @"\b([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+component\b");
            if (tentative && uncertainComponent.Success)
            {
                Hierarchy hierarchy = new(pairs);
                questionableLabels.UnionWith(hierarchy.Leaves(uncertainComponent.Groups[1].Value));
                matchedNotes.Add(i);
                continue;
            }
            string scope = body[..optical.Index].Trim().TrimEnd(':').Trim();
            int scopeColon = scope.IndexOf(':');
            if (scopeColon >= 0) scope = scope[..scopeColon].Trim();
            scope = Regex.Replace(scope, @"\s+(?:is|are|was|were)(?:\s+(?:probably|likely|possibly|perhaps|a|gravitationally))*$",
                "", RegexOptions.IgnoreCase);
            List<Pair> selected = [];
            Match namedComponent = Regex.Match(scope, @"\b([A-Z](?:[a-z](?:[1-9]\d*)?)?)\s+component$");
            if (namedComponent.Success)
            {
                string label = namedComponent.Groups[1].Value;
                selected.AddRange(pairs.Where(p => p.Second == label));
                Hierarchy hierarchy = new(pairs);
                targetLabels.UnionWith(hierarchy.Leaves(label));
                matchedNotes.Add(i);
                targetPairs.UnionWith(selected.Select(p => p.Line));
                continue;
            }
            if (Regex.IsMatch(scope, @"\bthe pair$", RegexOptions.IgnoreCase)) scope = "";
            if (scope.Length > 0)
            {
                bool valid = true;
                foreach (string token in Regex.Split(scope, @",\s+(?:and\s+)?|\s+(?:and|&)\s+", RegexOptions.IgnoreCase))
                {
                    var endpoints = Pair.Endpoints(token.Trim());
                    if (!Hierarchy.Endpoint(endpoints.First) || !Hierarchy.Endpoint(endpoints.Second) ||
                        endpoints.First == endpoints.Second) { valid = false; break; }
                    Pair[] matches = pairs.Where(p => p.First == endpoints.First && p.Second == endpoints.Second).ToArray();
                    selected.AddRange(matches);
                }
                if (!valid)
                {
                    selected.Clear();
                    selected.AddRange(pairs.Where(p => p.Discoverer == discoverer));
                }
            }
            else
            {
                Pair[] candidates = pairs.Where(p => p.Discoverer == discoverer).ToArray();
                if (candidates.Length == 0 && discoverer.Length == 0) continue;
                selected.AddRange(candidates);
            }
            matchedNotes.Add(i);
            targetPairs.UnionWith(selected.Select(p => p.Line));
        }
        return new(matchedPairs, matchedNotes, labels, questionablePairs, questionableLabels, physicalPairs, physicalLabels);
    }

    private string Role(Star star)
    {
        if (star.System is null) return star.ClassificationUnknown ? "U" : "S";
        if (star.System.UnknownLabels.Contains(star.Component)) return "U";
        if (star.System.Primary == star) return "P";
        if (_userReviews.TryGetValue((star.System.Wds, star.Component), out var userReview))
        {
            if (userReview.ComponentId != star.OmsId)
                throw new InvalidDataException($"User review identity mismatch for {star.System.Wds} {star.Component}: " +
                    $"review OMS {userReview.ComponentId} versus catalog OMS {star.OmsId}");
            _userReviewsApplied.Add((star.System.Wds, star.Component));
            _lockedRoles.Add(star.OmsId);
            Count("user_component_reviews_applied");
            return userReview.Desired;
        }
        if (IsCuratedVisualCompanion(star.System.Wds, star.Component)) return "V";
        if (IsCuratedPhysicalCompanion(star.System.Wds, star.Component)) return "C";
        Star primary = star.System.Primary;
        var own = EffectiveMotion(Program.Number(Field(star.Fields, "pmra")), Program.Number(Field(star.Fields, "pmdec")), star.Component, star.System.Pairs);
        var main = EffectiveMotion(Program.Number(Field(primary.Fields, "pmra")), Program.Number(Field(primary.Fields, "pmdec")), primary.Component, star.System.Pairs);
        // User-specified kinematic rules for unreviewed components: a relative
        // tangential velocity above 75 km/s marks an unbound visual alignment,
        // and separations under 10 arcseconds are companions otherwise.
        double? velocity = RelativeTangentialVelocityKms(own.Ra, own.Dec, main.Ra, main.Dec,
            Program.Number(Field(primary.Fields, "dist")), Program.Number(Field(star.Fields, "dist")));
        if (velocity is > 75.0)
        {
            _lockedRoles.Add(star.OmsId);
            Count("kinematic_rule_visual");
            return "V";
        }
        if (SeparationFromPrimary(star.System.Pairs, l => star.System.Hierarchy!.Leaves(l),
                primary.Component, star.Component) is < 10.0)
        {
            Count("kinematic_rule_companion");
            return "C";
        }
        if (star.System.QuestionableLabels.Contains(star.Component)) return "Q";
        if (star.System.VisualLabels.Contains(star.Component))
            return star.System.PhysicalLabels.Contains(star.Component) ? "Q" : "V";
        return IsExtremeMotionDifference(own.Ra, own.Dec, main.Ra, main.Dec) ? "Q" : "C";
    }

    // Loads catalogs/user-component-reviews.csv: the user's in-game developer
    // component reviews, applied as the highest-priority role overrides
    // (after P) and protected from ORB6 promotion.
    private void LoadUserReviews()
    {
        string path = Path.Combine(output, "user-component-reviews.csv");
        if (!File.Exists(path)) return;
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            string[] r = Csv.Parse(line);
            if (r.Length < 7) throw new InvalidDataException("Malformed user component review row: " + line);
            if (r[5] is not ("S" or "P" or "C" or "V" or "Q" or "U"))
                throw new InvalidDataException($"Unsupported user review type '{r[5]}' for {r[1]} {r[3]}");
            _userReviews[(r[1], r[3])] = (long.Parse(r[2], Program.Invariant), r[5]);
        }
        Console.WriteLine($"Loaded {_userReviews.Count:N0} user component reviews.");
    }

    // Relative tangential velocity between a component and its primary from
    // their proper-motion difference (mas/yr) at the system's distance (pc):
    // v_t [km/s] = 4.740470 * delta_mu [arcsec/yr] * distance [pc].
    internal static double? RelativeTangentialVelocityKms(double? ra, double? dec,
        double? primaryRa, double? primaryDec, double? primaryDistance, double? starDistance)
    {
        if (ra is not double r || dec is not double d0 ||
            primaryRa is not double pr || primaryDec is not double pd) return null;
        double? distance = primaryDistance is > 0 ? primaryDistance : starDistance;
        if (distance is not double dist || dist <= 0) return null;
        double masPerYear = Math.Sqrt((r - pr) * (r - pr) + (d0 - pd) * (d0 - pd));
        return 4.740470 * (masPerYear / 1000.0) * dist;
    }

    // Most recent measured separation of a component from the system primary,
    // using the measurements owned by that component (primary-anchored pairs).
    internal static double? SeparationFromPrimary(IEnumerable<Pair> pairs,
        Func<string, string[]> leaves, string primaryLabel, string label)
    {
        double? best = null;
        int bestYear = int.MinValue;
        foreach (Pair p in pairs)
        {
            if (p.Rho is not double rho) continue;
            string[] second = leaves(p.Second);
            string[] first = leaves(p.First);
            if (second.Length == 0 || first.Length == 0 ||
                second[0] != label || first[0] != primaryLabel) continue;
            int year = p.LastYear ?? int.MinValue;
            if (best is null || year > bestYear)
            {
                best = rho;
                bestYear = year;
            }
        }
        return best;
    }

    internal static bool IsCuratedPhysicalCompanion(string wds, string label) =>
        (wds == "18443+3940" && label is "B" or "Ca" or "Cb" or "D") ||
        (wds == "02039+4220" && label is "B" or "C") ||
        (wds == "02291+6724" && label is "Ab" or "B" or "Ca" or "Cb") ||
        (wds is "07393+0514" or "06451-1643" && label == "B") ||
        (wds == "22292+5825" && label == "C") ||
        (wds == "05167+4600" && label is "Ab" or "H" or "L");

    internal static bool IsCuratedVisualCompanion(string wds, string label) =>
        (wds, label) switch
        {
            ("02291+6724", "D") => true,
            ("07393+0514" or "06451-1643", not ("A" or "B")) => true,
            ("22292+5825", not ("A" or "C")) => true,
            ("05020+4349", not "A") => true,
            ("13252-1110", not "Aa") => true,
            ("02318+8916", "C" or "D") => true,
            ("17190-3459", "D") => true,
            ("05167+4600", not ("Aa" or "Ab" or "H" or "L")) => true,
            ("13239+5456", "D" or "E" or "F" or "G" or "H") => true,
            ("11221-2447", "C") => true,
            _ => false
        };

    private void Validate()
    {
        foreach (Star star in AllStars())
        {
            if (star.Fields.Length != _header.Length) throw new InvalidDataException("Missing source fields for " + star.Key);
        }
        foreach (var system in _systems)
        {
            if (system.Primary.Component != system.PrimaryLabel || system.Members.Values.Distinct().Count() != system.Members.Count)
                throw new InvalidDataException("Inconsistent system identity " + system.Wds);
            foreach (Pair p in system.Pairs)
                if (Pair.Excluded(p.Flags) || !system.Hierarchy!.Leaves(p.First).Concat(system.Hierarchy.Leaves(p.Second)).All(system.Members.ContainsKey))
                    throw new InvalidDataException("Invalid accepted pair " + p);
        }
        foreach (Star star in AllStars().OrderBy(s => s.AthygId ?? int.MaxValue).ThenBy(s => s.Key, StringComparer.Ordinal))
        {
            if (!_registry.TryGetValue(star.Key, out long id)) _registry[star.Key] = id = _nextId++;
            star.OmsId = id;
        }
    }

    private void WriteOutputs()
    {
        string catalogPath = Path.Combine(output, "oms_catalog.csv");
        string staging = catalogPath + ".tmp";
        using (var writer = new StreamWriter(staging, false, new UTF8Encoding(false)))
        {
            writer.WriteLine(Csv.Line(new[] { "oms_id" }.Concat(_header).Concat(Extra)));
            foreach (Star star in AllStars().OrderBy(s => s.OmsId))
            {
                SystemGroup? sys = star.System;
                string role = Role(star);
                string primaryId = (sys?.Primary.OmsId ?? star.OmsId).ToString(Program.Invariant);
                Pair[] measures = sys is null ? [] : sys.Pairs.Where(p => sys.Hierarchy!.Leaves(p.Second)[0] == star.Component).ToArray();
                string Measurements(Func<Pair, string> select) => string.Join("|", measures.Select(select));
                static string Value(double? number) => number.HasValue ? Program.F(number.Value) : "";
                string[] extra =
                [
                    sys?.Primary.OmsId.ToString(Program.Invariant) ?? "",
                    role, primaryId, star.Component,
                    star.AthygId.HasValue ? "GIA" : "WDS",
                    string.Join(";", star.H), string.Join(";", star.Stf), string.Join(";", star.Stt), string.Join(";", star.Bds), string.Join(";", star.Bu),
                    sys?.Wds ?? "",
                    Measurements(p => p.First + ">" + p.Second),
                    Measurements(p => Value(p.Theta)), Measurements(p => Value(p.Rho)),
                    Measurements(p => p.LastYear?.ToString(Program.Invariant) ?? ""),
                    Measurements(p => p.Line.ToString(Program.Invariant))
                ];
                writer.WriteLine(Csv.Line(new[] { star.OmsId.ToString(Program.Invariant) }.Concat(star.Fields).Concat(extra)));
                Count("output_" + role);
                if (star.Bds.Count > 0) Count("output_bds_identified_systems");
                if (star.Bu.Count > 0) Count("output_bu_identified_systems");
                if (star.AthygId is null) Count("output_wds_only_stars");
                else if (!InBase(Field(star.Fields, "mag"))) Count("output_faint_athyg_companions");
            }
        }
        File.Move(staging, catalogPath, true);
        if (_userReviewsApplied.Count != _userReviews.Count)
        {
            var missing = _userReviews.Keys.Where(k => !_userReviewsApplied.Contains(k)).Take(10);
            throw new InvalidDataException("User component reviews did not match any catalog star: " +
                string.Join(", ", missing.Select(k => k.Wds + " " + k.Label)));
        }
        foreach (var (role, count) in OrbitalClassification.Apply(catalogPath, Path.Combine(output, "orb6orbits.txt"), Input("WDS", "wds_precise.txt"), _lockedRoles))
        {
            _counts["output_" + role] -= count;
            _counts["output_C"] = _counts.GetValueOrDefault("output_C") + count;
            _counts["orb6_promoted_" + role + "_to_C"] = count;
        }
        File.Delete(Path.Combine(output, "validation.json"));
        using (var writer = new StreamWriter(Path.Combine(output, "identity-registry.csv"), false, new UTF8Encoding(false)))
        {
            writer.WriteLine("source_key,oms_id");
            foreach (var entry in _registry.OrderBy(k => k.Value))
                writer.WriteLine(Csv.Line([entry.Key, entry.Value.ToString(Program.Invariant)]));
        }
        using (var writer = new StreamWriter(Path.Combine(output, "match-review.csv"), false, new UTF8Encoding(false)))
        {
            writer.WriteLine("wds_id,discoverer,components,source_line,reason");
            foreach (string[] row in _review) writer.WriteLine(Csv.Line(row));
        }
        string[] used =
        [
            Input("AT-HYG_40", "athyg_40.csv"), Input("AT-HYG_40", "LICENSE"), Input("AT-HYG_40", "ACKNOWLEDGMENTS.md"),
            Input("WDS", "wds_precise.txt"), Input("WDS", "wdsnewnotes_main.txt"), Input("WDS", "h.txt"),
            Input("WDS", "stf.txt"), Input("WDS", "stt.txt"), Input("WDS", "hds.txt"),
            Input("WDS", "wdserror.txt"), Input("WDS", "wdsweb_format.txt"),
            Input("Henry Draper", "catalog.dat"), Input("Henry Draper", "ReadMe.txt"), Input("Henry Draper", "errata.htx"),
            Input("WDS", "bf.txt"), Input("WDS", "wdsidsadsbds.txt"), Path.Combine(output, "orb6orbits.txt")
        ];
        Program.WriteJson(Path.Combine(output, "build-manifest.json"), new
        {
            schema_version = 13, status = "PROVISIONAL_RUNTIME_DEFAULT", counts = _counts,
            sources = used.Select(p => new { file = p, sha256 = Hash(p) }),
            catalog_sha256 = Hash(catalogPath),
            wds_matching = "Identity-first trial: unique component-scoped HIP/HD references before positional fallback; unique DM-to-HD links and simple unblended primary aliases require <=6 arcsec and <=1 mag agreement; matched AT-HYG first endpoints anchor epoch-aware companion searches; positional fallback retains 6 arcsec search, original 2/3 arcsec core and expanded-only <=2 mag guard",
            conflicting_system_assignment = "retain first system assignment; omit later conflicting relationship without duplicating star",
            spectral_precedence = "Preserve known AT-HYG spectra; fill missing spectra from exact individual WDS endpoints with spect_src=WDS. Single spectra apply only to the first endpoint; simple '+' pairs supply both. Conflicting, blended/group and complex parenthesized spectra are not inferred.",
            wds_new_star_proximity_rejection = false, wds_pair_separation_limits = false,
            curated_sirius_b = "AT40:586689 is individual B of WDS 06451-1643; normalize Ba to B, retain optical Bb independently; retire duplicate WDS Ba identity without recycling its OMS ID",
            curated_epsilon_lyrae = "WDS 18443+3940: preserve AT40:1915855 as A and AT40:1915854 as B; AT40:1915959 is Ca, AT40:1915960 is D, CHR77 Cb remains a distinct source-only inner companion; B/Ca/Cb/D are physical C, A remains primary; retire duplicate WDS D without recycling its OMS ID",
            curated_izar = "WDS 14450+2704: AT40:1462701 (Izar, HD 129989) is A; AT40:1462698 (HD 129988) is B; override misleading primary-position match and retain original photometry and measurements; retire duplicate WDS B without recycling its OMS ID",
            curated_almach = "WDS 02039+4220: AT40:156575 is A; user-approved AT40:156597 / HD 12534 represents B/BC with original blended-source fields preserved; B and C are curated physical companions overriding conflicting WDS A-BC S flag; C remains distinct WDS-only; D unchanged; no inference of resolved B photometry or additional spectroscopic leaves",
            curated_iota_cassiopeiae = "WDS 02291+6724: retain AT40:188568 as Aa, CHR6 Ab and separate STF262 B; BC row DM identifies A/system, not B, so do not enrich B with A's HD and discard it as a duplicate. Ab/B/Ca/Cb are physical C; D is user-curated optical V, outside the physical quintuple. Preserve all WDS pair measurements and stable identities.",
            curated_alpha_centauri = "WDS 14396-6050: A=AT40:1455072 (Rigil Kentaurus), B=AT40:1455030 (Toliman), C=AT40:1440825 (Proxima Centauri). Exclude SCZ1 Ca-Cb, explicitly rejected as not a binary in WDS notes (Tok2014d). Preserve source astrometry, photometry, spectra and stable OMS identities; read the full precise separation field, including AC=7960.200 arcseconds.",
            curated_review_batch = "User-reviewed Procyon, Sirius, Delta Cephei, Almaaz, Spica, Polaris, Gliese 667, Capella, Mizar and HD 98800 component roles override automatic motion/optical/ORB6 classification. Primary P is retained. Capella HEL1 O/P entries are suppressed by request, not asserted to be verified duplicates; their OMS IDs stay reserved. HD 98800 is OMS 160137, not unrelated OMS 160139. See oms-notes.txt and definitively-correct-systems.csv.",
            user_component_reviews = "catalogs/user-component-reviews.csv applies the user's in-game developer component reviews (imported from dev/system-review-overrides.csv) as the highest-priority component role overrides after P; each row's component OMS identity is verified and the resulting roles are protected from ORB6 promotion",
            kinematic_classification = "unreviewed components: a relative tangential velocity versus the primary above 75 km/s (4.740470 * |delta proper motion| * system distance, primary distance preferred) classifies V; otherwise a most-recent primary-anchored separation under 10 arcseconds classifies C; remaining components fall back to WDS flags and the extreme-motion heuristic",
            missing_wdss = true, shared_athyg_gaia_ids_allowed = true,
            visual_motion_threshold_masyr = 100, visual_motion_primary_fraction = 1.0,
            visual_motion_comparison = "Q only if companion-minus-primary vector norm is strictly >100 mas/year AND strictly greater than primary vector norm; AT-HYG preferred",
            component_policy = "Supported unique ORB6 individual pairs promote Q/V to C through plot-matched geometry or an exact WDS source-pair match to the measurement-owning representative leaf; other subgroup leaves are not inferred; primary P and I-derived U preserved; otherwise Q for motion-only difference >100 mas/year AND >100% of primary motion, or tentative/conflicting optical evidence; V for uncontested explicit optical notes or WDS S/U/Y",
            limitations = new[] { "Unrecognized or conflicting hierarchies require review", "Positional associations require scientific review",
                "No universal exact WDS-Gaia crossmatch supplied", "No structured WDSS file supplied",
                "Unresolved WDS records remain in match-review.csv", "Default binary generation retains magnitude filtering; catalog associations remain provisional" }
        });
    }
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
