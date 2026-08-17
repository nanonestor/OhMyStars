using Brutal.Numerics;

namespace StellariumCatalog;

public sealed class StellariumCatalogSettings {

    public StellariumCatalogSettings Clone() {
        return new StellariumCatalogSettings {
        };
    }
}
internal static class StellariumCatalogSettingsStore {
    private static SaveScopedSettingsStore<StellariumCatalogSettings>? _store;
    private static StellariumCatalogSettings _current = new();

    public static StellariumCatalogSettings Current {
        get {
            EnsureInitialized();
            return _current;
        }
    }

    public static void LoadForSave(string saveId) {
        EnsureInitialized();
        
        if(string.IsNullOrEmpty(saveId)) {
            _current = new StellariumCatalogSettings();
            return;
        }
        
        _store!.Load();
        _current = _store.GetCurrent(saveId).Clone();
    }

    public static void SaveForSave(string saveId) {
        EnsureInitialized();

        if(string.IsNullOrEmpty(saveId))
            return;

        _store!.Set(saveId, _current.Clone());
        _store.Save(saveId);
    }

    public static void SetCurrentFromDefaults() {
        _current = new StellariumCatalogSettings();
    }

    private static void EnsureInitialized() {
        if(_store == null)
            throw new InvalidOperationException("StellariumCatalogSettingsStore.Init() must be called before use.");
    }

    public static void Init() {
        string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string savesDir = Path.Combine(
            userDocs,
            "My Games",
            "Kitten Space Agency",
            "saves");

        _store = new SaveScopedSettingsStore<StellariumCatalogSettings>(
            savesDir,
            "StellariumCatalog_settings.toml",
            () => new StellariumCatalogSettings(),
            StellariumCatalogSettingsToml.Read,
            StellariumCatalogSettingsToml.Write);
    }

    public static void Load() {
        EnsureInitialized();
        _store!.Load();
    }

    public static void Save() {
        EnsureInitialized();
        _store!.Save();
    }
}

internal static class StellariumCatalogSettingsToml {
    public static StellariumCatalogSettings Read(SettingsBlock block) {
        var s = new StellariumCatalogSettings();

        return s;
    }

    public static void Write(
        SettingsBlockWriter writer,
        string saveId,
        StellariumCatalogSettings s) {

        writer.EndBlock();
    }
}
