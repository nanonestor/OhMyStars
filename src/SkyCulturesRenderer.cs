using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OhMyStars;

public record ConstellationSegment(int FromHip, int ToHip);
public record ConstellationName(string NativeName, string EnglishName);
public readonly record struct StarInformation(
    int Hip,
    string DisplayName,
    string Bayer,
    string RightAscension,
    string Declination,
    string Magnitude,
    string AbsoluteMagnitude,
    string SpectralType,
    string Distance);

public readonly record struct ConstellationInformation(
    string Id,
    string Name,
    IReadOnlyList<StarInformation> Stars);

public class SkyCulture {
    public string Name { get; init; } = "";
    public Dictionary<string, List<ConstellationSegment>> Constellations { get; } = new();
    public Dictionary<string, ConstellationName> ConstellationNames { get; } = new();
}

public static class SkyCulturesRenderer {
    private static readonly List<SkyCulture> _skyCultures = new();

    public static IReadOnlyList<SkyCulture> SkyCultures => _skyCultures;

    private static int _activeSkyCultureIndex = -1;

    public static int ActiveSkyCultureIndex {
        get => _activeSkyCultureIndex;
        set {
            if(value < -1 || value >= _skyCultures.Count)
                return;

            if(_activeSkyCultureIndex == value)
                return;

            _activeSkyCultureIndex = value;
            ResolveSegments();
        }
    }

    public static SkyCulture? ActiveSkyCulture =>
        _activeSkyCultureIndex >= 0 && _activeSkyCultureIndex < _skyCultures.Count
            ? _skyCultures[_activeSkyCultureIndex]
            : null;

    public static IReadOnlyList<ConstellationInformation> GetActiveConstellationInformation() {
        SkyCulture? culture = ActiveSkyCulture;
        if(culture == null)
            return Array.Empty<ConstellationInformation>();

        List<ConstellationInformation> constellations = new(culture.Constellations.Count);
        foreach(KeyValuePair<string, List<ConstellationSegment>> entry in culture.Constellations.OrderBy(entry => entry.Key)) {
            HashSet<int> starHips = new();
            foreach(ConstellationSegment segment in entry.Value) {
                starHips.Add(segment.FromHip);
                starHips.Add(segment.ToHip);
            }

            List<StarInformation> stars = new(starHips.Count);
            foreach(int hip in starHips) {
                if(hipToInformation.TryGetValue(hip, out StarInformation information)) {
                    stars.Add(information);
                }
            }

            stars.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
            string name = culture.ConstellationNames.TryGetValue(entry.Key, out ConstellationName? constellationName)
                ? (!string.IsNullOrWhiteSpace(constellationName.EnglishName)
                    ? constellationName.EnglishName
                    : constellationName.NativeName)
                : entry.Key;

            constellations.Add(new ConstellationInformation(entry.Key, name, stars));
        }

        return constellations;
    }

    public static bool TryGetStarDirection(int hip, out double3 direction) {
        return hipToDirection.TryGetValue(hip, out direction);
    }

    private static readonly ImColor8 white = new ImColor8(255, 255, 255, 255);

    private static readonly Dictionary<int, double3> hipToDirection = new();
    private static readonly List<ResolvedConstellationSegment> resolvedSegments = new();
    private static readonly List<ResolvedConstellationLabel> resolvedLabels = new();
    private static readonly Dictionary<int, string> hipToName = new();
    private static readonly Dictionary<int, StarInformation> hipToInformation = new();
    private static readonly List<ResolvedNamedStar> resolvedNamedStars = new();
    private static readonly HashSet<StarDirectionKey> renderedStarDirections = new();

    private readonly record struct StarDirectionKey(int X, int Y, int Z);

    public readonly record struct ResolvedConstellationSegment(double3 A, double3 B);
    public readonly record struct ResolvedConstellationLabel(
        string Text,
        double3 Direction);

    public readonly record struct ResolvedNamedStar(
        string Name,
        double3 Direction);

