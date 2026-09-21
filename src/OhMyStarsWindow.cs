using Brutal.ImGuiApi;
using Brutal.Logging;
using Brutal.Numerics;
using KSA;
using System.Globalization;
using ImGui = Brutal.ImGuiApi.ImGui;

namespace OhMyStars;

internal static class OhMyStarsWindow {
    private const string SettingsFileName = "settings.ini";

    private enum WindowDisplay {
        Settings,
        Information,
        SpectrumLegend
    }

    private static class Defaults {
        public const bool ShowStarNames = false;
        public const bool ShowIAUConstellations = false;
        public const bool ShowAsterisms = true;
        public const bool ShowAsterismNames = true;
        public const bool ShowNavballMarker = false;
        public const bool ShowNavballMarkerLabel = true;
        public const bool ShowRaDecGrid = false;
        public const int DecSpacingDegrees = 10;
        public const bool ShowRaMinuteBands = false;
        public const float GridLineThickness = 1f;
        public const WindowDisplay Tab = WindowDisplay.Settings;
        public const float IAULineOpacity = 0.4f;
        public static readonly float3 IAULineColor = new float3(1f, 1f, 1f);
        public const float AsterismLineOpacity = 0.4f;
        public static readonly float3 AsterismLineColor = new float3(0f, 0f, 1f);
        public const float AlignmentRotationXDegrees = 0f;
        public const float AlignmentRotationYDegrees = 0f;
        public const float AlignmentRotationZDegrees = 0f;
        public const bool StarPointerDualLines = false;
        public static readonly float3 StarPointerColor = new float3(1f, 1f, 1f);
        public const bool StarPointerRainbow = false;
    }

    private static bool _showWindow = true;

    private static bool _skyCultureExpanded = false;
    private static bool _asterismsExpanded = false;
    private static bool _iauConstellationsExpanded = false;
    private static bool _raDecGridExpanded = false;
    private static bool _starPointerExpanded = false;
    private static bool _alignmentExpanded = false;
    private static WindowDisplay _display = WindowDisplay.Settings;
    private static string? _selectedConstellationId;
    private static int _selectedStarHip;
    private static bool _showStarPointer;
    private static float2 _windowPosition;
    private static float2 _windowSize;

    // Per-vehicle Information tab selection: each vehicle remembers the constellation/star the user
    // last had selected while controlling it, so switching control restores that vehicle's selection
    // instead of leaving a stale one behind. Keyed by vehicle; vehicles with no entry fall back to
    // the global selection.
    private static readonly Dictionary<Vehicle, (string? ConstellationId, int StarHip)> _vehicleSelections = new();
    private static Vehicle? _lastSelectionVehicle;

    // Per-vehicle star orientation memory: each vehicle (regular vessel or kitten) independently
    // remembers whether the mod's star orientation mode is engaged on it and which star it tracks.
    // The game's own attitude modes are already per-vehicle state on each FlightComputer; this map
    // is the mod-side equivalent, so switching control neither transfers star orientation to another
    // vehicle nor loses the state a vehicle was left in.
    private static readonly Dictionary<Vehicle, int> _orientedVehicles = new();

    // The angular-rate command the vehicle had in CustomAttitudeTarget before the mod overwrote it
    // with star-pointing euler angles. The stock game's rate mode reads CustomAttitudeTarget as the
    // commanded body rate and its buttons never touch the field (it is the player's persistent rate
    // command), so the mod must restore the pre-star value on every exit or rate-hold spins wildly.
    private static readonly Dictionary<Vehicle, double3> _savedRateCommands = new();

    // The attitude mode the vehicle was in when star orientation got engaged on it. Pressing the
    // Orient to Star button again restores this previous mode, making the button a toggle back out
    // of the mod's mode into whatever the vehicle had before (rate-hold included).
    private static readonly Dictionary<Vehicle, PreviousAttitudeMode> _previousAttitudeModes = new();

    // Vehicles pending disengage: their orientation entry stays registered so the mod's
    // PrepareWorker reapply performs the disengage itself (restoring the previous mode) on the next
    // tick instead of re-applying the star one final time with stale angles.
    private static readonly HashSet<Vehicle> _pendingDisengage = new();

    private sealed record PreviousAttitudeMode {
        public required FlightComputerAttitudeMode AttitudeMode;
        public required VehicleReferenceFrame AttitudeFrame;
        public required FlightComputerAttitudeTrackTarget AttitudeTrackTarget;
        public required double3 CustomAttitudeTarget;
    }

    private static readonly float2 DefaultWindowSize = new float2(860f, 1000f);

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

        StellariumRenderer.showStarNames = ReadBool(values, "ShowStarNames", Defaults.ShowStarNames);
        StellariumRenderer.showIAUConstellations = ReadBool(values, "ShowIAUConstellations", Defaults.ShowIAUConstellations);
        StellariumRenderer.showAsterisms = ReadBool(values, "ShowAsterisms", Defaults.ShowAsterisms);
        StellariumRenderer.showAsterismNames = ReadBool(values, "ShowAsterismNames", Defaults.ShowAsterismNames);
        NavballMarkerRenderer.showNavballMarker = ReadBool(values, "ShowNavballMarker", Defaults.ShowNavballMarker);
        NavballMarkerRenderer.showMarkerLabel = ReadBool(values, "ShowNavballMarkerLabel", Defaults.ShowNavballMarkerLabel);
        StellariumRenderer.showRaDecGrid = ReadBool(values, "ShowRaDecGrid", Defaults.ShowRaDecGrid);
        int decSpacing = ReadInt(values, "DecSpacingDegrees", Defaults.DecSpacingDegrees);
        StellariumRenderer.decSpacingDegrees = decSpacing is 1 or 5 ? decSpacing : Defaults.DecSpacingDegrees;
        StellariumRenderer.showRaMinuteBands = ReadBool(values, "ShowRaMinuteBands", Defaults.ShowRaMinuteBands);
        StellariumRenderer.gridLineThickness = ReadFloat(values, "GridLineThickness", Defaults.GridLineThickness);

        _display = ReadTab(values, "Tab", Defaults.Tab);

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

