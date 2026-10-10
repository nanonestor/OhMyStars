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
    string ConstellationAbbrev,
    bool HasProperName,
    string HipparcosNumber,
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

// A Bayer-designated star paired with its display name for the Information tab's Bayer
// selection mode, e.g. "Cassiopeia Iota" - full constellation name plus expanded Bayer letter.
public readonly record struct BayerStarInformation(
    StarInformation Star,
    string DisplayName);

public class SkyCulture {
    public string Name { get; init; } = "";
    public Dictionary<string, List<ConstellationSegment>> Constellations { get; } = new();
    public Dictionary<string, ConstellationName> ConstellationNames { get; } = new();
}

public static class SkyCulturesRenderer {
    // Offset added to a catalog row's own id to synthesize a stable "hip" key for named/Bayer stars
    // that have no real Hipparcos number, keeping them clear of the real HIP range (max ~120,416).
    private const int SyntheticHipOffset = 10_000_000;

    // The catalog hip resolved to the star named "Sol" - the game's actual central star, which sits
    // at the origin of its own coordinate system rather than at a fixed sky direction. If the game
    // ever renames or replaces the central star (e.g. traveling to another system), this should be
    // re-pointed at whatever the new central star's catalog entry is.
    private static int _centralStarHip;

    public static bool IsCentralStar(int hip) => hip > 0 && hip == _centralStarHip;

    // Catalog stars that also exist as real bodies in the game's celestial system. Their true in-game
    // position differs from the catalog direction (especially up close), so pointers/labels/orientation
    // target the body itself whenever it is loaded.
    private static readonly Dictionary<int, string> hipToGameBodyId = new() {
        { 70890, "ProximaCentauri" },
        { 71683, "AlphaCentauriA" },
        { 71681, "AlphaCentauriB" },
        { 87937, "BarnardsStar" },
        { 8102, "TauCeti" },
    };

    // Camera-relative (ego) position of the game body representing this catalog star, if one is loaded.
    public static bool TryGetGameBodyEgo(int hip, Camera camera, out double3 ego) {
        ego = default;
        IPosition? body = ResolveGameBody(hip, camera);
        if(body == null)
            return false;

        ego = camera.GetPositionEgo(body);
        return !VectorMath.IsZero(ego);
    }

    // Vector from an observer (e.g. the controlled vessel) to the game body representing this catalog star,
    // in ego/ecliptic axes.
    public static bool TryGetGameBodyVectorFrom(int hip, Camera camera, IPosition observer, out double3 vector) {
        vector = default;
        if(observer == null)
            return false;

        IPosition? body = ResolveGameBody(hip, camera);
        if(body == null)
            return false;

        vector = camera.GetPositionEgo(body) - camera.GetPositionEgo(observer);
        return !VectorMath.IsZero(vector);
    }

    private static IPosition? ResolveGameBody(int hip, Camera camera) {
        if(hip <= 0 || camera == null)
            return null;

        if(IsCentralStar(hip))
            return Universe.CurrentSystem?.Get("Sol") ?? (IPosition?)StellariumRenderer.GetCentralStarBody(camera);

        if(hipToGameBodyId.TryGetValue(hip, out string? bodyId))
            return Universe.CurrentSystem?.Get(bodyId);

        return null;
    }

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

    // All catalog stars with a proper name, sorted alphabetically for the Information tab's Proper selection mode.
    public static IReadOnlyList<StarInformation> GetAllNamedStars() {
        List<StarInformation> stars = new();
        foreach(StarInformation information in hipToInformation.Values) {
            if(information.HasProperName)
                stars.Add(information);
        }

        stars.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return stars;
    }

    // All catalog stars with a Bayer designation, sorted alphabetically by their full "Constellation Letter"
    // display name for the Information tab's Bayer selection mode.
    public static IReadOnlyList<BayerStarInformation> GetAllBayerStars() {
        List<BayerStarInformation> stars = new();
        foreach(StarInformation information in hipToInformation.Values) {
            if(string.IsNullOrWhiteSpace(information.Bayer))
                continue;

            string constellationName = GetConstellationFullName(information.ConstellationAbbrev);
            string displayName = string.IsNullOrWhiteSpace(constellationName)
                ? information.Bayer
                : $"{constellationName} {information.Bayer}";
            stars.Add(new BayerStarInformation(information, displayName));
        }

        stars.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        return stars;
    }

