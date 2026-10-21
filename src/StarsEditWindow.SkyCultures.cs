using Brutal.ImGuiApi;
using KSA;
using System.Globalization;
using System.Text;
using ImGui = Brutal.ImGuiApi.ImGui;

namespace OhMyStars;

// Sky Cultures tab: authoring of user sky cultures (constellation lines picked with the cursor).
// The tab always edits the active sky culture, so whatever is being edited is what is displayed.
// Built-in cultures are read-only; they can only be copied into a new custom culture.
internal static partial class StarsEditWindow
{
	private const string ShipFileName = "constellationship.fab";
	private const string NamesFileName = "constellation_names.eng.fab";

	private static readonly ImInputString _scNewCultureBuffer = new ImInputString(64);
	private static readonly ImInputString _scAbbrevBuffer = new ImInputString(16);
	private static readonly ImInputString _scConstNameBuffer = new ImInputString(64);
	private static string _scSelectedAbbrev = string.Empty;
	private static string _scStatus = string.Empty;
	private const string ScPickerOwner = "SkyCultures";
	private static int _scLineStartHip;

	private static void CancelLinePicking()
	{
		if (StarPicker.IsActiveFor(ScPickerOwner))
		{
			StarPicker.Cancel();
		}
		_scLineStartHip = 0;
	}

	// Continuous picking for each end of a new line; each end must be confirmed before moving on.
	private static void StartLinePick(string folder, string abbrev)
	{
		_scLineStartHip = 0;
		StarPicker.StartConfirm(ScPickerOwner, "Line start", start =>
		{
			_scLineStartHip = start;
			StarPicker.StartConfirm(ScPickerOwner, "Line end", end =>
			{
				_scLineStartHip = 0;
				AddSegment(folder, abbrev, start, end);
			});
		});
	}