    public static void Init() {
        string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string skyculturesPath = Path.Combine(
            userDocs,
            "My Games",
            "Kitten Space Agency",
            "mods",
            "OhMyStars",
            "skycultures");

        string catalogPath = Path.Combine(
            userDocs,
            "My Games",
            "Kitten Space Agency",
            "mods",
            "OhMyStars",
            "athyg_32_reduced_m10.csv");


        LoadRenderedStarDirections();
        LoadSkyCultures(skyculturesPath);

        _activeSkyCultureIndex = 29;

        LoadCatalog(catalogPath);
        ResolveSegments();
    }

    public static void Draw(ImDrawListPtr draw_list, Camera camera, double3 center, double radius, bool showAsterisms, bool showAsterismNames, bool showStarNames) {
        if(showAsterisms)
            DrawAsterisms(draw_list, camera, center, radius);

        if(showAsterismNames)
            DrawAsterismNames(draw_list, camera, center, radius);

        if(showStarNames)
            DrawStarNames(draw_list, camera, center, radius);
    }


    public static void DrawAsterisms(ImDrawListPtr draw_list, Camera camera, double3 center, double radius) {
        ImColor8 lineColor = StellariumRenderer.ToLineColor(StellariumRenderer.asterismLineColor, StellariumRenderer.asterismLineOpacity);

        foreach(ResolvedConstellationSegment segment in resolvedSegments) {
            double3 a = center + StellariumRenderer.ApplyAlignment(segment.A) * radius;
            double3 b = center + StellariumRenderer.ApplyAlignment(segment.B) * radius;

            double3 midpoint = (a + b) * 0.5d;
            if((camera.IsPointWithinFov(a) || camera.IsPointWithinFov(b) || camera.IsPointWithinFov(center)) &&
                StellariumRenderer.IsVisibleFromCamera(a) &&
                StellariumRenderer.IsVisibleFromCamera(midpoint) &&
                StellariumRenderer.IsVisibleFromCamera(b)) {
                ImDrawListExtensions.AddLine(
                    draw_list,
                    StellariumRenderer.EgoToOverlayScreen(camera, a),
                    StellariumRenderer.EgoToOverlayScreen(camera, b),
                    lineColor,
                    2f);
            }


                            // if (camera.IsPointWithinFov(a) || camera.IsPointWithinFov(b)) {
            //     ImDrawListExtensions.AddLine(
            //         draw_list,
            //         camera.EclToScreen(a),
            //         camera.EclToScreen(b),
            //         lineColor,
            //         2f);
            // }

        }
    }

    public static void DrawAsterismNames(
        ImDrawListPtr draw_list,
        Camera camera,
        double3 center,
        double radius) {

        foreach(ResolvedConstellationLabel label in resolvedLabels) {
            double3 position = center + StellariumRenderer.ApplyAlignment(label.Direction) * radius;

            float2 screen = StellariumRenderer.EgoToOverlayScreen(camera, position);

            if (!camera.IsPointWithinFov(position)) {
                continue;
            }
            if(!StellariumRenderer.IsVisibleFromCamera(position)) {
                continue;
            }
            ImDrawListExtensions.AddText(
                draw_list,
                screen,
                white,
                label.Text);
        }
    }

    public static void DrawStarNames(
        ImDrawListPtr draw_list,
        Camera camera,
        double3 center,
        double radius) {
        foreach(ResolvedNamedStar namedStar in resolvedNamedStars) {
            double3 position = center + StellariumRenderer.ApplyAlignment(namedStar.Direction) * radius;
            if(!camera.IsPointWithinFov(position))
                continue;
            if(!StellariumRenderer.IsVisibleFromCamera(position)) {
                continue;
            }

            float2 screen = StellariumRenderer.EgoToOverlayScreen(camera, position);
            
            ImDrawListExtensions.AddText(
                draw_list,
                screen,
                white,
                namedStar.Name);
        }
    }

