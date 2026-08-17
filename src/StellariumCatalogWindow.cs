using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;
using ImGui = Brutal.ImGuiApi.ImGui;

namespace StellariumCatalog;

internal static class StellariumCatalogWindow {
    private const string SettingsFileName = "settings.ini";

    private static class Defaults {
        public const bool ShowTooltips = false;
        public const bool ShowStarNames = false;
        public const bool ShowIAUConstellations = false;
        public const bool ShowAsterisms = true;
        public const bool ShowAsterismNames = true;
        public const float IAULineOpacity = 0.4f;
        public static readonly float3 IAULineColor = new float3(1f, 1f, 1f);
        public const float AsterismLineOpacity = 0.4f;
        public static readonly float3 AsterismLineColor = new float3(0f, 0f, 1f);
        public const float AlignmentRotationXDegrees = -0.8f;
        public const float AlignmentRotationYDegrees = -0.1f;
        public const float AlignmentRotationZDegrees = 0f;
    }

    private static bool _showWindow = true;
    private static bool _showTooltips = Defaults.ShowTooltips;

    private static bool _iauColorExpanded = false;
    private static bool _asterismColorExpanded = false;

    private static readonly float2 DefaultWindowSize = new float2(420f, 320f);

    public static void ToggleWindow() {
        _showWindow = !_showWindow;
    }

    public static void LoadSettings() {
        string? settingsPath = GetSettingsFilePath();
        if(string.IsNullOrWhiteSpace(settingsPath)) {
            return;
        }

        if(!File.Exists(settingsPath)) {
            SaveSettings();
            return;
        }

        Dictionary<string, string> values = ReadSettingsFile(settingsPath);

        _showTooltips = ReadBool(values, "ShowTooltips", Defaults.ShowTooltips);
        StellariumRenderer.showStarNames = ReadBool(values, "ShowStarNames", Defaults.ShowStarNames);
        StellariumRenderer.showIAUConstellations = ReadBool(values, "ShowIAUConstellations", Defaults.ShowIAUConstellations);
        StellariumRenderer.showAsterisms = ReadBool(values, "ShowAsterisms", Defaults.ShowAsterisms);
        StellariumRenderer.showAsterismNames = ReadBool(values, "ShowAsterismNames", Defaults.ShowAsterismNames);

        StellariumRenderer.iauLineOpacity = ReadFloat(values, "IAULineOpacity", Defaults.IAULineOpacity);
        StellariumRenderer.iauLineColor = new float3(
            ReadFloat(values, "IAULineColorR", Defaults.IAULineColor.X),
            ReadFloat(values, "IAULineColorG", Defaults.IAULineColor.Y),
            ReadFloat(values, "IAULineColorB", Defaults.IAULineColor.Z));

        StellariumRenderer.asterismLineOpacity = ReadFloat(values, "AsterismLineOpacity", Defaults.AsterismLineOpacity);
        StellariumRenderer.asterismLineColor = new float3(
            ReadFloat(values, "AsterismLineColorR", Defaults.AsterismLineColor.X),
            ReadFloat(values, "AsterismLineColorG", Defaults.AsterismLineColor.Y),
            ReadFloat(values, "AsterismLineColorB", Defaults.AsterismLineColor.Z));

        StellariumRenderer.alignmentRotationXDegrees = ReadFloat(values, "AlignmentRotationXDegrees", Defaults.AlignmentRotationXDegrees);
        StellariumRenderer.alignmentRotationYDegrees = ReadFloat(values, "AlignmentRotationYDegrees", Defaults.AlignmentRotationYDegrees);
        StellariumRenderer.alignmentRotationZDegrees = ReadFloat(values, "AlignmentRotationZDegrees", Defaults.AlignmentRotationZDegrees);
    }

