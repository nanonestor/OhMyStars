using System.Globalization;

namespace OhMyStars;

internal sealed class DeepSkySettings {
    public const float DefaultMagnitudeCutoff = 12f;
    public const float MinMagnitudeCutoff = -5f;
    public const float MaxMagnitudeCutoff = 25f;

    internal sealed class TypeStyle(float red, float green, float blue) {
        public bool Enabled;
        public float MagnitudeCutoff = DefaultMagnitudeCutoff;
        public float Red = red;
        public float Green = green;
        public float Blue = blue;
    }

    public bool ShowHighlightCircles;
    public bool AlwaysShow;
    public bool ShowNebulaOutlines;
    public bool IncludeUnknownMagnitude = true;
    public Dictionary<DeepSkyCatalog.CatalogKind, bool> Catalogs { get; } =
        Enum.GetValues<DeepSkyCatalog.CatalogKind>().Where(kind => kind != DeepSkyCatalog.CatalogKind.All)
            .ToDictionary(kind => kind, _ => true);
    public Dictionary<string, TypeStyle> Styles { get; } = new(StringComparer.Ordinal);

    public bool AllTypesEnabled {
        get {
            foreach(var (code, style) in Styles)
                if(code is not ("Dup" or "NonEx") && !style.Enabled)
                    return false;
            return true;
        }
        set {
            foreach(var (code, style) in Styles)
                if(code is not ("Dup" or "NonEx"))
                    style.Enabled = value;
        }
    }

    public DeepSkySettings() {
        for(int i = 0; i < DeepSkyCatalog.Types.Length; i++) {
            float hue = i * 6f / DeepSkyCatalog.Types.Length;
            float rising = 0.35f + 0.65f * (hue - MathF.Floor(hue));
            float falling = 1.35f - rising;
            var (red, green, blue) = (int)hue switch {
                0 => (1f, rising, 0.35f), 1 => (falling, 1f, 0.35f),
                2 => (0.35f, 1f, rising), 3 => (0.35f, falling, 1f),
                4 => (rising, 0.35f, 1f), _ => (1f, 0.35f, falling)
            };
            Styles.Add(DeepSkyCatalog.Types[i].Code, new(red, green, blue));
        }
    }

    public bool IsVisible(DeepSkyCatalog.Entry entry) {
        TypeStyle style = Styles[entry.Type];
        if(!entry.CanDraw || !style.Enabled ||
            !(entry.Magnitude is double mag ? mag <= Math.Round(style.MagnitudeCutoff, 2) : IncludeUnknownMagnitude))
            return false;
        foreach(var (kind, enabled) in Catalogs)
            if(enabled && entry.InCatalog(kind))
                return true;
        return false;
    }

    public static DeepSkySettings Read(IReadOnlyDictionary<string, string> values) {
        DeepSkySettings settings = new();
        bool ReadBool(string key, bool fallback) =>
            values.TryGetValue(key, out string? text) && bool.TryParse(text, out bool value) ? value : fallback;
        float ReadFloat(string key, float fallback, float min, float max) =>
            values.TryGetValue(key, out string? text) &&
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && float.IsFinite(value)
                ? Math.Clamp(value, min, max) : fallback;
        settings.ShowHighlightCircles = ReadBool("DeepSkyShowHighlightCircles", settings.ShowHighlightCircles);
        settings.AlwaysShow = ReadBool("DeepSkyAlwaysShow", settings.AlwaysShow);
        settings.ShowNebulaOutlines = ReadBool("DeepSkyShowNebulaOutlines", settings.ShowNebulaOutlines);
        settings.IncludeUnknownMagnitude = ReadBool("DeepSkyIncludeUnknownMagnitude", settings.IncludeUnknownMagnitude);
        foreach(var kind in settings.Catalogs.Keys.ToArray())
            settings.Catalogs[kind] = ReadBool($"DeepSkyCatalog_{kind}", true);
        foreach(var (code, style) in settings.Styles) {
            string prefix = $"DeepSkyType_{code}_";
            style.Enabled = ReadBool(prefix + "Enabled", false);
            style.MagnitudeCutoff = ReadFloat(prefix + "Magnitude", DefaultMagnitudeCutoff, MinMagnitudeCutoff, MaxMagnitudeCutoff);
            style.Red = ReadFloat(prefix + "R", style.Red, 0, 1);
            style.Green = ReadFloat(prefix + "G", style.Green, 0, 1);
            style.Blue = ReadFloat(prefix + "B", style.Blue, 0, 1);
        }
        return settings;
    }

    public IEnumerable<string> Serialize() {
        yield return $"DeepSkyShowHighlightCircles={ShowHighlightCircles}";
        yield return $"DeepSkyAlwaysShow={AlwaysShow}";
        yield return $"DeepSkyShowNebulaOutlines={ShowNebulaOutlines}";
        yield return $"DeepSkyIncludeUnknownMagnitude={IncludeUnknownMagnitude}";
        foreach(var (kind, enabled) in Catalogs)
            yield return $"DeepSkyCatalog_{kind}={enabled}";
        foreach(var (code, style) in Styles) {
            string prefix = $"DeepSkyType_{code}_";
            yield return $"{prefix}Enabled={style.Enabled}";
            yield return $"{prefix}Magnitude={style.MagnitudeCutoff.ToString(CultureInfo.InvariantCulture)}";
            yield return $"{prefix}R={style.Red.ToString(CultureInfo.InvariantCulture)}";
            yield return $"{prefix}G={style.Green.ToString(CultureInfo.InvariantCulture)}";
            yield return $"{prefix}B={style.Blue.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
