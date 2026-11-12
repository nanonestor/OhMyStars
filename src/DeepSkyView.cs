using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;

namespace OhMyStars;

internal static class DeepSkyView {
    private const string PickerOwner = "DeepSky";
    private const float RingRadius = 8f;
    private static readonly ImInputString SearchBuffer = new(256);
    private static readonly List<MultipleStarCatalog.Target> Targets = [];
    private static readonly TypeControl[] TypeControls = DeepSkyCatalog.Types
        .Select(type => new TypeControl(type.Code, type.Name)).ToArray();
    private static readonly TypeControl[] RingControls = TypeControls.Where(control => !control.SearchOnly).ToArray();
    private static readonly Dictionary<string, TypeControl> TypeControlsByCode = TypeControls.ToDictionary(type => type.Code);
    private static readonly (DeepSkyCatalog.CatalogKind Kind, string Name, string ToggleLabel)[] CatalogControls =
        Enum.GetValues<DeepSkyCatalog.CatalogKind>().Select(kind =>
            (kind, kind == DeepSkyCatalog.CatalogKind.All ? "All catalogs" : kind.ToString(), $"{kind}##DeepSkyShowCatalog{kind}")).ToArray();
    private static Task<DeepSkyCatalog>? _loading;
    private static DeepSkyCatalog? _catalog;
    private static Dictionary<long, DeepSkyCatalog.Entry> _entries = [];
    private static string? _error;
    private static DeepSkyCatalog.Entry? _selected;
    private static string? _type;
    private static string _typePreview = "All object types";
    private static DeepSkyCatalog.CatalogKind _searchCatalog;
    private static string _catalogPreview = "All catalogs";
    private static string _query = "";
    private static bool _searchDirty = true;
    private static string _resultPreview = "Select an object";
    private static string _resultCount = "";
    private static DeepSkyCatalog.Entry[] _results = [];
    private static DeepSkyVisibilityCache? _visibility;
    private static DeepSkyCatalog.Entry[]? _visibleEntries;
    private static Dictionary<long, double3> _directions = [];
    private static (DeepSkyCatalog.Entry Entry, double3 Direction, TypeControl Style)[] _markers = [];
    private static (float X, float Y, float Z)? _alignment;
    private static ImFontPtr _labelFont;
    private static float _labelFontSize;
    private static float _typeLabelWidth;
    private static bool _pendingSettingsSave;
    private static bool _showVisibilityOptions;
    private static long _settingsChangedAt;

    private sealed class TypeControl(string code, string name) {
        public readonly string Code = code;
        public readonly string Name = name;
        public readonly string SearchLabel = $"{name} ({code})";
        public readonly string ToggleLabel = $"{name}##DeepSkyType{code}";
        public readonly string SliderId = $"DeepSkyMagnitude{code}";
        public readonly string ColorId = $"##DeepSkyColor{code}";
        public readonly bool SearchOnly = code is "Dup" or "NonEx";
        private double _displayedCutoff = double.NaN;
        private float3 _color = new(float.NaN);
        public string MagnitudeText { get; private set; } = "";
        public ImColor8 RingColor { get; private set; }

        public void UpdateMagnitude(float cutoff) {
            double rounded = Math.Round(cutoff, 2);
            if(_displayedCutoff == rounded)
                return;
            _displayedCutoff = rounded;
            MagnitudeText = rounded.ToString("F2", CultureInfo.InvariantCulture);
        }

        public void UpdateColor(DeepSkySettings.TypeStyle style) {
            if(_color.X == style.Red && _color.Y == style.Green && _color.Z == style.Blue)
                return;
            _color = new(style.Red, style.Green, style.Blue);
            RingColor = StellariumRenderer.ToLineColor(_color, 0.85f);
        }
    }

    public static DeepSkySettings Settings = new();
    
    public static bool TabVisible;
    public static bool IsMousePickerActive => StarPicker.IsActiveFor(PickerOwner);

    public static bool TryGetSelectedDirection(out double3 direction) {
        direction = default;
        return _selected is { } entry && _directions.TryGetValue(entry.Id, out direction);
    }