    public static void SaveSettings() {
        string? settingsPath = GetSettingsFilePath();
        if(string.IsNullOrWhiteSpace(settingsPath)) {
            return;
        }

        string[] lines = new[] {
            "[Window]",
            $"ShowTooltips={_showTooltips.ToString(CultureInfo.InvariantCulture)}",
            $"ShowStarNames={StellariumRenderer.showStarNames.ToString(CultureInfo.InvariantCulture)}",
            $"ShowIAUConstellations={StellariumRenderer.showIAUConstellations.ToString(CultureInfo.InvariantCulture)}",
            $"ShowAsterisms={StellariumRenderer.showAsterisms.ToString(CultureInfo.InvariantCulture)}",
            $"ShowAsterismNames={StellariumRenderer.showAsterismNames.ToString(CultureInfo.InvariantCulture)}",
            $"IAULineOpacity={StellariumRenderer.iauLineOpacity.ToString(CultureInfo.InvariantCulture)}",
            $"IAULineColorR={StellariumRenderer.iauLineColor.X.ToString(CultureInfo.InvariantCulture)}",
            $"IAULineColorG={StellariumRenderer.iauLineColor.Y.ToString(CultureInfo.InvariantCulture)}",
            $"IAULineColorB={StellariumRenderer.iauLineColor.Z.ToString(CultureInfo.InvariantCulture)}",
            $"AsterismLineOpacity={StellariumRenderer.asterismLineOpacity.ToString(CultureInfo.InvariantCulture)}",
            $"AsterismLineColorR={StellariumRenderer.asterismLineColor.X.ToString(CultureInfo.InvariantCulture)}",
            $"AsterismLineColorG={StellariumRenderer.asterismLineColor.Y.ToString(CultureInfo.InvariantCulture)}",
            $"AsterismLineColorB={StellariumRenderer.asterismLineColor.Z.ToString(CultureInfo.InvariantCulture)}",
            $"AlignmentRotationXDegrees={StellariumRenderer.alignmentRotationXDegrees.ToString(CultureInfo.InvariantCulture)}",
            $"AlignmentRotationYDegrees={StellariumRenderer.alignmentRotationYDegrees.ToString(CultureInfo.InvariantCulture)}",
            $"AlignmentRotationZDegrees={StellariumRenderer.alignmentRotationZDegrees.ToString(CultureInfo.InvariantCulture)}",
        };

        File.WriteAllLines(settingsPath, lines);
    }

    private static void ResetToDefaults() {
        _showTooltips = Defaults.ShowTooltips;
        StellariumRenderer.showIAUConstellations = Defaults.ShowIAUConstellations;
        StellariumRenderer.showAsterisms = Defaults.ShowAsterisms;
        StellariumRenderer.showAsterismNames = Defaults.ShowAsterismNames;
        StellariumRenderer.iauLineOpacity = Defaults.IAULineOpacity;
        StellariumRenderer.iauLineColor = Defaults.IAULineColor;
        StellariumRenderer.asterismLineOpacity = Defaults.AsterismLineOpacity;
        StellariumRenderer.asterismLineColor = Defaults.AsterismLineColor;
        StellariumRenderer.alignmentRotationXDegrees = Defaults.AlignmentRotationXDegrees;
        StellariumRenderer.alignmentRotationYDegrees = Defaults.AlignmentRotationYDegrees;
        StellariumRenderer.alignmentRotationZDegrees = Defaults.AlignmentRotationZDegrees;

        SaveSettings();
    }

