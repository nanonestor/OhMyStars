using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;

namespace OhMyStars;

internal static class MultipleStarView {
    private const string PickerOwner = "MultiPrimary";
    private const float RingRadius = 8f;
#if DEBUG
    private static readonly float4 DefinitiveTextColor = new(0, 1, 0, 1);
    private static string? _approvalError;
    private static readonly Dictionary<long, ImInputString> ReviewNotes = new();
    private static readonly HashSet<long> ShowReviewNotes = new();
    private static readonly Dictionary<long, string> ReviewErrors = new();
    private static readonly Dictionary<long, MultipleStarCatalog.ComponentReview> FailedReviews = new();
#endif
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
#if DEBUG
        _approvalError = null;
#endif
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
#if DEBUG
        if(_selected is long primaryId && _systems.TryGetValue(primaryId, out var reviewSystem) &&
            reviewSystem.Primary.Role == "P" && _catalog is not null) {
            bool approved = _catalog.DefinitivelyCorrectPrimaries.Contains(primaryId);
            float approvalRowWidth = ImGui.GetContentRegionAvail().X;
            string approvalLabel = approved ? "Undo approval" : "Approve as correct";
            if(ImGui.Button(approvalLabel + "##MultiApproval")) {
                try {
                    _catalog.SetDefinitiveApproval(primaryId, !approved);
                    _approvalError = null;
                } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException) {
                    _approvalError = $"Could not save system approval: {ex.Message}";
                    Console.WriteLine($"OhMyStars: {_approvalError}");
                }
            }
            if(ImGui.IsItemHovered())
                ConsoleWidgets.Tooltip("Developer review only. Saved locally for later import; does not change component types.");
            if(_catalog.DefinitivelyCorrectPrimaries.Contains(primaryId)) {
                const string status = "Definitively correct";
                float statusWidth = ImGui.CalcTextSize(status).X;
                if(approvalRowWidth >= ImGui.CalcTextSize(approvalLabel).X +
                    ImGui.GetStyle().FramePadding.X * 2 + statusWidth + ImGui.GetStyle().ItemSpacing.X) {
                    ImGui.SameLine();
                }
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() +
                    Math.Max(0, ImGui.GetContentRegionAvail().X - statusWidth));
                ImGui.TextColored(in DefinitiveTextColor, status);
            }
            if(_approvalError is not null)
                ImGui.TextWrapped(_approvalError);
        }