    private static void Load() {
        _loading ??= Task.Run(() => DeepSkyCatalog.Load(
            ModPaths.Combine("catalogs", "OpenNGC", "database_files", "NGC.csv"),
            ModPaths.Combine("catalogs", "OpenNGC", "database_files", "addendum.csv")));
        if(!_loading.IsCompleted || _catalog is not null || _error is not null)
            return;
        if(_loading.IsFaulted) {
            _error = _loading.Exception!.GetBaseException().Message;
            Console.WriteLine($"OhMyStars: could not load OpenNGC: {_loading.Exception}");
            return;
        }
        _catalog = _loading.GetAwaiter().GetResult();
        _entries = _catalog.Entries.ToDictionary(entry => entry.Id);
        _directions = _catalog.Entries.Where(entry => entry.RaHours.HasValue && entry.DecDegrees.HasValue).ToDictionary(entry => entry.Id,
            entry => StarDirectionConverter.RaDecToDirection(entry.RaHours!.Value, entry.DecDegrees!.Value));
        _visibility = new(_catalog);
    }

    public static void Draw() {
        Load();
        if(_error is not null) {
            ImGui.TextWrapped($"OpenNGC load failed: {_error}");
            if(ImGui.Button("Retry OpenNGC load")) {
                _loading = null;
                _error = null;
            }
            return;
        }
        if(_catalog is null) {
            ImGui.Text("Loading OpenNGC deep sky catalog...");
            return;
        }
        ImGui.Text("* Besides searching here you can mouse click on objects visible in the sky view!");
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##DeepSkyObjectType", _typePreview)) {
            if(ImGui.Selectable("Show all object types", _type is null)) {
                _type = null;
                _typePreview = "All object types";
                _searchDirty = true;
            }
            foreach(var control in TypeControls)
                if(ImGui.Selectable(control.SearchLabel, _type == control.Code)) {
                    _type = control.Code;
                    _typePreview = control.Name;
                    _searchDirty = true;
                }
            ImGui.EndCombo();
        }
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("##DeepSkyCatalog", _catalogPreview)) {
            foreach(var (kind, name, _) in CatalogControls)
                if(ImGui.Selectable(name, _searchCatalog == kind)) {
                    _searchCatalog = kind;
                    _catalogPreview = name;
                    _searchDirty = true;
                }
            ImGui.EndCombo();
        }
        ImGui.Text("Search names / identifiers");
        ImGui.SetNextItemWidth(-1);
        if(ImGui.InputText(new ImString("##DeepSkySearch"), SearchBuffer)) {
            _query = ((string)SearchBuffer.Value).Trim();
            _searchDirty = true;
        }
        if(_searchDirty) {
            _results = _catalog.Search(_query, _type, _searchCatalog);
            _resultPreview = $"Select an object ({_results.Length} matches)";
            _resultCount = $"{_results.Length} matching catalog entries";
            _searchDirty = false;
        }
        ImGui.SetNextItemWidth(-1);
        bool messierSearch = _searchCatalog == DeepSkyCatalog.CatalogKind.Messier;
        string preview = _selected is { } selected ? messierSearch ? selected.MessierLabel : selected.Label : _resultPreview;
        if(ImGui.BeginCombo("##DeepSkyObjects", preview)) {
            ImGuiListClipper clipper = new();
            clipper.Begin(_results.Length);
            while(clipper.Step())
                for(int index = clipper.DisplayStart; index < clipper.DisplayEnd; index++) {
                    var entry = _results[index];
                    if(ImGui.Selectable(messierSearch ? entry.MessierSelectableLabel : entry.SelectableLabel, _selected?.Id == entry.Id))
                        _selected = entry;
                }
            clipper.End();
            if(_results.Length == 0)
                ImGui.Text("No matching objects.");
            ImGui.EndCombo();
        }
        ImGui.Text(_resultCount);
        ImGui.Separator();
        DrawOverlayControls();
        KeepMousePicker();
        ImGui.Separator();
        DrawDetails();
    }

    private static void DrawOverlayControls() {
        // makes a button as a master toggle to show objects to the background at all based on the current settings
        // uses the same button as in the DrawTopBarToggle in OhMyStarsWindow.cs

        if(OhMyStarsWindow.DrawTopBarToggle("Show deep sky objects", Settings.ShowHighlightCircles))
            Settings.ShowHighlightCircles = !Settings.ShowHighlightCircles;


        bool changed = ImGui.Checkbox("Show enabled rings / outlines even when this tab / window is closed", ref Settings.AlwaysShow);
        changed |= ImGui.Checkbox("Show nebula outlines", ref Settings.ShowNebulaOutlines);
        if(Settings.ShowNebulaOutlines)
            DeepSkyOutlineRenderer.DrawStatus(_catalog!);
        changed |= ImGui.Checkbox("Include unknown magnitude (no V or B)", ref Settings.IncludeUnknownMagnitude);
        ImGui.Text("Show catalogs:");
        foreach(var (kind, _, label) in CatalogControls) {
            if(kind == DeepSkyCatalog.CatalogKind.All)
                continue;
            ImGui.SameLine();
            bool enabled = Settings.Catalogs[kind];
            if(ImGui.Checkbox(new ImString(label), ref enabled)) {
                Settings.Catalogs[kind] = enabled;
                changed = true;
            }
        }
        if(ImGui.ArrowButton("##DeepSkyVisibilityOptions", _showVisibilityOptions ? ImGuiDir.Down : ImGuiDir.Right))
            _showVisibilityOptions = !_showVisibilityOptions;
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Visibility Filter Options");
        if(_showVisibilityOptions && ImGui.BeginTable("##DeepSkyStyles", 3, ImGuiTableFlags.SizingStretchProp)) {
            if(_typeLabelWidth == 0 || !_labelFont.Equals(ImGui.GetFont()) || _labelFontSize != ImGui.GetFontSize()) {
                _labelFont = ImGui.GetFont();
                _labelFontSize = ImGui.GetFontSize();
                _typeLabelWidth = 0;
                foreach(var control in RingControls)
                    _typeLabelWidth = Math.Max(_typeLabelWidth, ImGui.CalcTextSize(control.Name).X);
            }
            ImGui.TableSetupColumn("Show object type", ImGuiTableColumnFlags.WidthFixed,
                _typeLabelWidth + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X);
            ImGui.TableSetupColumn("Magnitude cutoff", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            bool allTypesEnabled = Settings.AllTypesEnabled;
            changed |= ImGui.Checkbox("All object types##DeepSkyAllTypes", ref allTypesEnabled); 
            if(changed) {
                Settings.AllTypesEnabled = allTypesEnabled;
                changed = true;
            }
            foreach(var control in RingControls) {
                var style = Settings.Styles[control.Code];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                changed |= ImGui.Checkbox(new ImString(control.ToggleLabel), ref style.Enabled);
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                control.UpdateMagnitude(style.MagnitudeCutoff);
                changed |= ConsoleWidgets.SliderFloat(control.SliderId, ref style.MagnitudeCutoff,
                    DeepSkySettings.MinMagnitudeCutoff, DeepSkySettings.MaxMagnitudeCutoff,
                    control.MagnitudeText, pending: false);
                ImGui.TableNextColumn();
                float3 color = new(style.Red, style.Green, style.Blue);
                if(ImGui.ColorEdit3(new ImString(control.ColorId), ref color, ImGuiColorEditFlags.NoInputs)) {
                    style.Red = color.X;
                    style.Green = color.Y;
                    style.Blue = color.Z;
                    changed = true;
                }
            }
            ImGui.EndTable();
        }
        if(changed) {
            Targets.Clear();
            DeepSkyOutlineRenderer.ClearTargets();
            _pendingSettingsSave = true;
            _settingsChangedAt = Environment.TickCount64;
        }
    }

    public static void FlushPendingSettings(bool force = false) {
        if(!_pendingSettingsSave || !force && (Environment.TickCount64 - _settingsChangedAt < 250 ||
            ImGui.IsMouseDown(ImGuiMouseButton.Left)))
            return;
        OhMyStarsWindow.SaveSettings();
        _pendingSettingsSave = false;
    }

    private static void KeepMousePicker() {
        if(!OhMyStarsWindow.MousePointerEnabled || StarsEditWindow.VanillaMode) {
            if(IsMousePickerActive)
                StarPicker.Cancel();
            return;
        }
        if(!StarPicker.Active || IsMousePickerActive || StarPicker.IsActiveFor("StarsMouse") ||
            MultipleStarView.IsMousePickerActive) {
            if(!IsMousePickerActive)
                StarPicker.StartCustom(PickerOwner,
                    mouse => DeepSkyOutlineRenderer.Pick(
                        MultipleStarCatalog.PickNearest(Targets, mouse.X, mouse.Y, RingRadius), mouse),
                    id => _selected = _entries[id]);
            StarPicker.KeepAlive(PickerOwner);
        }
    }

    private static void DrawDetails() {
        if(_selected is not { } entry) {
            ImGui.TextWrapped("Select a catalog entry above, or enable an object type and click one of its rings in the sky.");
            return;
        }
        var details = entry.DisplayDetails;
        ImGui.TextWrapped(entry.Label);
        ImGui.Text(details.Type);
        ImGui.TextWrapped(details.Identifiers);
        ImGui.TextWrapped(details.ApparentMagnitude);
        if(details.Distance is string distance)
            ImGui.TextWrapped(distance);
        if(!entry.CanDraw)
            ImGui.TextWrapped(entry.Type == "Dup"
                ? "Duplicate catalog entry: use the NGC / IC cross-reference above for the master object. No separate ring is drawn."
                : "This entry has no drawable sky position.");
        else if(!Settings.IsVisible(entry))
            ImGui.TextWrapped("This object's ring is hidden by the current type, catalog or magnitude controls.");
        foreach(string line in details.FieldLines)
            ImGui.TextWrapped(line);
        ImGui.Separator();
        foreach(string line in details.NoteLines)
            ImGui.TextWrapped(line);
        if(details.NoteLines.Length == 0)
            ImGui.Text("No catalog notes recorded.");
    }

    public static void DrawOverlay(ImDrawListPtr draw, Camera camera, double3 center, double radius) {
        if(!Settings.ShowHighlightCircles)
            return;
        Targets.Clear();
        DeepSkyOutlineRenderer.ClearTargets();
        if(!TabVisible && !Settings.AlwaysShow)
            return;
        bool anyEnabled = false;
        foreach(var control in TypeControls) {
            var style = Settings.Styles[control.Code];
            anyEnabled |= style.Enabled && !control.SearchOnly;
            control.UpdateColor(style);
        }
        if(!anyEnabled && !Settings.ShowNebulaOutlines)
            return;
        Load();
        if(_visibility is null)
            return;
        var entries = _visibility.GetEntries(Settings);
        var alignment = (StellariumRenderer.alignmentRotationXDegrees, StellariumRenderer.alignmentRotationYDegrees,
            StellariumRenderer.alignmentRotationZDegrees);
        bool entriesChanged = !ReferenceEquals(_visibleEntries, entries);
        if(entriesChanged) {
            _markers = new (DeepSkyCatalog.Entry, double3, TypeControl)[entries.Length];
            _visibleEntries = entries;
            Targets.EnsureCapacity(entries.Length);
        }
        if(entriesChanged || _alignment != alignment) {
            for(int i = 0; i < entries.Length; i++) {
                var entry = entries[i];
                _markers[i] = (entry, StellariumRenderer.ApplyAlignment(_directions[entry.Id]), TypeControlsByCode[entry.Type]);
            }
            _alignment = alignment;
        }
        bool collectTargets = TabVisible && IsMousePickerActive;
        DeepSkyOutlineRenderer.Draw(draw, camera, center, radius, _catalog!, Settings, collectTargets, _selected?.Id);
        foreach(var (entry, direction, control) in _markers) {
            double3 position = center + direction * radius;
            if(!camera.IsPointWithinFov(position) || !StellariumRenderer.IsVisibleFromCamera(position))
                continue;
            float2 screen = StellariumRenderer.EgoToOverlayScreen(camera, position);
            if(collectTargets)
                Targets.Add(new(entry.Id, screen.X, screen.Y));
            ImColor8 color = control.RingColor;
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, new ImColor8(0, 0, 0, 200), 0, 3f);
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, color, 0, 1.5f);
            if(_selected?.Id == entry.Id) {
                ImDrawListExtensions.AddCircle(draw, screen, RingRadius + 3, new ImColor8(255, 255, 0, 255), 0, 1.5f);
                SkyLabelRenderer.Add(screen + new float2(RingRadius + 6, 0), color, entry.Label, 3);
            }
        }
    }
}
