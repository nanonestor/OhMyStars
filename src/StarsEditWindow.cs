using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;
using System.Reflection;
using ImGui = Brutal.ImGuiApi.ImGui;

namespace OhMyStars;

// Star binary switcher window (formerly the standalone StarsEdit mod).
internal static partial class StarsEditWindow
{
	private sealed class BinaryEntry
	{
		public required Mod OwnerMod { get; init; }

		public required string ModName { get; init; }

		public required string RelativePath { get; init; }

		public required string FullPath { get; init; }

		public bool Enabled { get; set; }
	}

	// Game-styled choice modal, laid out like the game's own ConfirmActionPopup.
	private sealed class ChoicePopup : Popup
	{
		private readonly string _title;

		private readonly string _message;

		private readonly PopupButton<Popup>[] _buttons;

		public ChoicePopup(string title, string message, params (string Label, Action? Action, PopupButtonKind Kind)[] choices)
		{
			_title = title.ToUpperInvariant();
			_message = message;
			_buttons = choices.Select(static choice => Popup.CreateButton<Popup>(choice.Label, popup =>
			{
				choice.Action?.Invoke();
				popup.Active = false;
			}, choice.Kind)).ToArray();
		}

		protected override void OnDrawUi()
		{
			if (BeginConsoleModal(WindowId.AsSpan(), _title.AsSpan(), Popup.DEFAULT_SIZE_UV))
			{
				ConsoleStyle.BeginBody();
				ConsoleStyle.PushWidgetStyle();
				Popup.CenteredWrappedText(_message.AsSpan());
				EndConsoleBody();
				Popup.DrawConsoleButtonRow<Popup>(this, _buttons);
				Popup.EndConsoleModal();
			}
		}
	}

	private static void ShowYesNo(string title, string message, Action onYes)
	{
		new ChoicePopup(title, message, ("YES", onYes, PopupButtonKind.Primary), ("NO", null, PopupButtonKind.Neutral));
	}

	private sealed class SavedSetting
	{
		public required string Name { get; init; }

		public required SliderSettings Sliders { get; init; }

		// Each entry is "ModName|RelativePath" of a selected star binary.
		public required List<string> Binaries { get; init; }
	}

	private const string SavedSectionPrefix = "Saved:";

	private static readonly List<SavedSetting> SavedSettings = new List<SavedSetting>();

	private static readonly ImInputString _saveNameBuffer = new ImInputString(128);

	// Slider values and selected binaries as the mod first loaded them this session.
	private static SliderSettings _initialSliders = new SliderSettings();

	private static List<string> _initialBinaries = new List<string>();

	private const string ModNamespace = "OhMyStars";
	private const string SettingsFileName = "starsedit_settings.ini";

	private const int CapacityPadding = 1_000;

	private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

	private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	private static readonly List<BinaryEntry> Entries = new List<BinaryEntry>();

	// Each loaded mod's StarBinaries as the game originally loaded them, before any edits.
	private static readonly Dictionary<Mod, string[]> DefaultStarBinaries = new Dictionary<Mod, string[]>();

	private static Mod? _mod;

	internal static bool VanillaMode;

	private const string CuratedBinaryName = StarIdentityFile.DefaultBinaryName;
	// Earlier names of the default binary and its companions; removed so they don't show up as extra binaries.
	private static readonly string[] LegacyCuratedFiles =
	{
		"OhMyStars_curated.bin", "OhMyStars_curated.ohmystars.bin", "OhMyStars_curated.bin.version",
		// Default binaries used to live in the mod root; they are now in the binaries folder.
		StarIdentityFile.DefaultBinaryName, StarIdentityFile.DefaultIdentityName, StarIdentityFile.DefaultBinaryName + ".version",
	};

	private static string CuratedRelativePath => BinariesFolderName + "/" + CuratedBinaryName;

	private static string CuratedCatalogPath => ModPaths.Combine("catalogs", "oms_catalog.csv");

	private static string _status = "Waiting for mods to load...";

	private static bool _showWindow = true;

	private static readonly float2 DefaultWindowSize = new float2(900f, 720f);
	private static float _defaultMagnitudeScale;

	private static float _magnitudeScale;

	private static float _sizeCurveGamma = 1f;

	private static float _sizeLog2Offset = 0f;

	private static int _renderDataAdditiveOffset = 0;

	private static int _sizeFloorThreshold = 0;

	private static float _colorMultiplier = 1f;

	private static float _redMultiplier = 1f;

	private static float _greenMultiplier = 1f;

	private static float _blueMultiplier = 1f;

	private static FieldInfo? _starTechniqueField;

	private static FieldInfo? _starBinariesField;

	private static FieldInfo? _maxInstancesField;

	private static FieldInfo? _lookupField;

	private static MethodInfo? _lookupGetListMethod;

	private static readonly List<string> LastApplyFileStats = new List<string>();

	private static bool _pendingInitialSettingsApply;

	private static bool _showTooltips = true;

	// Generator UI fields
	private enum StarsEditTab
	{
		Editor,
		Generator,
		SkyCultures
	}

	private static StarsEditTab _currentTab = StarsEditTab.Editor;

	private static readonly ImInputString _genInputCsvBuffer = new ImInputString(260);
	private static readonly ImInputString _genOutputBinBuffer = new ImInputString(260);
	private static readonly ImInputString _genExportBinBuffer = new ImInputString(260);
	// Defaults match the settings used to build the mod's own binaries\OhMyStars_stars.bin.
	private static SizingColorMode _genSizingColorMode = SizingColorMode.PowerShellFlux;
	private static bool _genUseAbsoluteMagnitudeForSizing;
	private static float _genMagnitudeScale = GeneratorOptions.DefaultMagnitudeScale;
	private static float _genStarScale = 5.0f;
	private static float _genMagnitudeOffset = -1.5f;
	private static float _genDimStart = 2.0f;
	private static float _genDimEnd = 0.0f;
	private static float _genMinBrightness = 0.1f;
	private static bool _genUseMaxDistance;
	private static float _genMaxDistance = 100f;
	private static bool _genUseMinDistance;
	private static float _genMinDistance = 1000f;
	private static bool _genUseMaxAbsMag;
	private static float _genMaxAbsMag = 5f;
	private static bool _genUseMaxAppMag = true;
	private static float _genMaxAppMag = 8.795f;
	private static bool _genDistanceInLightYears;
	private static bool _genAddSol = true;
	private static bool _genUseAlwaysInclude;
	private static bool _genAlwaysIncludeByProper = true;
	private static readonly ImInputString _genAlwaysIncludeBuffer = new ImInputString(1024);
	private static bool _genWriteSister = true;
	private static bool _genPathsInitialized = false;
	private static List<string> _genCatalogFiles = new();
	private static bool _genCatalogsScanned;
	private static int _lastSeenCompletedWriteJobs;

	private static string DocumentsModFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Kitten Space Agency", "mods", "OhMyStars");

	private static void ScanCatalogFiles()
	{
		_genCatalogsScanned = true;
		try
		{
			_genCatalogFiles = StarBinaryGenerator.FindCatalogFiles(new[] { ModPaths.ModRoot, DocumentsModFolder });
		}
		catch
		{
			_genCatalogFiles = new List<string>();
		}
	}

	private static string CatalogDisplayName(string path)
	{
		foreach (string root in new[] { ModPaths.ModRoot, DocumentsModFolder })
		{
			try
			{
				string rel = Path.GetRelativePath(root, path);
				if (!rel.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(rel))
				{
					return rel;
				}
			}
			catch
			{
			}
		}

		return path;
	}

	// Refreshes the binaries list once a generator/export job that wrote a file has finished.
	private static void CheckGeneratorJobsCompleted()
	{
		int completed = StarBinaryGenerator.CompletedWriteJobs;
		if (completed != _lastSeenCompletedWriteJobs && !StarBinaryGenerator.IsRunning)
		{
			_lastSeenCompletedWriteJobs = completed;
			RebuildEntriesFromMods();
			ScanCatalogFiles();
		}
	}

	private static void DrawTabButton(string label, StarsEditTab tab)
	{
		bool selected = _currentTab == tab;
		bool clicked = selected
			? OhMyStarsWindow.DrawColoredTabButton(label)
			: ConsoleWidgets.Button(label, $"StarsEditTab_{label}", default);

		if (clicked && !selected)
		{
			_currentTab = tab;
		}
	}

	private sealed class SliderSettings
	{
		public float MagnitudeScale { get; init; } = _defaultMagnitudeScale;

		public float SizeCurveGamma { get; init; } = 1f;

		public float SizeLog2Offset { get; init; }

		public int RenderDataAdditiveOffset { get; init; }

		public int SizeFloorThreshold { get; init; }

		public float ColorMultiplier { get; init; } = 1f;

		public float RedMultiplier { get; init; } = 1f;

		public float GreenMultiplier { get; init; } = 1f;

		public float BlueMultiplier { get; init; } = 1f;
	}

	private static void InitializeGeneratorPaths()
	{
		if (_genPathsInitialized)
		{
			return;
		}

		_genPathsInitialized = true;
		string defaultCsv = CuratedCatalogPath;
		if (!File.Exists(defaultCsv))
		{
			string altCsv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Kitten Space Agency", "mods", "OhMyStars", "catalogs", "oms_catalog.csv");
			if (File.Exists(altCsv))
			{
				defaultCsv = altCsv;
			}
		}
		_genInputCsvBuffer.SetValue(defaultCsv.AsSpan());

		_genOutputBinBuffer.SetValue("generated".AsSpan());
		_genExportBinBuffer.SetValue("export".AsSpan());
	}

	internal const string BinariesFolderName = "binaries";

	private static string BinariesFolder => ModPaths.Combine(BinariesFolderName);