    public static void ResolveSegments() {
        resolvedSegments.Clear();
        resolvedLabels.Clear();
        resolvedNamedStars.Clear();

        SkyCulture? culture = ActiveSkyCulture;

        if(culture == null)
            return;

        HashSet<int> namedStarHips = new();

        foreach(KeyValuePair<string, List<ConstellationSegment>> entry in culture.Constellations) {
            string constellationId = entry.Key;
            List<ConstellationSegment> segments = entry.Value;

            HashSet<int> labelStarHips = new();

            foreach(ConstellationSegment segment in segments) {
                if(!hipToDirection.TryGetValue(segment.FromHip, out double3 a))
                    continue;

                if(!hipToDirection.TryGetValue(segment.ToHip, out double3 b))
                    continue;

                if(!IsRenderedStar(a) || !IsRenderedStar(b))
                    continue;

                resolvedSegments.Add(new ResolvedConstellationSegment(a, b));

                labelStarHips.Add(segment.FromHip);
                labelStarHips.Add(segment.ToHip);
                AddResolvedStarName(segment.FromHip, a, namedStarHips);
                AddResolvedStarName(segment.ToHip, b, namedStarHips);
            }

            if(labelStarHips.Count == 0)
                continue;

            if(!culture.ConstellationNames.TryGetValue(constellationId, out ConstellationName? name) || name is null)
                continue;

            double3 sum = default;
            int count = 0;

            foreach(int hip in labelStarHips) {
                if(!hipToDirection.TryGetValue(hip, out double3 direction))
                    continue;

                sum += direction;
                count++;
            }

            if(count == 0)
                continue;

            double length = Math.Sqrt(
                sum.X * sum.X +
                sum.Y * sum.Y +
                sum.Z * sum.Z);

            if(length <= 0.000001)
                continue;

            double3 labelDirection = sum / length;

            string labelText = !string.IsNullOrWhiteSpace(name.EnglishName)
                ? name.EnglishName
                : name.NativeName;

            resolvedLabels.Add(new ResolvedConstellationLabel(
                labelText,
                labelDirection));
        }
    }

    private static void AddResolvedStarName(int hip, double3 direction, HashSet<int> namedStarHips) {
        if(namedStarHips.Add(hip) && hipToName.TryGetValue(hip, out string? name)) {
            resolvedNamedStars.Add(new ResolvedNamedStar(name, direction));
        }
    }

    private static void LoadRenderedStarDirections() {
        renderedStarDirections.Clear();

        Mod? coreMod = ModLibrary.Find("Core");
        if(coreMod is null || coreMod == Mod.Empty || string.IsNullOrWhiteSpace(coreMod.DirectoryPath))
            return;

        string starBinaryPath = Path.Combine(
            coreMod.DirectoryPath,
            "briars_binary_dimmer_better_99k_stars.bin");
        if(!File.Exists(starBinaryPath))
            return;

        using FileStream stream = File.OpenRead(starBinaryPath);
        using BinaryReader reader = new(stream);

        if(stream.Length < sizeof(int))
            return;

        int starCount = reader.ReadInt32();
        long availableStarCount = (stream.Length - stream.Position) / 16;
        int recordsToRead = (int)Math.Min(Math.Max(starCount, 0), availableStarCount);

        for(int index = 0; index < recordsToRead; index++) {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            stream.Position += 4;

            renderedStarDirections.Add(new StarDirectionKey(
                BitConverter.SingleToInt32Bits(x),
                BitConverter.SingleToInt32Bits(y),
                BitConverter.SingleToInt32Bits(z)));
        }
    }

    private static bool IsRenderedStar(double3 direction) {
        if(renderedStarDirections.Count == 0)
            return true;

        return renderedStarDirections.Contains(new StarDirectionKey(
            BitConverter.SingleToInt32Bits((float)direction.X),
            BitConverter.SingleToInt32Bits((float)direction.Y),
            BitConverter.SingleToInt32Bits((float)direction.Z)));
    }