#endif
        bool rings = ShowPrimaryRings;
        if(ImGui.Checkbox("Highlight multi star systems", ref rings)) {
            ShowPrimaryRings = rings;
            OhMyStarsWindow.SaveSettings();
        }
        ImGui.SameLine();
        // makes the button right aligned to the window
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.CalcTextSize("? Information##MultiHelp").X);
        if(ImGui.Button("? Information##MultiHelp"))
            OhMyStarsWindow.OpenMultipleStarHelp();
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Information");
        ImGui.Text("Filters");
        ImGui.SameLine();
        if(ImGui.ArrowButton("##MultiFilters", _showFilters ? ImGuiDir.Down : ImGuiDir.Right))
            _showFilters = !_showFilters;
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Filters");
        

        if(_showFilters) {
            ImGui.Separator();
            DrawFilters();
        }
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
            Field("RA", star.Number("ra")?.ToString("F3", CultureInfo.InvariantCulture) ?? "", "h"),
            Field("Dec", star.Number("dec")?.ToString("F3", CultureInfo.InvariantCulture) ?? "", "deg")
        }.Where(field => field.Length > 0)));
    }

    private static void DrawComponents(MultipleStarCatalog.System system) {
        string rho = ImGui.GetFont().IsGlyphInFont(0x03c1) ? "\u03c1" : "rho";
        string theta = ImGui.GetFont().IsGlyphInFont(0x03b8) ? "\u03b8" : "theta";
        string[] headers = ["Component / Rel", "App Mag",
            rho == "rho" ? "rho" : $"{rho} rho",
            theta == "theta" ? "Theta" : $"{theta} Theta",
            "rel PM", "Spectral", "Abs Mag"];
        var eligibleMembers = system.Members.Where(star => MultipleStarCatalog.IsEntryVisible(star, ExcludeWdsOnly, ExcludeVisual)).ToArray();
        var unlisted = eligibleMembers.Where(star => MultipleStarCatalog.HasNoPositionData(star, system)).ToArray();
        var members = eligibleMembers.OrderBy(star => MultipleStarCatalog.HasNoPositionData(star, system))
            .ThenBy(star => MultipleStarCatalog.IsDimComponent(star, system.Primary, ComponentMagnitudeCutoff)).ToArray();
        List<string[]> rows = new();
        foreach(var star in members) {
            string relationship = star.RelationshipDisplay;
            string[] fields = [
                relationship.Length > 0 ? $"{star.ListLabel} / {relationship}" : star.ListLabel,
                star.Number("mag") is double magnitude
                    ? magnitude.ToString("F2", CultureInfo.InvariantCulture) : "",
                MeasurementField("", star["rho_arcsec"]),
                MeasurementField("", star["theta_deg"]),
                MultipleStarCatalog.RelativeProperMotionMagnitude(star, system.Primary)?.ToString("0.###", CultureInfo.InvariantCulture) ?? "U",
                Field("", star["spect"]),
                star.Number("absmag") is double absoluteMagnitude
                    ? absoluteMagnitude.ToString("F2", CultureInfo.InvariantCulture) : ""
            ];
            rows.Add(fields);
        }
        int[] columns = Enumerable.Range(0, headers.Length).ToArray();
        float[] widths = headers.Select(header => ImGui.CalcTextSize(header).X).ToArray();
        widths[5] += ImGui.GetStyle().FramePadding.X * 2;
        foreach(string[] row in rows)
            for(int column = 0; column < row.Length; column++)
                widths[column] = Math.Max(widths[column], ImGui.CalcTextSize(row[column]).X);
#if DEBUG
        float reviewWidth = ImGui.CalcTextSize("C - Companion").X + ImGui.GetFrameHeight() * 2 +
            ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X * 2;
        float componentTextWidth = widths[0];
        widths[0] += reviewWidth + ImGui.GetStyle().ItemSpacing.X;
#endif
        float checkboxX = ImGui.GetCursorPosX();
        float startX = checkboxX + ImGui.GetFrameHeight() + ImGui.CalcTextSize("  ").X;
        float spacing = ImGui.CalcTextSize("  ").X;
        float headerX = startX;
        foreach(int column in columns) {
            if(column > 0)
                ImGui.SameLine();
            ImGui.SetCursorPosX(headerX);
            if(headers[column] == "Spectral") {
                if(ImGui.SmallButton("Spectral##MultiSpectralLegend"))
                    OhMyStarsWindow.OpenSpectrumLegend();
            } else {
                ImGui.AlignTextToFramePadding();
                ImGui.Text(headers[column]);
            }
            headerX += widths[column] + spacing;
        }
        ImGui.Separator();
        bool dimSection = false;
        for(int index = 0; index < rows.Count; index++) {
            var star = members[index];
            string[] row = rows[index];
            if(!dimSection && MultipleStarCatalog.IsDimComponent(star, system.Primary, ComponentMagnitudeCutoff)) {
                dimSection = true;
                ImGui.Separator();
                ImGui.TextWrapped($"Fainter than apparent magnitude {ComponentMagnitudeCutoff.ToString("F2", CultureInfo.InvariantCulture)} (check to plot)");
            }
            if(star.Id != system.Primary.Id && !MultipleStarCatalog.HasNoPositionData(star, system)) {
                ImGui.SetCursorPosX(checkboxX);
                bool visible = MultipleStarCatalog.IsPlotVisible(star, system.Primary, ComponentMagnitudeCutoff, ComponentVisibility, ExcludeWdsOnly, ExcludeVisual);
                if(ImGui.Checkbox($"##MultiVisible{star.Id}", ref visible))
                    ComponentVisibility[star.Id] = visible;
                ImGui.SameLine();
            }
            float x = startX;
            bool first = true;
            foreach(int column in columns) {
                if(row[column].Length > 0) {
                    if(!first)
                        ImGui.SameLine();
                    ImGui.SetCursorPosX(x);
#if DEBUG
                    ImGui.AlignTextToFramePadding();
#endif
                    ImGui.Text(row[column]);
#if DEBUG
                    if(column == 0) {
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(x + componentTextWidth + ImGui.GetStyle().ItemSpacing.X);
                        DrawComponentReview(system, star, reviewWidth);
                    }
#endif
                    first = false;
                }
                if(widths[column] > 0)
                    x += widths[column] + spacing;
            }
#if DEBUG
            DrawComponentReviewNotes(system, star, startX);
#endif
        }
        if(unlisted.Length > 0)
            ImGui.TextWrapped($"Components without position data (no RA/Dec or rho) are shown above without a plotted position: {string.Join(", ", unlisted.Select(star => star.Label))}");
        ImGui.Separator();
        ImGui.Text($"{(rho == "rho" ? "" : rho + " / ")}rho:  angular separation (arcseconds)");
        ImGui.Text($"{(theta == "theta" ? "" : theta + " / ")}theta: position angle (deg)");
        ImGui.Text("rel PM: relative proper motion (mas/year); magnitude of both axes, or the available axis; U = undetermined");
        if(members.Any(star => star.IsWdsPosition))
            ImGui.Text("* = WDS only sourced entry");
        DrawPlot(system);
    }