	private static string SanitizeBaseName(string raw, string fallback)
	{
		string name = Path.GetFileName((raw ?? string.Empty).Trim());
		if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
		{
			name = name[..^4];
		}
		foreach (string suffix in new[] { StarIdentityFile.IdentitySuffix, StarIdentityFile.GameSuffix })
		{
			if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				name = name[..^suffix.Length];
				break;
			}
		}
		foreach (char c in Path.GetInvalidFileNameChars())
		{
			name = name.Replace(c.ToString(), string.Empty);
		}
		name = name.Trim().TrimEnd('.');
		return name.Length == 0 ? fallback : name;
	}

	private static string BuildOutputFileName(string raw, string fallback)
	{
		return SanitizeBaseName(raw, fallback) + StarIdentityFile.GameSuffix + ".bin";
	}

	private static string BuildSisterFileName(string raw, string fallback)
	{
		return SanitizeBaseName(raw, fallback) + StarIdentityFile.IdentitySuffix + ".bin";
	}

	private static string BuildOutputPath(string raw, string fallback)
	{
		Directory.CreateDirectory(BinariesFolder);
		return Path.Combine(BinariesFolder, BuildOutputFileName(raw, fallback));
	}

	private static GeneratorOptions BuildGeneratorOptions()
	{
		return new GeneratorOptions
		{
			UseMaxDistance = _genUseMaxDistance,
			MaxDistance = _genMaxDistance,
			UseMinDistance = _genUseMinDistance,
			MinDistance = _genMinDistance,
			UseMaxAbsoluteMagnitude = _genUseMaxAbsMag,
			MaxAbsoluteMagnitude = _genMaxAbsMag,
			UseMaxApparentMagnitude = _genUseMaxAppMag,
			MaxApparentMagnitude = _genMaxAppMag,
			DistanceInLightYears = _genDistanceInLightYears,
			AddSol = _genAddSol,
			WriteSisterFile = _genWriteSister,
			SizingColorMode = _genSizingColorMode,
			MagnitudeScale = _genMagnitudeScale,
			StarScale = _genStarScale,
			MagnitudeOffset = _genMagnitudeOffset,
			DimStart = _genDimStart,
			DimEnd = _genDimEnd,
			MinBrightness = _genMinBrightness,
			UseAbsoluteMagnitudeForSizing = _genUseAbsoluteMagnitudeForSizing,
			UseAlwaysInclude = _genUseAlwaysInclude,
			AlwaysIncludeList = _genAlwaysIncludeBuffer.Value.ToString(),
			AlwaysIncludeByProperName = _genAlwaysIncludeByProper,
		};
	}

	private static void DrawFilterRow(string label, string id, string tooltip, ref bool enabled, ref float value, string format)
	{
		ConsoleWidgets.BeginRow(label);
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip(tooltip);
		}
		ConsoleWidgets.Checkbox(id + "Use", ref enabled, pending: false);
		ConsoleWidgets.EndRow();

		// ConsoleWidgets.Checkbox right-aligns to the row end, so the value box gets its own row.
		if (enabled)
		{
			ConsoleWidgets.BeginRow("    Value");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip(tooltip);
			}
			ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
			ImGui.InputFloat("##" + id, ref value, 0f, 0f, format, ImGuiInputTextFlags.None);
			ConsoleWidgets.EndRow();
		}
	}

	private static void DrawGeneratorTab()
	{
		InitializeGeneratorPaths();

		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("20-Byte Star Binary Generator");

		if (_showTooltips)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Generates 20-byte distance-aware star binaries (3x float XYZ, 1x size, 3x RGB, 1x float invParsecs).");
		}

		// Catalog selector (CSV files in the mod folder that contain the required columns)
		if (!_genCatalogsScanned)
		{
			ScanCatalogFiles();
		}

		string currentCsv = _genInputCsvBuffer.Value.ToString();
		string currentFull;
		try
		{
			currentFull = string.IsNullOrWhiteSpace(currentCsv) ? string.Empty : Path.GetFullPath(currentCsv);
		}
		catch
		{
			currentFull = string.Empty;
		}
		int catalogIndex = _genCatalogFiles.FindIndex(f => string.Equals(f, currentFull, StringComparison.OrdinalIgnoreCase));
		if (catalogIndex < 0 && currentFull.Length > 0)
		{
			// Selected file disappeared after a rescan: clear the selection.
			_genInputCsvBuffer.SetValue(string.Empty.AsSpan());
		}

		ImGui.Text("Catalog CSV files");
		if (_showTooltips && ImGui.IsItemHovered())
		{
			ConsoleWidgets.Tooltip("CSV catalogs found in the mod folder (and subfolders) that have a magnitude column plus ra/dec (or x/y/z).\nThe toggled-on catalog is used by Generate, Analyze and Generate TOML. Only one can be on at a time.");
		}
		ImGui.SameLine();
		if (ConsoleWidgets.Button("Rescan", "GenCatalogRescan", default))
		{
			ScanCatalogFiles();
		}

		if (_genCatalogFiles.Count == 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "(no usable catalog CSV files found in the mod folder)");
		}
		for (int i = 0; i < _genCatalogFiles.Count; i++)
		{
			string file = _genCatalogFiles[i];
			bool on = i == catalogIndex;
			ConsoleWidgets.BeginRow(CatalogDisplayName(file));
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip(file);
			}
			if (ConsoleWidgets.Checkbox($"GenCatalog{i}", ref on, pending: false))
			{
				_genInputCsvBuffer.SetValue((on ? file : string.Empty).AsSpan());
			}
			ConsoleWidgets.EndRow();
		}

		// Output base name
		ConsoleWidgets.BeginRow("Output Name");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			string genName = _genOutputBinBuffer.Value.ToString();
			ConsoleWidgets.Tooltip("Base name for the generated files, written to the mod's '" + BinariesFolderName + "' folder:\n  "
				+ BuildOutputFileName(genName, "generated") + "  (game star binary)\n  "
				+ BuildSisterFileName(genName, "generated") + "  (Oh My Stars identity file)\n'_stars' and '_stars_IDs' are appended automatically.");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##GenOutputBin"), _genOutputBinBuffer);
		ConsoleWidgets.EndRow();

		// Sizing & Color Mode Toggle
		ConsoleWidgets.BeginRow("Sizing & Color");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip(_genSizingColorMode == SizingColorMode.PowerShellFlux
				? "PowerShell Flux: the mod's flux formula & B-V colours (default; used for the mod's own OhMyStars_stars.bin)."
				: "KSA Native: the game's starScale/brightness curve and BvToRgb (matches the game's own 'generatestarbinary'). Its Min Brightness cull can drop most faint stars.");
		}
		string sizingLabel = _genSizingColorMode == SizingColorMode.PowerShellFlux ? "PowerShell (Flux)" : "KSA Native";
		if (ConsoleWidgets.Button(sizingLabel, "GenSizingModeToggle", default))
		{
			_genSizingColorMode = _genSizingColorMode == SizingColorMode.PowerShellFlux
				? SizingColorMode.KsaNative
				: SizingColorMode.PowerShellFlux;
		}
		ConsoleWidgets.EndRow();

		// Magnitude Scale
		float magScale = _genMagnitudeScale;
		ConsoleWidgets.BeginRow("Magnitude Scale");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("brightness.magnitudeScale, also written to the binary header (the game uses it as the size-vs-distance exponent).\nDefault: 0.15. Range: 0.01 to 5.");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		if (ImGui.InputFloat("##GenMagScale", ref magScale, 0f, 0f, "%.2f", ImGuiInputTextFlags.CharsDecimal))
		{
			_genMagnitudeScale = Math.Clamp(magScale, 0.01f, 5.0f);
		}
		ConsoleWidgets.EndRow();

		ConsoleWidgets.BeginRow("Sizing Magnitude");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Whether star sizes use apparent magnitude (as seen from Sol) or the CSV's absmag column. The game rescales by distance at runtime, so apparent magnitude is the normal choice.");
		}
		string sizingMagnitudeLabel = _genUseAbsoluteMagnitudeForSizing ? "Absolute Magnitude" : "Apparent Magnitude";
		if (ConsoleWidgets.Button(sizingMagnitudeLabel, "GenSizingMagnitudeToggle", default))
		{
			_genUseAbsoluteMagnitudeForSizing = !_genUseAbsoluteMagnitudeForSizing;
		}
		ConsoleWidgets.EndRow();

		if (_genSizingColorMode == SizingColorMode.KsaNative)
		{
			float starScale = _genStarScale;
			ConsoleWidgets.BeginRow("Star Scale");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip("starScale: overall multiplier for sprite sizes (game default 5).");
			}
			if (ConsoleWidgets.SliderFloat("GenStarScale", ref starScale, 0.1f, 30f, starScale.ToString("F1", CultureInfo.InvariantCulture), pending: false))
			{
				_genStarScale = starScale;
			}
			ConsoleWidgets.EndRow();

			float magOffset = _genMagnitudeOffset;
			ConsoleWidgets.BeginRow("Magnitude Offset");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip("magnitudeOffset: shifts overall star brightness (game default -1.5).");
			}
			if (ConsoleWidgets.SliderFloat("GenMagOffset", ref magOffset, -5f, 5f, magOffset.ToString("F2", CultureInfo.InvariantCulture), pending: false))
			{
				_genMagnitudeOffset = magOffset;
			}
			ConsoleWidgets.EndRow();

			float dimStart = _genDimStart;
			ConsoleWidgets.BeginRow("Dim Start");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip("dimStart: below this size value stars stop shrinking and fade their colour instead (game default 2).");
			}
			if (ConsoleWidgets.SliderFloat("GenDimStart", ref dimStart, 0f, 10f, dimStart.ToString("F2", CultureInfo.InvariantCulture), pending: false))
			{
				_genDimStart = dimStart;
			}
			ConsoleWidgets.EndRow();

			float dimEnd = _genDimEnd;
			ConsoleWidgets.BeginRow("Dim End");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip("dimEnd: size value at which a star is fully faded (game default 0).");
			}
			if (ConsoleWidgets.SliderFloat("GenDimEnd", ref dimEnd, -5f, 5f, dimEnd.ToString("F2", CultureInfo.InvariantCulture), pending: false))
			{
				_genDimEnd = dimEnd;
			}
			ConsoleWidgets.EndRow();

			float minBrightness = _genMinBrightness;
			ConsoleWidgets.BeginRow("Min Brightness");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip("Stars whose dimmed colour is below this on every channel are dropped (game uses 0.1).");
			}
			if (ConsoleWidgets.SliderFloat("GenMinBrightness", ref minBrightness, 0f, 1f, minBrightness.ToString("F2", CultureInfo.InvariantCulture), pending: false))
			{
				_genMinBrightness = minBrightness;
			}
			ConsoleWidgets.EndRow();
		}

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Star Filters");
		if (_showTooltips)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Enabled filters combine inclusively: a star is kept if it passes ANY enabled filter. With no filter enabled every star is kept.");
		}

		string distUnit = _genDistanceInLightYears ? "ly" : "pc";
		ConsoleWidgets.BeginRow("Distance Units");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Units for the distance boxes below. The catalog is in parsecs; light-year values are converted (1 pc = 3.26156 ly).");
		}
		if (ConsoleWidgets.Button(_genDistanceInLightYears ? "Light Years" : "Parsecs", "GenDistUnitToggle", default))
		{
			const float LyPerPc = 3.26156f;
			_genDistanceInLightYears = !_genDistanceInLightYears;
			float factor = _genDistanceInLightYears ? LyPerPc : 1f / LyPerPc;
			_genMaxDistance *= factor;
			_genMinDistance *= factor;
		}
		ConsoleWidgets.EndRow();

		DrawFilterRow("Distance Closer Than", "GenMaxDist", "Keep stars nearer than this distance (" + distUnit + ").", ref _genUseMaxDistance, ref _genMaxDistance, "%.2f " + distUnit);
		DrawFilterRow("Distance Farther Than", "GenMinDist", "Keep stars farther than this distance (" + distUnit + ").", ref _genUseMinDistance, ref _genMinDistance, "%.2f " + distUnit);
		DrawFilterRow("Absolute Mag Below", "GenMaxAbsMag", "Keep stars whose absolute magnitude is lower (intrinsically brighter) than this.", ref _genUseMaxAbsMag, ref _genMaxAbsMag, "%.3f");
		DrawFilterRow("Apparent Mag Below", "GenMaxAppMag", "Keep stars whose apparent magnitude from Sol is lower (brighter) than this. 8.795 yields ~100k stars.", ref _genUseMaxAppMag, ref _genMaxAppMag, "%.3f");

		ConsoleWidgets.BeginRow("Always Include Stars");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Always writes the listed stars, regardless of the filters above and the dimness cull.");
		}
		ConsoleWidgets.Checkbox("GenAlwaysIncludeUse", ref _genUseAlwaysInclude, pending: false);
		ConsoleWidgets.EndRow();

		if (_genUseAlwaysInclude)
		{
			ConsoleWidgets.BeginRow("    Match By");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip(_genAlwaysIncludeByProper
					? "Entries are matched against the catalog's 'proper' column (e.g. Barnard's Star, Polaris). Case-insensitive. Click to match by catalog ID instead."
					: "Entries match 'oms_id' for OMS catalogs, otherwise the imported catalog's 'id'. Click to match by proper name instead.");
			}
			if (ConsoleWidgets.Button(_genAlwaysIncludeByProper ? "Proper Name" : "Catalog ID", "GenAlwaysIncludeMode", default))
			{
				_genAlwaysIncludeByProper = !_genAlwaysIncludeByProper;
			}
			ConsoleWidgets.EndRow();

			ConsoleWidgets.BeginRow("    Stars");
			if (_showTooltips && ConsoleWidgets.RowHovered)
			{
				ConsoleWidgets.Tooltip(_genAlwaysIncludeByProper
					? "Comma-delimited list of proper names, e.g. Polaris, Barnard's Star, Proxima Centauri"
					: "Comma-delimited catalog IDs (OMS IDs for the compiled OMS catalog), e.g. 1, 71683, 118254");
			}
			ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
			ImGui.InputText(new ImString("##GenAlwaysIncludeList"), _genAlwaysIncludeBuffer);
			ConsoleWidgets.EndRow();
		}

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Output Options");

		ConsoleWidgets.BeginRow("Add Sol");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adds the Sun at the origin (0,0,0) to the generated or exported binary, only if no star at the origin is already present. Sol is invisible from inside the solar system and appears as a star once you travel away.");
		}
		ConsoleWidgets.Checkbox("GenAddSol", ref _genAddSol, pending: false);
		ConsoleWidgets.EndRow();

		ConsoleWidgets.BeginRow("Write Identity File");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Also writes '<name>" + StarIdentityFile.IdentitySuffix + ".bin' next to '<name>" + StarIdentityFile.GameSuffix + ".bin': the mod's sister file holding each record's HIP/HD/HR/Gaia/Tycho IDs and proper name, used to link constellation lines, labels and pointers to the right stars. The game ignores it.");
		}
		ConsoleWidgets.Checkbox("GenWriteSister", ref _genWriteSister, pending: false);
		ConsoleWidgets.EndRow();

		ImGui.Dummy(new float2(0f, 4f));

		// Action buttons
		bool running = StarBinaryGenerator.IsRunning;
		bool noCatalog = _genInputCsvBuffer.Value.ToString().Trim().Length == 0;
		if (noCatalog)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Toggle on a catalog CSV above to enable Generate and Analyze.");
		}
		ConsoleStyle.PushDisabled(running || noCatalog);

		if (OhMyStarsWindow.DrawColoredTabButton("Generate Binary"))
		{
			StarBinaryGenerator.StartGeneration(
				_genInputCsvBuffer.Value.ToString(),
				BuildOutputPath(_genOutputBinBuffer.Value.ToString(), "generated"),
				BuildGeneratorOptions());
		}

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Analyze", "GenAnalyze", default))
		{
			StarBinaryGenerator.StartAnalysis(_genInputCsvBuffer.Value.ToString(), BuildGeneratorOptions());
		}
		if (_showTooltips && ImGui.IsItemHovered())
		{
			ConsoleWidgets.Tooltip("Dry run over the selected catalog with the current settings: counts how many star entries would be written, without writing any file.");
		}

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Generate TOML"))
		{
			try
			{
				string outPath = BuildOutputPath(_genOutputBinBuffer.Value.ToString(), "generated");
				string tomlPath = Path.ChangeExtension(outPath, ".toml");
				string datPath = Path.ChangeExtension(outPath, ".dat");
				int exported = StarBinaryGenerator.ExportCatalogAndManifest(
					_genInputCsvBuffer.Value.ToString(),
					datPath,
					tomlPath,
					_genMagnitudeScale,
					false,
					_genMaxAppMag,
					_genStarScale,
					_genMagnitudeOffset,
					_genDimStart,
					_genDimEnd);
				_status = $"Exported {exported:N0} stars to {Path.GetFileName(datPath)} with manifest {Path.GetFileName(tomlPath)}";
			}
			catch (Exception ex)
			{
				_status = $"TOML error: {ex.Message}";
			}
		}

		ImGui.SameLine();
		ConsoleStyle.PopDisabled();
		ConsoleStyle.PushDisabled(running);
		if (ConsoleWidgets.Button("Reset Defaults", "GenResetPaths", default))
		{
			_genPathsInitialized = false;
			InitializeGeneratorPaths();
		}
		if (_showTooltips && ImGui.IsItemHovered())
		{
			ConsoleWidgets.Tooltip("Re-selects the mod's default catalog and resets the output/export base names.");
		}

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Export Current Stars");
		if (_showTooltips)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Writes every star currently loaded in the game (all enabled binaries) to one new binary with header, plus its identity file.");
		}

		ConsoleWidgets.BeginRow("Export Name");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			string expName = _genExportBinBuffer.Value.ToString();
			ConsoleWidgets.Tooltip("Base name for the exported files, written to the mod's '" + BinariesFolderName + "' folder:\n  "
				+ BuildOutputFileName(expName, "export") + "  (game star binary)\n  "
				+ BuildSisterFileName(expName, "export") + "  (Oh My Stars identity file)\n'_stars' and '_stars_IDs' are appended automatically.");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##GenExportBin"), _genExportBinBuffer);
		ConsoleWidgets.EndRow();

		if (OhMyStarsWindow.DrawColoredTabButton("Export Current Stars"))
		{
			List<string> sources = Entries.Where(e => e.Enabled).Select(e => e.FullPath).ToList();
			StarBinaryGenerator.StartExportCurrentStars(
				BuildOutputPath(_genExportBinBuffer.Value.ToString(), "export"),
				_genAddSol,
				_genWriteSister,
				sources);
		}

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Refresh Binaries"))
		{
			RebuildEntriesFromMods();
		}

		ConsoleStyle.PopDisabled();

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Status");

		// Status readout
		ConsoleWidgets.Readout("Generator Status", StarBinaryGenerator.StatusMessage.AsSpan());
		if (running && StarBinaryGenerator.Progress > 0f)
		{
			ImGui.ProgressBar(StarBinaryGenerator.Progress, new float2(ImGui.GetContentRegionAvail().X, 16f), $"{(StarBinaryGenerator.Progress * 100f):F0}%");
		}

		StarBinaryGenerator.CatalogAnalysis? analysis = StarBinaryGenerator.LastAnalysis;
		if (analysis is not null)
		{
			ImGui.Dummy(new float2(0f, 4f));
			ConsoleWidgets.Readout("Stars that would be written", $"{analysis.TotalWritten:N0}".AsSpan());
			foreach (string line in analysis.ToLines(_genDistanceInLightYears))
			{
				ImGui.TextColored(in ConsoleStyle.TextMuted, line);
			}
		}
	}

	public static void ToggleWindow()
	{
		_showWindow = !_showWindow;
	}

	public static bool IsOpen
	{
		get => _showWindow;
		set => _showWindow = value;
	}

	public static void OnAllModsLoaded()
	{
		StarsEditPatcher.Patch();
		CaptureDefaultStarBinaries();
		_defaultMagnitudeScale = ReadDefaultMagnitudeScale();
		_magnitudeScale = _defaultMagnitudeScale;
		_mod = ResolveOwnMod();
		EnsureSettingsFileExists();
		LoadSettingsFromDisk(updateStatus: false);
		LoadSavedSettings();
		EnsureCuratedBinaryExists();
		RebuildEntriesFromMods();
		SelectStartupBinaries();
		_initialSliders = CaptureSliderSettings();
		_initialBinaries = CurrentBinaryKeys();
		_pendingInitialSettingsApply = true;
	}

	internal static void Update()
	{
		CheckGeneratorJobsCompleted();
		if (_pendingInitialSettingsApply && CanApply() && ApplySelection())
		{
			_pendingInitialSettingsApply = false;
		}
	}

	public static void Draw()
	{
		if (!_showWindow)
		{
			return;
		}

		// ConsoleStyle.BeginWindow forces NoSavedSettings, so a plain ImGui window is used instead to stay
		// resizable/movable/scrollable and to persist position+size in imgui.ini; ConsoleStyle only supplies widget colors.
		ImGui.SetNextWindowSize(DefaultWindowSize, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowBgAlpha(1f);
		if (!ImGui.Begin("Stars Editor - Star properties editor & Star Binaries Manager", ref _showWindow, ImGuiWindowFlags.None))
		{
			ImGui.End();
			return;
		}

		ConsoleStyle.PushWidgetStyle();

		DrawTabButton("Editor", StarsEditTab.Editor);
		ImGui.SameLine();
		DrawTabButton("Generator", StarsEditTab.Generator);
		ImGui.SameLine();
		DrawTabButton("Sky Cultures", StarsEditTab.SkyCultures);
		ImGui.Separator();

		ConsoleWidgets.BeginRow("Show Tooltips");
		ConsoleWidgets.Checkbox("ShowTooltips", ref _showTooltips, pending: false);
		ConsoleWidgets.EndRow();

		if (_currentTab == StarsEditTab.Generator)
		{
			DrawGeneratorTab();
			ConsoleStyle.PopWidgetStyle();
			ImGui.End();
			return;
		}

		if (_currentTab == StarsEditTab.SkyCultures)
		{
			DrawSkyCulturesTab();
			ConsoleStyle.PopWidgetStyle();
			ImGui.End();
			return;
		}

		bool canApply = _mod is not null && GetStarTechnique() is not null;
		DrawEditorTab(canApply);

		ConsoleStyle.PopWidgetStyle();
		ImGui.End();
	}

	private static void DrawEditorTab(bool canApply)
	{
		bool sliderChanged = false;

		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.Readout("Status", _status);

		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Render Tuning");

		float magnitudeScale = _magnitudeScale;
		ConsoleWidgets.BeginRow("Magnitude Scale");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("brightness.magnitudeScale: controls how star brightness falls off with distance.\nApplies to all selected binaries without changing their files. Enter a finite value greater than zero.\nRestore Defaults uses the last selected binary's header and keeps your binary selection.\nInitial game binary default: "
				+ _defaultMagnitudeScale.ToString("G9", CultureInfo.InvariantCulture) + ".");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		if (ImGui.InputFloat("##EditorMagnitudeScale", ref magnitudeScale, 0f, 0f, "%.6g"))
		{
			if (magnitudeScale > 0f && float.IsFinite(magnitudeScale))
			{
				_magnitudeScale = magnitudeScale;
				InstancedStarTechnique? starTechnique = GetStarTechnique();
				if (starTechnique is not null)
				{
					starTechnique.MagnitudeScale = magnitudeScale;
				}
			}
			else
			{
				TimedAlert.CreateWarning("Magnitude Scale must be a finite value greater than zero");
			}
		}
		ConsoleWidgets.EndRow();

		// Size Curve Gamma - Lower values boost mid/faint stars more; higher values make size fall off faster with magnitude
		float sizeCurveGamma = _sizeCurveGamma;
		ConsoleWidgets.BeginRow("Size Curve Gamma");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the curve of how star size falls off with magnitude. Lower values boost mid/faint stars more; higher values make size fall off faster with magnitude.");
		}
		if (ConsoleWidgets.SliderFloat("SizeCurveGamma", ref sizeCurveGamma, 0.01f, 2f, sizeCurveGamma.ToString("F2", CultureInfo.InvariantCulture), pending: false))
		{
			_sizeCurveGamma = sizeCurveGamma;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		float sizeLog2Offset = _sizeLog2Offset;
		ConsoleWidgets.BeginRow("Size Offset");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the overall size of stars. Positive values make stars larger; negative values make them smaller.");
		}
		string sizeOffsetText = (sizeLog2Offset >= 0f ? "+" : "") + sizeLog2Offset.ToString("F2", CultureInfo.InvariantCulture);
		if (ConsoleWidgets.SliderFloat("SizeOffset", ref sizeLog2Offset, -10f, 10f, sizeOffsetText, pending: false))
		{
			_sizeLog2Offset = sizeLog2Offset;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		int renderDataAdditiveOffset = _renderDataAdditiveOffset;
		ConsoleWidgets.BeginRow("Size +/- Scale");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the additive offset for star size data. Positive values make stars larger; negative values make them smaller.");
		}
		string additiveOffsetText = (renderDataAdditiveOffset >= 0 ? "+" : "") + renderDataAdditiveOffset.ToString(CultureInfo.InvariantCulture);
		if (ConsoleWidgets.SliderInt("SizeAdditiveScale", ref renderDataAdditiveOffset, -100, 100, additiveOffsetText, pending: false))
		{
			_renderDataAdditiveOffset = renderDataAdditiveOffset;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		int sizeFloorThreshold = _sizeFloorThreshold;
		ConsoleWidgets.BeginRow("Size Floor (Hide Below)");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Sets a threshold below which stars will be hidden. Stars with size data below this value will not be rendered.");
		}
		if (ConsoleWidgets.SliderInt("SizeFloor", ref sizeFloorThreshold, 0, 50, sizeFloorThreshold.ToString(CultureInfo.InvariantCulture), pending: false))
		{
			_sizeFloorThreshold = sizeFloorThreshold;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		float colorMultiplier = _colorMultiplier;
		ConsoleWidgets.BeginRow("Color Multiplier");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the overall brightness of star colors. Values above 1.0 make stars brighter; values below 1.0 make them dimmer.");
		}
		if (ConsoleWidgets.SliderFloat("ColorMultiplier", ref colorMultiplier, 0f, 4f, colorMultiplier.ToString("F2", CultureInfo.InvariantCulture) + "x", pending: false))
		{
			_colorMultiplier = colorMultiplier;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		float redMultiplier = _redMultiplier;
		ConsoleWidgets.BeginRow("Red Multiplier");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the red component of star colors. Values above 1.0 increase red; values below 1.0 decrease red.");
		}
		if (ConsoleWidgets.SliderFloat("RedMultiplier", ref redMultiplier, 0f, 4f, redMultiplier.ToString("F2", CultureInfo.InvariantCulture) + "x", pending: false))
		{
			_redMultiplier = redMultiplier;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		float greenMultiplier = _greenMultiplier;
		ConsoleWidgets.BeginRow("Green Multiplier");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the green component of star colors. Values above 1.0 increase green; values below 1.0 decrease green.");
		}
		if (ConsoleWidgets.SliderFloat("GreenMultiplier", ref greenMultiplier, 0f, 4f, greenMultiplier.ToString("F2", CultureInfo.InvariantCulture) + "x", pending: false))
		{
			_greenMultiplier = greenMultiplier;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		float blueMultiplier = _blueMultiplier;
		ConsoleWidgets.BeginRow("Blue Multiplier");

		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Adjusts the blue component of star colors. Values above 1.0 increase blue; values below 1.0 decrease blue.");
		}
		if (ConsoleWidgets.SliderFloat("BlueMultiplier", ref blueMultiplier, 0f, 4f, blueMultiplier.ToString("F2", CultureInfo.InvariantCulture) + "x", pending: false))
		{
			_blueMultiplier = blueMultiplier;
			sliderChanged = true;
		}
		ConsoleWidgets.EndRow();

		ImGui.Dummy(new float2(0f, 4f));
		if (ConsoleWidgets.Button("Restore Defaults"))
		{
			ShowYesNo("Restore Defaults", "Reset render tuning and restore Magnitude Scale from the last selected star binary? Your selected binaries will not change. With none selected, the game's default Magnitude Scale is used.", RestoreAllDefaults);
		}

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Restore Settings"))
		{
			new ChoicePopup(
				"Restore Settings",
				"Restore the last used settings from the settings file, or the settings the mod initially loaded with this session?",
				("LAST USED", RestoreLastUsedSettings, PopupButtonKind.Primary),
				("INITIAL LOAD", RestoreInitialSettings, PopupButtonKind.Primary),
				("CANCEL", null, PopupButtonKind.Neutral));
		}

		if (sliderChanged && canApply)
		{
			ApplySelection();
		}

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Star Binaries");
		if (VanillaMode)
		{
			ImGui.TextColored(in ConsoleStyle.Pending, "Vanilla mode: a non-OhMyStars binary is loaded, so OhMyStars features are disabled.");
		}

		if (Entries.Count == 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "No star binaries were discovered across loaded mods.");
		}
		else
		{
			for (int i = 0; i < Entries.Count; i++)
			{
				BinaryEntry entry = Entries[i];
				bool enabled = entry.Enabled;
				string fileName = Path.GetFileName(entry.RelativePath);
				if (string.IsNullOrWhiteSpace(fileName))
				{
					fileName = entry.RelativePath;
				}

				ConsoleWidgets.BeginRow(fileName);
				ConsoleStyle.PushDisabled(!canApply);
				if (ConsoleWidgets.Checkbox($"Entry{i}", ref enabled, pending: false))
				{
					if (enabled && !IsOhMyStarsFormatted(entry))
					{
						// Stays off until the user confirms; confirming enables and applies it.
						ConfirmNonModBinary(entry, fileName);
					}
					else
					{
						entry.Enabled = enabled;
						ApplySelection();
					}
				}
				ConsoleStyle.PopDisabled();
				if (!IsOhMyStarsFormatted(entry))
				{
					ImGui.SameLine();
					ImGui.TextColored(in ConsoleStyle.TextMuted, "(non-OhMyStars)");
				}
				ConsoleWidgets.EndRow();
			}
		}

		ImGui.Dummy(new float2(0f, 4f));

		if (ConsoleWidgets.Button("Refresh"))
		{
			RebuildEntriesFromMods();
		}

		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Saved Settings");

		if (ConsoleWidgets.Button("Save Settings"))
		{
			SaveNamedSetting(_saveNameBuffer.Value);
		}
		ImGui.SameLine();
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##SaveSettingName"), _saveNameBuffer);

		if (SavedSettings.Count == 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "No saved settings yet.");
		}

		for (int i = 0; i < SavedSettings.Count; i++)
		{
			SavedSetting saved = SavedSettings[i];
			if (ConsoleWidgets.Button("Use", $"UseSaved{i}", default))
			{
				UseSavedSetting(saved, canApply);
			}
			ImGui.SameLine();
			if (ConsoleWidgets.Button("Del", $"DelSaved{i}", default))
			{
				string name = saved.Name;
				ShowYesNo("Delete Saved Setting", $"Delete saved setting '{name}'?", () => DeleteSavedSetting(name));
			}
			ImGui.SameLine();
			ImGui.AlignTextToFramePadding();
			ImGui.Text(saved.Name);
		}
		ImGui.NewLine();
	}

	private static void CaptureDefaultStarBinaries()
	{
		if (DefaultStarBinaries.Count > 0)
		{
			return;
		}

		foreach (Mod loadedMod in GetAllLoadedMods())
		{
			DefaultStarBinaries[loadedMod] = (string[])GetStarBinariesForMod(loadedMod).Clone();
		}
	}

	private static float ReadDefaultMagnitudeScale()
	{
		Mod? coreMod = ResolveCoreOwnerMod(DefaultStarBinaries.Keys.ToList());
		if (coreMod is null || !DefaultStarBinaries.TryGetValue(coreMod, out string[]? binaries)
			|| binaries.Length == 0)
		{
			throw new InvalidOperationException("Cannot find the game's default star binary for Magnitude Scale.");
		}

		// Match the game's loader: the last stock binary supplies the distance-brightness exponent.
		return ReadBinaryMagnitudeScale(Path.Combine(coreMod.DirectoryPath, binaries[^1]));
	}

	private static float ReadBinaryMagnitudeScale(string path)
	{
		using BinaryReader reader = new BinaryReader(File.OpenRead(path));
		if (reader.BaseStream.Length < StarHeaderBytes)
		{
			throw new InvalidDataException($"Star binary '{path}' has an incomplete header.");
		}

		reader.ReadInt32();
		float magnitudeScale = reader.ReadSingle();
		if (magnitudeScale <= 0f || !float.IsFinite(magnitudeScale))
		{
			throw new InvalidDataException($"Star binary '{path}' has an invalid Magnitude Scale.");
		}

		return magnitudeScale;
	}

	private static Mod? ResolveOwnMod()
	{
		// Match by folder first: an external modloader may load the mod from a folder not named after it.
		string modRoot = Path.GetFullPath(ModPaths.ModRoot).TrimEnd('\\', '/');
		foreach (Mod loadedMod in GetAllLoadedMods())
		{
			if (string.IsNullOrWhiteSpace(loadedMod.DirectoryPath))
			{
				continue;
			}

			string dir = Path.GetFullPath(loadedMod.DirectoryPath).TrimEnd('\\', '/');
			if (string.Equals(dir, modRoot, StringComparison.OrdinalIgnoreCase))
			{
				return loadedMod;
			}
		}

		Mod? mod = ModLibrary.Find(ModNamespace);
		if (mod is not null && mod != Mod.Empty)
		{
			return mod;
		}

		_status = $"Could not find mod entry '{ModNamespace}'.";
		return null;
	}

	private static void RebuildEntriesFromMods()
	{
		Entries.Clear();

		if (_mod is null)
		{
			_status = "Mod not resolved yet.";
			return;
		}

		List<Mod> loadedMods = GetAllLoadedMods();
		if (loadedMods.Count == 0)
		{
			_status = "No loaded mods discovered yet.";
			return;
		}

		HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> loadedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Mod loadedMod in loadedMods)
		{
			foreach (string loadedPath in GetStarBinariesForMod(loadedMod))
			{
				string normalized = NormalizePath(loadedPath);
				if (string.IsNullOrWhiteSpace(normalized))
				{
					continue;
				}

				loadedPaths.Add(normalized);
				string fileName = Path.GetFileName(normalized);
				if (!string.IsNullOrWhiteSpace(fileName))
				{
					loadedFileNames.Add(fileName);
				}
			}
		}

		IEnumerable<string> files = Directory.EnumerateFiles(_mod.DirectoryPath, "*.bin", SearchOption.AllDirectories);
		foreach (string file in files.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
		{
			if (StarIdentityFile.IsSisterFile(file))
			{
				continue;
			}

			string relativePath = Path.GetRelativePath(_mod.DirectoryPath, file).Replace('\\', '/');
			string key = BuildEntryKey(_mod, relativePath);
			if (!seen.Add(key))
			{
				continue;
			}

			Entries.Add(new BinaryEntry
			{
				OwnerMod = _mod,
				ModName = GetModLabel(_mod),
				RelativePath = relativePath,
				FullPath = file,
				Enabled = IsPathLoaded(relativePath, loadedPaths, loadedFileNames)
			});
		}

		Mod? coreOwnerMod = ResolveCoreOwnerMod(loadedMods);
		if (coreOwnerMod is not null)
		{
			string coreDir = ResolveCoreDirectory(coreOwnerMod);
			if (Directory.Exists(coreDir))
			{
				IEnumerable<string> coreFiles = Directory.EnumerateFiles(coreDir, "*.bin", SearchOption.TopDirectoryOnly);
				foreach (string coreFile in coreFiles.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
				{
					if (!TryBuildCoreRelativePath(coreOwnerMod, coreFile, out string? relativePath) || string.IsNullOrWhiteSpace(relativePath))
					{
						continue;
					}

					string key = BuildEntryKey(coreOwnerMod, relativePath);
					if (!seen.Add(key))
					{
						continue;
					}

					Entries.Add(new BinaryEntry
					{
						OwnerMod = coreOwnerMod,
						ModName = GetModLabel(coreOwnerMod),
						RelativePath = relativePath,
						FullPath = coreFile,
						Enabled = IsPathLoaded(relativePath, loadedPaths, loadedFileNames)
					});
				}
			}
		}

		Entries.Sort(static (a, b) =>
		{
			int modCompare = StringComparer.OrdinalIgnoreCase.Compare(a.ModName, b.ModName);
			if (modCompare != 0)
			{
				return modCompare;
			}

			return StringComparer.OrdinalIgnoreCase.Compare(a.RelativePath, b.RelativePath);
		});

		_status = Entries.Count == 0 ? "No star binaries discovered." : $"Found {Entries.Count} star binaries (mod folder root + core root).";
	}

	private static Mod? ResolveCoreOwnerMod(List<Mod> loadedMods)
	{
		foreach (Mod mod in loadedMods)
		{
			if (GetStarBinariesForMod(mod).Any(static p => p.StartsWith("Content/Core/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("Content\\Core\\", StringComparison.OrdinalIgnoreCase)))
			{
				return mod;
			}
		}

		foreach (Mod mod in loadedMods)
		{
			string coreDir = Path.Combine(mod.DirectoryPath, "Content", "Core");
			if (Directory.Exists(coreDir))
			{
				return mod;
			}
		}

		foreach (Mod mod in loadedMods)
		{
			string modLabel = GetModLabel(mod);
			if (modLabel.Contains("core", StringComparison.OrdinalIgnoreCase) || mod.DirectoryPath.Contains("core", StringComparison.OrdinalIgnoreCase))
			{
				return mod;
			}
		}

		return null;
	}

	private static string ResolveCoreDirectory(Mod coreOwnerMod)
	{
		string fromOwner = Path.Combine(coreOwnerMod.DirectoryPath, "Content", "Core");
		if (Directory.Exists(fromOwner))
		{
			return fromOwner;
		}

		string gameBase = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? string.Empty;
		string fromAssembly = Path.Combine(gameBase, "Content", "Core");
		if (Directory.Exists(fromAssembly))
		{
			return fromAssembly;
		}

		return fromOwner;
	}

	private static bool TryBuildCoreRelativePath(Mod coreOwnerMod, string coreFilePath, out string? relativePath)
	{
		relativePath = null;

		string fileNormalized = coreFilePath.Replace('\\', '/');
		string marker = "/Content/Core/";
		int markerIndex = fileNormalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (markerIndex >= 0)
		{
			relativePath = fileNormalized[(markerIndex + 1)..];
			return true;
		}

		try
		{
			relativePath = Path.GetRelativePath(coreOwnerMod.DirectoryPath, coreFilePath).Replace('\\', '/');
			return !string.IsNullOrWhiteSpace(relativePath);
		}
		catch
		{
			return false;
		}
	}

	private static bool ApplySelection()
	{
		if (_mod is null)
		{
			_status = "Cannot apply: mod entry not resolved.";
			return false;
		}

		InstancedStarTechnique? starTechnique = GetStarTechnique();
		if (starTechnique is null)
		{
			_status = "Cannot apply: star technique not available yet.";
			return false;
		}

		List<Mod> loadedMods = GetAllLoadedMods();
		if (loadedMods.Count == 0)
		{
			_status = "Cannot apply: no loaded mods discovered.";
			return false;
		}

		Mod? coreOwnerMod = ResolveCoreOwnerMod(loadedMods);

		Dictionary<Mod, List<string>> selectedByMod = new Dictionary<Mod, List<string>>();
		selectedByMod[_mod] = new List<string>();
		if (coreOwnerMod is not null && !selectedByMod.ContainsKey(coreOwnerMod))
		{
			selectedByMod[coreOwnerMod] = new List<string>();
		}

		foreach (BinaryEntry entry in Entries)
		{
			if (!entry.Enabled)
			{
				continue;
			}

			if (!selectedByMod.TryGetValue(entry.OwnerMod, out List<string>? list))
			{
				continue;
			}

			list.Add(entry.RelativePath);
		}

		int selectedTotal = 0;
		foreach (KeyValuePair<Mod, List<string>> kvp in selectedByMod)
		{
			string[] selected = kvp.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			if (!SetStarBinariesForMod(kvp.Key, selected))
			{
				_status = $"Cannot apply: failed to update StarBinaries for mod '{GetModLabel(kvp.Key)}'.";
				return false;
			}

			selectedTotal += selected.Length;
		}

		starTechnique.MagnitudeScale = _magnitudeScale;
		GameStarSpace.ResetLoaded(starTechnique);
		foreach (IViewport viewport in ViewportRegistry.Views)
		{
			starTechnique.ResetInstances(viewport);
		}

		List<BinaryEntry> selectedEntries = Entries.Where(static e => e.Enabled).ToList();
		// Any binary without an OhMyStars identity sister file switches the mod to the game's stock star display.
		VanillaMode = selectedEntries.Any(static e => !IsOhMyStarsFormatted(e));
		LastApplyFileStats.Clear();
		int loadedStars;
		int availableStars;
		int capacity;
		int requestedCapacity;
		StarsEditPatcher.BeginApplyCycle();
		try
		{
			(loadedStars, availableStars, capacity, requestedCapacity) = LoadSelectedStarsMixed(starTechnique, selectedEntries);
		}
		finally
		{
			StarsEditPatcher.EndApplyCycle();
		}
		if (StarsEditPatcher.DroppedSinceLastApply > 0)
		{
			_status = $"Loaded stars: {loadedStars} | Capacity: {capacity} | Dropped this apply: {StarsEditPatcher.DroppedSinceLastApply}.";
		}
		else
		{
			_status = $"Loaded stars: {loadedStars} | Capacity: {capacity}.";
		}
		return true;
	}

	internal static InstancedStarTechnique? GetStarTechnique()
	{
		_starTechniqueField ??= typeof(Program).GetField("_starStarTechnique", PrivateStatic);
		return _starTechniqueField?.GetValue(null) as InstancedStarTechnique;
	}

	private static (int loaded, int available, int capacity, int requestedCapacity) LoadSelectedStarsMixed(InstancedStarTechnique starTechnique, List<BinaryEntry> selectedEntries)
	{
		if (selectedEntries.Count == 0)
		{
			return (0, 0, 0, 0);
		}

		int entryCount = selectedEntries.Count;
		int[] availablePerFile = new int[entryCount];
		int totalAvailable = 0;
		for (int i = 0; i < entryCount; i++)
		{
			availablePerFile[i] = GetStarFileCount(selectedEntries[i]);
			totalAvailable += availablePerFile[i];
		}

		if (totalAvailable <= 0)
		{
			for (int i = 0; i < entryCount; i++)
			{
				string emptyName = Path.GetFileName(selectedEntries[i].RelativePath);
				if (string.IsNullOrWhiteSpace(emptyName))
				{
					emptyName = selectedEntries[i].RelativePath;
				}

				LastApplyFileStats.Add($"{emptyName}: 0 (available 0, quota 0)");
			}

			return (0, 0, 0, 0);
		}

		int requestedCapacity = totalAvailable + CapacityPadding;
		StarsEditPatcher.SetRequestedStarCapacity(requestedCapacity);

		int maxInstances = StarsEditPatcher.TryAdaptCapacityNow(starTechnique, requestedCapacity);
		if (maxInstances <= 0)
		{
			maxInstances = GetMaxInstances(starTechnique);
		}

		if (maxInstances <= 0)
		{
			return (0, totalAvailable, 0, requestedCapacity);
		}

		int[] quotaPerFile = new int[entryCount];
		if (totalAvailable <= maxInstances)
		{
			for (int i = 0; i < entryCount; i++)
			{
				quotaPerFile[i] = availablePerFile[i];
			}
		}
		else
		{
			double[] fractional = new double[entryCount];
			int used = 0;

			for (int i = 0; i < entryCount; i++)
			{
				double raw = (double)availablePerFile[i] * maxInstances / totalAvailable;
				int quota = (int)Math.Floor(raw);
				if (quota > availablePerFile[i])
				{
					quota = availablePerFile[i];
				}

				quotaPerFile[i] = quota;
				fractional[i] = raw - quota;
				used += quota;
			}

			int remaining = maxInstances - used;
			int[] order = Enumerable.Range(0, entryCount).OrderByDescending(i => fractional[i]).ToArray();
			while (remaining > 0)
			{
				bool assigned = false;
				foreach (int i in order)
				{
					if (quotaPerFile[i] >= availablePerFile[i])
					{
						continue;
					}

					quotaPerFile[i]++;
					remaining--;
					assigned = true;
					if (remaining <= 0)
					{
						break;
					}
				}

				if (!assigned)
				{
					break;
				}
			}
		}

		int loaded = 0;
		for (int i = 0; i < selectedEntries.Count; i++)
		{
			BinaryEntry entry = selectedEntries[i];
			int quota = quotaPerFile[i];
			if (quota <= 0)
			{
				string skippedName = Path.GetFileName(entry.RelativePath);
				if (string.IsNullOrWhiteSpace(skippedName))
				{
					skippedName = entry.RelativePath;
				}

				LastApplyFileStats.Add($"{skippedName}: 0 (available {availablePerFile[i]}, quota 0)");
				continue;
			}

			int fileLoaded = LoadStarFileWithQuota(starTechnique, entry, quota);
			loaded += fileLoaded;

			string fileName = Path.GetFileName(entry.RelativePath);
			if (string.IsNullOrWhiteSpace(fileName))
			{
				fileName = entry.RelativePath;
			}

			LastApplyFileStats.Add($"{fileName}: {fileLoaded} (available {availablePerFile[i]}, quota {quota})");
		}

		// The game's star-system masking copies the master list into each viewport's instance buffer.
		GameStarSpace.RefreshMasks();

		return (loaded, totalAvailable, maxInstances, requestedCapacity);
	}

	// Game star binary (v2026.10.X.5597+): int count, float magnitudeScale, then per star
	// float3 direction, byte scale, byte r, byte g, byte b, float inverseParsecs.
	private const int StarHeaderBytes = sizeof(int) + sizeof(float);
	private const int StarRecordBytes = sizeof(float) * 3 + sizeof(byte) * 4 + sizeof(float);

	private static int GetStarFileCount(BinaryEntry entry)
	{
		string fullPath = entry.FullPath;
		if (!File.Exists(fullPath))
		{
			return 0;
		}

		using FileStream stream = File.OpenRead(fullPath);
		using BinaryReader reader = new BinaryReader(stream);
		if (reader.BaseStream.Length < StarHeaderBytes)
		{
			return 0;
		}

		int declaredTotal = reader.ReadInt32();
		long maxByLength = (reader.BaseStream.Length - StarHeaderBytes) / StarRecordBytes;
		return (int)Math.Min(Math.Max(declaredTotal, 0), Math.Max(maxByLength, 0));
	}

	private static int LoadStarFileWithQuota(InstancedStarTechnique starTechnique, BinaryEntry entry, int quota)
	{
		string fullPath = entry.FullPath;
		if (!File.Exists(fullPath) || quota <= 0)
		{
			return 0;
		}

		using FileStream stream = File.OpenRead(fullPath);
		using BinaryReader reader = new BinaryReader(stream);

		if (reader.BaseStream.Length < StarHeaderBytes)
		{
			return 0;
		}

		int declaredTotal = reader.ReadInt32();
		reader.ReadSingle(); // The editor controls magnitude scale instead of individual binary headers.

		long maxByLength = (reader.BaseStream.Length - StarHeaderBytes) / StarRecordBytes;
		int total = (int)Math.Min(Math.Max(declaredTotal, 0), Math.Max(maxByLength, 0));
		if (total <= 0)
		{
			return 0;
		}

		int target = Math.Min(quota, total);
		double step = (double)total / target;
		double nextPick = 0d;
		int picked = 0;

		for (int index = 0; index < total; index++)
		{
			if (reader.BaseStream.Position + StarRecordBytes > reader.BaseStream.Length)
			{
				break;
			}

			float3 direction = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			byte scale = reader.ReadByte();
			byte r = reader.ReadByte();
			byte g = reader.ReadByte();
			byte b = reader.ReadByte();
			float inverseParsecs = reader.ReadSingle();

			if (picked < target && index >= (int)Math.Floor(nextPick))
			{
				direction = float3.Normalize(direction);
				if (scale > InstancedStarTechnique.MAX_SPRITE_SCALE)
				{
					scale = InstancedStarTechnique.MAX_SPRITE_SCALE;
				}

				starTechnique.AddStar(direction, inverseParsecs, scale, new byte4(r, g, b, byte.MaxValue));
				picked++;
				nextPick += step;
			}
		}

		return picked;
	}

	private static int GetMaxInstances(InstancedStarTechnique starTechnique)
	{
		_maxInstancesField ??= typeof(InstancedRenderTechnique<SpriteInstance>).GetField("_maxInstances", BindingFlags.Instance | BindingFlags.NonPublic);
		if (_maxInstancesField?.GetValue(starTechnique) is int value)
		{
			return value;
		}

		return 0;
	}

	internal static byte TransformRenderByte(byte value, float multiplier, int additiveOffset)
	{
		int scaled = (int)MathF.Round(value * multiplier) + additiveOffset;
		scaled = Math.Clamp(scaled, 0, byte.MaxValue);
		return (byte)scaled;
	}

	internal static byte TransformScaleByte(byte value)
	{
		float normalized = value / (float)byte.MaxValue;
		float curved = MathF.Pow(normalized, _sizeCurveGamma);
		float scaled = curved * byte.MaxValue;
		return TransformRenderByte((byte)Math.Clamp((int)MathF.Round(scaled), 0, byte.MaxValue), SizeMultiplier, _renderDataAdditiveOffset);
	}

	internal static float SizeCurveGamma => _sizeCurveGamma;
	internal static float SizeMultiplier => MathF.Pow(2f, _sizeLog2Offset * 0.5f);

	internal static int RenderDataAdditiveOffset => _renderDataAdditiveOffset;

	internal static int SizeFloorThreshold => _sizeFloorThreshold;

	internal static float ColorMultiplier => _colorMultiplier;

	internal static float RedMultiplier => _redMultiplier;

	internal static float GreenMultiplier => _greenMultiplier;

	internal static float BlueMultiplier => _blueMultiplier;

	private static List<Mod> GetAllLoadedMods()
	{
		_lookupField ??= typeof(ModLibrary).GetField("Lookup", BindingFlags.NonPublic | BindingFlags.Static);
		object? lookup = _lookupField?.GetValue(null);
		if (lookup is null)
		{
			return new List<Mod>();
		}

		_lookupGetListMethod ??= lookup.GetType().GetMethod("GetList", BindingFlags.Public | BindingFlags.Instance);
		if (_lookupGetListMethod?.Invoke(lookup, null) is IEnumerable<Mod> mods)
		{
			return mods.Where(static m => m is not null).ToList();
		}

		return new List<Mod>();
	}

	private static string[] GetStarBinariesForMod(Mod mod)
	{
		_starBinariesField ??= typeof(Mod).GetField("StarBinaries", AnyInstance);
		if (_starBinariesField?.GetValue(mod) is string[] current)
		{
			return current;
		}

		return Array.Empty<string>();
	}

	private static bool SetStarBinariesForMod(Mod mod, string[] selectedBinaries)
	{
		_starBinariesField ??= typeof(Mod).GetField("StarBinaries", AnyInstance);
		if (_starBinariesField is null)
		{
			return false;
		}

		_starBinariesField.SetValue(mod, selectedBinaries);
		return true;
	}

	private static string BuildEntryKey(Mod mod, string relativePath)
	{
		return $"{mod.DirectoryPath}|{NormalizePath(relativePath)}";
	}

	private static string NormalizePath(string path)
	{
		return path.Replace('\\', '/');
	}

	private static bool IsPathLoaded(string relativePath, HashSet<string> loadedPaths, HashSet<string> loadedFileNames)
	{
		string normalized = NormalizePath(relativePath);
		if (loadedPaths.Contains(normalized))
		{
			return true;
		}

		return false;
	}

	private static bool IsOhMyStarsFormatted(BinaryEntry entry)
	{
		return File.Exists(StarIdentityFile.GetSisterPath(entry.FullPath));
	}

	private static void ConfirmNonModBinary(BinaryEntry entry, string fileName)
	{
		new ChoicePopup(
			"Non-OhMyStars Binary",
			$"'{fileName}' is not an Oh My Stars formatted binary (no '{Path.GetFileName(StarIdentityFile.GetSisterPath(entry.FullPath))}' identity file next to it).\n\n" +
			"Loading it will disable the mod's features: constellation/asterism lines, star and constellation labels, pointers, navball markers, and the brightness/color edits. " +
			"Stars will be shown the game's normal way until only Oh My Stars formatted binaries are selected again.\n\nEnable it anyway?",
			("ENABLE", () =>
			{
				entry.Enabled = true;
				ApplySelection();
			}, PopupButtonKind.Primary),
			("CANCEL", null, PopupButtonKind.Neutral));
	}

	// Bump when the curated generation settings change so existing curated files are rebuilt.
	private const string CuratedBinaryVersion = "8-almach-anchor-fix";

	// Generates the bundled curated binary (plus its identity sister file) in the mod's binaries folder if missing or outdated.
	private static void EnsureCuratedBinaryExists()
	{
		foreach (string legacy in LegacyCuratedFiles)
		{
			try
			{
				File.Delete(ModPaths.Combine(legacy));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[OhMyStars] Could not remove legacy file '{legacy}': {ex.Message}");
			}
		}

		Directory.CreateDirectory(BinariesFolder);
		string curatedPath = Path.Combine(BinariesFolder, CuratedBinaryName);
		string versionPath = curatedPath + ".version";
		bool upToDate = File.Exists(versionPath) && File.ReadAllText(versionPath).Trim() == CuratedBinaryVersion;
		if (upToDate && File.Exists(curatedPath) && File.Exists(StarIdentityFile.GetSisterPath(curatedPath)))
		{
			return;
		}

		string csvPath = CuratedCatalogPath;
		if (!File.Exists(csvPath))
		{
			return;
		}

		try
		{
			// Keep the existing magnitude/colour policy while sourcing stars from the OMS catalog.
			GeneratorOptions curatedOptions = new()
			{
				UseMaxApparentMagnitude = true,
				MaxApparentMagnitude = 8.795f,
				SizingColorMode = SizingColorMode.PowerShellFlux,
				AddSol = true,
				WriteSisterFile = true,
			};
			StarBinaryGenerator.GenerateBinary(csvPath, curatedPath, curatedOptions);
			File.WriteAllText(versionPath, CuratedBinaryVersion);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[OhMyStars] Failed to generate curated star binary: {ex.Message}");
		}
	}

	// First run (or no usable saved selection): load only the mod's curated binary so the game's stock binary isn't loaded alongside it.
	private static void SelectStartupBinaries()
	{
		string settingsPath = GetSettingsFilePath();
		// Selections saved before the curated default existed are ignored once so the curated binary takes over.
		List<string>? lastUsed = File.Exists(settingsPath) && ReadSelectionVersion(settingsPath) >= CurrentSelectionVersion ? ReadLastUsedBinaries(settingsPath) : null;
		if (lastUsed is not null)
		{
			List<string> present = lastUsed.Where(key => Entries.Any(e => string.Equals($"{e.ModName}|{e.RelativePath}", key, StringComparison.OrdinalIgnoreCase) && File.Exists(e.FullPath))).ToList();
			if (present.Count > 0)
			{
				ApplyBinarySelection(present);
				return;
			}
		}

		BinaryEntry? curated = Entries.FirstOrDefault(e => e.OwnerMod == _mod && string.Equals(e.RelativePath, CuratedRelativePath, StringComparison.OrdinalIgnoreCase));
		if (curated is null)
		{
			return;
		}

		foreach (BinaryEntry entry in Entries)
		{
			entry.Enabled = entry == curated;
		}
	}

	private static string GetModLabel(Mod mod)
	{
		if (!string.IsNullOrWhiteSpace(mod.Name))
		{
			return mod.Name;
		}

		return Path.GetFileName(mod.DirectoryPath);
	}

	private static void ApplySliderSettings(SliderSettings settings)
	{
		_magnitudeScale = settings.MagnitudeScale;
		_sizeCurveGamma = settings.SizeCurveGamma;
		_sizeLog2Offset = settings.SizeLog2Offset;
		_renderDataAdditiveOffset = settings.RenderDataAdditiveOffset;
		_sizeFloorThreshold = settings.SizeFloorThreshold;
		_colorMultiplier = settings.ColorMultiplier;
		_redMultiplier = settings.RedMultiplier;
		_greenMultiplier = settings.GreenMultiplier;
		_blueMultiplier = settings.BlueMultiplier;
	}

	private static SliderSettings CaptureSliderSettings()
	{
		return new SliderSettings
		{
			MagnitudeScale = _magnitudeScale,
			SizeCurveGamma = _sizeCurveGamma,
			SizeLog2Offset = _sizeLog2Offset,
			RenderDataAdditiveOffset = _renderDataAdditiveOffset,
			SizeFloorThreshold = _sizeFloorThreshold,
			ColorMultiplier = _colorMultiplier,
			RedMultiplier = _redMultiplier,
			GreenMultiplier = _greenMultiplier,
			BlueMultiplier = _blueMultiplier
		};
	}

	private static void EnsureSettingsFileExists()
	{
		string settingsPath = GetSettingsFilePath();
		if (File.Exists(settingsPath))
		{
			return;
		}

		WriteSettingsFile(settingsPath, CaptureSliderSettings());
	}

	private static bool LoadSettingsFromDisk(bool updateStatus)
	{
		string settingsPath = GetSettingsFilePath();
		if (!File.Exists(settingsPath))
		{
			if (updateStatus)
			{
				_status = $"{SettingsFileName} not found.";
			}

			return false;
		}

		SliderSettings settings = ReadSettingsFile(settingsPath);
		ApplySliderSettings(settings);
		if (updateStatus)
		{
			_status = $"Loaded slider settings from '{SettingsFileName}'.";
		}

		return true;
	}

	private static void SaveNamedSetting(string rawName)
	{
		string name = rawName.Replace("[", "").Replace("]", "").Replace("\r", "").Replace("\n", "").Trim();
		if (name.Length == 0)
		{
			TimedAlert.CreateWarning("Enter a name before saving the star settings");
			return;
		}

		bool exists = SavedSettings.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
		string message = exists
			? $"Overwrite the existing saved setting '{name}' with the current settings?"
			: $"Save the current settings as '{name}'?";
		ShowYesNo("Save Settings", message, () => SaveNamedSettingConfirmed(name));
	}

	private static void SaveNamedSettingConfirmed(string name)
	{
		SavedSetting saved = new SavedSetting
		{
			Name = name,
			Sliders = CaptureSliderSettings(),
			Binaries = Entries.Where(static e => e.Enabled).Select(static e => $"{e.ModName}|{e.RelativePath}").ToList()
		};

		int existing = SavedSettings.FindIndex(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
		if (existing >= 0)
		{
			SavedSettings[existing] = saved;
		}
		else
		{
			SavedSettings.Add(saved);
		}

		// Also becomes the "last used" settings block that Restore Settings and startup read.
		WriteSettingsFile(GetSettingsFilePath(), saved.Sliders, saved.Binaries);
		_saveNameBuffer.Clear();
		_status = $"Saved settings '{name}' to '{SettingsFileName}'.";
		TimedAlert.Create($"Star settings '{name}' saved", Color.Green, 3.0);
	}

	private static bool CanApply()
	{
		return _mod is not null && GetStarTechnique() is not null;
	}

	private static List<string> CurrentBinaryKeys()
	{
		return Entries.Where(static e => e.Enabled).Select(static e => $"{e.ModName}|{e.RelativePath}").ToList();
	}

	private static void RestoreAllDefaults()
	{
		BinaryEntry? lastSelected = Entries.LastOrDefault(static entry => entry.Enabled);
		float magnitudeScale;
		try
		{
			magnitudeScale = lastSelected is null ? _defaultMagnitudeScale : ReadBinaryMagnitudeScale(lastSelected.FullPath);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			_status = $"Cannot restore render defaults: {ex.Message}";
			TimedAlert.CreateWarning(_status);
			return;
		}

		ApplySliderSettings(new SliderSettings { MagnitudeScale = magnitudeScale });
		if (CanApply() && !ApplySelection())
		{
			return;
		}

		_status = "Restored default render settings; selected star binaries unchanged.";
	}

	private static void RestoreLastUsedSettings()
	{
		string settingsPath = GetSettingsFilePath();
		if (!File.Exists(settingsPath))
		{
			_status = $"{SettingsFileName} not found.";
			return;
		}

		ApplySliderSettings(ReadSettingsFile(settingsPath));
		List<string>? binaries = ReadLastUsedBinaries(settingsPath);
		List<string> missing = binaries is null ? new List<string>() : ApplyBinarySelection(binaries);
		if (CanApply())
		{
			ApplySelection();
		}

		_status = $"Restored last used settings from '{SettingsFileName}'.{MissingText(missing)} " + _status;
	}

	private static void RestoreInitialSettings()
	{
		ApplySliderSettings(_initialSliders);
		List<string> missing = ApplyBinarySelection(_initialBinaries);
		if (CanApply())
		{
			ApplySelection();
		}

		_status = $"Restored initially loaded settings.{MissingText(missing)} " + _status;
	}

	// Flashes which binaries were skipped and returns the matching status-line suffix.
	private static string MissingText(List<string> missing)
	{
		if (missing.Count == 0)
		{
			return string.Empty;
		}

		string names = string.Join(", ", missing);
		TimedAlert.CreateWarning($"Ignoring star binaries not detected: {names}", 8.0);
		return $" Ignored (not detected): {names}.";
	}

	// Ticks exactly the given "ModName|RelativePath" binaries that are present; returns the file names of the ones not detected.
	private static List<string> ApplyBinarySelection(IEnumerable<string> binaries)
	{
		List<string> missing = new List<string>();
		foreach (BinaryEntry entry in Entries)
		{
			entry.Enabled = false;
		}

		foreach (string binary in binaries)
		{
			int separator = binary.IndexOf('|');
			string modName = separator >= 0 ? binary[..separator] : string.Empty;
			string relativePath = separator >= 0 ? binary[(separator + 1)..] : binary;

			BinaryEntry? match = Entries.FirstOrDefault(e =>
					string.Equals(e.ModName, modName, StringComparison.OrdinalIgnoreCase) &&
					string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

			if (match is null || !File.Exists(match.FullPath))
			{
				string fileName = Path.GetFileName(relativePath);
				missing.Add(string.IsNullOrWhiteSpace(fileName) ? relativePath : fileName);
				continue;
			}

			match.Enabled = true;
		}

		return missing;
	}

	private static void UseSavedSetting(SavedSetting saved, bool canApply)
	{
		ApplySliderSettings(saved.Sliders);
		List<string> missing = ApplyBinarySelection(saved.Binaries);

		if (canApply)
		{
			ApplySelection();
		}

		string settingsPath = GetSettingsFilePath();
		WriteSettingsFile(settingsPath, saved.Sliders, saved.Binaries);

		_status = $"Using saved settings '{saved.Name}'.{MissingText(missing)} " + _status;
	}

	private static void DeleteSavedSetting(string name)
	{
		if (SavedSettings.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) == 0)
		{
			return;
		}

		// Keep the [Sliders] block as it currently is on disk; only the saved list changed.
		string settingsPath = GetSettingsFilePath();
		SliderSettings sliders = File.Exists(settingsPath) ? ReadSettingsFile(settingsPath) : CaptureSliderSettings();
		List<string>? lastUsedBinaries = File.Exists(settingsPath) ? ReadLastUsedBinaries(settingsPath) : null;
		WriteSettingsFile(settingsPath, sliders, lastUsedBinaries);
		_status = $"Deleted saved settings '{name}'.";
	}

	private static void LoadSavedSettings()
	{
		SavedSettings.Clear();
		string settingsPath = GetSettingsFilePath();
		if (!File.Exists(settingsPath))
		{
			return;
		}

		foreach ((string section, List<KeyValuePair<string, string>> pairs) in ParseIniSections(settingsPath))
		{
			if (!section.StartsWith(SavedSectionPrefix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string name = section[SavedSectionPrefix.Length..].Trim();
			if (name.Length == 0)
			{
				continue;
			}

			SavedSettings.Add(new SavedSetting
			{
				Name = name,
				Sliders = SlidersFromValues(ToDictionary(pairs)),
				Binaries = pairs.Where(static p => string.Equals(p.Key, "Binary", StringComparison.OrdinalIgnoreCase)).Select(static p => p.Value).ToList()
			});
		}
	}

	// Lines before any section header count as [Sliders], matching the original single-section file.
	private static List<(string Section, List<KeyValuePair<string, string>> Pairs)> ParseIniSections(string settingsPath)
	{
		List<(string Section, List<KeyValuePair<string, string>> Pairs)> sections = new();
		List<KeyValuePair<string, string>> current = new();
		sections.Add(("Sliders", current));

		foreach (string rawLine in File.ReadAllLines(settingsPath))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}

			if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
			{
				current = new List<KeyValuePair<string, string>>();
				sections.Add((line[1..^1].Trim(), current));
				continue;
			}

			int separatorIndex = line.IndexOf('=');
			if (separatorIndex <= 0)
			{
				continue;
			}

			current.Add(new KeyValuePair<string, string>(line[..separatorIndex].Trim(), line[(separatorIndex + 1)..].Trim()));
		}

		return sections;
	}

	private static Dictionary<string, string> ToDictionary(List<KeyValuePair<string, string>> pairs)
	{
		Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, string> pair in pairs)
		{
			values[pair.Key] = pair.Value;
		}

		return values;
	}

	private static SliderSettings ReadSettingsFile(string settingsPath)
	{
		Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach ((string section, List<KeyValuePair<string, string>> pairs) in ParseIniSections(settingsPath))
		{
			if (string.Equals(section, "Sliders", StringComparison.OrdinalIgnoreCase))
			{
				foreach (KeyValuePair<string, string> pair in pairs)
				{
					values[pair.Key] = pair.Value;
				}
			}
		}

		return SlidersFromValues(values);
	}

	// Null when the [Sliders] block has no recorded binaries (older files), so selection is left alone.
	private static List<string>? ReadLastUsedBinaries(string settingsPath)
	{
		List<string> binaries = new List<string>();
		foreach ((string section, List<KeyValuePair<string, string>> pairs) in ParseIniSections(settingsPath))
		{
			if (string.Equals(section, "Sliders", StringComparison.OrdinalIgnoreCase))
			{
				binaries.AddRange(pairs.Where(static p => string.Equals(p.Key, "Binary", StringComparison.OrdinalIgnoreCase)).Select(static p => p.Value));
			}
		}

		return binaries.Count > 0 ? binaries : null;
	}

	private const int CurrentSelectionVersion = 3;

	private static int ReadSelectionVersion(string settingsPath)
	{
		foreach ((string section, List<KeyValuePair<string, string>> pairs) in ParseIniSections(settingsPath))
		{
			if (!string.Equals(section, "Sliders", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			foreach (KeyValuePair<string, string> pair in pairs)
			{
				if (string.Equals(pair.Key, "SelectionVersion", StringComparison.OrdinalIgnoreCase) && int.TryParse(pair.Value, out int version))
				{
					return version;
				}
			}
		}

		return 0;
	}

	private static SliderSettings SlidersFromValues(Dictionary<string, string> values)
	{
		SliderSettings defaults = new SliderSettings();
		return new SliderSettings
		{
			MagnitudeScale = ReadMagnitudeScale(values, defaults.MagnitudeScale),
			SizeCurveGamma = ReadFloat(values, nameof(SliderSettings.SizeCurveGamma), defaults.SizeCurveGamma),
			SizeLog2Offset = ReadFloat(values, nameof(SliderSettings.SizeLog2Offset), defaults.SizeLog2Offset),
			RenderDataAdditiveOffset = ReadInt(values, nameof(SliderSettings.RenderDataAdditiveOffset), defaults.RenderDataAdditiveOffset),
			SizeFloorThreshold = ReadInt(values, nameof(SliderSettings.SizeFloorThreshold), defaults.SizeFloorThreshold),
			ColorMultiplier = ReadFloat(values, nameof(SliderSettings.ColorMultiplier), defaults.ColorMultiplier),
			RedMultiplier = ReadFloat(values, nameof(SliderSettings.RedMultiplier), defaults.RedMultiplier),
			GreenMultiplier = ReadFloat(values, nameof(SliderSettings.GreenMultiplier), defaults.GreenMultiplier),
			BlueMultiplier = ReadFloat(values, nameof(SliderSettings.BlueMultiplier), defaults.BlueMultiplier)
		};
	}

	// Rewrites the whole file: the [Sliders] block plus every saved setting section.
	private static void WriteSettingsFile(string settingsPath, SliderSettings settings, IReadOnlyList<string>? lastUsedBinaries = null)
	{
		List<string> lines = new List<string> { "[Sliders]", $"SelectionVersion={CurrentSelectionVersion}" };
		AppendSliderLines(lines, settings);
		if (lastUsedBinaries is not null)
		{
			foreach (string binary in lastUsedBinaries)
			{
				lines.Add($"Binary={binary}");
			}
		}

		foreach (SavedSetting saved in SavedSettings)
		{
			lines.Add(string.Empty);
			lines.Add($"[{SavedSectionPrefix}{saved.Name}]");
			AppendSliderLines(lines, saved.Sliders);
			foreach (string binary in saved.Binaries)
			{
				lines.Add($"Binary={binary}");
			}
		}

		File.WriteAllLines(settingsPath, lines);
	}

	private static void AppendSliderLines(List<string> lines, SliderSettings settings)
	{
		lines.Add($"{nameof(SliderSettings.MagnitudeScale)}={settings.MagnitudeScale.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.SizeCurveGamma)}={settings.SizeCurveGamma.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.SizeLog2Offset)}={settings.SizeLog2Offset.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.RenderDataAdditiveOffset)}={settings.RenderDataAdditiveOffset.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.SizeFloorThreshold)}={settings.SizeFloorThreshold.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.ColorMultiplier)}={settings.ColorMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.RedMultiplier)}={settings.RedMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.GreenMultiplier)}={settings.GreenMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.BlueMultiplier)}={settings.BlueMultiplier.ToString(CultureInfo.InvariantCulture)}");
	}

	private static float ReadMagnitudeScale(Dictionary<string, string> values, float defaultValue)
	{
		if (!values.TryGetValue(nameof(SliderSettings.MagnitudeScale), out string? rawValue))
		{
			return defaultValue;
		}

		if (float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
			&& parsed > 0f && float.IsFinite(parsed))
		{
			return parsed;
		}

		TimedAlert.CreateWarning("Invalid saved Magnitude Scale; using the game binary default");
		return defaultValue;
	}

	private static float ReadFloat(Dictionary<string, string> values, string key, float defaultValue)
	{
		if (values.TryGetValue(key, out string? rawValue) && float.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float parsed))
		{
			return parsed;
		}

		return defaultValue;
	}

	private static int ReadInt(Dictionary<string, string> values, string key, int defaultValue)
	{
		if (values.TryGetValue(key, out string? rawValue) && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return parsed;
		}

		return defaultValue;
	}

	private static string GetSettingsFilePath()
	{
		return ModPaths.Combine(SettingsFileName);
	}
}