    // Resolves a catalog constellation abbreviation (e.g. "Cas") to the active sky culture's full
    // English name (e.g. "Cassiopeia"), falling back to the abbreviation if no culture is active or
    // it has no name for that abbreviation.
    private static string GetConstellationFullName(string abbrev) {
        if(string.IsNullOrWhiteSpace(abbrev))
            return abbrev;

        SkyCulture? culture = ActiveSkyCulture;
        if(culture != null && culture.ConstellationNames.TryGetValue(abbrev, out ConstellationName? name)) {
            return !string.IsNullOrWhiteSpace(name.EnglishName) ? name.EnglishName : name.NativeName;
        }

        return abbrev;
    }

    // Returns the star's direction as seen from the camera. With experimental parallax active this is the
    // camera-relative direction to the star's 3D catalog position (matching the moved background stars).
    public static bool TryGetStarDirection(int hip, out double3 direction) {
        if(!hipToDirection.TryGetValue(hip, out double3 staticDirection)) {
            direction = default;
            return false;
        }

        Camera? camera = StarParallax.IsActive ? Program.GetMainCamera() : null;
        if(camera == null) {
            direction = staticDirection;
            return true;
        }

        direction = GetEffectiveDirection(hip, staticDirection, StarParallax.GetCameraPc(camera), out _);
        return true;
    }

    // Like TryGetStarDirection but from an arbitrary heliocentric observer position (parsecs).
    public static bool TryGetStarDirectionFrom(int hip, double3 observerPc, out double3 direction) {
        if(!hipToDirection.TryGetValue(hip, out double3 staticDirection)) {
            direction = default;
            return false;
        }

        direction = GetEffectiveDirection(hip, staticDirection, observerPc, out _);
        return true;
    }

    // Static catalog direction unless parallax is active and the star has a usable 3D position inside the
    // parallax cutoff; then the normalized camera->star vector. `hidden` mirrors the near-camera star cull.
    private static double3 GetEffectiveDirection(int hip, double3 staticDirection, double3 cameraPc, out bool hidden) {
        hidden = false;
        if(!StarParallax.IsActive || !hipToPositionPc.TryGetValue(hip, out double3 position))
            return staticDirection;

        double3 delta = position - cameraPc;
        double distanceSq = delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z;
        double hideRadius = Math.Max(0d, StarParallax.HideRadiusPc);
        if(distanceSq <= hideRadius * hideRadius || distanceSq < 1e-18) {
            hidden = true;
            return staticDirection;
        }

        double cutoff = StarParallax.CutoffPc;
        if(cutoff > 0d) {
            double sunDistanceSq = position.X * position.X + position.Y * position.Y + position.Z * position.Z;
            if(sunDistanceSq > cutoff * cutoff)
                return staticDirection;
        }

        return delta / Math.Sqrt(distanceSq);
    }