        StellariumRenderer.starPointerDualLines = ReadBool(values, "StarPointerDualLines", Defaults.StarPointerDualLines);
        StellariumRenderer.starPointerRainbow = ReadBool(values, "StarPointerRainbow", Defaults.StarPointerRainbow);
        StellariumRenderer.starPointerColor = new float3(
            ReadFloat(values, "StarPointerColorR", Defaults.StarPointerColor.X),
            ReadFloat(values, "StarPointerColorG", Defaults.StarPointerColor.Y),
            ReadFloat(values, "StarPointerColorB", Defaults.StarPointerColor.Z));
    }

    public static void SaveSettings() {
        string? settingsPath = GetSettingsFilePath();
        if(string.IsNullOrWhiteSpace(settingsPath)) {
            return;
        }

        string[] lines = new[] {
            "[Window]",
            $"Tab={GetPersistableTab()}",
            $"ShowStarNames={StellariumRenderer.showStarNames.ToString(CultureInfo.InvariantCulture)}",
            $"ShowIAUConstellations={StellariumRenderer.showIAUConstellations.ToString(CultureInfo.InvariantCulture)}",
            $"ShowAsterisms={StellariumRenderer.showAsterisms.ToString(CultureInfo.InvariantCulture)}",
            $"ShowAsterismNames={StellariumRenderer.showAsterismNames.ToString(CultureInfo.InvariantCulture)}",
            $"ShowNavballMarker={NavballMarkerRenderer.showNavballMarker.ToString(CultureInfo.InvariantCulture)}",
            $"ShowNavballMarkerLabel={NavballMarkerRenderer.showMarkerLabel.ToString(CultureInfo.InvariantCulture)}",
            $"ShowRaDecGrid={StellariumRenderer.showRaDecGrid.ToString(CultureInfo.InvariantCulture)}",
            $"DecSpacingDegrees={StellariumRenderer.decSpacingDegrees.ToString(CultureInfo.InvariantCulture)}",
            $"ShowRaMinuteBands={StellariumRenderer.showRaMinuteBands.ToString(CultureInfo.InvariantCulture)}",
            $"GridLineThickness={StellariumRenderer.gridLineThickness.ToString(CultureInfo.InvariantCulture)}",
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
            $"StarPointerDualLines={StellariumRenderer.starPointerDualLines.ToString(CultureInfo.InvariantCulture)}",
            $"StarPointerColorR={StellariumRenderer.starPointerColor.X.ToString(CultureInfo.InvariantCulture)}",
            $"StarPointerColorG={StellariumRenderer.starPointerColor.Y.ToString(CultureInfo.InvariantCulture)}",
            $"StarPointerColorB={StellariumRenderer.starPointerColor.Z.ToString(CultureInfo.InvariantCulture)}",
            $"StarPointerRainbow={StellariumRenderer.starPointerRainbow.ToString(CultureInfo.InvariantCulture)}",
        };

        File.WriteAllLines(settingsPath, lines);
    }

    private static void ResetToDefaults() {
        StellariumRenderer.showIAUConstellations = Defaults.ShowIAUConstellations;
        StellariumRenderer.showAsterisms = Defaults.ShowAsterisms;
        StellariumRenderer.showAsterismNames = Defaults.ShowAsterismNames;
        NavballMarkerRenderer.showNavballMarker = Defaults.ShowNavballMarker;
        NavballMarkerRenderer.showMarkerLabel = Defaults.ShowNavballMarkerLabel;
        StellariumRenderer.showRaDecGrid = Defaults.ShowRaDecGrid;
        StellariumRenderer.decSpacingDegrees = Defaults.DecSpacingDegrees;
        StellariumRenderer.showRaMinuteBands = Defaults.ShowRaMinuteBands;
        StellariumRenderer.gridLineThickness = Defaults.GridLineThickness;
        _display = Defaults.Tab;
        StellariumRenderer.iauLineOpacity = Defaults.IAULineOpacity;
        StellariumRenderer.iauLineColor = Defaults.IAULineColor;
        StellariumRenderer.asterismLineOpacity = Defaults.AsterismLineOpacity;
        StellariumRenderer.asterismLineColor = Defaults.AsterismLineColor;
        StellariumRenderer.alignmentRotationXDegrees = Defaults.AlignmentRotationXDegrees;
        StellariumRenderer.alignmentRotationYDegrees = Defaults.AlignmentRotationYDegrees;
        StellariumRenderer.alignmentRotationZDegrees = Defaults.AlignmentRotationZDegrees;
        StellariumRenderer.starPointerDualLines = Defaults.StarPointerDualLines;
        StellariumRenderer.starPointerColor = Defaults.StarPointerColor;
        StellariumRenderer.starPointerRainbow = Defaults.StarPointerRainbow;

        SaveSettings();
    }

    public static void Draw() {
        if(!_showWindow)
            return;

        // ConsoleStyle.BeginWindow forces NoSavedSettings, so a plain ImGui window is used instead to stay
        // resizable/movable/scrollable and to persist position+size in imgui.ini; ConsoleStyle only supplies widget colors.
        ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(1f);
        if(!ImGui.Begin("Oh My Stars", ref _showWindow, ImGuiWindowFlags.None)) {
            ImGui.End();
            return;
        }

        _windowPosition = ImGui.GetWindowPos();
        _windowSize = ImGui.GetWindowSize();

        ConsoleStyle.PushWidgetStyle();

        DrawTabButton("Settings", WindowDisplay.Settings);
        ImGui.SameLine();
        DrawTabButton("Information", WindowDisplay.Information);
        ImGui.Separator();

        if(_display == WindowDisplay.Information) {
            DrawInformation();
            ConsoleStyle.PopWidgetStyle();
            ImGui.End();
            return;
        }
        if(_display == WindowDisplay.SpectrumLegend) {
            DrawSpectrumLegend();
            ConsoleStyle.PopWidgetStyle();
            ImGui.End();
            return;
        }

        bool showStarNamesTop = StellariumRenderer.showStarNames;
        ConsoleWidgets.BeginRow("Show Star Names");
        if(ConsoleWidgets.Checkbox("ShowStarNames", ref showStarNamesTop, pending: false)) {
            StellariumRenderer.showStarNames = showStarNamesTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showAsterismNamesTop = StellariumRenderer.showAsterismNames;
        ConsoleWidgets.BeginRow("Show Constellation Names");
        if(ConsoleWidgets.Checkbox("ShowNames", ref showAsterismNamesTop, pending: false)) {
            StellariumRenderer.showAsterismNames = showAsterismNamesTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showAsterismsTop = StellariumRenderer.showAsterisms;
        ConsoleWidgets.BeginRow("Show Asterisms");
        if(ConsoleWidgets.Checkbox("ShowAsterisms", ref showAsterismsTop, pending: false)) {
            StellariumRenderer.showAsterisms = showAsterismsTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showIAUConstellationsTop = StellariumRenderer.showIAUConstellations;
        ConsoleWidgets.BeginRow("Show Constellation Boundaries");
        if(ConsoleWidgets.Checkbox("ShowConstellationBoundaries", ref showIAUConstellationsTop, pending: false)) {
            StellariumRenderer.showIAUConstellations = showIAUConstellationsTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool showRaDecGridTop = StellariumRenderer.showRaDecGrid;
        ConsoleWidgets.BeginRow("Show RA/Dec Grid");
        if(ConsoleWidgets.Checkbox("ShowRaDecGrid", ref showRaDecGridTop, pending: false)) {
            StellariumRenderer.showRaDecGrid = showRaDecGridTop;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        DrawThickSeparator();
        DrawSectionHeader("Sky Culture", "Selects the Stellarium sky culture that provides the asterism lines, constellation names, and star labels.", ref _skyCultureExpanded);

        if(_skyCultureExpanded) {
            ImGui.Separator();
            BeginEntryIndent();

            SkyCulture? activeSkyCulture = SkyCulturesRenderer.ActiveSkyCulture;
            string previewName = activeSkyCulture?.Name ?? "None";

            if(ImGui.BeginCombo("Sky Culture", previewName)) {
                // Pin a duplicate "Modern" entry at the top of the list for quick access; both this
                // and its regular entry select the same culture, the rest of the list keeps its
                // original order.
                int modernIndex = FindSkyCultureIndexByName("Modern");
                if(modernIndex >= 0) {
                    bool modernSelected = modernIndex == SkyCulturesRenderer.ActiveSkyCultureIndex;
                    // The ## suffix keeps the displayed text identical to the regular Modern entry
                    // while giving this pinned duplicate its own ImGui ID.
                    if(ImGui.Selectable(SkyCulturesRenderer.SkyCultures[modernIndex].Name + "##PinnedModern", modernSelected)) {
                        SkyCulturesRenderer.ActiveSkyCultureIndex = modernIndex;
                    }
                }

                for(int i = 0; i < SkyCulturesRenderer.SkyCultures.Count; i++) {
                    SkyCulture skyCulture = SkyCulturesRenderer.SkyCultures[i];
                    bool selected = i == SkyCulturesRenderer.ActiveSkyCultureIndex;

                    if(ImGui.Selectable(skyCulture.Name, selected)) {
                        SkyCulturesRenderer.ActiveSkyCultureIndex = i;
                    }
                }

                ImGui.EndCombo();
            }
            EndEntryIndent();
        }

        DrawThickSeparator();
        DrawSectionHeader("Asterisms", "Asterisms are culturally defined patterns that connect selected stars into familiar shapes and constellations.", ref _asterismsExpanded);

        if(_asterismsExpanded) {
            ImGui.Separator();
            BeginEntryIndent();

            float asterismLineOpacity = StellariumRenderer.asterismLineOpacity;
            ConsoleWidgets.BeginRow("Asterism Line Brightness");
            if(ConsoleWidgets.SliderFloat("AsterismLineOpacity", ref asterismLineOpacity, 0f, 1f, asterismLineOpacity.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
                StellariumRenderer.asterismLineOpacity = asterismLineOpacity;
                SaveSettings();
            }
            ConsoleWidgets.EndRow();

            DrawColorEditRow(
                "Asterism Line Color",
                "AsterismColor",
                () => StellariumRenderer.asterismLineColor,
                color => {
                    StellariumRenderer.asterismLineColor = color;
                    SaveSettings();
                });
            EndEntryIndent();
        }

        DrawThickSeparator();
        DrawSectionHeader("IAU Constellations", "The International Astronomical Union's modern 88-constellation scheme and its standardized constellation boundaries.", ref _iauConstellationsExpanded);

        if(_iauConstellationsExpanded) {
            ImGui.Separator();
            BeginEntryIndent();

            float iauLineOpacity = StellariumRenderer.iauLineOpacity;
            ConsoleWidgets.BeginRow("IAU Line Brightness");
            if(ConsoleWidgets.SliderFloat("IAULineOpacity", ref iauLineOpacity, 0f, 1f, iauLineOpacity.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
                StellariumRenderer.iauLineOpacity = iauLineOpacity;
                SaveSettings();
            }
            ConsoleWidgets.EndRow();

            DrawColorEditRow(
                "IAU Line Color",
                "IAUColor",
                () => StellariumRenderer.iauLineColor,
                color => {
                    StellariumRenderer.iauLineColor = color;
                    SaveSettings();
                });
            EndEntryIndent();
        }

        DrawThickSeparator();
        DrawSectionHeader("RA/Dec Grid", "Draws celestial coordinate lines of right ascension and declination over the sky background.", ref _raDecGridExpanded);

        if(_raDecGridExpanded) {
            ImGui.Separator();
            BeginEntryIndent();

            ImGui.Text("Dec Spacing (degrees)");
            DrawDecSpacingButton(1);
            ImGui.SameLine();
            DrawDecSpacingButton(5);
            ImGui.SameLine();
            DrawDecSpacingButton(10);

            if(StellariumRenderer.showRaMinuteBands) {
                if(ConsoleWidgets.PositiveButton("RA 10-Minute Bands", "RaMinuteBands", default)) {
                    StellariumRenderer.showRaMinuteBands = false;
                    SaveSettings();
                }
                ImGui.SameLine();
                ImGui.TextColored(in ConsoleStyle.Positive, "Bands On");
            } else {
                if(ConsoleWidgets.Button("RA 10-Minute Bands", "RaMinuteBands", default)) {
                    StellariumRenderer.showRaMinuteBands = true;
                    SaveSettings();
                }
            }

            float gridLineThickness = StellariumRenderer.gridLineThickness;
            ConsoleWidgets.BeginRow("Grid Line Thickness");
            if(ConsoleWidgets.SliderFloat("GridLineThickness", ref gridLineThickness, 0.5f, 5f, gridLineThickness.ToString("F1", CultureInfo.InvariantCulture), pending: false)) {
                StellariumRenderer.gridLineThickness = gridLineThickness;
                SaveSettings();
            }
            ConsoleWidgets.EndRow();
            EndEntryIndent();
        }

        DrawThickSeparator();
        DrawSectionHeader("Star Pointer", "Point from the window to the selected star.", ref _starPointerExpanded);

        if(_starPointerExpanded) {
            ImGui.Separator();
            BeginEntryIndent();
            DrawStarPointerSection();
            EndEntryIndent();
        }

        DrawThickSeparator();
        DrawSectionHeader("Alignment", "Fine-tunes the rotation of the asterism and IAU constellation lines - mainly a debug feature.", ref _alignmentExpanded);

        if(_alignmentExpanded) {
            ImGui.Separator();
            BeginEntryIndent();

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
            EndEntryIndent();
        }

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.Separator();

        if(ConsoleWidgets.Button("Reset to Defaults")) {
            ResetToDefaults();
        }

        ConsoleStyle.PopWidgetStyle();
        ImGui.End();
    }

    // Thicker category rule replacing a plain Separator between sections. ImGui's Separator draws
    // a 1px hairline on the line at the current cursor Y and then advances past it; this paints a
    // 3px rule centered on that same separator line position, so it sits exactly where the normal
    // separator would instead of overlapping the widgets below.
    private static void DrawThickSeparator() {
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        float2 pos = ImGui.GetCursorScreenPos();
        float startX = pos.X + ImGui.GetCursorPosX();
        float endX = startX + ImGui.GetContentRegionAvail().X;
        float y = pos.Y;
        uint color = ConsoleStyle.ApplyAlpha(ConsoleStyle.HairlineU32);

        drawList.AddLine(new float2(startX, y), new float2(endX, y), color, 3f);

        // Reserve the separator's vertical slot so the widgets below start below the rule.
        ImGui.Dummy(new float2(0f, 3f));
    }

    // Declination band spacing buttons: the active spacing is drawn as a filled positive button,
    // the others as plain buttons; clicking one sets how many degrees apart the Dec parallels are.
    private static void DrawDecSpacingButton(int spacingDegrees) {
        string label = spacingDegrees.ToString(CultureInfo.InvariantCulture);
        bool active = StellariumRenderer.decSpacingDegrees == spacingDegrees;
        bool clicked = active
            ? ConsoleWidgets.PositiveButton(label, "DecSpacing" + label, default)
            : ConsoleWidgets.Button(label, "DecSpacing" + label, default);

        if(clicked && !active) {
            StellariumRenderer.decSpacingDegrees = spacingDegrees;
            SaveSettings();
        }
    }

    // Collapsible section header: a button that toggles the items below it on and off, in the same
    // style the Alignment section uses ("v" expanded, ">" collapsed), plus the "(?)" help tooltip.
    // The button is indented two text spaces from the left edge to visually set it apart.
    private static void DrawSectionHeader(string title, string helpText, ref bool expanded) {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.CalcTextSize("  ").X);

        if(ImGui.Button(expanded ? title + " v" : title + " >")) {
            expanded = !expanded;
        }

        ImGui.SameLine();
        ImGui.Text("(?)");
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.None)) {
            ConsoleWidgets.Tooltip(helpText);
        }
    }

    // Indents every widget row inside an expanded category four text spaces from the left edge.
    // ImGui.Indent shifts the cursor origin persistently (unlike a one-off SetCursorPosX), which is
    // what makes it stick for ConsoleWidgets.BeginRow rows: BeginRowCore captures the cursor as the
    // row origin and EndRow resets back to it, so a pre-BeginRow nudge would only survive one row.
    private static float EntryIndentWidth => ImGui.CalcTextSize("    ").X;
    private static void BeginEntryIndent() {
        ImGui.Indent(EntryIndentWidth);
    }
    private static void EndEntryIndent() {
        ImGui.Unindent(EntryIndentWidth);
    }

    // Star Pointer settings: one line from the nearest window corner or one from each of the two
    // nearest corners, the line color, and a rainbow override that cycles the line color through
    // the spectrum while the pointer is drawn.
    private static void DrawStarPointerSection() {
        bool dualLines = StellariumRenderer.starPointerDualLines;
        ConsoleWidgets.BeginRow("Pointer Lines");
        if(ConsoleWidgets.Button(dualLines ? "Two Lines" : "One Line", "StarPointerLineMode", default)) {
            StellariumRenderer.starPointerDualLines = !dualLines;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        bool rainbow = StellariumRenderer.starPointerRainbow;
        float3 pointerColor = StellariumRenderer.starPointerColor;
        ConsoleWidgets.BeginRow("Pointer Color");
        using(new ImGuiDisabledScope(rainbow)) {
            if(ImGui.ColorEdit3(new ImString("##StarPointerColor"), ref pointerColor, ImGuiColorEditFlags.NoInputs)) {
                StellariumRenderer.starPointerColor = pointerColor;
                SaveSettings();
            }
        }
        ConsoleWidgets.EndRow();

        if(rainbow) {
            if(ConsoleWidgets.PositiveButton("Rainbow Spectrum", "StarPointerRainbow", default)) {
                StellariumRenderer.starPointerRainbow = false;
                SaveSettings();
            }
        } else {
            if(ConsoleWidgets.Button("Rainbow Spectrum", "StarPointerRainbow", default)) {
                StellariumRenderer.starPointerRainbow = true;
                SaveSettings();
            }
        }
        if(rainbow) {
            ImGui.SameLine();
            ImGui.TextColored(in ConsoleStyle.Positive, "Rainbow On");
        }
    }

    // Same as ConsoleWidgets.RegionHeader (uppercase muted label plus trailing hairline rule),
    // but with the label font scaled up so the selected star name stands out.
    private static void DrawRegionHeaderLarge(string label, float scale) {
        string upper = label.ToUpperInvariant();

        ConsoleStyle.PushLabelFont();
        float fontSize = ImGui.GetFontSize() * scale;
        ConsoleStyle.PopFont();

        ImGui.PushFont(ImGui.GetFont(), fontSize);
        float2 cursorScreenPos = ImGui.GetCursorScreenPos();
        ImGui.TextColored(in ConsoleStyle.TextMuted, upper);
        float textWidth = ImGui.CalcTextSize(upper).X;
        ImGui.PopFont();

        float ruleY = cursorScreenPos.Y + fontSize * 0.5f;
        float ruleStart = cursorScreenPos.X + textWidth + ConsoleWidgets.RegionHeaderRuleGapPx;
        float ruleEnd = cursorScreenPos.X + ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();
        if(ruleEnd > ruleStart) {
            ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
            windowDrawList.AddLine(new float2(ruleStart, ruleY), new float2(ruleEnd, ruleY), ConsoleStyle.ApplyAlpha(ConsoleStyle.HairlineU32), ConsoleStyle.WindowBorderThicknessPx);
        }
    }

    // The active tab is shaded with a filled button in the same color the game uses for ImGui
    // window title bars, read live from the ImGui style so it follows any theme change.
    private static void DrawTabButton(string label, WindowDisplay tab) {
        bool selected = _display == tab;
        bool clicked = selected
            ? DrawColoredTabButton(label)
            : ConsoleWidgets.Button(label, label, default);

        if(clicked && !selected) {
            _display = tab;
            SaveSettings();
        }
    }

    // Same layout/rounding/padding as ConsoleWidgets' filled buttons (see PrimaryButton in
    // ConsoleWidgets.DrawButtonCore), but filled with the window title bar color and white text.
    private static bool DrawColoredTabButton(string label) {
        ConsoleStyle.PushLabelFont();
        float2 textSize = ImGui.CalcTextSize(label);
        float fontSize = ImGui.GetFontSize();
        ConsoleStyle.PopFont();

        float2 size = new float2(
            textSize.X + ConsoleWidgets.ButtonHorizontalPaddingPx * 2f,
            fontSize + ConsoleWidgets.ButtonVerticalPaddingPx * 2f);
        float2 pMin = ImGui.GetCursorScreenPos();
        float2 pMax = pMin + size;

        bool clicked = ImGui.InvisibleButton(new ImString($"##Tab_{label}"), in size, ImGuiButtonFlags.None);
        bool hovered = ImGui.IsItemHovered();

        float4 titleBarColor = ImGui.GetStyleColorVec4(ImGuiCol.TitleBgActive);
        uint fill = ImGui.ColorConvertFloat4ToU32(new float4(titleBarColor.X, titleBarColor.Y, titleBarColor.Z, 1f));
        uint border = ConsoleStyle.FrameU32;
        uint textColor = ImGui.ColorConvertFloat4ToU32(new float4(1f, 1f, 1f, 1f));

        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(in pMin, in pMax, ConsoleStyle.ApplyAlpha(fill), ConsoleStyle.FrameRoundingPx);
        if(hovered) {
            drawList.AddRectFilled(in pMin, in pMax, ConsoleStyle.ApplyAlpha(ConsoleStyle.ButtonHoverLightenU32), ConsoleStyle.FrameRoundingPx);
        }
        drawList.AddRect(in pMin, in pMax, ConsoleStyle.ApplyAlpha(border), ConsoleStyle.FrameRoundingPx, ImDrawFlags.None, ConsoleStyle.WindowBorderThicknessPx);

        ConsoleStyle.PushLabelFont();
        drawList.AddText(pMin + (size - textSize) * 0.5f, ConsoleStyle.ApplyAlpha(textColor), label);
        ConsoleStyle.PopFont();
        return clicked;
    }

    private static void DrawInformation() {
        IReadOnlyList<ConstellationInformation> constellations = SkyCulturesRenderer.GetActiveConstellationInformation();
        if(constellations.Count == 0) {
            ImGui.Text("No constellation information is available for the active sky culture.");
            return;
        }

        // On a control switch, stash the outgoing vehicle's selection and load the incoming
        // vehicle's remembered selection (or fall back to its oriented star if it has one).
        Vehicle? controlledVehicle = Program.ControlledVehicle;
        if(!ReferenceEquals(controlledVehicle, _lastSelectionVehicle)) {
            if(_lastSelectionVehicle != null) {
                _vehicleSelections[_lastSelectionVehicle] = (_selectedConstellationId, _selectedStarHip);
            }

            if(controlledVehicle != null) {
                if(_vehicleSelections.TryGetValue(controlledVehicle, out var saved) &&
                   FindConstellationIndex(constellations, saved.ConstellationId) >= 0) {
                    _selectedConstellationId = saved.ConstellationId;
                    _selectedStarHip = saved.StarHip;
                } else if(_orientedVehicles.TryGetValue(controlledVehicle, out int orientedHip) && orientedHip > 0) {
                    SelectStar(constellations, orientedHip);
                }
            }

            _lastSelectionVehicle = controlledVehicle;
        }

        int selectedConstellationIndex = FindConstellationIndex(constellations, _selectedConstellationId);
        if(selectedConstellationIndex < 0) {
            selectedConstellationIndex = 0;
            _selectedConstellationId = constellations[0].Id;
            _selectedStarHip = 0;
        }

        ConstellationInformation constellation = constellations[selectedConstellationIndex];
        if(ImGui.BeginCombo("Constellation", constellation.Name)) {
            for(int index = 0; index < constellations.Count; index++) {
                ConstellationInformation candidate = constellations[index];
                bool selected = candidate.Id == _selectedConstellationId;

                if(ImGui.Selectable(candidate.Name, selected)) {
                    _selectedConstellationId = candidate.Id;
                    _selectedStarHip = 0;
                }
            }

            ImGui.EndCombo();
        }

        selectedConstellationIndex = FindConstellationIndex(constellations, _selectedConstellationId);
        constellation = constellations[selectedConstellationIndex];
        if(constellation.Stars.Count == 0) {
            ImGui.Text("No catalog stars are available for this constellation.");
            return;
        }

        int selectedStarIndex = FindStarIndex(constellation.Stars, _selectedStarHip);
        if(selectedStarIndex < 0) {
            selectedStarIndex = 0;
            _selectedStarHip = constellation.Stars[0].Hip;
        }

        StarInformation star = constellation.Stars[selectedStarIndex];
        if(ImGui.BeginCombo("Star", star.DisplayName)) {
            for(int index = 0; index < constellation.Stars.Count; index++) {
                StarInformation candidate = constellation.Stars[index];
                bool selected = candidate.Hip == _selectedStarHip;

                if(ImGui.Selectable(candidate.DisplayName, selected)) {
                    _selectedStarHip = candidate.Hip;
                }
            }

            ImGui.EndCombo();
        }

        if(_showStarPointer) {
            if(ConsoleWidgets.PositiveButton("Star Pointer", "StarPointer", default)) {
                _showStarPointer = false;
            }
        } else {
            if(ConsoleWidgets.Button("Star Pointer", "StarPointer", default)) {
                _showStarPointer = true;
            }
        }
        if(_showStarPointer) {
            ImGui.SameLine();
            ImGui.TextColored(in ConsoleStyle.Positive, "Pointer On");
        }

        selectedStarIndex = FindStarIndex(constellation.Stars, _selectedStarHip);
        star = constellation.Stars[selectedStarIndex];
        ImGui.NewLine();
        ImGui.Separator();
        DrawRegionHeaderLarge(star.DisplayName, 1.5f);
        ImGui.Separator();
        DrawInformationValue("Hipparcos #", star.Hip.ToString(CultureInfo.InvariantCulture));
        DrawInformationValue("RA (hrs:min:sec)", star.RightAscension);
        DrawInformationValue("Dec (deg)", star.Declination);
        if(!string.IsNullOrWhiteSpace(star.Bayer)) {
            DrawInformationValue("Bayer", star.Bayer);
        }
        DrawInformationValueWithHelp(
            "Apparent Magnitude",
            star.Magnitude,
            "How bright the star appears from Earth. Smaller or negative values are brighter.");
        DrawInformationValueWithHelp(
            "Absolute Magnitude",
            star.AbsoluteMagnitude,
            "How bright the star would appear from a standard distance of 10 parsecs (about 32.6 light years). Smaller or negative values are brighter.");
        DrawSpectrumInformationValue(star.SpectralType);
        DrawInformationValue("Distance (LY)", star.Distance);

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.NewLine();
        ImGui.Separator();

        bool isOriented = IsOrientedToStar(star.Hip);
        Vehicle? vehicle = Program.ControlledVehicle;
        bool hasVehicle = vehicle != null;

        using(new ImGuiDisabledScope(!hasVehicle)) {
            if(isOriented) {
                if(ConsoleWidgets.PositiveButton("Orient to Star", "OrientToStar", default)) {
                    ToggleOrientToStar(star.Hip);
                }
            } else {
                if(ConsoleWidgets.Button("Orient to Star", "OrientToStar", default)) {
                    ToggleOrientToStar(star.Hip);
                }
            }
        }

        if(isOriented) {
            ImGui.SameLine();
            ImGui.TextColored(in ConsoleStyle.Positive, "Active Orientation " + star.DisplayName);
        } else if(!hasVehicle) {
            ImGui.SameLine();
            ImGui.TextColored(in ConsoleStyle.TextMuted, "(No active vehicle)");
        }

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.Separator();

        bool showNavballMarker = NavballMarkerRenderer.showNavballMarker;
        if(showNavballMarker) {
            if(ConsoleWidgets.PositiveButton("Mark Navball", "MarkNavball", default)) {
                NavballMarkerRenderer.showNavballMarker = false;
                SaveSettings();
            }
        } else {
            if(ConsoleWidgets.Button("Mark Navball", "MarkNavball", default)) {
                NavballMarkerRenderer.showNavballMarker = true;
                SaveSettings();
            }
        }
        if(showNavballMarker) {
            ImGui.SameLine();
            ImGui.TextColored(in ConsoleStyle.Positive, "Marker On " + star.DisplayName);
        }

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.Separator();

        bool showNavballMarkerLabel = NavballMarkerRenderer.showMarkerLabel;
        ConsoleWidgets.BeginRow("Navball Show Name");
        if(ConsoleWidgets.Checkbox("ShowNavballMarkerLabel", ref showNavballMarkerLabel, pending: false)) {
            NavballMarkerRenderer.showMarkerLabel = showNavballMarkerLabel;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        // Persist the current selection onto the controlled vehicle so it is remembered across
        // control switches even when the user only browses without switching away first.
        if(vehicle != null) {
            _vehicleSelections[vehicle] = (_selectedConstellationId, _selectedStarHip);
        }
    }

    public static int SelectedStarHip => _selectedStarHip;

    public static bool TryGetOrientedStar(Vehicle vehicle, out int hip) {
        hip = 0;
        return vehicle != null && _orientedVehicles.TryGetValue(vehicle, out hip) && hip > 0;
    }

    // Clears every remembered star orientation (save/load reset). Flight computer fields are not
    // touched here: on a fresh load the vehicles themselves are new instances anyway.
    public static void ClearOrientedStar() {
        _orientedVehicles.Clear();
        _previousAttitudeModes.Clear();
        _pendingDisengage.Clear();
        _savedRateCommands.Clear();
    }

    // Skip the normal PrepareWorker reapply for a vehicle whose disengage is handled this tick.
    public static bool IsDisengagePending(Vehicle vehicle) {
        return vehicle != null && _pendingDisengage.Contains(vehicle);
    }

    // Performs the disengage for a pending vehicle from inside the PrepareWorker reapply: restores
    // the attitude mode the vehicle had before the mod took over (via the same TrackTarget/RateHold
    // the game buttons call), with CustomAttitudeTarget cleaned first, then drops the orientation
    // entry so the mod stops reapplying. Falls back to a rate-hold when no previous mode survived.
    public static void DisengagePending(Vehicle vehicle) {
        if(vehicle == null || !_pendingDisengage.Remove(vehicle))
            return;

        FlightComputer flightComputer = vehicle.FlightComputer;
        if(_previousAttitudeModes.TryGetValue(vehicle, out PreviousAttitudeMode? previous)) {
            _previousAttitudeModes.Remove(vehicle);
            ApplyPreviousAttitudeMode(flightComputer, previous);
        } else {
            flightComputer.CustomAttitudeTarget = _savedRateCommands.TryGetValue(vehicle, out double3 saved)
                ? saved
                : double3.Zero;
            flightComputer.AttitudeTarget = AttitudeTarget.Zero;
            flightComputer.RateHold(vehicle.NavBallData.Frame);
        }

        _savedRateCommands.Remove(vehicle);
        _orientedVehicles.Remove(vehicle);
    }

    // Called from the flight computer patches whenever the game itself changes attitude mode on a
    // vehicle, so that vehicle leaves the mod's star orientation mode. Also called from
    // DetachOrientedStar, which supplies the vehicle directly. CustomAttitudeTarget holds our
    // star-pointing euler angles while AttitudeTrackTarget is Custom, but the engine reinterprets
    // that same field as a body angular rate command once the mode becomes rate-hold (see
    // UpdateAttitudeTarget: TrackTarget.None routes CustomAttitudeTarget into AttitudeTarget.RatesCci).
    // SetNullRot does not clear the field - in stock play it only ever holds a zero or player-set
    // rate - so without this cleanup the leftover star angles would drive a ~1 rad/s spin the
    // moment the vehicle drops into rate-hold.
    public static void ClearOrientedStar(FlightComputer? flightComputer) {
        if(flightComputer == null)
            return;

        Vehicle? owner = null;
        foreach(KeyValuePair<Vehicle, int> pair in _orientedVehicles) {
            if(ReferenceEquals(pair.Key.FlightComputer, flightComputer)) {
                owner = pair.Key;
                break;
            }
        }

        // The orientation entry is usually already gone on a disengage (removed in ToggleOrientToStar),
        // so also release the pending-disengage flag for whichever vehicle owns this flight computer,
        // otherwise the reapply would stay suppressed after the queued button press has run.
        if(owner == null) {
            foreach(Vehicle candidate in _pendingDisengage) {
                if(ReferenceEquals(candidate.FlightComputer, flightComputer)) {
                    owner = candidate;
                    break;
                }
            }
        }

        if(owner != null) {
            ClearOrientedStar(owner);
        }
    }

    // Removes the vehicle from the mod's star orientation mode. These patches (SetNullRot, RateHold,
    // TrackTarget, etc.) run when the game is leaving the mod's mode, but their Prefix fires BEFORE
    // the mode actually flips - the computer still reads Custom/EclBody at that point, so an
    // "already left the mod's mode" check would skip the cleanup entirely. The star euler angles the
    // mod wrote into CustomAttitudeTarget are about to be read by rate-hold as an angular-rate
    // command, so hand back the player's pre-star rate command unconditionally.
    private static void ClearOrientedStar(Vehicle vehicle) {
        _orientedVehicles.Remove(vehicle);
        _pendingDisengage.Remove(vehicle);
        _previousAttitudeModes.Remove(vehicle);

        FlightComputer flightComputer = vehicle.FlightComputer;
        flightComputer.CustomAttitudeTarget = _savedRateCommands.TryGetValue(vehicle, out double3 saved)
            ? saved
            : double3.Zero;
        flightComputer.AttitudeTarget = AttitudeTarget.Zero;
        _savedRateCommands.Remove(vehicle);
    }

    // Removes the vehicle from the mod's star orientation mode, sanitizing the flight computer
    // fields via ClearOrientedStar (which hands back the player's pre-star rate command).
    public static void DetachOrientedStar(Vehicle vehicle) {
        ClearOrientedStar(vehicle);
    }

    // Whether the mod's star orientation is currently assigned to the controlled vehicle for the
    // given star. The dictionary is the source of truth: once the mod engages a vehicle's mode the
    // entry is what keeps it oriented, and the game buttons are the only thing that removes it.
    public static bool IsOrientedToStar(int hip) {
        if(hip <= 0)
            return false;

        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null)
            return false;

        return _orientedVehicles.TryGetValue(vehicle, out int orientedHip) && orientedHip == hip;
    }

    private static PreviousAttitudeMode SnapshotAttitudeMode(FlightComputer flightComputer) {
        return new PreviousAttitudeMode {
            AttitudeMode = flightComputer.AttitudeMode,
            AttitudeFrame = flightComputer.AttitudeFrame,
            AttitudeTrackTarget = flightComputer.AttitudeTrackTarget,
            CustomAttitudeTarget = flightComputer.CustomAttitudeTarget
        };
    }

    // Applies a remembered attitude mode using the same calls the game's own mode buttons run: a
    // named track target goes through TrackTarget, rate-hold through RateHold on the remembered
    // frame. CustomAttitudeTarget is restored/zeroed FIRST because the patches fire before the mode
    // flips (still Custom) and rate-hold reads that field as the body rate command - leaving the
    // mod's star angles there is what caused the wild spin.
    private static void ApplyPreviousAttitudeMode(FlightComputer flightComputer, PreviousAttitudeMode previous) {
        if(previous.AttitudeTrackTarget is FlightComputerAttitudeTrackTarget.None or FlightComputerAttitudeTrackTarget.Custom) {
            flightComputer.CustomAttitudeTarget = previous.CustomAttitudeTarget;
        } else {
            flightComputer.CustomAttitudeTarget = double3.Zero;
        }
        flightComputer.AttitudeTarget = AttitudeTarget.Zero;

        if(previous.AttitudeTrackTarget != FlightComputerAttitudeTrackTarget.None &&
           previous.AttitudeTrackTarget != FlightComputerAttitudeTrackTarget.Custom) {
            flightComputer.TrackTarget(previous.AttitudeTrackTarget);
        } else {
            flightComputer.RateHold(previous.AttitudeFrame);
        }

        // The buttons always leave Auto engaged for these paths; restore Manual if that is what the
        // vehicle was in before the mod took over.
        flightComputer.AttitudeMode = previous.AttitudeMode;
    }

    // Marks the vehicle for disengage; the mod's PrepareWorker reapply performs the previous-mode
    // restore on the next tick, atomically with no stale star-angle write interleaving.
    private static void PressGameRateButton(Vehicle vehicle) {
        _pendingDisengage.Add(vehicle);
    }

    // Toggles star orientation for the given star on the currently controlled vehicle. Pressing the
    // button while the vehicle is still in the mod's orientation mode disengages it into the game's
    // own Rate mode. Leaving star orientation any other way is done via the game's own standard
    // mode buttons/keybinds, which the flight computer patches detect and react to per vehicle.
    public static void ToggleOrientToStar(int hip) {
        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null || hip <= 0)
            return;

        // Pressing the button on the star the vehicle is already oriented to toggles the mod's mode
        // off into the game's own Rate mode. The entry stays registered so the mod's PrepareWorker
        // reapply performs the rate-hold cleanly on the next tick.
        if(IsOrientedToStar(hip)) {
            DefaultCategory.Log.Info($"[OhMyStars] ToggleOrientToStar disengage vehicle={vehicle.Id} hip={hip}");
            PressGameRateButton(vehicle);
            return;
        }

        // Engaging (or retargeting to a different star). Snapshot the attitude mode in effect right
        // now, before the mod takes over - but if the vehicle is already star-oriented, its current
        // mode is the mod's own Custom/EclBody mode, which is meaningless as a "previous mode", so
        // keep the snapshot taken when the mod first engaged instead. Also capture the player's rate
        // command the first time the mod takes over so it can be handed back when the mod leaves.
        bool alreadyOriented = _orientedVehicles.ContainsKey(vehicle);
        if(!alreadyOriented) {
            FlightComputer flightComputer = vehicle.FlightComputer;
            _previousAttitudeModes[vehicle] = SnapshotAttitudeMode(flightComputer);
            _savedRateCommands[vehicle] = flightComputer.CustomAttitudeTarget;
        }

        _orientedVehicles[vehicle] = hip;
        DefaultCategory.Log.Info($"[OhMyStars] ToggleOrientToStar engage vehicle={vehicle.Id} hip={hip} (alreadyOriented={alreadyOriented})");
        ApplyStarOrientation(vehicle, hip);
    }

    public static void ApplyStarOrientation(Vehicle vehicle, int hip) {
        if(hip <= 0 || vehicle == null)
            return;

        if(!SkyCulturesRenderer.TryGetStarDirection(hip, out double3 rawDir))
            return;

        double3 starDirEcl = StellariumRenderer.ApplyAlignment(rawDir);
        double len = VectorMath.Length(starDirEcl);
        if(len <= 1e-12)
            return;
        starDirEcl /= len;

        // In KSA, VehicleReferenceFrame.EclBody is the ecliptic body-reference frame,
        // which relates to Ecliptic (CCE) coordinates via BODY2UPFRAME (negating Y and Z).
        // For EclBody, Euler angles (roll, yaw, pitch) map to unit forward as:
        // forward vector = (cos(pitch)*cos(yaw), sin(pitch), -cos(pitch)*sin(yaw)).
        // Since starDirEcl in EclBody coordinates is (starDirEcl.X, -starDirEcl.Y, -starDirEcl.Z),
        // we can solve directly for pitch and yaw:
        double dx = starDirEcl.X;
        double dy = -starDirEcl.Y;
        double dz = -starDirEcl.Z;

        double pitch = Math.Asin(Math.Clamp(dy, -1.0, 1.0));
        double yaw = Math.Atan2(-dz, dx);
        double roll = 0.0;

        // Kittens face along body +X like regular vehicles, but their body up axis is -Z instead of
        // +Z (see KittenServoPrecomp/PoseIntegratorCallbacks: KittenBodyForwardAxisBody=+X,
        // KittenBodyUpAxisBody=-Z). So for kittens build a look-at frame with -Z up instead and
        // convert it back to EclBody euler angles using the game's own helpers.
        if(vehicle is KittenEva) {
            double3 fwd = new double3(dx, dy, dz);
            double3 upHint = new double3(0.0, 0.0, -1.0);
            double3 right = double3.Cross(upHint, fwd);
            double rightLen = VectorMath.Length(right);
            if(rightLen <= 1e-12) {
                // Star is along the up hint; yaw straight at it with a zeroed-out right axis.
                pitch = -Math.Sign(dz) * Math.PI / 2.0;
                right = double3.UnitX;
            } else {
                right /= rightLen;
            }
            double3 up = double3.Cross(fwd, right);
            doubleQuat desired2Frame = doubleQuat.CreateFromRotationMatrix(new double4x4(
                fwd.X, fwd.Y, fwd.Z, 0.0,
                right.X, right.Y, right.Z, 0.0,
                up.X, up.Y, up.Z, 0.0,
                0.0, 0.0, 0.0, 1.0));
            double3 euler = VehicleReferenceFrame.EclBody.QuaternionToEulerAngles(desired2Frame);
            roll = euler.X;
            yaw = euler.Y;
            pitch = euler.Z;
        }

        vehicle.FlightComputer.AttitudeMode = FlightComputerAttitudeMode.Auto;
        vehicle.FlightComputer.AttitudeFrame = VehicleReferenceFrame.EclBody;
        vehicle.FlightComputer.AttitudeTrackTarget = FlightComputerAttitudeTrackTarget.Custom;
        vehicle.FlightComputer.CustomAttitudeTarget = new double3(roll, yaw, pitch);
    }

    public static bool TryGetStarPointer(out int hip, out float2 windowPosition, out float2 windowSize) {
        hip = _selectedStarHip;
        windowPosition = _windowPosition;
        windowSize = _windowSize;
        return _showWindow && _showStarPointer && hip > 0;
    }

    // Screen rect currently occupied by the mod's own window, so the navball marker can hide
    // when it is covered by this window (it renders above the navball like the marker does).
    public static bool TryGetWindowRect(out float2 windowPosition, out float2 windowSize) {
        windowPosition = _windowPosition;
        windowSize = _windowSize;
        return _showWindow;
    }

    private static void DrawSpectrumInformationValue(string spectralType) {
        float2 rowPosition = ImGui.GetCursorScreenPos();
        ConsoleWidgets.BeginRow("Spectrum");
        float2 valuePosition = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(new float2(
            rowPosition.X + ImGui.CalcTextSize("Spectrum ").X + 8f,
            rowPosition.Y));
        if(ImGui.SmallButton("(Legend)")) {
            _display = WindowDisplay.SpectrumLegend;
        }

        ImGui.SetCursorScreenPos(valuePosition);
        ImGui.Text(string.IsNullOrWhiteSpace(spectralType) ? "N/A" : spectralType);
        ConsoleWidgets.EndRow();
    }

    private static void DrawSpectrumLegend() {
        if(ImGui.Button("Back")) {
            _display = WindowDisplay.Information;
            return;
        }

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Spectral Type");
        ImGui.Separator();
        DrawWrappedText("Classifies a star by surface temperature and color.");
        ImGui.NewLine();
        DrawWrappedText("O: Blue; hottest, at least 30,000 K.");
        DrawWrappedText("B: Blue-white; 10,000 to 30,000 K.");
        DrawWrappedText("A: White; 7,500 to 10,000 K.");
        DrawWrappedText("F: Yellow-white; 6,000 to 7,500 K.");
        DrawWrappedText("G: Yellow; 5,200 to 6,000 K. The Sun is G2.");
        DrawWrappedText("K: Orange; 3,700 to 5,200 K.");
        DrawWrappedText("M: Red; below 3,700 K.");
        ImGui.NewLine();
        DrawWrappedText("Other types: L, T, and Y describe progressively cooler brown dwarfs. C and S are cool carbon-rich stars. W or WR identifies hot Wolf-Rayet stars with strong stellar winds.");
        ImGui.NewLine();

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Digit");
        ImGui.Separator();
        DrawWrappedText("The digit after the letter refines temperature within that class. 0 is hotter and 9 is cooler.");

        ImGui.NewLine();
        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Luminosity Class");
        ImGui.Separator();
        DrawWrappedText("Roman numerals describe luminosity class. I is a supergiant, III a giant, IV a subgiant, and V a main-sequence dwarf.");
        ImGui.NewLine();
        DrawWrappedText("0 / Ia-0 / Ia+: Hypergiants or extremely luminous supergiants");
        DrawWrappedText("Ia: Luminous supergiants");
        DrawWrappedText("Iab: Intermediate-luminosity supergiants");
        DrawWrappedText("Ib: Less luminous supergiants");
        DrawWrappedText("II: Bright giants");
        DrawWrappedText("III: Giants");
        DrawWrappedText("IV: Subgiants");
        DrawWrappedText("V: Main-sequence dwarfs");
        DrawWrappedText("sd / VI: Subdwarfs");
        DrawWrappedText("D / VII: White dwarfs");
        ImGui.NewLine();

        ImGui.Separator();
        ConsoleWidgets.RegionHeader("Prefixes and Suffixes");
        ImGui.Separator();
        DrawWrappedText("Prefixes and suffixes add details (a few examples)");
        DrawWrappedText("He Wk: Weak helium lines");
        DrawWrappedText("m: Metal-rich spectra");
        DrawWrappedText("n: Broadening of spectral lines due to rapid rotation");
        DrawWrappedText("p: Peculiar spectra");
        DrawWrappedText("v / var: Variable spectra");
        ImGui.NewLine();
    }

    private static void DrawWrappedText(string text) {
        float wrapPosition = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        ImGui.PushTextWrapPos(wrapPosition);
        ImGui.Text(text);
        ImGui.PopTextWrapPos();
    }

    private static int FindConstellationIndex(IReadOnlyList<ConstellationInformation> constellations, string? id) {
        for(int index = 0; index < constellations.Count; index++) {
            if(constellations[index].Id == id)
                return index;
        }

        return -1;
    }

    private static int FindSkyCultureIndexByName(string name) {
        IReadOnlyList<SkyCulture> skyCultures = SkyCulturesRenderer.SkyCultures;
        for(int index = 0; index < skyCultures.Count; index++) {
            if(string.Equals(skyCultures[index].Name, name, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static int FindStarIndex(IReadOnlyList<StarInformation> stars, int hip) {
        for(int index = 0; index < stars.Count; index++) {
            if(stars[index].Hip == hip)
                return index;
        }

        return -1;
    }

    // Selects the given star and the constellation containing it in the Information tab.
    private static void SelectStar(IReadOnlyList<ConstellationInformation> constellations, int hip) {
        foreach(ConstellationInformation constellation in constellations) {
            if(FindStarIndex(constellation.Stars, hip) >= 0) {
                _selectedConstellationId = constellation.Id;
                _selectedStarHip = hip;
                return;
            }
        }
    }

    private static void DrawInformationValue(string label, string value) {
        ConsoleWidgets.BeginRow(label);
        ImGui.Text(string.IsNullOrWhiteSpace(value) ? "N/A" : value);
        ConsoleWidgets.EndRow();
    }

    private static void DrawInformationValueWithHelp(string label, string value, string helpText) {
        float2 rowPosition = ImGui.GetCursorScreenPos();
        ConsoleWidgets.BeginRow(label);
        float2 valuePosition = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(new float2(
            rowPosition.X + ImGui.CalcTextSize(label).X + 8f,
            rowPosition.Y));
        ImGui.Text("(?)");
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.None)) {
            ConsoleWidgets.Tooltip(helpText);
        }

        ImGui.SetCursorScreenPos(valuePosition);
        ImGui.Text(string.IsNullOrWhiteSpace(value) ? "N/A" : value);
        ConsoleWidgets.EndRow();
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

    // Color picker row: a ColorEdit3 swatch that opens ImGui's full color picker popup (hue
    // bar/wheel plus RGB/HSV/hex inputs). NoInputs hides the inline per-channel float boxes so
    // only the swatch shows; the picker popup still provides them.
    private static void DrawColorEditRow(
        string rowLabel,
        string idPrefix,
        Func<float3> getColor,
        Action<float3> setColor) {

        float3 color = getColor();
        ConsoleWidgets.BeginRow(rowLabel);
        if(ImGui.ColorEdit3(new ImString("##" + idPrefix), ref color, ImGuiColorEditFlags.NoInputs)) {
            setColor(color);
        }
        ConsoleWidgets.EndRow();
    }

    private static string? GetSettingsFilePath() {
        Mod? mod = ModLibrary.Find("OhMyStars");
        if(mod is not null && mod != Mod.Empty && !string.IsNullOrWhiteSpace(mod.DirectoryPath)) {
            return Path.Combine(mod.DirectoryPath, SettingsFileName);
        }

        string? assemblyDirectory = Path.GetDirectoryName(typeof(OhMyStarsWindow).Assembly.Location);
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

    private static WindowDisplay ReadTab(Dictionary<string, string> values, string key, WindowDisplay defaultValue) {
        if(values.TryGetValue(key, out string? rawValue) &&
           Enum.TryParse(rawValue, ignoreCase: true, out WindowDisplay parsed)) {
            return NormalizeTab(parsed);
        }

        return defaultValue;
    }

    // SpectrumLegend is a sub-view of the Information tab, so it is persisted as its parent tab.
    private static WindowDisplay NormalizeTab(WindowDisplay display) {
        return display == WindowDisplay.SpectrumLegend ? WindowDisplay.Information : display;
    }

    private static WindowDisplay GetPersistableTab() {
        return NormalizeTab(_display);
    }

    private static float ReadFloat(Dictionary<string, string> values, string key, float defaultValue) {
        if(values.TryGetValue(key, out string? rawValue) &&
           float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsed)) {
            return parsed;
        }

        return defaultValue;
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int defaultValue) {
        if(values.TryGetValue(key, out string? rawValue) &&
           int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {
            return parsed;
        }

        return defaultValue;
    }
}
