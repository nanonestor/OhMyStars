using Brutal.Numerics;
using System.Globalization;
using System.Text;

namespace OhMyStars;

internal sealed class SaveScopedSettingsStore<TSettings> {
    private readonly Dictionary<string, TSettings> _bySaveId = new();

    private readonly string _fileName;
    private readonly string _savesDir;
    private readonly Func<TSettings> _createDefault;
    private readonly Func<SettingsBlock, TSettings> _readBlock;
    private readonly Action<SettingsBlockWriter, string, TSettings> _writeBlock;

    public SaveScopedSettingsStore(
        string savesDir,
        string fileName,
        Func<TSettings> createDefault,
        Func<SettingsBlock, TSettings> readBlock,
        Action<SettingsBlockWriter, string, TSettings> writeBlock) {
        _savesDir = savesDir;
        _fileName = fileName;
        _createDefault = createDefault;
        _readBlock = readBlock;
        _writeBlock = writeBlock;
    }

    public TSettings GetCurrent(string saveId) {
        if(!_bySaveId.TryGetValue(saveId, out TSettings? settings)) {
            settings = _createDefault();
            _bySaveId[saveId] = settings;
        }

        return settings;
    }

    public void Reset() {
        _bySaveId.Clear();
    }

    public void RekeyTransientTo(string newSaveId) {
        if(string.IsNullOrEmpty(newSaveId))
            return;

        if(!_bySaveId.TryGetValue(string.Empty, out TSettings? transient))
            return;

        _bySaveId.Remove(string.Empty);
        _bySaveId[newSaveId] = transient;
    }

    public void Load() {
        _bySaveId.Clear();

        if(!Directory.Exists(_savesDir))
            return;

        foreach(string saveDir in Directory.EnumerateDirectories(_savesDir)) {
            string saveId = Path.GetFileName(saveDir);
            if(string.IsNullOrEmpty(saveId))
                continue;

            string configPath = GetConfigPath(saveId);
            if(!File.Exists(configPath))
                continue;

            foreach(SettingsBlock block in SettingsBlockFile.Read(configPath)) {
                // Since the file is now already scoped to a save directory,
                // prefer the directory saveId. This also tolerates old/missing SaveId values.
                _bySaveId[saveId] = _readBlock(block);
                break;
            }
        }
    }

    public void Save() {
        foreach(var pair in _bySaveId) {
            string saveId = pair.Key;

            if(string.IsNullOrEmpty(saveId))
                continue;

            SaveOne(saveId, pair.Value);
        }
    }

    public void Save(string saveId) {
        if(string.IsNullOrEmpty(saveId))
            return;

        if(!_bySaveId.TryGetValue(saveId, out TSettings? settings))
            return;

        SaveOne(saveId, settings);
    }

    private void SaveOne(string saveId, TSettings settings) {
        string configPath = GetConfigPath(saveId);
        string saveDir = Path.GetDirectoryName(configPath)!;
        string tempPath = configPath + ".tmp";

        try {
            Directory.CreateDirectory(saveDir);

            using(var writer = new StreamWriter(tempPath)) {
                var blockWriter = new SettingsBlockWriter(writer);

                // Keep writing the saveId into the block if your existing parser/writer format expects it.
                // Even though the file is now save-scoped, this preserves compatibility.
                _writeBlock(blockWriter, saveId, settings);
            }

            File.Move(tempPath, configPath, overwrite: true);
        } catch {
            if(File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    private string GetConfigPath(string saveId) {
        return Path.Combine(_savesDir, saveId, _fileName);
    }

    public void Set(string saveId, TSettings settings) {
        if(string.IsNullOrEmpty(saveId))
            return;

        _bySaveId[saveId] = settings;
    }
}

internal sealed class SettingsBlock {
    private readonly Dictionary<string, string> _fields;

    public int HeaderLine { get; }

    public string SaveId =>
        GetString("save_id", string.Empty);

    public IReadOnlyDictionary<string, string> Fields => _fields;

    public SettingsBlock(
        Dictionary<string, string> fields,
        int headerLine) {
        _fields = fields;
        HeaderLine = headerLine;
    }

    public bool Has(string key) {
        return _fields.ContainsKey(key);
    }

    public string GetString(string key, string fallback = "") {
        if(_fields.TryGetValue(key, out string? value))
            return value;

        return fallback;
    }

    public bool GetBool(string key, bool fallback = false) {
        if(_fields.TryGetValue(key, out string? s) &&
            bool.TryParse(s, out bool value))
            return value;

        return fallback;
    }

    public int GetInt(string key, int fallback = 0) {
        if(_fields.TryGetValue(key, out string? s) &&
            int.TryParse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value))
            return value;

        return fallback;
    }

    public float GetFloat(string key, float fallback = 0f) {
        if(_fields.TryGetValue(key, out string? s) &&
            float.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value))
            return value;

        return fallback;
    }

    public bool TryEnum<T>(string key, out T value)
        where T : struct {
        if(_fields.TryGetValue(key, out string? s) &&
            Enum.TryParse(s, ignoreCase: true, out value))
            return true;

        value = default;
        return false;
    }

    public float4 GetFloat4(string key, float4 fallback) {
        if(!_fields.TryGetValue(key, out string? s))
            return fallback;

        s = s.Trim();

        if(!s.StartsWith("[") || !s.EndsWith("]"))
            return fallback;

        string inner = s.Substring(1, s.Length - 2);
        string[] parts = inner.Split(',');

        if(parts.Length != 4)
            return fallback;

        if(!float.TryParse(parts[0].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float x))
            return fallback;

        if(!float.TryParse(parts[1].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float y))
            return fallback;

        if(!float.TryParse(parts[2].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float z))
            return fallback;

        if(!float.TryParse(parts[3].Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float w))
            return fallback;

        return new float4(x, y, z, w);
    }
}

internal sealed class SettingsBlockWriter {
    private readonly StreamWriter _writer;

