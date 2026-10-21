using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;

namespace OhMyStars;

internal static class MultipleStarView {
    private const string PickerOwner = "MultiPrimary";
    private const float RingRadius = 8f;
    public const double DefaultMagnitudeCutoff = 8.9;
    public const double DefaultComponentMagnitudeCutoff = 11;
    public const float MinMagnitudeCutoff = 0f;
    public const float MaxMagnitudeCutoff = 20f;
    private static Task<MultipleStarCatalog>? _loading;
    private static Task<MultipleStarOrbits>? _loadingOrbits;
    private static MultipleStarOrbits? _orbits;
    private static string? _orbitError;
    private static MultipleStarOrbits.Curve[]? _orbitCurves;
    private static MultipleStarCatalog? _catalog;
    private static string? _error;
    private static long? _selected;
    private static bool _showFilters;
    private static bool _showCatalogIds;
    private static readonly List<MultipleStarCatalog.Target> Targets = new();
    private readonly record struct PrimaryMarker(long Id, int Hip, double3 Direction, float InverseParsecs, double? Magnitude);
    private static readonly List<PrimaryMarker> Markers = new();
    private static readonly HashSet<long> SystemsWithEligibleCompanions = new();
    private static double _cachedComponentCutoff = double.NaN;
    private static readonly List<PrimaryMarker> EligibleMarkers = new();
    private static double _cachedPrimaryCutoff = double.NaN;
    private static bool _cachedExcludeWdsOnly;
    private static bool _cachedExcludeVisual;
    private static Dictionary<long, MultipleStarCatalog.System> _systems = new();
    private static Dictionary<string, MultipleStarCatalog.Offset>? _layout;
    private static readonly HashSet<string> ApproximatePositions = new(StringComparer.Ordinal);
    private static readonly Dictionary<long, bool> ComponentVisibility = new();
    private static float _zoom = 1;
    private static float _panEast;
    private static float _panNorth;
    public static bool ShowPrimaryRings;
    public static bool LimitRingsToCenter;
    public const float DefaultRingRadiusPercent = 25f;
    public static float RingRadiusPercent = DefaultRingRadiusPercent;
    public static double MagnitudeCutoff = DefaultMagnitudeCutoff;
    public static double ComponentMagnitudeCutoff = DefaultComponentMagnitudeCutoff;
    public static bool ExcludeWdsOnly = false;
    public static bool ExcludeVisual = false;
    public static bool TabVisible;
    public static bool IsMousePickerActive => StarPicker.IsActiveFor(PickerOwner);

    private static void Load() {
        _loadingOrbits ??= Task.Run(() => MultipleStarOrbits.Load(ModPaths.Combine("catalogs", "orb6orbits.txt")));
        if(_loadingOrbits.IsCompleted && _orbits is null && _orbitError is null) {
            if(_loadingOrbits.IsFaulted) {
                _orbitError = _loadingOrbits.Exception!.GetBaseException().Message;
                Console.WriteLine($"OhMyStars: could not load ORB6 catalog: {_loadingOrbits.Exception}");
            } else {
                _orbits = _loadingOrbits.GetAwaiter().GetResult();
                _orbitCurves = null;
            }
        }
        _loading ??= Task.Run(() => OmsCatalog.Value);
        if(!_loading.IsCompleted || _catalog is not null || _error is not null)
            return;
        if(_loading.IsFaulted) {
            _error = _loading.Exception!.GetBaseException().Message;
            Console.WriteLine($"OhMyStars: could not load Multi catalog: {_loading.Exception}");
            return;
        }
        _catalog = _loading.GetAwaiter().GetResult();
        _systems = _catalog.Systems.ToDictionary(s => s.Primary.Id);
        foreach(var system in _catalog.Systems) {
            var star = system.Primary;
            if(star.Role == "P" && star.Number("ra") is double ra && ra >= 0 && ra < 24 &&
                star.Number("dec") is double dec && dec >= -90 && dec <= 90)
            {
                double? distance = star.Number("dist");
                Markers.Add(new(star.Id, checked((int)star.Id), StarDirectionConverter.RaDecToDirection(ra, dec),
                    distance is > 0 ? (float)(1 / distance.Value) : 0, star.Number("mag")));
            }
        }
    }

