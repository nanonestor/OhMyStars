namespace OhMyStars;

internal sealed class DeepSkyVisibilityCache(DeepSkyCatalog catalog) {
    private readonly (bool Enabled, double Cutoff)[] _types = new (bool, double)[DeepSkyCatalog.Types.Length];
    private bool _initialized;
    private bool _includeUnknown;
    private int _catalogMask;
    private DeepSkyCatalog.Entry[] _entries = [];

    public DeepSkyCatalog.Entry[] GetEntries(DeepSkySettings settings) {
        int catalogMask = 0;
        foreach(var (kind, enabled) in settings.Catalogs)
            if(enabled)
                catalogMask |= 1 << (int)kind;
        bool changed = !_initialized || _includeUnknown != settings.IncludeUnknownMagnitude || _catalogMask != catalogMask;
        for(int i = 0; i < _types.Length; i++) {
            var style = settings.Styles[DeepSkyCatalog.Types[i].Code];
            var current = (style.Enabled, style.Enabled ? Math.Round(style.MagnitudeCutoff, 2) : 0);
            changed |= _types[i] != current;
            _types[i] = current;
        }
        if(!changed)
            return _entries;

        List<DeepSkyCatalog.Entry> entries = [];
        foreach(var entry in catalog.Entries)
            if(settings.IsVisible(entry))
                entries.Add(entry);
        _entries = entries.ToArray();
        _includeUnknown = settings.IncludeUnknownMagnitude;
        _catalogMask = catalogMask;
        _initialized = true;
        return _entries;
    }
}
