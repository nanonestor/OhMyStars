using Brutal.ImGuiApi;
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
        public const WindowDisplay Tab = WindowDisplay.Settings;
        public const float IAULineOpacity = 0.4f;
        public static readonly float3 IAULineColor = new float3(1f, 1f, 1f);
        public const float AsterismLineOpacity = 0.4f;
        public static readonly float3 AsterismLineColor = new float3(0f, 0f, 1f);
        public const float AlignmentRotationXDegrees = 0f;
        public const float AlignmentRotationYDegrees = 0f;
        public const float AlignmentRotationZDegrees = 0f;
    }

    private static bool _showWindow = true;

    private static bool _iauColorExpanded = false;
    private static bool _asterismColorExpanded = false;
    private static bool _alignmentExpanded = false;
    private static WindowDisplay _display = WindowDisplay.Settings;
    private static string? _selectedConstellationId;
    private static int _selectedStarHip;
    private static int _orientedStarHip;
    private static bool _showStarPointer;
    private static float2 _windowPosition;
    private static float2 _windowSize;

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
        StellariumRenderer.showIAUConstellations = Defaults.ShowIAUConstellations;
        StellariumRenderer.showAsterisms = Defaults.ShowAsterisms;
        StellariumRenderer.showAsterismNames = Defaults.ShowAsterismNames;
        NavballMarkerRenderer.showNavballMarker = Defaults.ShowNavballMarker;
        NavballMarkerRenderer.showMarkerLabel = Defaults.ShowNavballMarkerLabel;
        _display = Defaults.Tab;
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

        DrawSectionHeader("Sky Culture", "Selects the Stellarium sky culture that provides the asterism lines, constellation names, and star labels.");

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

        ImGui.Separator();

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

        ImGui.Separator();
        DrawSectionHeader("Asterisms", "Asterisms are culturally defined patterns that connect selected stars into familiar shapes and constellations.");
        ImGui.Separator();

        bool showAsterisms = StellariumRenderer.showAsterisms;
        ConsoleWidgets.BeginRow("Show Asterisms");
        if(ConsoleWidgets.Checkbox("ShowAsterisms", ref showAsterisms, pending: false)) {
            StellariumRenderer.showAsterisms = showAsterisms;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        float asterismLineOpacity = StellariumRenderer.asterismLineOpacity;
        ConsoleWidgets.BeginRow("Asterism Line Brightness");
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
        DrawSectionHeader("IAU Constellations", "The International Astronomical Union's modern 88-constellation scheme and its standardized constellation boundaries.");
        ImGui.Separator();

        bool showIAUConstellations = StellariumRenderer.showIAUConstellations;
        ConsoleWidgets.BeginRow("IAU Constellations");
        if(ConsoleWidgets.Checkbox("IAUConstellations", ref showIAUConstellations, pending: false)) {
            StellariumRenderer.showIAUConstellations = showIAUConstellations;
            SaveSettings();
        }
        ConsoleWidgets.EndRow();

        float iauLineOpacity = StellariumRenderer.iauLineOpacity;
        ConsoleWidgets.BeginRow("IAU Line Brightness");
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
        if(ImGui.Button(_alignmentExpanded ? "Alignment v" : "Alignment >")) {
            _alignmentExpanded = !_alignmentExpanded;
        }

        if(_alignmentExpanded) {
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
        }

        ImGui.Dummy(new float2(0f, 4f));
        ImGui.Separator();

        if(ConsoleWidgets.Button("Reset to Defaults")) {
            ResetToDefaults();
        }

        ConsoleStyle.PopWidgetStyle();
        ImGui.End();
    }

    private static void DrawSectionHeader(string title, string helpText) {
        ConsoleWidgets.RegionHeader(title);
        ImGui.SameLine();
        ImGui.Text("(?)");
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.None)) {
            ConsoleWidgets.Tooltip(helpText);
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
    }

    public static int OrientedStarHip => _orientedStarHip;

    public static int SelectedStarHip => _selectedStarHip;

    public static void ClearOrientedStar() {
        ClearOrientedStar(null);
    }

    // Called from the flight computer patches whenever the game itself changes attitude mode.
    // CustomAttitudeTarget holds our star-pointing angles while AttitudeTrackTarget is Custom, but
    // the engine reinterprets that same field as an angular rate once the mode becomes None (rate-hold),
    // so it must be zeroed out whenever we stop being the ones driving attitude. AttitudeTarget (the
    // resolved quaternion) also needs zeroing: for named track targets the engine only overwrites it
    // when it can resolve a direction (e.g. Prograde with zero velocity resolves to nothing), otherwise
    // it silently keeps whatever quaternion was last there - our star-pointing one - and the vehicle
    // keeps pointing at the star even though the selected mode says otherwise.
    public static void ClearOrientedStar(FlightComputer? flightComputer) {
        if(_orientedStarHip > 0 && flightComputer != null) {
            flightComputer.CustomAttitudeTarget = double3.Zero;
            flightComputer.AttitudeTarget = AttitudeTarget.Zero;
        }
        _orientedStarHip = 0;
    }

    public static bool IsOrientedToStar(int hip) {
        if(hip <= 0 || _orientedStarHip != hip)
            return false;

        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null)
            return false;

        FlightComputer fc = vehicle.FlightComputer;
        return fc.AttitudeMode == FlightComputerAttitudeMode.Auto &&
               fc.AttitudeTrackTarget == FlightComputerAttitudeTrackTarget.Custom &&
               fc.AttitudeFrame == VehicleReferenceFrame.EclBody;
    }

    // One-way: engages star orientation for the given star. Leaving star orientation is done via the
    // game's own standard mode buttons/keybinds, which the flight computer patches detect and react to.
    public static void ToggleOrientToStar(int hip) {
        Vehicle? vehicle = Program.ControlledVehicle;
        if(vehicle == null || hip <= 0 || IsOrientedToStar(hip))
            return;

        _orientedStarHip = hip;
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

        vehicle.FlightComputer.AttitudeMode = FlightComputerAttitudeMode.Auto;
        vehicle.FlightComputer.AttitudeFrame = VehicleReferenceFrame.EclBody;
        vehicle.FlightComputer.AttitudeTrackTarget = FlightComputerAttitudeTrackTarget.Custom;
        vehicle.FlightComputer.CustomAttitudeTarget = new double3(roll, yaw, pitch);
    }

    public static bool TryGetStarPointer(out int hip, out float2 windowPosition, out float2 windowSize) {
        hip = _selectedStarHip;
        windowPosition = _windowPosition;
        windowSize = _windowSize;
        return _showWindow && _display == WindowDisplay.Information && _showStarPointer && hip > 0;
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

    private static int FindStarIndex(IReadOnlyList<StarInformation> stars, int hip) {
        for(int index = 0; index < stars.Count; index++) {
            if(stars[index].Hip == hip)
                return index;
        }

        return -1;
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
}