	private static void DrawSkyCulturesTab()
	{
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Sky Culture");
		if (_showTooltips)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "The culture selected here becomes the active sky culture, so your edits show in-game immediately.");
		}

		IReadOnlyList<SkyCulture> cultures = SkyCulturesRenderer.SkyCultures;
		SkyCulture? active = SkyCulturesRenderer.ActiveSkyCulture;

		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		if (ImGui.BeginCombo("##ScCulture", active == null ? "None" : CultureLabel(active)))
		{
			for (int i = 0; i < cultures.Count; i++)
			{
				if (ImGui.Selectable(CultureLabel(cultures[i]) + "##ScCult" + i, i == SkyCulturesRenderer.ActiveSkyCultureIndex))
				{
					SkyCulturesRenderer.ActiveSkyCultureIndex = i;
					_scSelectedAbbrev = string.Empty;
					CancelLinePicking();
				}
			}
			ImGui.EndCombo();
		}

		// New culture: copy the active one, or start an empty one.
		ConsoleWidgets.BeginRow("New Culture Name");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Folder name for a new custom culture, created in the mod's 'skycultures' folder.");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##ScNewCulture"), _scNewCultureBuffer);
		ConsoleWidgets.EndRow();

		using (new ImGuiDisabledScope(active == null))
		{
			if (ConsoleWidgets.Button("Copy Active as Custom", "ScCopyCulture", default) && active != null)
			{
				CreateCustomCulture(_scNewCultureBuffer.Value.ToString(), active);
			}
		}
		if (_showTooltips && ImGui.IsItemHovered())
		{
			ConsoleWidgets.Tooltip("Copies every file of the active culture into a new editable custom culture.");
		}
		ImGui.SameLine();
		if (ConsoleWidgets.Button("Create Empty", "ScCreateCulture", default))
		{
			CreateCustomCulture(_scNewCultureBuffer.Value.ToString(), null);
		}

		if (_scStatus.Length > 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, _scStatus);
		}

		if (active == null)
		{
			return;
		}

		if (!active.IsCustom)
		{
			ImGui.NewLine();
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Built-in culture (read-only). Copy it as a custom culture to edit it.");
			ImGui.TextColored(in ConsoleStyle.TextMuted, $"{active.Constellations.Count} constellations, {active.Constellations.Values.Sum(s => s.Count)} lines.");
			return;
		}

		using (new ImGuiDisabledScope(StarPicker.IsActiveFor(ScPickerOwner)))
		{
			if (ConsoleWidgets.Button("Delete Culture", "ScDeleteCulture", default))
			{
				string folder = active.FolderPath;
				string name = active.Name;
				ShowYesNo("Delete Sky Culture", $"Permanently delete custom culture '{name}' and its folder?", () => DeleteCustomCulture(folder));
			}
		}

		DrawConstellationEditor(active);
	}

	private static string CultureLabel(SkyCulture culture) => culture.Name + (culture.IsCustom ? "  (custom)" : "  (built-in, read-only)");

	private static void DrawConstellationEditor(SkyCulture culture)
	{
		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Constellations");

		ConsoleWidgets.BeginRow("Abbreviation");
		if (_showTooltips && ConsoleWidgets.RowHovered)
		{
			ConsoleWidgets.Tooltip("Short unique id (no spaces), e.g. 'Kit'.");
		}
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##ScAbbrev"), _scAbbrevBuffer);
		ConsoleWidgets.EndRow();

		ConsoleWidgets.BeginRow("Name");
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		ImGui.InputText(new ImString("##ScConstName"), _scConstNameBuffer);
		ConsoleWidgets.EndRow();

		if (ConsoleWidgets.Button("Add Constellation", "ScAddConst", default))
		{
			AddConstellation(culture, _scAbbrevBuffer.Value.ToString(), _scConstNameBuffer.Value.ToString());
		}

		if (culture.Constellations.Count == 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "No constellations yet.");
			return;
		}

		ImGui.NewLine();
		int index = 0;
		foreach (KeyValuePair<string, List<ConstellationSegment>> pair in culture.Constellations.ToList())
		{
			string abbrev = pair.Key;
			string label = $"{ConstellationDisplayName(culture, abbrev)} [{abbrev}] - {pair.Value.Count} lines##ScConst{index}";
			if (ImGui.Selectable(label, abbrev == _scSelectedAbbrev))
			{
				_scSelectedAbbrev = abbrev == _scSelectedAbbrev ? string.Empty : abbrev;
				CancelLinePicking();
			}
			index++;
		}

		if (!culture.Constellations.TryGetValue(_scSelectedAbbrev, out List<ConstellationSegment>? segments))
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "Select a constellation to edit its lines.");
			return;
		}

		DrawLineEditor(culture, _scSelectedAbbrev, segments);
	}

	private static void DrawLineEditor(SkyCulture culture, string abbrev, List<ConstellationSegment> segments)
	{
		ImGui.NewLine();
		OhMyStarsWindow.DrawThickSeparator();
		ConsoleWidgets.RegionHeader("Lines: " + ConstellationDisplayName(culture, abbrev));

		string folder = culture.FolderPath;
		StarPicker.KeepAlive(ScPickerOwner);
		bool picking = StarPicker.IsActiveFor(ScPickerOwner);
		using (new ImGuiDisabledScope(picking))
		{
			if (ConsoleWidgets.Button("Add Line", "ScAddLine", default))
			{
				StartLinePick(folder, abbrev);
			}
		}
		if (_showTooltips && ImGui.IsItemHovered())
		{
			ConsoleWidgets.Tooltip("Pick the line's start star, then its end star. Left-click in the sky picks the star closest to the cursor - keep clicking until the right star is marked, then Confirm.");
		}
		ImGui.SameLine();
		using (new ImGuiDisabledScope(picking))
		{
			if (ConsoleWidgets.Button("Delete Constellation", "ScDelConst", default))
			{
				ShowYesNo("Delete Constellation", $"Delete constellation '{ConstellationDisplayName(culture, abbrev)}' and all its lines?",
					() => DeleteConstellation(folder, abbrev));
			}
		}

		if (picking)
		{
			bool pickingEnd = _scLineStartHip > 0;
			if (pickingEnd)
			{
				ImGui.TextColored(in ConsoleStyle.TextMuted, "Line start: " + SkyCulturesRenderer.DescribeStar(_scLineStartHip));
			}
			string which = pickingEnd ? "Line end" : "Line start";
			ImGui.TextColored(in ConsoleStyle.TextMuted, which + ": " + (StarPicker.PendingHip > 0 ? SkyCulturesRenderer.DescribeStar(StarPicker.PendingHip) : "(click a star in the sky)"));
			if (StarPicker.NoStarNearLastClick)
			{
				ImGui.TextColored(in ConsoleStyle.TextMuted, "No star near the last click.");
			}

			using (new ImGuiDisabledScope(StarPicker.PendingHip <= 0))
			{
				if (ConsoleWidgets.PositiveButton("Confirm " + which, "ScConfirmEnd", default))
				{
					StarPicker.ConfirmPending();
				}
			}
			ImGui.SameLine();
			if (ConsoleWidgets.Button("Cancel Line", "ScCancelLine", default))
			{
				CancelLinePicking();
			}
		}
		if (segments.Count == 0)
		{
			ImGui.TextColored(in ConsoleStyle.TextMuted, "No lines yet.");
			return;
		}

		for (int i = 0; i < segments.Count; i++)
		{
			ConstellationSegment seg = segments[i];
			int lineIndex = i;
			string text = $"{SkyCulturesRenderer.DescribeStar(seg.FromHip)} - {SkyCulturesRenderer.DescribeStar(seg.ToHip)}";
			using (new ImGuiDisabledScope(false))
			{
				if (ConsoleWidgets.Button("Del", $"ScDelLine{i}", default))
				{
					ShowYesNo("Delete Line", $"Delete line {text}?", () => DeleteSegment(folder, abbrev, lineIndex, seg));
				}
			}
			ImGui.SameLine();
			ImGui.AlignTextToFramePadding();
			ImGui.Text(text);
		}
	}

	private static string ConstellationDisplayName(SkyCulture culture, string abbrev)
	{
		if (culture.ConstellationNames.TryGetValue(abbrev, out ConstellationName? name))
		{
			string display = string.IsNullOrWhiteSpace(name.EnglishName) ? name.NativeName : name.EnglishName;
			if (!string.IsNullOrWhiteSpace(display))
			{
				return display;
			}
		}
		return abbrev;
	}

	// Returns the loaded custom culture for a folder, or null if it is missing or built-in.
	private static SkyCulture? GetEditableCulture(string folder)
	{
		int index = SkyCulturesRenderer.FindCultureIndexByFolder(folder);
		if (index < 0)
		{
			return null;
		}
		SkyCulture culture = SkyCulturesRenderer.SkyCultures[index];
		return culture.IsCustom ? culture : null;
	}

	private static void AddConstellation(SkyCulture culture, string abbrev, string name)
	{
		abbrev = abbrev.Trim();
		name = name.Trim();
		if (abbrev.Length == 0 || abbrev.Any(char.IsWhiteSpace) || abbrev.StartsWith('#'))
		{
			_scStatus = "Abbreviation must be non-empty, without spaces, and not start with '#'.";
			return;
		}
		if (culture.Constellations.Keys.Any(k => string.Equals(k, abbrev, StringComparison.OrdinalIgnoreCase)))
		{
			_scStatus = $"A constellation '{abbrev}' already exists.";
			return;
		}
		if (!culture.IsCustom)
		{
			return;
		}

		culture.Constellations[abbrev] = new List<ConstellationSegment>();
		culture.ConstellationNames[abbrev] = new ConstellationName(name.Length > 0 ? name : abbrev, name.Length > 0 ? name : abbrev);
		if (SaveCulture(culture))
		{
			_scSelectedAbbrev = abbrev;
			_scAbbrevBuffer.SetValue(string.Empty.AsSpan());
			_scConstNameBuffer.SetValue(string.Empty.AsSpan());
			_scStatus = $"Added constellation '{abbrev}'.";
		}
	}

	private static void AddSegment(string folder, string abbrev, int from, int to)
	{
		SkyCulture? culture = GetEditableCulture(folder);
		if (culture == null || !culture.Constellations.TryGetValue(abbrev, out List<ConstellationSegment>? segments))
		{
			return;
		}
		if (from == to)
		{
			_scStatus = "A line needs two different stars.";
			return;
		}
		if (segments.Any(s => (s.FromHip == from && s.ToHip == to) || (s.FromHip == to && s.ToHip == from)))
		{
			_scStatus = "That line already exists.";
			return;
		}
		segments.Add(new ConstellationSegment(from, to));
		if (SaveCulture(culture))
		{
			_scStatus = $"Added line {SkyCulturesRenderer.DescribeStar(from)} - {SkyCulturesRenderer.DescribeStar(to)}.";
		}
	}

	private static void DeleteSegment(string folder, string abbrev, int index, ConstellationSegment expected)
	{
		SkyCulture? culture = GetEditableCulture(folder);
		if (culture == null || !culture.Constellations.TryGetValue(abbrev, out List<ConstellationSegment>? segments))
		{
			return;
		}
		if (index < 0 || index >= segments.Count || segments[index] != expected)
		{
			index = segments.IndexOf(expected);
		}
		if (index < 0)
		{
			return;
		}
		segments.RemoveAt(index);
		if (SaveCulture(culture))
		{
			_scStatus = "Line deleted.";
		}
	}

	private static void DeleteConstellation(string folder, string abbrev)
	{
		SkyCulture? culture = GetEditableCulture(folder);
		if (culture == null)
		{
			return;
		}
		culture.Constellations.Remove(abbrev);
		culture.ConstellationNames.Remove(abbrev);
		if (SaveCulture(culture))
		{
			_scSelectedAbbrev = string.Empty;
			_scStatus = $"Deleted constellation '{abbrev}'.";
		}
	}

	private static void CreateCustomCulture(string rawName, SkyCulture? source)
	{
		string name = SanitizeFolderName(rawName);
		if (name.Length == 0)
		{
			_scStatus = "Enter a name for the new culture.";
			return;
		}

		string root = ModPaths.Combine("skycultures");
		string folder = Path.Combine(root, name);
		if (Directory.Exists(folder))
		{
			_scStatus = $"A culture folder named '{name}' already exists.";
			return;
		}

		try
		{
			Directory.CreateDirectory(folder);
			if (source != null)
			{
				foreach (string file in Directory.EnumerateFiles(source.FolderPath))
				{
					if (!string.Equals(Path.GetFileName(file), SkyCulturesRenderer.CustomCultureMarkerFile, StringComparison.OrdinalIgnoreCase))
					{
						File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
					}
				}
			}
			else
			{
				File.WriteAllText(Path.Combine(folder, ShipFileName), "# Custom sky culture created with Oh My Stars\n");
				File.WriteAllText(Path.Combine(folder, NamesFileName), string.Empty);
			}
			File.WriteAllText(Path.Combine(folder, SkyCulturesRenderer.CustomCultureMarkerFile),
				"This sky culture was created with Oh My Stars and can be edited in the Stars Editor's Sky Cultures tab.\n");
		}
		catch (Exception ex)
		{
			_scStatus = "Failed to create culture: " + ex.Message;
			return;
		}

		CancelLinePicking();
		SkyCulturesRenderer.ReloadSkyCultures(folder);
		_scSelectedAbbrev = string.Empty;
		_scNewCultureBuffer.SetValue(string.Empty.AsSpan());
		_scStatus = source != null ? $"Copied '{source.Name}' to custom culture '{name}'." : $"Created custom culture '{name}'.";
	}

	private static void DeleteCustomCulture(string folder)
	{
		if (GetEditableCulture(folder) == null)
		{
			return;
		}
		try
		{
			Directory.Delete(folder, recursive: true);
		}
		catch (Exception ex)
		{
			_scStatus = "Failed to delete culture: " + ex.Message;
			return;
		}

		CancelLinePicking();
		_scSelectedAbbrev = string.Empty;
		// Fall back to Modern (or whatever is first) once the deleted culture is gone.
		SkyCulturesRenderer.ReloadSkyCultures(ModPaths.Combine("skycultures", "modern"));
		if (SkyCulturesRenderer.ActiveSkyCultureIndex < 0 && SkyCulturesRenderer.SkyCultures.Count > 0)
		{
			SkyCulturesRenderer.ActiveSkyCultureIndex = 0;
		}
		_scStatus = "Culture deleted.";
	}

	private static string SanitizeFolderName(string raw)
	{
		StringBuilder sb = new StringBuilder();
		foreach (char c in raw.Trim())
		{
			if (char.IsLetterOrDigit(c) || c == '-')
			{
				sb.Append(char.ToLowerInvariant(c));
			}
			else if (c == ' ' || c == '_')
			{
				sb.Append('_');
			}
		}
		return sb.ToString().Trim('_');
	}

	// Writes the ship and names files of a custom culture, then reloads so the change shows in-game.
	private static bool SaveCulture(SkyCulture culture)
	{
		if (!culture.IsCustom)
		{
			return false;
		}

		try
		{
			StringBuilder ship = new StringBuilder();
			ship.Append("# Custom sky culture created with Oh My Stars\n");
			foreach (KeyValuePair<string, List<ConstellationSegment>> pair in culture.Constellations)
			{
				ship.Append(pair.Key).Append(' ').Append(pair.Value.Count.ToString(CultureInfo.InvariantCulture));
				foreach (ConstellationSegment seg in pair.Value)
				{
					ship.Append(' ').Append((-seg.FromHip).ToString(CultureInfo.InvariantCulture))
						.Append(' ').Append((-seg.ToHip).ToString(CultureInfo.InvariantCulture));
				}
				ship.Append('\n');
			}
			File.WriteAllText(Path.Combine(culture.FolderPath, ShipFileName), ship.ToString());

			StringBuilder names = new StringBuilder();
			foreach (KeyValuePair<string, ConstellationName> pair in culture.ConstellationNames)
			{
				names.Append(pair.Key).Append(" \"").Append(EscapeFab(pair.Value.NativeName))
					.Append("\" _(\"").Append(EscapeFab(pair.Value.EnglishName)).Append("\")\n");
			}
			File.WriteAllText(Path.Combine(culture.FolderPath, NamesFileName), names.ToString());
		}
		catch (Exception ex)
		{
			_scStatus = "Failed to save culture: " + ex.Message;
			return false;
		}

		SkyCulturesRenderer.ReloadSkyCultures(culture.FolderPath);
		return true;
	}

	private static string EscapeFab(string text) => text
		.Replace("\\", "\\\\")
		.Replace("\"", "\\\"")
		.Replace("\n", "\\n")
		.Replace("\t", "\\t");
}
