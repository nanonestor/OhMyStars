using Brutal.Numerics;

namespace OhMyStars;

public sealed class OhMyStarsSettings {

    public OhMyStarsSettings Clone() {
        return new OhMyStarsSettings {
        };
    }
}
internal static class OhMyStarsSettingsStore {
    private static SaveScopedSettingsStore<OhMyStarsSettings>? _store;
    private static OhMyStarsSettings _current = new();

    public static OhMyStarsSettings Current {
        get {
            EnsureInitialized();
            return _current;
        }
    }

    public static void LoadForSave(string saveId) {
        EnsureInitialized();
        
        if(string.IsNullOrEmpty(saveId)) {
            _current = new OhMyStarsSettings();
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
        _current = new OhMyStarsSettings();
    }

    private static void EnsureInitialized() {
        if(_store == null)
            throw new InvalidOperationException("OhMyStarsSettingsStore.Init() must be called before use.");
    }

    public static void Init() {
        string userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string savesDir = Path.Combine(
            userDocs,
            "My Games",
            "Kitten Space Agency",
            "saves");

        _store = new SaveScopedSettingsStore<OhMyStarsSettings>(
            savesDir,
            "OhMyStars_settings.toml",
            () => new OhMyStarsSettings(),
            OhMyStarsSettingsToml.Read,
            OhMyStarsSettingsToml.Write);
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

internal static class OhMyStarsSettingsToml {
    public static OhMyStarsSettings Read(SettingsBlock block) {
        var s = new OhMyStarsSettings();

        return s;
    }

    public static void Write(
        SettingsBlockWriter writer,
        string saveId,
        OhMyStarsSettings s) {

        writer.EndBlock();
    }
}