    public static bool HasPrimary(long omsId) {
        Load();
        return _systems.TryGetValue(omsId, out var system) &&
            MultipleStarCatalog.HasEligibleCompanion(system, ComponentMagnitudeCutoff, ExcludeWdsOnly, ExcludeVisual);
    }

    private static void Select(long? id) {
        if(_selected == id)
            return;
        _selected = id;
        _layout = null;
        ApproximatePositions.Clear();
        _orbitCurves = null;
        ComponentVisibility.Clear();
        ResetPlot();
    }

    public static void DrawHelp() {
        if(ImGui.Button("Back")) {
            OhMyStarsWindow.CloseMultipleStarHelp();
            return;
        }
        ImGui.Separator();
        ImGui.TextWrapped("Multiple star systems are very common!  The way(s) in which stars get identified and recorded sometimes makes determining whether a star nearby to another is truly an orbital companion difficult.  Catalogs recording them often list them using their best data and estimations as being truly a system Companion, a purely optical Visual star from Earth's area persspective, or Questionable / Indeterminate.");
        ImGui.NewLine();
        ImGui.TextWrapped("The catalog used for OMS is compiled from several different sources - mainly the AT-HYG and WDS (Washington Double Stars) catalogs.  Efforts were made to sort out inconsistencies and which data to keep and use - it's an ongoing effort, so believe some of the stars information with skepticism.  If you find glaring errors or omissions please let the author know!");
    }

    public static void Draw() {
        Load();
        long? selection = OhMyStarsWindow.SelectedOmsId;
        if(_catalog is not null)
            Select(selection.HasValue && _systems.ContainsKey(selection.Value) ? selection : null);
        bool rings = ShowPrimaryRings;
        if(ImGui.Checkbox("Highlight multi star systems", ref rings)) {
            ShowPrimaryRings = rings;
            OhMyStarsWindow.SaveSettings();
        }
        if(ImGui.ArrowButton("##MultiFilters", _showFilters ? ImGuiDir.Down : ImGuiDir.Right))
            _showFilters = !_showFilters;
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Filters");
        ImGui.SameLine();
        if(ImGui.Button("?##MultiHelp"))
            OhMyStarsWindow.OpenMultipleStarHelp();
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Information");
        if(_showFilters)
            DrawFilters();
        ImGui.Separator();
        if(_error is not null) {
            ImGui.TextWrapped($"Could not load curated catalog: {_error}");
            if(ImGui.Button("Retry catalog load")) {
                _loading = null;
                _error = null;
            }
            return;
        }
        if(_catalog is null) {
            ImGui.Text("Loading curated multiple-star catalog...");
            return;
        }
        DrawSystem();
    }