    private static void LoadCatalog(string path) {
        hipToDirection.Clear();
        hipToName.Clear();
        hipToInformation.Clear();

        if(!File.Exists(path))
            return;

        using StreamReader reader = new StreamReader(path);

        string? headerLine = reader.ReadLine();

        if(headerLine == null)
            return;

        string[] headers = SplitCsvLine(headerLine);

        int hipIndex = Array.IndexOf(headers, "hip");
        int raIndex = Array.IndexOf(headers, "ra");
        int decIndex = Array.IndexOf(headers, "dec");
        int bayerIndex = Array.IndexOf(headers, "bayer");
        int constellationIndex = Array.IndexOf(headers, "con");
        int properIndex = Array.IndexOf(headers, "proper");
        int magnitudeIndex = Array.IndexOf(headers, "mag");
        int absoluteMagnitudeIndex = Array.IndexOf(headers, "absmag");
        int spectralTypeIndex = Array.IndexOf(headers, "spect");
        int distanceIndex = Array.IndexOf(headers, "dist");
        

        if(hipIndex < 0 || raIndex < 0 || decIndex < 0 ||
            bayerIndex < 0 || constellationIndex < 0 || properIndex < 0 ||
            magnitudeIndex < 0 || absoluteMagnitudeIndex < 0 || spectralTypeIndex < 0 || distanceIndex < 0)
            return;

        while(reader.ReadLine() is string rawLine) {
            if(string.IsNullOrWhiteSpace(rawLine))
                continue;

            string[] parts = SplitCsvLine(rawLine);

            int maxNeededIndex = Math.Max(
                Math.Max(hipIndex, Math.Max(raIndex, decIndex)),
                Math.Max(
                    Math.Max(bayerIndex, Math.Max(constellationIndex, properIndex)),
                    Math.Max(magnitudeIndex, Math.Max(absoluteMagnitudeIndex, Math.Max(spectralTypeIndex, distanceIndex)))));

            if(parts.Length <= maxNeededIndex)
                continue;

            string hipText = parts[hipIndex];

            if(string.IsNullOrWhiteSpace(hipText))
                continue;

            if(!int.TryParse(
                hipText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int hip)) {
                continue;
            }

            if(hip <= 0)
                continue;

            if(!double.TryParse(
                parts[raIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double raHours)) {
                continue;
            }

            if(!double.TryParse(
                parts[decIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double decDegrees)) {
                continue;
            }

            hipToDirection[hip] = StarDirectionConverter.RaDecToDirection(raHours, decDegrees);

            string displayName = GetDisplayName(
                parts[properIndex],
                parts[constellationIndex],
                parts[bayerIndex],
                hip);
            hipToName[hip] = displayName;
            hipToInformation[hip] = new StarInformation(
                hip,
                displayName,
                ExpandBayerDesignation(parts[bayerIndex]),
                FormatRightAscension(raHours),
                FormatSignedDeclination(parts[decIndex]),
                parts[magnitudeIndex],
                parts[absoluteMagnitudeIndex],
                parts[spectralTypeIndex],
                parts[distanceIndex]);
        }
    }

    private static string FormatSignedDeclination(string declination) {
        if(double.TryParse(
            declination,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double value)) {
            return value.ToString("+0.########;-0.########;+0", CultureInfo.InvariantCulture);
        }

        return declination;
    }

    private static string FormatRightAscension(double hours) {
        int totalSeconds = (int)Math.Round(hours * 3600d, MidpointRounding.AwayFromZero);
        totalSeconds %= 24 * 60 * 60;
        if(totalSeconds < 0)
            totalSeconds += 24 * 60 * 60;

        int wholeHours = totalSeconds / 3600;
        int wholeMinutes = totalSeconds % 3600 / 60;
        int wholeSeconds = totalSeconds % 60;
        return $"{wholeHours:D2}:{wholeMinutes:D2}:{wholeSeconds:D2}";
    }

    private static string ExpandBayerDesignation(string bayer) {
        if(string.IsNullOrWhiteSpace(bayer))
            return bayer;

        string[] parts = bayer.Split('-', 2);
        string letter = parts[0] switch {
            "Alp" => "Alpha",
            "Bet" => "Beta",
            "Gam" => "Gamma",
            "Del" => "Delta",
            "Eps" => "Epsilon",
            "Zet" => "Zeta",
            "Eta" => "Eta",
            "The" => "Theta",
            "Iot" => "Iota",
            "Kap" => "Kappa",
            "Lam" => "Lambda",
            "Mu" => "Mu",
            "Nu" => "Nu",
            "Xi" => "Xi",
            "Omi" => "Omicron",
            "Pi" => "Pi",
            "Rho" => "Rho",
            "Sig" => "Sigma",
            "Tau" => "Tau",
            "Ups" => "Upsilon",
            "Phi" => "Phi",
            "Chi" => "Chi",
            "Psi" => "Psi",
            "Ome" => "Omega",
            _ => parts[0]
        };

        return parts.Length == 1 ? letter : $"{letter}-{parts[1]}";
    }

    private static string GetDisplayName(string properName, string constellation, string bayer, int hip) {
        if(!string.IsNullOrWhiteSpace(properName))
            return properName;

        if(!string.IsNullOrWhiteSpace(bayer))
            return string.IsNullOrWhiteSpace(constellation) ? bayer : $"{constellation} {bayer}";

        return string.IsNullOrWhiteSpace(constellation) ? $"HIP {hip}" : $"{constellation} HIP {hip}";
    }

    private static string[] SplitCsvLine(string line) {
        List<string> fields = new();
        int start = 0;
        bool inQuotes = false;

        for(int i = 0; i < line.Length; i++) {
            char c = line[i];

            if(c == '"') {
                if(inQuotes && i + 1 < line.Length && line[i + 1] == '"') {
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if(c == ',' && !inQuotes) {
                fields.Add(UnquoteCsvField(line.Substring(start, i - start)));
                start = i + 1;
            }
        }

        fields.Add(UnquoteCsvField(line.Substring(start)));

        return fields.ToArray();
    }

    private static string UnquoteCsvField(string field) {
        field = field.Trim();

        if(field.Length >= 2 && field[0] == '"' && field[^1] == '"') {
            field = field.Substring(1, field.Length - 2);
            field = field.Replace("\"\"", "\"");
        }

        return field;
    }

    public static void LoadSkyCultures(string path) {
        _skyCultures.Clear();

        if(!Directory.Exists(path))
            return;

        foreach(string cultureDir in Directory.EnumerateDirectories(path)) {
            string shipFile = Path.Combine(cultureDir, "constellationship.fab");

            if(!File.Exists(shipFile))
                continue;

            string name = Path.GetFileName(cultureDir);
            name = name.Replace('_', ' ');
            name = char.ToUpper(name[0]) + name.Substring(1);

            SkyCulture culture = new SkyCulture {
                Name = name
            };

            LoadConstellationShipFile(shipFile, culture);

            string? namesFile = Path.Combine(cultureDir, "constellation_names.eng.fab");

            if(!File.Exists(namesFile)) {
                namesFile = Directory
                    .EnumerateFiles(cultureDir, "constellation_names.*.fab")
                    .FirstOrDefault();
            }

            if(namesFile != null)
                LoadConstellationNamesFile(namesFile, culture);

            _skyCultures.Add(culture);
        }
    }

    private static void LoadConstellationShipFile(string file, SkyCulture culture) {
        foreach(string rawLine in File.ReadLines(file)) {
            string line = rawLine.Trim();

            if(line.Length == 0)
                continue;

            if(line.StartsWith("#"))
                continue;

            string[] parts = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

            if(parts.Length < 2)
                continue;

            string abbrev = parts[0];

            if(!int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int segmentCount)) {
                continue;
            }

            int expectedStarIdCount = segmentCount * 2;

            if(parts.Length < 2 + expectedStarIdCount)
                continue;

            List<ConstellationSegment> segments = new();

            for(int i = 0; i < segmentCount; i++) {
                int fromIndex = 2 + i * 2;
                int toIndex = fromIndex + 1;

                if(!int.TryParse(
                    parts[fromIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int fromHip)) {
                    continue;
                }

                if(!int.TryParse(
                    parts[toIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int toHip)) {
                    continue;
                }

                segments.Add(new ConstellationSegment(fromHip, toHip));
            }

            culture.Constellations[abbrev] = segments;
        }
    }

    private static readonly Regex _constellationNameRegex = new(
        @"^\s*(\S+)\s+""((?:[^""\\]|\\.)*)""\s+_\(""((?:[^""\\]|\\.)*)""\)",
        RegexOptions.Compiled);

    private static void LoadConstellationNamesFile(string file, SkyCulture culture) {
        foreach(string rawLine in File.ReadLines(file)) {
            string line = rawLine.Trim();

            if(line.Length == 0)
                continue;

            if(line.StartsWith("#"))
                continue;

            Match match = _constellationNameRegex.Match(line);

            if(!match.Success)
                continue;

            string id = match.Groups[1].Value;
            string nativeName = UnescapeFabString(match.Groups[2].Value);
            string englishName = UnescapeFabString(match.Groups[3].Value);

            culture.ConstellationNames[id] = new ConstellationName(
                nativeName,
                englishName);
        }
    }

    private static string UnescapeFabString(string text) {
        return text
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }
}