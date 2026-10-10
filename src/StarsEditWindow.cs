using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System.Globalization;
using System.Reflection;
using ImGui = Brutal.ImGuiApi.ImGui;

namespace OhMyStars;

// Star binary switcher window (formerly the standalone StarsEdit mod).
internal static class StarsEditWindow
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
	private static List<string> _preParallaxBinaries = new List<string>();
	private const string ParallaxBinaryFileName = "ohmystars_parallax_99k.bin";

	private const string ModNamespace = "OhMyStars";
	private const string SettingsFileName = "starsedit_settings.ini";

	private const int CapacityPadding = 1_000;

	private static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

	private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

	private static readonly List<BinaryEntry> Entries = new List<BinaryEntry>();

	// Each loaded mod's StarBinaries as the game originally loaded them, before any edits.
	private static readonly Dictionary<Mod, string[]> DefaultStarBinaries = new Dictionary<Mod, string[]>();

	private static Mod? _mod;

	private static string _status = "Waiting for mods to load...";

	private static bool _showWindow = true;

	private static readonly float2 DefaultWindowSize = new float2(900f, 720f);
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

	private sealed class SliderSettings
	{
		public float SizeCurveGamma { get; init; } = 1f;

		public float SizeLog2Offset { get; init; }

		public int RenderDataAdditiveOffset { get; init; }

		public int SizeFloorThreshold { get; init; }

		public float ColorMultiplier { get; init; } = 1f;

		public float RedMultiplier { get; init; } = 1f;

		public float GreenMultiplier { get; init; } = 1f;

		public float BlueMultiplier { get; init; } = 1f;

		public bool ParallaxEnabled { get; init; } = true;

		public float ParallaxCutoffPc { get; init; } = StarParallax.DefaultCutoffPc;

		public float ParallaxHideRadiusPc { get; init; } = StarParallax.DefaultHideRadiusPc;
	}

	private static void DrawParallaxSection()
	{
		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Parallax (Experimental)");

		ConsoleWidgets.BeginRow("Enable Parallax");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Moves stars as the camera moves away from the Sun (true scale) and rescales their brightness from absolute magnitude. Enabling switches the star binaries to " + ParallaxBinaryFileName + " only; disabling restores the previous selection. Inside the solar system the shift is physically tiny. Constellation lines, labels, star names, the star pointer, navball marker and orient-to-star follow moved stars; Sol becomes a background star once the camera is beyond the hide radius. IAU boundaries and the RA/Dec grid stay fixed.");
		}
		bool parallaxEnabled = StarParallax.Enabled;
		if (ConsoleWidgets.Checkbox("ParallaxEnabled", ref parallaxEnabled, pending: false))
		{
			SetParallaxEnabled(parallaxEnabled);
		}
		ConsoleWidgets.EndRow();

		float cutoff = StarParallax.CutoffPc;
		ConsoleWidgets.BeginRow("Moving Star Cutoff");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Only stars within this distance (parsecs) of the Sun move; farther stars stay static. 0 = no limit. Lower values are cheaper.");
		}
		string cutoffText = cutoff <= 0f ? "Unlimited" : cutoff.ToString("F0", CultureInfo.InvariantCulture) + " pc";
		if (ConsoleWidgets.SliderFloat("ParallaxCutoff", ref cutoff, 0f, 1000f, cutoffText, pending: false))
		{
			StarParallax.CutoffPc = cutoff;
			StarParallax.MarkDirty();
		}
		ConsoleWidgets.EndRow();

		float hideRadius = StarParallax.HideRadiusPc;
		ConsoleWidgets.BeginRow("Hide Stars Near Camera");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Background stars closer to the camera than this distance (parsecs) are hidden, so the game's detailed rendering of a visited star replaces the background dot. 1 pc = 206,265 AU. 0 = never hide.");
		}
		string hideText = hideRadius <= 0f ? "Off" : hideRadius.ToString("0.###", CultureInfo.InvariantCulture) + " pc";
		if (ConsoleWidgets.SliderFloat("ParallaxHideRadius", ref hideRadius, 0f, 2f, hideText, pending: false))
		{
			StarParallax.HideRadiusPc = hideRadius;
			StarParallax.MarkDirty();
		}
		ConsoleWidgets.EndRow();

		string mappedText = "Stars with parallax data loaded: " + StarParallax.MappedStarCount;
		ImGui.TextColored(in ConsoleStyle.TextMuted, mappedText);
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
		_mod = ResolveOwnMod();
		EnsureSettingsFileExists();
		LoadSettingsFromDisk(updateStatus: false);
		LoadSavedSettings();
		RebuildEntriesFromMods();
		_preParallaxBinaries = StarParallax.Enabled ? CurrentBinaryKeys() : new List<string>();
		EnforceParallaxBinarySelection();
		_initialSliders = CaptureSliderSettings();
		_initialBinaries = CurrentBinaryKeys();
		_pendingInitialSettingsApply = true;
	}

	internal static void Update()
	{
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

		ConsoleWidgets.BeginRow("Show Tooltips");
		ConsoleWidgets.Checkbox("ShowTooltips", ref _showTooltips, pending: false);
		ConsoleWidgets.EndRow();

		bool sliderChanged = false;
		bool canApply = _mod is not null && GetStarTechnique() is not null;

		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.Readout("Status", _status);

		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Render Tuning");

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
			ShowYesNo("Restore Defaults", "Restore all settings and star file(s) to the game's defaults?", RestoreAllDefaults);
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

		DrawParallaxSection();

        ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Star Binaries");

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
				if (ConsoleWidgets.Checkbox($"Entry{i}", ref enabled, pending: false))
				{
					entry.Enabled = enabled;
				}
				ConsoleWidgets.EndRow();
			}
		}

		ImGui.Dummy(new float2(0f, 4f));

		ConsoleStyle.PushDisabled(!canApply);
		if (OhMyStarsWindow.DrawColoredTabButton("Apply"))
		{
			ApplySelection();
		}
		ConsoleStyle.PopDisabled();

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Refresh"))
		{
			RebuildEntriesFromMods();
		}

		ImGui.SameLine();
		if (ConsoleWidgets.Button("Game Default", "RestoreDefaultBinaries", default))
		{
			ShowYesNo("Restore Default Binaries", "Restore the game's default star binaries?", RestoreDefaultBinaries);
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

		ConsoleStyle.PopWidgetStyle();
		ImGui.End();
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

	private static void RestoreDefaultBinaries()
	{
		foreach (KeyValuePair<Mod, string[]> kvp in DefaultStarBinaries)
		{
			SetStarBinariesForMod(kvp.Key, (string[])kvp.Value.Clone());
		}

		// Mods discovered after the snapshot had no defaults recorded, so they get none.
		foreach (Mod loadedMod in GetAllLoadedMods())
		{
			if (!DefaultStarBinaries.ContainsKey(loadedMod))
			{
				SetStarBinariesForMod(loadedMod, Array.Empty<string>());
			}
		}

		RebuildEntriesFromMods();

		if (_mod is not null && GetStarTechnique() is not null)
		{
			ApplySelection();
			_status = "Restored game default star binaries. " + _status;
		}
		else
		{
			_status = "Restored game default star binaries.";
		}
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

		_status = Entries.Count == 0 ? "No star binaries discovered." : $"Found {Entries.Count} star binaries (mod folder recursive + core root only).";
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

		StarParallax.ClearMap();
		foreach (IViewport viewport in ViewportRegistry.Views)
		{
			starTechnique.ResetInstances(viewport);
		}

		List<BinaryEntry> selectedEntries = Entries.Where(static e => e.Enabled).ToList();
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
		StarParallax.OnStarsReloaded(starTechnique);
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

		foreach (IViewport viewport in ViewportRegistry.Views)
		{
			starTechnique.UpdateInstanceBuffer(viewport, 0);
		}

		return (loaded, totalAvailable, maxInstances, requestedCapacity);
	}

	private static int GetStarFileCount(BinaryEntry entry)
	{
		string fullPath = entry.FullPath;
		if (!File.Exists(fullPath))
		{
			return 0;
		}

		using FileStream stream = File.OpenRead(fullPath);
		using BinaryReader reader = new BinaryReader(stream);
		if (reader.BaseStream.Length < sizeof(int))
		{
			return 0;
		}

		int declaredTotal = reader.ReadInt32();
		long maxByLength = (reader.BaseStream.Length - sizeof(int)) / (sizeof(float) * 3 + sizeof(byte) * 4);
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

		if (reader.BaseStream.Length < sizeof(int))
		{
			return 0;
		}

		int declaredTotal = reader.ReadInt32();
		long maxByLength = (reader.BaseStream.Length - sizeof(int)) / (sizeof(float) * 3 + sizeof(byte) * 4);
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
			if (reader.BaseStream.Position + (sizeof(float) * 3 + sizeof(byte) * 4) > reader.BaseStream.Length)
			{
				break;
			}

			float3 forward = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			byte scale = reader.ReadByte();
			byte r = reader.ReadByte();
			byte g = reader.ReadByte();
			byte b = reader.ReadByte();

			if (picked < target && index >= (int)Math.Floor(nextPick))
			{
				int countBefore = starTechnique.InstanceCount[Program.MainViewport.ShaderSlot];
				starTechnique.AddInstance(Program.MainViewport, forward, scale, new byte4(r, g, b, byte.MaxValue), 19.5f);
				if (starTechnique.InstanceCount[Program.MainViewport.ShaderSlot] > countBefore)
				{
					StarParallax.Record(countBefore, fullPath, index);
				}
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

		string fileName = Path.GetFileName(normalized);
		if (!string.IsNullOrWhiteSpace(fileName) && loadedFileNames.Contains(fileName))
		{
			return true;
		}

		return false;
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
		_sizeCurveGamma = settings.SizeCurveGamma;
		_sizeLog2Offset = settings.SizeLog2Offset;
		_renderDataAdditiveOffset = settings.RenderDataAdditiveOffset;
		_sizeFloorThreshold = settings.SizeFloorThreshold;
		_colorMultiplier = settings.ColorMultiplier;
		_redMultiplier = settings.RedMultiplier;
		_greenMultiplier = settings.GreenMultiplier;
		_blueMultiplier = settings.BlueMultiplier;
		StarParallax.Enabled = settings.ParallaxEnabled;
		StarParallax.CutoffPc = Math.Clamp(settings.ParallaxCutoffPc, 0f, 1000f);
		StarParallax.HideRadiusPc = Math.Clamp(settings.ParallaxHideRadiusPc, 0f, 2f);
		StarParallax.MarkDirty();
	}

	private static SliderSettings CaptureSliderSettings()
	{
		return new SliderSettings
		{
			SizeCurveGamma = _sizeCurveGamma,
			SizeLog2Offset = _sizeLog2Offset,
			RenderDataAdditiveOffset = _renderDataAdditiveOffset,
			SizeFloorThreshold = _sizeFloorThreshold,
			ColorMultiplier = _colorMultiplier,
			RedMultiplier = _redMultiplier,
			GreenMultiplier = _greenMultiplier,
			BlueMultiplier = _blueMultiplier,
			ParallaxEnabled = StarParallax.Enabled,
			ParallaxCutoffPc = StarParallax.CutoffPc,
			ParallaxHideRadiusPc = StarParallax.HideRadiusPc
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

	private static BinaryEntry? FindParallaxEntry()
	{
		return Entries.FirstOrDefault(static e =>
			string.Equals(Path.GetFileName(e.RelativePath), ParallaxBinaryFileName, StringComparison.OrdinalIgnoreCase) &&
			File.Exists(e.FullPath));
	}

	// While parallax is on, the parallax binary is the only star binary; returns false (and turns parallax off) if it is missing.
	private static bool EnforceParallaxBinarySelection()
	{
		if (!StarParallax.Enabled)
		{
			return true;
		}

		BinaryEntry? parallaxEntry = FindParallaxEntry();
		if (parallaxEntry is null)
		{
			StarParallax.Enabled = false;
			StarParallax.MarkDirty();
			TimedAlert.CreateWarning($"Star parallax disabled: {ParallaxBinaryFileName} not found.", 8.0);
			return false;
		}

		foreach (BinaryEntry entry in Entries)
		{
			entry.Enabled = ReferenceEquals(entry, parallaxEntry);
		}

		return true;
	}

	internal static void SetParallaxEnabled(bool enable)
	{
		if (enable == StarParallax.Enabled)
		{
			return;
		}

		SetParallaxEnabledCore(enable);
		PersistParallaxEnabled();
	}

	// Writes only the parallax on/off flag into the [Sliders] block on disk, leaving the other stored values untouched.
	private static void PersistParallaxEnabled()
	{
		try
		{
			string settingsPath = GetSettingsFilePath();
			SliderSettings onDisk = File.Exists(settingsPath) ? ReadSettingsFile(settingsPath) : CaptureSliderSettings();
			List<string>? lastUsedBinaries = File.Exists(settingsPath) ? ReadLastUsedBinaries(settingsPath) : null;
			SliderSettings updated = new SliderSettings
			{
				SizeCurveGamma = onDisk.SizeCurveGamma,
				SizeLog2Offset = onDisk.SizeLog2Offset,
				RenderDataAdditiveOffset = onDisk.RenderDataAdditiveOffset,
				SizeFloorThreshold = onDisk.SizeFloorThreshold,
				ColorMultiplier = onDisk.ColorMultiplier,
				RedMultiplier = onDisk.RedMultiplier,
				GreenMultiplier = onDisk.GreenMultiplier,
				BlueMultiplier = onDisk.BlueMultiplier,
				ParallaxEnabled = StarParallax.Enabled,
				ParallaxCutoffPc = onDisk.ParallaxCutoffPc,
				ParallaxHideRadiusPc = onDisk.ParallaxHideRadiusPc
			};
			WriteSettingsFile(settingsPath, updated, lastUsedBinaries);
		}
		catch (Exception ex)
		{
			_status = $"Failed to save parallax setting: {ex.Message}";
		}
	}

	private static void SetParallaxEnabledCore(bool enable)
	{
		if (enable)
		{
			List<string> previous = CurrentBinaryKeys();
			StarParallax.Enabled = true;
			if (!EnforceParallaxBinarySelection())
			{
				return;
			}

			_preParallaxBinaries = previous;
			StarParallax.MarkDirty();
			if (CanApply())
			{
				ApplySelection();
			}

			_status = $"Star parallax enabled; using {ParallaxBinaryFileName} only. " + _status;
			return;
		}

		StarParallax.Enabled = false;
		StarParallax.MarkDirty();
		bool hasPrevious = _preParallaxBinaries.Any(static k =>
			!string.Equals(Path.GetFileName(k), ParallaxBinaryFileName, StringComparison.OrdinalIgnoreCase));
		if (hasPrevious)
		{
			List<string> missing = ApplyBinarySelection(_preParallaxBinaries);
			if (CanApply())
			{
				ApplySelection();
			}

			_status = $"Star parallax disabled; restored previous star binaries.{MissingText(missing)} " + _status;
		}
		else
		{
			RestoreDefaultBinaries();
			_status = "Star parallax disabled. " + _status;
		}

		_preParallaxBinaries = new List<string>();
	}

	private static void RestoreAllDefaults()
	{
		ApplySliderSettings(new SliderSettings());
		RestoreDefaultBinaries();
		_preParallaxBinaries = new List<string>();
		if (StarParallax.Enabled && EnforceParallaxBinarySelection() && CanApply())
		{
			ApplySelection();
		}
		_status = "Restored default settings and star binaries. " + _status;
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
		EnforceParallaxBinarySelection();
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
		EnforceParallaxBinarySelection();
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
					string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
				?? Entries.FirstOrDefault(e => string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

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
		EnforceParallaxBinarySelection();

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

	private static SliderSettings SlidersFromValues(Dictionary<string, string> values)
	{
		SliderSettings defaults = new SliderSettings();
		return new SliderSettings
		{
			SizeCurveGamma = ReadFloat(values, nameof(SliderSettings.SizeCurveGamma), defaults.SizeCurveGamma),
			SizeLog2Offset = ReadFloat(values, nameof(SliderSettings.SizeLog2Offset), defaults.SizeLog2Offset),
			RenderDataAdditiveOffset = ReadInt(values, nameof(SliderSettings.RenderDataAdditiveOffset), defaults.RenderDataAdditiveOffset),
			SizeFloorThreshold = ReadInt(values, nameof(SliderSettings.SizeFloorThreshold), defaults.SizeFloorThreshold),
			ColorMultiplier = ReadFloat(values, nameof(SliderSettings.ColorMultiplier), defaults.ColorMultiplier),
			RedMultiplier = ReadFloat(values, nameof(SliderSettings.RedMultiplier), defaults.RedMultiplier),
			GreenMultiplier = ReadFloat(values, nameof(SliderSettings.GreenMultiplier), defaults.GreenMultiplier),
			BlueMultiplier = ReadFloat(values, nameof(SliderSettings.BlueMultiplier), defaults.BlueMultiplier),
			ParallaxEnabled = ReadInt(values, nameof(SliderSettings.ParallaxEnabled), defaults.ParallaxEnabled ? 1 : 0) != 0,
			ParallaxCutoffPc = ReadFloat(values, nameof(SliderSettings.ParallaxCutoffPc), defaults.ParallaxCutoffPc),
			ParallaxHideRadiusPc = ReadFloat(values, nameof(SliderSettings.ParallaxHideRadiusPc), defaults.ParallaxHideRadiusPc)
		};
	}

	// Rewrites the whole file: the [Sliders] block plus every saved setting section.
	private static void WriteSettingsFile(string settingsPath, SliderSettings settings, IReadOnlyList<string>? lastUsedBinaries = null)
	{
		List<string> lines = new List<string> { "[Sliders]" };
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
		lines.Add($"{nameof(SliderSettings.SizeCurveGamma)}={settings.SizeCurveGamma.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.SizeLog2Offset)}={settings.SizeLog2Offset.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.RenderDataAdditiveOffset)}={settings.RenderDataAdditiveOffset.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.SizeFloorThreshold)}={settings.SizeFloorThreshold.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.ColorMultiplier)}={settings.ColorMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.RedMultiplier)}={settings.RedMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.GreenMultiplier)}={settings.GreenMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.BlueMultiplier)}={settings.BlueMultiplier.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.ParallaxEnabled)}={(settings.ParallaxEnabled ? 1 : 0)}");
		lines.Add($"{nameof(SliderSettings.ParallaxCutoffPc)}={settings.ParallaxCutoffPc.ToString(CultureInfo.InvariantCulture)}");
		lines.Add($"{nameof(SliderSettings.ParallaxHideRadiusPc)}={settings.ParallaxHideRadiusPc.ToString(CultureInfo.InvariantCulture)}");
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