    public static void Draw() {
        if(!_showWindow)
            return;

        // ConsoleStyle.BeginWindow forces NoSavedSettings, so a plain ImGui window is used instead to stay
        // resizable/movable/scrollable and to persist position+size in imgui.ini; ConsoleStyle only supplies widget colors.
        ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
        if(!ImGui.Begin("StellariumCatalog", ref _showWindow, ImGuiWindowFlags.None)) {
            ImGui.End();
            return;
        }

        ConsoleStyle.PushWidgetStyle();

        ConsoleWidgets.BeginRow("Show Tooltips");
        if(ConsoleWidgets.Checkbox("ShowTooltips", ref _showTooltips, pending: false)) {
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showStarNamesTop = StellariumRenderer.showStarNames;
        ConsoleWidgets.BeginRow("Show Star Names");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Draws the star name labels from the active sky culture.");
        }
        if(ConsoleWidgets.Checkbox("ShowStarNames", ref showStarNamesTop, pending: false)) {
            StellariumRenderer.showStarNames = showStarNamesTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showAsterismNamesTop = StellariumRenderer.showAsterismNames;
        ConsoleWidgets.BeginRow("Show Constellation Names");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Draws the constellation name labels from the active sky culture.");
        }
        if(ConsoleWidgets.Checkbox("ShowNames", ref showAsterismNamesTop, pending: false)) {
            StellariumRenderer.showAsterismNames = showAsterismNamesTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("IAU Constellations");
        ImGui.Separator();

        bool showIAUConstellations = StellariumRenderer.showIAUConstellations;
        ConsoleWidgets.BeginRow("IAU Constellations");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Draws the official IAU constellation boundary/line set.");
        }
        if(ConsoleWidgets.Checkbox("IAUConstellations", ref showIAUConstellations, pending: false)) {
            StellariumRenderer.showIAUConstellations = showIAUConstellations;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        float iauLineOpacity = StellariumRenderer.iauLineOpacity;
        ConsoleWidgets.BeginRow("IAU Line Brightness");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Adjusts the opacity/brightness of the IAU constellation lines.");
        }
        if(ConsoleWidgets.SliderFloat("IAULineOpacity", ref iauLineOpacity, 0f, 1f, iauLineOpacity.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            StellariumRenderer.iauLineOpacity = iauLineOpacity;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        DrawColorDropdown(
            "IAU Line Color",
            "IAUColor",
            ref _iauColorExpanded,
            () => StellariumRenderer.iauLineColor,
            color => {
                StellariumRenderer.iauLineColor = color;
                SaveSettings();
            });

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Asterisms");
        ImGui.Separator();

        bool showAsterisms = StellariumRenderer.showAsterisms;
        ConsoleWidgets.BeginRow("Show Asterisms");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Draws the asterism lines from the active sky culture.");
        }
        if(ConsoleWidgets.Checkbox("ShowAsterisms", ref showAsterisms, pending: false)) {
            StellariumRenderer.showAsterisms = showAsterisms;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        float asterismLineOpacity = StellariumRenderer.asterismLineOpacity;
        ConsoleWidgets.BeginRow("Asterism Line Brightness");
        if(_showTooltips && ConsoleWidgets.RowHovered) {
            ConsoleWidgets.Tooltip("Adjusts the opacity/brightness of the asterism lines.");
        }
        if(ConsoleWidgets.SliderFloat("AsterismLineOpacity", ref asterismLineOpacity, 0f, 1f, asterismLineOpacity.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            StellariumRenderer.asterismLineOpacity = asterismLineOpacity;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        DrawColorDropdown(
            "Asterism Line Color",
            "AsterismColor",
            ref _asterismColorExpanded,
            () => StellariumRenderer.asterismLineColor,
            color => {
                StellariumRenderer.asterismLineColor = color;
                SaveSettings();
            });

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Alignment");
        ImGui.Separator();

        DrawAlignmentAxisRow(
            "Rotation X",
            "AlignmentRotationX",
            "Fine-tunes the alignment of the lines with the game's star field by rotating around the X axis.",
            () => StellariumRenderer.alignmentRotationXDegrees,
            value => StellariumRenderer.alignmentRotationXDegrees = value);

        DrawAlignmentAxisRow(
            "Rotation Y",
            "AlignmentRotationY",
            "Fine-tunes the alignment of the lines with the game's star field by rotating around the Y axis.",
            () => StellariumRenderer.alignmentRotationYDegrees,
            value => StellariumRenderer.alignmentRotationYDegrees = value);

        DrawAlignmentAxisRow(
            "Rotation Z",
            "AlignmentRotationZ",
            "Fine-tunes the alignment of the lines with the game's star field by rotating around the Z axis.",
            () => StellariumRenderer.alignmentRotationZDegrees,
            value => StellariumRenderer.alignmentRotationZDegrees = value);

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Sky Culture");

        SkyCulture? activeSkyCulture = SkyCulturesRenderer.ActiveSkyCulture;
        string previewName = activeSkyCulture?.Name ?? "None";

        if(ImGui.BeginCombo("Sky Culture", previewName)) {
            for(int i = 0; i < SkyCulturesRenderer.SkyCultures.Count; i++) {
                SkyCulture skyCulture = SkyCulturesRenderer.SkyCultures[i];
                bool selected = i == SkyCulturesRenderer.ActiveSkyCultureIndex;

                if(ImGui.Selectable(skyCulture.Name, selected)) {
                    SkyCulturesRenderer.ActiveSkyCultureIndex = i;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.Separator();

        if(ConsoleWidgets.Button("Reset to Defaults")) {
            ResetToDefaults();
        }

        ConsoleStyle.PopWidgetStyle();
        ImGui.End();
    }

    private static void DrawAlignmentAxisRow(
        string rowLabel,
        string idPrefix,
        string tooltip,
        Func<float> getValue,
        Action<float> setValue) {

        float value = getValue();
        bool changed = false;
        
        ImGui.Text(rowLabel);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        if(ImGui.InputFloat("##" + idPrefix, ref value, 0f, 0f, "%.2f", ImGuiInputTextFlags.None)) {
            changed = true;
        }
        ImGui.SameLine();
        if(ConsoleWidgets.Button("-0.01", idPrefix + "Minus001", default)) {
            value -= 0.01f;
            changed = true;
        }
        ImGui.SameLine();
        if(ConsoleWidgets.Button("-0.1", idPrefix + "Minus01", default)) {
            value -= 0.1f;
            changed = true;
        }
        ImGui.SameLine();
        if(ConsoleWidgets.Button("+0.1", idPrefix + "Plus01", default)) {
            value += 0.1f;
            changed = true;
        }
        ImGui.SameLine();
        if(ConsoleWidgets.Button("+0.01", idPrefix + "Plus001", default)) {
            value += 0.01f;
            changed = true;
        }

        if(changed) {
            setValue(value);
            SaveSettings();
        }
    }

    private static void DrawColorDropdown(
        string rowLabel,
        string idPrefix,
        ref bool expanded,
        Func<float3> getColor,
        Action<float3> setColor) {

        ConsoleWidgets.BeginRow(rowLabel);
        string toggleLabel = expanded ? "Hide RGB \u25b2" : "Edit RGB \u25bc";
        if(ConsoleWidgets.Button(toggleLabel, idPrefix + "Toggle", default)) {
            expanded = !expanded;
        }
        ConsoleWidgets.EndRow();

        if(!expanded)
            return;

        float3 color = getColor();
        bool changed = false;

        float r = color.X;
        ConsoleWidgets.BeginRow("R");
        if(ConsoleWidgets.SliderFloat(idPrefix + "R", ref r, 0f, 1f, r.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            changed = true;
        }
        ConsoleWidgets.EndRow();

        float g = color.Y;
        ConsoleWidgets.BeginRow("G");
        if(ConsoleWidgets.SliderFloat(idPrefix + "G", ref g, 0f, 1f, g.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            changed = true;
        }
        ConsoleWidgets.EndRow();

        float b = color.Z;
        ConsoleWidgets.BeginRow("B");
        if(ConsoleWidgets.SliderFloat(idPrefix + "B", ref b, 0f, 1f, b.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            changed = true;
        }
        ConsoleWidgets.EndRow();

        if(changed) {
            setColor(new float3(r, g, b));
        }
    }

    private static string? GetSettingsFilePath() {
        Mod? mod = ModLibrary.Find("StellariumCatalog");
        if(mod is not null && mod != Mod.Empty && !string.IsNullOrWhiteSpace(mod.DirectoryPath)) {
            return Path.Combine(mod.DirectoryPath, SettingsFileName);
        }

        string? assemblyDirectory = Path.GetDirectoryName(typeof(StellariumCatalogWindow).Assembly.Location);
        if(!string.IsNullOrWhiteSpace(assemblyDirectory)) {
            return Path.Combine(assemblyDirectory, SettingsFileName);
        }

        return null;
    }

    private static Dictionary<string, string> ReadSettingsFile(string settingsPath) {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach(string rawLine in File.ReadAllLines(settingsPath)) {
            string line = rawLine.Trim();
            if(line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
               line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("[", StringComparison.Ordinal)) {
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if(separatorIndex <= 0) {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    private static bool ReadBool(Dictionary<string, string> values, string key, bool defaultValue) {
        if(values.TryGetValue(key, out string? rawValue) && bool.TryParse(rawValue, out bool parsed)) {
            return parsed;
        }

        return defaultValue;
    }

    private static float ReadFloat(Dictionary<string, string> values, string key, float defaultValue) {
        if(values.TryGetValue(key, out string? rawValue) &&
           float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsed)) {
            return parsed;
        }

        return defaultValue;
    }
}