    public SettingsBlockWriter(StreamWriter writer) {
        _writer = writer;
    }

    public void BeginSettingsBlock(string saveId) {
        _writer.WriteLine("[[settings]]");
        Write("save_id", saveId);
    }

    public void EndBlock() {
        _writer.WriteLine();
    }

    public void Write(string key, string value) {
        _writer.WriteLine($"{key} = \"{Escape(value)}\"");
    }

    public void Write(string key, bool value) {
        _writer.WriteLine($"{key} = {(value ? "true" : "false")}");
    }

    public void Write(string key, int value) {
        _writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} = {1}",
            key,
            value));
    }

    public void Write(string key, double value) {
        _writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} = {1:R}",
            key,
            value));
    }

    public void Write(string key, float4 value) {
        _writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} = [{1:R}, {2:R}, {3:R}, {4:R}]",
            key,
            value.X,
            value.Y,
            value.Z,
            value.W));
    }

    public void Write<TEnum>(string key, TEnum value)
        where TEnum : struct, Enum {
        Write(key, value.ToString());
    }

    public void WriteFloat4(string key, float4 value) {
        _writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0} = [{1:R}, {2:R}, {3:R}, {4:R}]",
            key,
            value.X,
            value.Y,
            value.Z,
            value.W));
    }

    private static string Escape(string s) {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}

internal static class SettingsBlockFile {
    public static List<SettingsBlock> Read(string path) {
        var blocks = new List<SettingsBlock>();

        PendingBlock? current = null;
        string[] lines = File.ReadAllLines(path);

        for(int li = 0; li < lines.Length; li++) {
            string line = lines[li].Trim();
            int lineNumber = li + 1;

            if(line.Length == 0 || line[0] == '#')
                continue;

            if(line == "[[settings]]") {
                Flush(current, blocks);
                current = new PendingBlock(lineNumber);
                continue;
            }

            if(line.Length > 0 && line[0] == '[') {
                Flush(current, blocks);
                current = null;
                continue;
            }

            if(current == null)
                continue;

            int eq = line.IndexOf('=');

            if(eq < 1)
                continue;

            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();

            if(val.Length >= 2 && val[0] == '"') {
                int closeIdx = FindClosingQuote(val, 0);

                if(closeIdx < 0)
                    continue;

                val = Unescape(val.Substring(1, closeIdx - 1));
            } else {
                int commentIdx = val.IndexOf('#');

                if(commentIdx >= 0)
                    val = val.Substring(0, commentIdx).Trim();
            }

            current.Fields[key] = val;
        }

        Flush(current, blocks);
        return blocks;
    }

    private static void Flush(
        PendingBlock? pending,
        List<SettingsBlock> sink) {
        if(pending == null)
            return;

        sink.Add(new SettingsBlock(
            pending.Fields,
            pending.HeaderLine));
    }

    private sealed class PendingBlock {
        public readonly Dictionary<string, string> Fields = new();

        public int HeaderLine { get; }

        public PendingBlock(int headerLine) {
            HeaderLine = headerLine;
        }
    }

    private static string Unescape(string s) {
        StringBuilder sb = new StringBuilder(s.Length);

        for(int i = 0; i < s.Length; i++) {
            char c = s[i];

            if(c == '\\' && i + 1 < s.Length) {
                char next = s[++i];

                if(next == '\\')
                    sb.Append('\\');
                else if(next == '"')
                    sb.Append('"');
                else {
                    sb.Append('\\');
                    sb.Append(next);
                }
            } else {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static int FindClosingQuote(string s, int openAt) {
        for(int i = openAt + 1; i < s.Length; i++) {
            char c = s[i];

            if(c == '\\' && i + 1 < s.Length) {
                i++;
                continue;
            }

            if(c == '"')
                return i;
        }

        return -1;
    }
}