#if DEBUG
    private static bool SaveComponentReview(MultipleStarCatalog.System system, MultipleStarCatalog.Star star,
        string desiredType, string notes) {
        try {
            _catalog!.SetComponentReview(system.Primary.Id, star.Id, desiredType, notes);
            ReviewErrors.Remove(star.Id);
            FailedReviews.Remove(star.Id);
            return true;
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException) {
            ReviewErrors[star.Id] = $"Could not save component {star.Label} review: {ex.Message}";
            FailedReviews[star.Id] = new(desiredType, notes);
            Console.WriteLine($"OhMyStars: {ReviewErrors[star.Id]}");
            return false;
        }
    }

    private static void DrawComponentReview(MultipleStarCatalog.System system, MultipleStarCatalog.Star star, float width) {
        var review = _catalog!.GetComponentReview(star.Id);
        string desiredType = review?.DesiredType ?? star.Role;
        bool showNotes = ShowReviewNotes.Contains(star.Id) || !string.IsNullOrEmpty(review?.Notes);
        ImGui.SetNextItemWidth(width - ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X);
        string preview = desiredType switch {
            "U" => "U - Unknown", "unlink" => "Unlink",
            _ => MultipleStarCatalog.ComponentReviewTypes.First(option => option.Type == desiredType).Label
        };
        if(ImGui.BeginCombo($"##MultiReviewType{star.Id}", preview)) {
            foreach(var (type, label) in MultipleStarCatalog.ComponentReviewTypes) {
                if(ImGui.Selectable(label, desiredType == type)) {
                    string notes = showNotes && ReviewNotes.TryGetValue(star.Id, out var buffer)
                        ? buffer.Value.ToString() : review?.Notes ?? "";
                    if(SaveComponentReview(system, star, type, notes))
                        desiredType = type;
                }
            }
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Developer request for later catalog import only. Select the original type to cancel a type change. Remove / unlink detaches this star from this system, not from the catalog.");
        ImGui.SameLine();
        bool notesChecked = showNotes;
        if(ImGui.Checkbox($"##MultiReviewNotesEnabled{star.Id}", ref notesChecked)) {
            if(notesChecked) {
                ShowReviewNotes.Add(star.Id);
            } else if(SaveComponentReview(system, star, desiredType, "")) {
                ShowReviewNotes.Remove(star.Id);
                ReviewNotes.Remove(star.Id);
            }
        }
        if(ImGui.IsItemHovered())
            ConsoleWidgets.Tooltip("Add a catalog correction note. Uncheck to clear the saved note.");
    }

    private static void DrawComponentReviewNotes(MultipleStarCatalog.System system, MultipleStarCatalog.Star star, float x) {
        var review = _catalog!.GetComponentReview(star.Id);
        if(ShowReviewNotes.Contains(star.Id) || !string.IsNullOrEmpty(review?.Notes)) {
            if(!ReviewNotes.TryGetValue(star.Id, out var buffer)) {
                string notes = review?.Notes ?? "";
                buffer = new ImInputString(Math.Max(4096, System.Text.Encoding.UTF8.GetByteCount(notes) + 1024));
                buffer.SetValue(notes.AsSpan());
                ReviewNotes.Add(star.Id, buffer);
            }
            ImGui.SetCursorPosX(x);
            ImGui.SetNextItemWidth(Math.Max(100, ImGui.GetContentRegionAvail().X));
            if(ImGui.InputText(new ImString($"##MultiReviewNotes{star.Id}"), buffer))
                SaveComponentReview(system, star, review?.DesiredType ?? star.Role, buffer.Value.ToString());
        }
        if(ReviewErrors.TryGetValue(star.Id, out string? error)) {
            ImGui.TextWrapped(error);
            if(ImGui.SmallButton($"Retry save##MultiReviewRetry{star.Id}")) {
                var failed = FailedReviews[star.Id];
                if(SaveComponentReview(system, star, failed.DesiredType, failed.Notes) && failed.Notes.Length == 0) {
                    ShowReviewNotes.Remove(star.Id);
                    ReviewNotes.Remove(star.Id);
                }
            }
        }
    }
#endif

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
        var projectedCurves = curves.Select(curve => (curve.Companion,
            Points: curve.Points.Select(point => {
                var projected = MultipleStarCatalog.PlotPosition(point, extent, size, _zoom, pan);
                return new MultipleStarCatalog.Offset((float)projected.East, (float)projected.North);
            }).ToArray())).ToArray();
        foreach(var curve in projectedCurves) {
            float2? previous = null;
            foreach(var point in curve.Points) {
                float2 current = origin + new float2((float)point.East, (float)point.North);
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
        if(MultipleStarCatalog.PlotScaleBar(extent, size, _zoom) is { } scaleBar) {
            string scaleLabel = $"{scaleBar.Arcseconds.ToString("G", CultureInfo.InvariantCulture)} " +
                (scaleBar.Arcseconds == 1 ? "arcsecond" : "arcseconds");
            float2 labelSize = ImGui.CalcTextSize(scaleLabel);
            float2 scaleOrigin = origin + new float2(12, size - labelSize.Y - 22);
            float2 start = scaleOrigin + new float2(0, 4);
            float2 end = start + new float2((float)scaleBar.Pixels, 0);
            ImColor8 scaleColor = new(220, 225, 235, 255);
            ImDrawListExtensions.AddLine(draw, start, end, scaleColor, 1f);
            ImDrawListExtensions.AddLine(draw, start - new float2(0, 4), start + new float2(0, 4), scaleColor, 1f);
            ImDrawListExtensions.AddLine(draw, end - new float2(0, 4), end + new float2(0, 4), scaleColor, 1f);
            ImDrawListExtensions.AddText(draw, scaleOrigin + new float2(0, 12), scaleColor, scaleLabel);
        }
        ImDrawListExtensions.PopClipRect(draw);
        if(plotted.Any(item => projectedCurves.Any(curve => curve.Companion == item.Star.Label &&
            !MultipleStarOrbits.TouchesCurve(new(item.Point.X - origin.X, item.Point.Y - origin.Y),
                item.Radius, curve.Points))))
            ImGui.TextWrapped("* some orbit data and observed location data disagree");
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
            ImColor8 color = new(120, 200, 255, 190);
#if DEBUG
            if(_catalog.DefinitivelyCorrectPrimaries.Contains(id))
                color = new(0, 255, 0, 255);
#endif
            if(_selected == id)
                color = new(255, 255, 0, 255);
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, new ImColor8(0, 0, 0, 180), 0, 3f);
            ImDrawListExtensions.AddCircle(draw, screen, RingRadius, color, 0, 1f);
        }
    }
}