    private static void DrawFilters() {
        float magnitudeCutoff = (float)MagnitudeCutoff;
        ConsoleWidgets.BeginRow("Primary apparent magnitude cutoff");
        if(ConsoleWidgets.SliderFloat("MultiMagnitudeCutoff", ref magnitudeCutoff, MinMagnitudeCutoff, MaxMagnitudeCutoff,
            magnitudeCutoff.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            MagnitudeCutoff = Math.Round(magnitudeCutoff, 2);
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        ConsoleWidgets.EndRow();
        float componentCutoff = (float)ComponentMagnitudeCutoff;
        ConsoleWidgets.BeginRow("Component apparent magnitude cutoff");
        if(ConsoleWidgets.SliderFloat("MultiComponentMagnitudeCutoff", ref componentCutoff, MinMagnitudeCutoff, MaxMagnitudeCutoff,
            componentCutoff.ToString("F2", CultureInfo.InvariantCulture), pending: false)) {
            ComponentMagnitudeCutoff = Math.Round(componentCutoff, 2);
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        ConsoleWidgets.EndRow();
        bool excludeWdsOnly = ExcludeWdsOnly;
        if(ImGui.Checkbox("Exclude WDS-only entries", ref excludeWdsOnly)) {
            ExcludeWdsOnly = excludeWdsOnly;
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        bool excludeVisual = ExcludeVisual;
        if(ImGui.Checkbox("Exclude Visual Entries", ref excludeVisual)) {
            ExcludeVisual = excludeVisual;
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        bool limitRings = LimitRingsToCenter;
        if(ImGui.Checkbox("Limit highlights to center radius", ref limitRings)) {
            LimitRingsToCenter = limitRings;
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        ConsoleWidgets.BeginRow("Highlight radius (% of screen height)");
        if(ConsoleWidgets.SliderFloat("MultiHighlightRadius", ref RingRadiusPercent, 0f, 50f,
            $"{RingRadiusPercent.ToString("F1", CultureInfo.InvariantCulture)}%", pending: false)) {
            Targets.Clear();
            OhMyStarsWindow.SaveSettings();
        }
        ConsoleWidgets.EndRow();
    }

    private static void DrawSystem() {
        if(!ShowPrimaryRings) {
            OhMyStarsWindow.KeepStarMousePicker();
        } else {
            KeepPrimaryMousePicker();
        }
        if(!_selected.HasValue || !_systems.TryGetValue(_selected.Value, out var system)) {
            if(OhMyStarsWindow.SelectedOmsId is long id && _catalog!.Stars.TryGetValue(id, out var star)) {
                DrawStarHeading(star);
                ImGui.Separator();
                DrawCoordinates(star);
                ImGui.Separator();
                ImGui.TextWrapped("The selected star has no recorded companions.");
            }
            return;
        }
        if(!MultipleStarCatalog.IsEntryVisible(system.Primary, ExcludeWdsOnly)) {
            ImGui.TextWrapped("Selected system has a WDS-only primary. Disable Exclude WDS-only entries to inspect it.");
            return;
        }
        if(!MultipleStarCatalog.HasEligibleCompanion(system, ComponentMagnitudeCutoff, ExcludeWdsOnly, ExcludeVisual)) {
            ImGui.TextWrapped("No companion for the selected star passes the current Multi filters.");
            return;
        }
        DrawStarHeading(system.Primary);
        if(ImGui.ArrowButton("##MultiCatalogIds", _showCatalogIds ? ImGuiDir.Down : ImGuiDir.Right))
            _showCatalogIds = !_showCatalogIds;
        if(_showCatalogIds) {
            foreach(var (catalog, column) in CatalogIdentifiers.Catalogs) {
                string value = Field("", system.Primary[column]);
                if(value.Length > 0)
                    ImGui.Text($"{(column == "hip" ? "HIP" : catalog)}: {StarDesignations.CatalogIdentifier(column, "", value, OhMyStarsWindow.SupportsStruveSymbols)}");
            }
        }
        ImGui.Separator();
        DrawCoordinates(system.Primary);
        ImGui.Separator();
        DrawComponents(system);
    }

    private static void KeepPrimaryMousePicker() {
        if(StarsEditWindow.VanillaMode) {
            if(StarPicker.IsActiveFor(PickerOwner))
                StarPicker.Cancel();
        } else if(OhMyStarsWindow.MousePointerEnabled &&
            (!StarPicker.Active || IsMousePickerActive || StarPicker.IsActiveFor("StarsMouse"))) {
            if(!IsMousePickerActive)
                StarPicker.StartCustom(PickerOwner,
                    mouse => MultipleStarCatalog.PickNearest(Targets, mouse.X, mouse.Y, RingRadius),
                    id => {
                        Select(id);
                        OhMyStarsWindow.SetSelectedOmsStar(id);
                    });
            StarPicker.KeepAlive(PickerOwner);
        }
    }

    private static void DrawStarHeading(MultipleStarCatalog.Star star) {
        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * 1.3f);
        DrawFields(StarDesignations.PrimaryHeading(star, OhMyStarsWindow.SupportsStruveSymbols));
        DrawFields(StarDesignations.Bayer(star["bayer"], star["con"]));
        ImGui.PopFont();
    }

    private static void DrawCoordinates(MultipleStarCatalog.Star star) {
        DrawFields(string.Join(" / ", new[] {
            Field("RA", star["ra"], "h"),
            Field("Dec", star["dec"], "deg")
        }.Where(field => field.Length > 0)));
    }

    private static void DrawComponents(MultipleStarCatalog.System system) {
        var eligibleMembers = system.Members.Where(star => MultipleStarCatalog.IsEntryVisible(star, ExcludeWdsOnly, ExcludeVisual)).ToArray();
        var unlisted = eligibleMembers.Where(star => MultipleStarCatalog.HasNoPositionData(star, system)).ToArray();
        var members = eligibleMembers.Where(star => !MultipleStarCatalog.HasNoPositionData(star, system))
            .OrderBy(star => MultipleStarCatalog.IsDimComponent(star, system.Primary, ComponentMagnitudeCutoff)).ToArray();
        List<string[]> rows = new();
        foreach(var star in members) {
            string relationship = star.RelationshipDisplay;
            var motion = MultipleStarCatalog.RelativeProperMotion(star, system.Primary);
            string motionText = string.Join("  ", new[] {
                Field("rel PM RA:", motion.Ra?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", "mas/year"),
                Field("rel PM dec:", motion.Dec?.ToString("0.###", CultureInfo.InvariantCulture) ?? "", "mas/year")
            }.Where(field => field.Length > 0));
            string[] fields = [
                relationship.Length > 0 ? $"{star.ListLabel} / {relationship}" : star.ListLabel,
                star.Number("mag") is double magnitude
                    ? $"App Mag:{magnitude.ToString("F2", CultureInfo.InvariantCulture)}" : "",
                star.Number("absmag") is double absoluteMagnitude
                    ? $"Abs Mag:{absoluteMagnitude.ToString("F2", CultureInfo.InvariantCulture)}" : "",
                MeasurementField("rho:", star["rho_arcsec"]),
                MeasurementField("theta:", star["theta_deg"]),
                Field("B-V:", star["ci"]),
                motionText.Length > 0 ? $"({motionText})" : ""
            ];
            rows.Add(fields);
        }
        float[] widths = new float[rows[0].Length];
        foreach(string[] row in rows)
            for(int column = 0; column < row.Length; column++)
                widths[column] = Math.Max(widths[column], ImGui.CalcTextSize(row[column]).X);
        float checkboxX = ImGui.GetCursorPosX();
        float startX = checkboxX + ImGui.GetFrameHeight() + ImGui.CalcTextSize("  ").X;
        float spacing = ImGui.CalcTextSize("  ").X;
        bool dimSection = false;
        for(int index = 0; index < rows.Count; index++) {
            var star = members[index];
            string[] row = rows[index];
            if(!dimSection && MultipleStarCatalog.IsDimComponent(star, system.Primary, ComponentMagnitudeCutoff)) {
                dimSection = true;
                ImGui.Separator();
                ImGui.TextWrapped($"Fainter than apparent magnitude {ComponentMagnitudeCutoff.ToString("F2", CultureInfo.InvariantCulture)} (check to plot)");
            }
            if(star.Id != system.Primary.Id) {
                ImGui.SetCursorPosX(checkboxX);
                bool visible = MultipleStarCatalog.IsPlotVisible(star, system.Primary, ComponentMagnitudeCutoff, ComponentVisibility, ExcludeWdsOnly, ExcludeVisual);
                if(ImGui.Checkbox($"##MultiVisible{star.Id}", ref visible))
                    ComponentVisibility[star.Id] = visible;
                ImGui.SameLine();
            }
            float x = startX;
            bool first = true;
            for(int column = 0; column < row.Length; column++) {
                if(row[column].Length > 0) {
                    if(!first)
                        ImGui.SameLine();
                    ImGui.SetCursorPosX(x);
                    ImGui.Text(row[column]);
                    first = false;
                }
                if(widths[column] > 0)
                    x += widths[column] + spacing;
            }
        }
        if(unlisted.Length > 0)
            ImGui.TextWrapped($"Components not listed becuase contain no ra/dec, or rho values: {string.Join(", ", unlisted.Select(star => star.Label))}");
        ImGui.Separator();
        ImGui.Text("rho:  angular separation (arcseconds)");
        ImGui.Text("theta: postion angle (deg)");
        if(members.Any(star => star.IsWdsPosition))
            ImGui.Text("* = WDS only sourced entry");
        DrawPlot(system);
    }

    private static void ResetPlot() {
        _zoom = 1;
        _panEast = _panNorth = 0;
    }

    private static string MeasurementField(string label, string value) =>
        Field(label, string.Join("|", value.Split('|').Where(item => Field("", item).Length > 0)
            .Select(item => double.Parse(item, CultureInfo.InvariantCulture).ToString("F1", CultureInfo.InvariantCulture))));

    private static string Field(string label, string value, string unit = "") {
        if(string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("U", StringComparison.OrdinalIgnoreCase))
            return "";
        return $"{label} {value} {unit}".Trim();
    }

    private static void DrawFields(params string[] fields) {
        string text = string.Join("  ", fields.Where(field => field.Length > 0));
        if(text.Length > 0)
            ImGui.TextWrapped(text);
    }

    private static ImColor8 Color(MultipleStarCatalog.Star star) {
        if(star.Number("ci") is not double ci)
            return new ImColor8(220, 220, 220, 255);
        byte4 rgb = Astronomical.BvToRgb(star["mag_src"] == "T" ? ci * 0.85 : ci);
        return new ImColor8(rgb.X, rgb.Y, rgb.Z, 255);
    }

    private static void DrawPlot(MultipleStarCatalog.System system) {
        var layout = _layout ??= MultipleStarCatalog.Layout(system, approximateCoordinates: true,
            approximateLabels: ApproximatePositions);
        ImGui.Separator();
        if(_orbitError is not null) {
            ImGui.TextWrapped($"Could not load ORB6 orbits: {_orbitError}");
            if(ImGui.Button("Retry orbit catalog load")) {
                _loadingOrbits = null;
                _orbitError = null;
            }
        } else if(_orbits is null) {
            ImGui.Text("Loading ORB6 orbits...");
        } else {
            _orbitCurves ??= _orbits.Curves(system, layout);
        }
        float availableWidth = ImGui.GetContentRegionAvail().X;
        if(availableWidth < 100f)
            return;
        float size = Math.Min(availableWidth, 420f);
        float2 origin = ImGui.GetCursorScreenPos();
        float2 dimensions = new(size, size);
        ImGui.InvisibleButton(new ImString("##MultiPlot"), in dimensions, ImGuiButtonFlags.None);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        ImDrawListExtensions.AddRectFilled(draw, origin, origin + dimensions, new ImColor8(12, 16, 24, 255), 0f, ImDrawFlags.None);
        var visibleMembers = system.Members.Where(star =>
            MultipleStarCatalog.IsPlotVisible(star, system.Primary, ComponentMagnitudeCutoff, ComponentVisibility, ExcludeWdsOnly, ExcludeVisual)).ToArray();
        string missingPositions = MultipleStarCatalog.MissingPositionText(visibleMembers, layout);
        string approximatePositions = string.Join(", ", visibleMembers.Where(star =>
            ApproximatePositions.Contains(star.Label)).Select(star => star.Label));
        string notice = string.Join("\n", new[] { missingPositions,
            approximatePositions.Length > 0 ? $"RA/Dec Approx - {approximatePositions}" : "" }.Where(text => text.Length > 0));
        if(notice.Length > 0) {
            float2 noticeEnd = origin + dimensions;
            ImDrawListExtensions.PushClipRect(draw, in origin, in noticeEnd, true);
            ImDrawListExtensions.AddText(draw, origin + new float2(6, 6),
                new ImColor8(240, 240, 240, 255), notice);
            ImDrawListExtensions.PopClipRect(draw);
            float noticeHeight = ImGui.CalcTextSize(notice).Y + 12;
            origin += new float2(noticeHeight * 0.5f, noticeHeight);
            size -= noticeHeight;
        }
        double extent = visibleMembers.Where(star => layout.ContainsKey(star.Label))
            .Select(star => layout[star.Label]).Select(p => Math.Sqrt(p.East * p.East + p.North * p.North)).DefaultIfEmpty(0).Max();
        var visibleLabels = visibleMembers.Select(s => s.Label).ToHashSet(StringComparer.Ordinal);
        var curves = _orbitCurves is not null
            ? _orbitCurves.Where(c => visibleLabels.Contains(c.Companion)).ToArray() : [];
        foreach(var curve in curves)
            foreach(var point in curve.Points)
                extent = Math.Max(extent, Math.Sqrt(point.East * point.East + point.North * point.North));
        MultipleStarCatalog.Offset pan = new(_panEast * extent, _panNorth * extent);
        var primaryPoint = MultipleStarCatalog.PlotPosition(new(0, 0), extent, size, _zoom, pan);
        float2 center = origin + new float2((float)primaryPoint.East, (float)primaryPoint.North);
        float2 clipEnd = origin + new float2(size, size);
        ImDrawListExtensions.PushClipRect(draw, in origin, in clipEnd, true);
        ImColor8 grid = new(90, 100, 115, 180);
        ImDrawListExtensions.AddLine(draw, new float2(center.X, origin.Y + 12), new float2(center.X, origin.Y + size - 12), grid, 1f);
        ImDrawListExtensions.AddLine(draw, new float2(origin.X + 12, center.Y), new float2(origin.X + size - 12, center.Y), grid, 1f);
        foreach(var curve in curves) {
            float2? previous = null;
            foreach(var point in curve.Points) {
                var projected = MultipleStarCatalog.PlotPosition(point, extent, size, _zoom, pan);
                float2 current = origin + new float2((float)projected.East, (float)projected.North);
                if(previous is float2 start)
                    ImDrawListExtensions.AddLine(draw, start, current, new ImColor8(100, 170, 210, 150), 1f);
                previous = current;
            }
        }
        List<(MultipleStarCatalog.Star Star, float2 Point, float Radius)> plotted = new();
        foreach(var star in visibleMembers) {
            if(!layout.TryGetValue(star.Label, out var offset))
                continue;
            var position = MultipleStarCatalog.PlotPosition(offset, extent, size, _zoom, pan);
            if(position.East < 0 || position.East > size || position.North < 0 || position.North > size)
                continue;
            float2 point = origin + new float2((float)position.East, (float)position.North);
            double delta = (star.Number("mag") ?? 8) - (system.Primary.Number("mag") ?? 8);
            float radius = (float)Math.Clamp(5 * Math.Pow(10, -0.08 * delta), 2, 8);
            plotted.Add((star, point, radius));
        }
        var labels = MultipleStarCatalog.PlaceLabels(plotted.Select(item => {
            float2 textSize = ImGui.CalcTextSize(item.Star.Label);
            return new MultipleStarCatalog.PlotLabel(item.Point.X - origin.X, item.Point.Y - origin.Y,
                item.Radius, textSize.X, textSize.Y);
        }).ToArray(), size, size);
        for(int i = 0; i < plotted.Count; i++) {
            var item = plotted[i];
            float2 textSize = ImGui.CalcTextSize(item.Star.Label);
            float2 label = origin + new float2(labels[i].X, labels[i].Y);
            float2 end = new(Math.Clamp(item.Point.X, label.X, label.X + textSize.X),
                Math.Clamp(item.Point.Y, label.Y, label.Y + textSize.Y));
            ImDrawListExtensions.AddLine(draw, item.Point, end, grid, 1f);
        }
        for(int i = 0; i < plotted.Count; i++) {
            var item = plotted[i];
            ImDrawListExtensions.AddCircleFilled(draw, item.Point, item.Radius, Color(item.Star), 0);
            ImDrawListExtensions.AddText(draw, origin + new float2(labels[i].X, labels[i].Y),
                new ImColor8(240, 240, 240, 255), item.Star.Label);
        }
        ImDrawListExtensions.PopClipRect(draw);
        ConsoleWidgets.BeginRow("Plot zoom");
        ConsoleWidgets.SliderFloat("MultiPlotZoom", ref _zoom, 1f, 10f,
            $"{_zoom.ToString("F1", CultureInfo.InvariantCulture)}x", pending: false);
        ConsoleWidgets.EndRow();
        ConsoleWidgets.BeginRow("Pan east / west");
        ConsoleWidgets.SliderFloat("MultiPlotPanEast", ref _panEast, -1f, 1f,
            _panEast.ToString("F2", CultureInfo.InvariantCulture), pending: false);
        ConsoleWidgets.EndRow();
        ConsoleWidgets.BeginRow("Pan north / south");
        ConsoleWidgets.SliderFloat("MultiPlotPanNorth", ref _panNorth, -1f, 1f,
            _panNorth.ToString("F2", CultureInfo.InvariantCulture), pending: false);
        ConsoleWidgets.EndRow();
        if(ImGui.Button("Reset plot view"))
            ResetPlot();
    }

    public static unsafe void DrawOverlay(ImDrawListPtr draw, Camera camera, double3 center, double radius) {
        Targets.Clear();
        if(!ShowPrimaryRings || !TabVisible || _catalog is null)
            return;
        bool componentCutoffChanged = _cachedComponentCutoff != ComponentMagnitudeCutoff ||
            _cachedExcludeWdsOnly != ExcludeWdsOnly || _cachedExcludeVisual != ExcludeVisual;
        if(componentCutoffChanged) {
            SystemsWithEligibleCompanions.Clear();
            foreach(var marker in Markers)
                if(MultipleStarCatalog.HasEligibleCompanion(_systems[marker.Id], ComponentMagnitudeCutoff, ExcludeWdsOnly, ExcludeVisual))
                    SystemsWithEligibleCompanions.Add(marker.Id);
            _cachedComponentCutoff = ComponentMagnitudeCutoff;
            _cachedExcludeWdsOnly = ExcludeWdsOnly;
            _cachedExcludeVisual = ExcludeVisual;
        }
        if(componentCutoffChanged || _cachedPrimaryCutoff != MagnitudeCutoff) {
            EligibleMarkers.Clear();
            foreach(var marker in Markers)
                if(MultipleStarCatalog.IsWithinMagnitudeCutoff(marker.Magnitude, MagnitudeCutoff) &&
                    SystemsWithEligibleCompanions.Contains(marker.Id))
                    EligibleMarkers.Add(marker);
            _cachedPrimaryCutoff = MagnitudeCutoff;
        }
        double3 observer = GameStarSpace.GetObserverPc(camera);
        ImGuiViewport* viewport = ImGui.GetMainViewport();
        float2 viewportOrigin = viewport == null ? new(0, 0) : viewport->Pos;
        float2 viewportSize = viewport == null ? ImGui.GetIO().DisplaySize : viewport->Size;
        float2 screenCenter = viewportOrigin + viewportSize * 0.5f;
        foreach(var marker in EligibleMarkers) {
            long id = marker.Id;
            double3 position;
            if(marker.Hip > 0 && SkyCulturesRenderer.TryGetGameBodyEgo(marker.Hip, camera, out var body))
                position = body;
            else {
                double3 shifted = GameStarSpace.Shift(marker.Direction, marker.InverseParsecs, observer, out bool hidden);
                if(hidden)
                    continue;
                position = center + StellariumRenderer.ApplyAlignment(shifted) * radius;
            }
            if(!camera.IsPointWithinFov(position) || !StellariumRenderer.IsVisibleFromCamera(position))
                continue;
            float2 screen = StellariumRenderer.EgoToOverlayScreen(camera, position);
            if(LimitRingsToCenter && !MultipleStarCatalog.IsWithinCenterRadius(screen.X, screen.Y,
                screenCenter.X, screenCenter.Y, viewportSize.Y, RingRadiusPercent))
                continue;
            Targets.Add(new(id, screen.X, screen.Y));
            ImColor8 color = _selected == id ? new(0, 255, 140, 255) : new(120, 200, 255, 190);
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, new ImColor8(0, 0, 0, 180), 0, 3f);
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, color, 0, 1f);
        }
    }
}