    public static bool TryGetStarDisplayName(int hip, out string name) {
        if(hipToName.TryGetValue(hip, out string? found) && !string.IsNullOrWhiteSpace(found)) {
            name = found;
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static readonly ImColor8 white = new ImColor8(255, 255, 255, 255);

    private static readonly Dictionary<int, double3> hipToDirection = new();
    // Heliocentric position in parsecs, in the same rendered frame as hipToDirection (direction * dist),
    // matching how tools/make_parallax_bin.py places stars in the parallax bin.
    private static readonly Dictionary<int, double3> hipToPositionPc = new();
    private static readonly List<ResolvedConstellationSegment> resolvedSegments = new();
    private static readonly List<ResolvedConstellationLabel> resolvedLabels = new();
    private static readonly Dictionary<int, string> hipToName = new();
    private static readonly Dictionary<int, StarInformation> hipToInformation = new();
    private static readonly List<ResolvedNamedStar> resolvedNamedStars = new();
    private static readonly HashSet<StarDirectionKey> renderedStarDirections = new();

    private readonly record struct StarDirectionKey(int X, int Y, int Z);

    public readonly record struct ResolvedConstellationSegment(int HipA, double3 A, int HipB, double3 B);
    public readonly record struct ResolvedConstellationLabel(
        string Text,
        double3 Direction,
        int[] Hips);

    public readonly record struct ResolvedNamedStar(
        int Hip,
        string Name,
        double3 Direction);

    public static void Init() {
        string skyculturesPath = ModPaths.Combine("skycultures");
        string catalogPath = ModPaths.Combine("athyg_32_reduced_m10.csv");

        LoadRenderedStarDirections();
        LoadSkyCultures(skyculturesPath);

        _activeSkyCultureIndex = 29;

        LoadCatalog(catalogPath);
        ResolveSegments();
    }

    public static void Draw(ImDrawListPtr draw_list, Camera camera, double3 center, double radius, bool showAsterisms, bool showAsterismNames, bool showStarNames) {
        double3 cameraPc = StarParallax.IsActive ? StarParallax.GetCameraPc(camera) : default;

        if(showAsterisms)
            DrawAsterisms(draw_list, camera, center, radius, cameraPc);

        if(showAsterismNames)
            DrawAsterismNames(draw_list, camera, center, radius, cameraPc);

        if(showStarNames)
            DrawStarNames(draw_list, camera, center, radius, cameraPc);
    }


    public static void DrawAsterisms(ImDrawListPtr draw_list, Camera camera, double3 center, double radius, double3 cameraPc) {
        ImColor8 lineColor = StellariumRenderer.ToLineColor(StellariumRenderer.asterismLineColor, StellariumRenderer.asterismLineOpacity);

        foreach(ResolvedConstellationSegment segment in resolvedSegments) {
            double3 directionA = GetEffectiveDirection(segment.HipA, segment.A, cameraPc, out bool hiddenA);
            double3 directionB = GetEffectiveDirection(segment.HipB, segment.B, cameraPc, out bool hiddenB);
            if(hiddenA || hiddenB)
                continue;

            double3 a = center + StellariumRenderer.ApplyAlignment(directionA) * radius;
            double3 b = center + StellariumRenderer.ApplyAlignment(directionB) * radius;

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
        double radius,
        double3 cameraPc) {

        foreach(ResolvedConstellationLabel label in resolvedLabels) {
            double3 labelDirection = label.Direction;
            if(StarParallax.IsActive) {
                double3 sum = default;
                foreach(int hip in label.Hips) {
                    if(!hipToDirection.TryGetValue(hip, out double3 staticDirection))
                        continue;

                    double3 direction = GetEffectiveDirection(hip, staticDirection, cameraPc, out bool hidden);
                    if(!hidden)
                        sum += direction;
                }

                double length = Math.Sqrt(sum.X * sum.X + sum.Y * sum.Y + sum.Z * sum.Z);
                if(length <= 0.000001)
                    continue;

                labelDirection = sum / length;
            }

            double3 position = center + StellariumRenderer.ApplyAlignment(labelDirection) * radius;

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
        double radius,
        double3 cameraPc) {
        foreach(ResolvedNamedStar namedStar in resolvedNamedStars) {
            double3 direction = GetEffectiveDirection(namedStar.Hip, namedStar.Direction, cameraPc, out bool hidden);
            if(hidden)
                continue;

            double3 position = center + StellariumRenderer.ApplyAlignment(direction) * radius;
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

    // Draws a single star's name at its catalog position - used for the Information tab's
    // Proper/Bayer dropdown selection, independent of the active sky culture's asterism labels.
    // The central star (Sol) has no meaningful catalog direction, so its label is placed at its
    // real in-game position instead.
    public static void DrawSelectedDropdownStarName(ImDrawListPtr draw_list, Camera camera, double3 center, double radius) {
        if(!OhMyStarsWindow.TryGetDropdownSelectedStarLabel(out int hip, out string name))
            return;

        double3 position;
        if(TryGetGameBodyEgo(hip, camera, out double3 bodyEgo)) {
            position = bodyEgo;
        } else if(IsCentralStar(hip) && !StarParallax.IsCameraOutsideSolarSystem(camera)) {
            return;
        } else {
            if(!hipToDirection.TryGetValue(hip, out double3 staticDirection))
                return;

            double3 cameraPc = StarParallax.IsActive ? StarParallax.GetCameraPc(camera) : default;
            double3 direction = GetEffectiveDirection(hip, staticDirection, cameraPc, out bool hidden);
            if(hidden)
                return;

            position = center + StellariumRenderer.ApplyAlignment(direction) * radius;
        }

        if(!camera.IsPointWithinFov(position))
            return;
        if(!StellariumRenderer.IsVisibleFromCamera(position))
            return;

        float2 screen = StellariumRenderer.EgoToOverlayScreen(camera, position);
        ImDrawListExtensions.AddText(draw_list, screen, white, name);
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

                resolvedSegments.Add(new ResolvedConstellationSegment(segment.FromHip, a, segment.ToHip, b));

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
                labelDirection,
                labelStarHips.ToArray()));
        }
    }

    private static void AddResolvedStarName(int hip, double3 direction, HashSet<int> namedStarHips) {
        if(namedStarHips.Add(hip) && hipToName.TryGetValue(hip, out string? name)) {
            resolvedNamedStars.Add(new ResolvedNamedStar(hip, name, direction));
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
        hipToPositionPc.Clear();
        hipToName.Clear();
        hipToInformation.Clear();
        _centralStarHip = 0;

        if(!File.Exists(path))
            return;

        using StreamReader reader = new StreamReader(path);

        string? headerLine = reader.ReadLine();

        if(headerLine == null)
            return;

        string[] headers = SplitCsvLine(headerLine);

        int idIndex = Array.IndexOf(headers, "id");
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
            string properName = parts[properIndex];
            string bayerRaw = parts[bayerIndex];
            bool hasProperName = !string.IsNullOrWhiteSpace(properName);
            bool hasBayer = !string.IsNullOrWhiteSpace(bayerRaw);

            bool hasRealHip = int.TryParse(
                hipText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsedHip) && parsedHip > 0;

            int hip;
            if(hasRealHip) {
                hip = parsedHip;
            } else if((hasProperName || hasBayer) &&
                idIndex >= 0 && idIndex < parts.Length &&
                int.TryParse(parts[idIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rowId)) {
                // Some named/Bayer catalog stars (e.g. Wolf 359) have no Hipparcos number at all. Synthesize
                // a stable id from the catalog's own row id so they can still be selected, oriented to, and
                // labeled - offset keeps synthetic ids clear of the real HIP range (max around 120,416).
                hip = SyntheticHipOffset + rowId;
            } else {
                continue;
            }

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

            double3 catalogDirection = StarDirectionConverter.RaDecToDirection(raHours, decDegrees);
            hipToDirection[hip] = catalogDirection;

            if(double.TryParse(
                parts[distanceIndex],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double distancePc) &&
                distancePc >= 0d && distancePc < 100000d) {
                hipToPositionPc[hip] = catalogDirection * distancePc;
            }

            string displayName = GetDisplayName(
                properName,
                parts[constellationIndex],
                bayerRaw,
                hip);
            hipToName[hip] = displayName;
            hipToInformation[hip] = new StarInformation(
                hip,
                displayName,
                ExpandBayerDesignation(bayerRaw),
                parts[constellationIndex],
                hasProperName,
                hasRealHip ? hip.ToString(CultureInfo.InvariantCulture) : string.Empty,
                FormatRightAscension(raHours),
                FormatSignedDeclination(parts[decIndex]),
                parts[magnitudeIndex],
                parts[absoluteMagnitudeIndex],
                parts[spectralTypeIndex],
                FormatDistanceLightYears(parts[distanceIndex]));

            if(string.Equals(properName.Trim(), "Sol", StringComparison.OrdinalIgnoreCase)) {
                _centralStarHip = hip;
            }
        }
    }

    // The catalog's "dist" column is in parsecs; the Information tab labels the field "Distance (LY)",
    // so it must be converted to light years before display.
    private const double ParsecsToLightYears = 3.26156377695d;

    private static string FormatDistanceLightYears(string distanceParsecs) {
        if(!double.TryParse(
            distanceParsecs,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsecs)) {
            return distanceParsecs;
        }

        return (parsecs * ParsecsToLightYears).ToString("0.###", CultureInfo.InvariantCulture);
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