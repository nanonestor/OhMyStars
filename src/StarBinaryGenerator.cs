using Brutal.Numerics;
using KSA;
using System.Globalization;
using System.Text;

namespace OhMyStars;

public enum SizingColorMode
{
	PowerShellFlux,
	KsaNative
}

// Settings for building a game star binary from a catalog (AT-HYG style CSV).
public sealed class GeneratorOptions
{
	public const float DefaultMagnitudeScale = 0.15f;

	// Inclusion filters. Every enabled filter is OR-ed: a star is kept if it passes ANY enabled filter.
	// With no filter enabled every star is kept.
	public bool UseMaxDistance;
	public float MaxDistance = 100f;      // keep stars closer than this
	public bool UseMinDistance;
	public float MinDistance = 1000f;     // keep stars farther than this
	public bool UseMaxAbsoluteMagnitude;
	public float MaxAbsoluteMagnitude = 5f;
	public bool UseMaxApparentMagnitude = true;
	public float MaxApparentMagnitude = 8.795f;

	// Distance filter values are entered in light years when true, parsecs otherwise (the catalog is parsecs).
	public bool DistanceInLightYears;

	// Defaults reproduce the mod's own binaries\OhMyStars_stars.bin.
	public bool AddSol = true;
	public bool WriteSisterFile = true;

	public SizingColorMode SizingColorMode = SizingColorMode.PowerShellFlux;
	public float MagnitudeScale = DefaultMagnitudeScale;
	public float StarScale = 5f;
	public float MagnitudeOffset = -1.5f;
	public float DimStart = 2f;
	public float DimEnd = 0f;
	public float MinBrightness = 0.1f;
	public bool UseAbsoluteMagnitudeForSizing;

	// Comma-delimited stars that are always written regardless of the filters and dimness cull.
	// Entries match 'proper' or 'oms_id' (falling back to 'id' for imported non-OMS CSVs).
	public bool UseAlwaysInclude;
	public string AlwaysIncludeList = string.Empty;
	public bool AlwaysIncludeByProperName;

	public HashSet<string> ParseAlwaysInclude()
	{
		HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
		if (!UseAlwaysInclude || string.IsNullOrWhiteSpace(AlwaysIncludeList))
		{
			return set;
		}

		foreach (string part in AlwaysIncludeList.Split(','))
		{
			string entry = part.Trim().Trim('"', '\'').Trim();
			if (entry.Length > 0)
			{
				set.Add(entry);
			}
		}

		return set;
	}

	public bool AnyFilterEnabled => UseMaxDistance || UseMinDistance || UseMaxAbsoluteMagnitude || UseMaxApparentMagnitude;

	public double ToParsecs(float value) => DistanceInLightYears ? value / GameStarSpace.LightYearsPerParsec : value;
}

public static class StarBinaryGenerator
{
	// The game loader clamps the size byte to this and warns, so generated files never exceed it.
	private const byte MaxGameScale = 128;

	// Sol is written as a star sitting (almost) on the origin; any record this close counts as Sol.
	private const float SolInverseParsecs = 1e9f;
	private const float SolDetectInverseParsecs = 1e5f;
	private const double SolApparentMagnitude = -26.74;
	private const double SolAbsoluteMagnitude = 4.83;
	private const double SolColorIndex = 0.65;

	public static bool IsRunning { get; private set; }
	public static float Progress { get; private set; }
	public static string StatusMessage { get; private set; } = "Ready";

	public static void StartGeneration(string inputCsvPath, string outputBinaryPath, GeneratorOptions options)
	{
		RunInBackground("Starting generation...", () =>
		{
			int count = GenerateBinary(inputCsvPath, outputBinaryPath, options);
			StatusMessage = $"Generated {count} stars -> {outputBinaryPath}";
			if (LastAlwaysIncludeNotFound.Count > 0)
			{
				StatusMessage += $" (always-include not found: {string.Join(", ", LastAlwaysIncludeNotFound)})";
			}
		});
	}

	private static List<string> LastAlwaysIncludeNotFound = new();

	public static void StartExportCurrentStars(string outputBinaryPath, bool addSol, bool writeSister, IReadOnlyList<string>? sourceBinaryPaths)
	{
		// The loaded star list must be captured on the calling (main) thread.
		InstancedStarTechnique? technique = GameStarSpace.GetTechnique();
		if (technique is null)
		{
			StatusMessage = "Error: the game's star renderer is not available yet.";
			return;
		}

		List<GameStarSpace.LoadedStar> stars = GameStarSpace.GetLoadedStars(technique);
		float magnitudeScale = technique.MagnitudeScale;
		RunInBackground("Exporting current stars...", () =>
		{
			int count = ExportStars(outputBinaryPath, stars, magnitudeScale, addSol, writeSister, sourceBinaryPaths);
			StatusMessage = $"Exported {count} stars -> {outputBinaryPath}";
		});
	}

	private static void RunInBackground(string startMessage, Action work, bool writesFile = true)
	{
		if (IsRunning)
		{
			return;
		}

		IsRunning = true;
		Progress = 0f;
		StatusMessage = startMessage;

		Task.Run(() =>
		{
			try
			{
				work();
				if (writesFile)
				{
					Interlocked.Increment(ref _completedWriteJobs);
				}
			}
			catch (Exception ex)
			{
				StatusMessage = $"Error: {ex.Message}";
			}
			finally
			{
				IsRunning = false;
			}
		});
	}

	private struct GameRecord
	{
		public float X, Y, Z;
		public byte Size, R, G, B;
		public float InverseParsecs;
	}

	// Game format (v2026.10.X.5602+): int count, float magnitudeScale, then 20-byte records
	// (float x, y, z unit direction; byte size; byte r, g, b; float inverse parsecs).
	private static void WriteGameBinary(string path, float magnitudeScale, IReadOnlyList<GameRecord> records)
	{
		string? outDir = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
		{
			Directory.CreateDirectory(outDir);
		}

		using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
		using BinaryWriter writer = new(fs);
		writer.Write(records.Count);
		writer.Write(magnitudeScale);
		foreach (GameRecord r in records)
		{
			writer.Write(r.X);
			writer.Write(r.Y);
			writer.Write(r.Z);
			writer.Write(r.Size);
			writer.Write(r.R);
			writer.Write(r.G);
			writer.Write(r.B);
			writer.Write(r.InverseParsecs);
		}
	}

	private static bool TryParseDouble(string[] cols, int index, out double value)
	{
		value = 0;
		return index >= 0 && index < cols.Length &&
			double.TryParse(cols[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
			double.IsFinite(value);
	}

	private static string GetString(string[] cols, int index)
	{
		return index >= 0 && index < cols.Length ? cols[index].Trim().Trim('"').Trim() : string.Empty;
	}

	private static int GetInt(string[] cols, int index)
	{
		string s = GetString(cols, index);
		return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
	}

	// Equatorial (RA/Dec) unit vector rotated into the game's ecliptic frame, exactly like the game generator.
	private static double3 DirectionFromRaDec(double raRad, double decRad)
	{
		double cosDec = Math.Cos(decRad);
		double3 eq = new(cosDec * Math.Cos(raRad), cosDec * Math.Sin(raRad), Math.Sin(decRad));
		return KSA.GalacticPlane.EquatorialToEcliptic(eq);
	}

	// Sizing/colour identical to the game's own generator (ModLibrary.GenerateStarBinary). Returns false when
	// the result is too dim to be worth writing (the game's minBrightness rule).
	private static bool ComputeKsaNative(double sizeMagnitude, double? bv, GeneratorOptions o, out byte size, out byte r, out byte g, out byte b)
	{
		float val = o.StarScale * MathF.Pow(10f, o.MagnitudeScale * (o.MagnitudeOffset - (float)sizeMagnitude));
		val = Math.Clamp(val, 0f, 30f);
		float dim = 1f;
		if (val < o.DimStart)
		{
			float denom = o.DimStart - o.DimEnd;
			float n = denom != 0f ? Math.Clamp((o.DimStart - val) / denom, 0f, 1f) : 1f;
			dim = (1f - n) * (1f - n);
		}

		val = Math.Max(val, o.DimStart);
		size = (byte)Math.Min(val / 30f * 255f, MaxGameScale);

		byte4 rgb = bv.HasValue ? KSA.Astronomical.BvToRgb(bv.Value) : new byte4(255, 255, 255, 255);
		r = (byte)Math.Clamp((int)MathF.Round(rgb.X * dim), 0, 255);
		g = (byte)Math.Clamp((int)MathF.Round(rgb.Y * dim), 0, 255);
		b = (byte)Math.Clamp((int)MathF.Round(rgb.Z * dim), 0, 255);
		return r / 255f >= o.MinBrightness || g / 255f >= o.MinBrightness || b / 255f >= o.MinBrightness;
	}

	private static void ComputeSizeAndColor(double sizeMagnitude, double? bv, GeneratorOptions o, out bool keep,
		out byte size, out byte r, out byte g, out byte b)
	{
		if (o.SizingColorMode == SizingColorMode.KsaNative)
		{
			keep = ComputeKsaNative(sizeMagnitude, bv, o, out size, out r, out g, out b);
			return;
		}

		(r, g, b) = bv.HasValue ? BvToLinearRgb(bv.Value) : ((byte)255, (byte)255, (byte)255);
		size = Math.Min(CalculateSizeAndAdjustColors(sizeMagnitude, ref r, ref g, ref b), MaxGameScale);
		keep = true;
	}

	private static StarIdentityFile.Record MakeSolIdentity()
	{
		return new StarIdentityFile.Record
		{
			Hip = 0,
			CatalogId = 0,
			DirX = 1f,
			DirY = 0f,
			DirZ = 0f,
			InverseParsecs = SolInverseParsecs,
			Magnitude = (float)SolApparentMagnitude,
			AbsoluteMagnitude = (float)SolAbsoluteMagnitude,
			ColorIndex = (float)SolColorIndex,
			Name = "Sol",
		};
	}

	// Sol sits ~1e-9 pc (inside the Sun) from the origin, so the shader's shift points it at the real Sun from
	// anywhere in or beyond the Solar System. Written at full game scale so dimming rules never cull it.
	private static GameRecord MakeSolRecord()
	{
		byte4 rgb = KSA.Astronomical.BvToRgb(SolColorIndex);
		return new GameRecord
		{
			X = 1f, Y = 0f, Z = 0f,
			Size = MaxGameScale,
			R = rgb.X, G = rgb.Y, B = rgb.Z,
			InverseParsecs = SolInverseParsecs,
		};
	}

	private enum RaUnit { Hours, Degrees, Radians, Guess }

	// Resolved column layout of a catalog CSV header.
	private sealed class CatalogColumns
	{
		public char Delimiter = ',';
		public int Mag = -1, AbsMag = -1, Dist = -1, Plx = -1, Ci = -1, MagSrc = -1;
		public int Hip = -1, Hyg = -1, Tyc = -1, Id = -1, Proper = -1, Bayer = -1, Con = -1;
		public int Ra = -1, Dec = -1, X = -1, Y = -1, Z = -1;
		public RaUnit RaUnit = RaUnit.Guess;
		public bool DecInRadians;

		public bool HasRaDec => Ra >= 0 && Dec >= 0;
		public bool HasXyz => X >= 0 && Y >= 0 && Z >= 0;
		public bool HasDistance => Dist >= 0 || Plx >= 0;
		public bool IsUsable => Mag >= 0 && (HasRaDec || HasXyz);
	}

	private static CatalogColumns ResolveColumns(string headerLine)
	{
		CatalogColumns c = new();
		char delimiter = ',';
		if (headerLine.Contains('|'))
		{
			delimiter = '|';
		}
		else if (headerLine.Contains('\t'))
		{
			delimiter = '\t';
		}
		else if (headerLine.Contains(';') && !headerLine.Contains(','))
		{
			delimiter = ';';
		}

		c.Delimiter = delimiter;
		string[] headers = ParseCsvLine(headerLine, delimiter);
		Dictionary<string, int> colMap = new(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < headers.Length; i++)
		{
			string clean = headers[i].Trim('\"', ' ', '\'', '\uFEFF');
			if (!string.IsNullOrEmpty(clean) && !colMap.ContainsKey(clean))
			{
				colMap[clean] = i;
			}
		}

		c.Mag = FindColumn(colMap, "mag", "vmag", "magnitude", "phot_g_mean_mag", "appmag", "hp");
		c.AbsMag = FindColumn(colMap, "absmag", "abs_mag", "absolute_magnitude", "absolute magnitude", "absmagnitude");
		c.Dist = FindColumn(colMap, "dist", "distance", "dist_pc", "distpc", "r_med_geo");
		c.Plx = c.Dist >= 0 ? -1 : FindColumn(colMap, "parallax", "plx", "plx_mas");
		c.Ci = FindColumn(colMap, "ci", "bv", "b_v", "b-v", "bvcolor", "bp_rp", "color");
		c.MagSrc = FindColumn(colMap, "mag_src");
		c.Hip = FindColumn(colMap, "hip");
		c.Hyg = FindColumn(colMap, "hyg");
		c.Tyc = FindColumn(colMap, "tyc");
		c.Id = FindColumn(colMap, "oms_id", "id");
		c.Proper = FindColumn(colMap, "proper");
		c.Bayer = FindColumn(colMap, "bayer");
		c.Con = FindColumn(colMap, "con");

		// RA unit comes from the column name; AT-HYG "ra" is in hours.
		if ((c.Ra = FindColumn(colMap, "ra", "rightascension", "right_ascension")) >= 0)
		{
			c.RaUnit = RaUnit.Hours;
		}
		else if ((c.Ra = FindColumn(colMap, "radeg", "ra_deg", "raj2000")) >= 0)
		{
			c.RaUnit = RaUnit.Degrees;
		}
		else if ((c.Ra = FindColumn(colMap, "rarad", "ra_rad")) >= 0)
		{
			c.RaUnit = RaUnit.Radians;
		}

		c.Dec = FindColumn(colMap, "dec", "declination", "decdeg", "dej2000", "dec_deg");
		if (c.Dec < 0 && (c.Dec = FindColumn(colMap, "decrad", "dec_rad")) >= 0)
		{
			c.DecInRadians = true;
		}

		c.X = FindColumn(colMap, "x");
		c.Y = FindColumn(colMap, "y");
		c.Z = FindColumn(colMap, "z");
		return c;
	}

	// Finds every *.csv in the given folders whose header has a magnitude column plus ra/dec (or x/y/z).
	public static List<string> FindCatalogFiles(IEnumerable<string> directories)
	{
		List<string> result = new();
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
		foreach (string dir in directories)
		{
			if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
			{
				continue;
			}

			IEnumerable<string> files;
			try
			{
				files = Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories).ToList();
			}
			catch
			{
				continue;
			}

			foreach (string file in files)
			{
				string full = Path.GetFullPath(file);
				if (!seen.Add(full))
				{
					continue;
				}

				try
				{
					using StreamReader reader = new(full, Encoding.UTF8);
					string? header = reader.ReadLine();
					if (header is not null && ResolveColumns(header).IsUsable)
					{
						result.Add(full);
					}
				}
				catch
				{
					// Unreadable or locked file: skip it.
				}
			}
		}

		result.Sort(StringComparer.OrdinalIgnoreCase);
		return result;
	}

	// Results of a dry run of the generator over a catalog with the current settings.
	public sealed class CatalogAnalysis
	{
		public string CatalogPath = string.Empty;
		public long DataRows;
		public long NoMagnitude;
		public long SolRowsSkipped;
		public long RejectedByFilters;
		public long MatchedAppMag, MatchedAbsMag, MatchedMaxDist, MatchedMinDist;
		public long NoAbsMagForSizing;
		public long NoPosition;
		public long TooDim;
		public long Kept;
		public long KeptWithoutDistance;
		public long KeptTycho;
		public long KeptNamed;
		public double MinDistPc = double.MaxValue, MaxDistPc;
		public double MinMag = double.MaxValue, MaxMag = double.MinValue;
		public bool SolAdded;
		public long KeptForced;
		public int AlwaysIncludeRequested;
		public List<string> AlwaysIncludeNotFound = new();
		public string Columns = string.Empty;

		public long TotalWritten => Kept + (SolAdded ? 1 : 0);

		public List<string> ToLines(bool lightYears)
		{
			string unit = lightYears ? "ly" : "pc";
			double scale = lightYears ? GameStarSpace.LightYearsPerParsec : 1.0;
			List<string> lines = new()
			{
				$"Catalog: {Path.GetFileName(CatalogPath)}",
				$"Columns: {Columns}",
				$"Data rows: {DataRows:N0}",
				$"Stars to write: {TotalWritten:N0}" + (SolAdded ? " (incl. Sol)" : string.Empty),
				$"Game binary size: {(8 + 20 * TotalWritten) / 1024.0 / 1024.0:F2} MB",
				$"Filter matches (OR-ed): app mag {MatchedAppMag:N0} | abs mag {MatchedAbsMag:N0} | closer {MatchedMaxDist:N0} | farther {MatchedMinDist:N0}",
				$"Dropped: filters {RejectedByFilters:N0} | too dim {TooDim:N0} | no position {NoPosition:N0} | no magnitude {NoMagnitude:N0} | no abs mag {NoAbsMagForSizing:N0} | Sol rows {SolRowsSkipped:N0}",
				$"Kept: named {KeptNamed:N0} | Tycho colour fix {KeptTycho:N0} | no distance {KeptWithoutDistance:N0}",
			};

			if (Kept > 0 && MinMag <= MaxMag)
			{
				lines.Add($"Apparent magnitude range: {MinMag:F2} to {MaxMag:F2}");
			}

			if (MaxDistPc > 0)
			{
				lines.Add($"Distance range: {MinDistPc * scale:N2} to {MaxDistPc * scale:N2} {unit}");
			}

			if (AlwaysIncludeRequested > 0)
			{
				lines.Add($"Always-include: {KeptForced:N0} rows matched from {AlwaysIncludeRequested:N0} entries");
				if (AlwaysIncludeNotFound.Count > 0)
				{
					lines.Add("Always-include not found: " + string.Join(", ", AlwaysIncludeNotFound));
				}
			}

			return lines;
		}
	}

	public static CatalogAnalysis? LastAnalysis { get; private set; }

	// Incremented each time a job that wrote a file finishes successfully.
	public static int CompletedWriteJobs => Volatile.Read(ref _completedWriteJobs);
	private static int _completedWriteJobs;

	public static void StartAnalysis(string inputCsvPath, GeneratorOptions options)
	{
		RunInBackground("Analyzing catalog...", () =>
		{
			CatalogAnalysis analysis = new() { CatalogPath = inputCsvPath };
			GenerateBinary(inputCsvPath, string.Empty, options, analysis);
			LastAnalysis = analysis;
			StatusMessage = $"Analysis complete: {analysis.TotalWritten:N0} of {analysis.DataRows:N0} rows would be written.";
		}, writesFile: false);
	}

	public static int GenerateBinary(string inputCsvPath, string outputBinaryPath, GeneratorOptions options, CatalogAnalysis? analysis = null)
	{
		if (!File.Exists(inputCsvPath))
		{
			throw new FileNotFoundException("Input CSV file does not exist.", inputCsvPath);
		}

		long fileLength = new FileInfo(inputCsvPath).Length;
		long bytesReadSoFar = 0;

		using FileStream inputStream = File.OpenRead(inputCsvPath);
		using StreamReader reader = new(inputStream, Encoding.UTF8);

		string? headerLine = reader.ReadLine() ?? throw new InvalidDataException("CSV file is empty.");
		bytesReadSoFar += Encoding.UTF8.GetByteCount(headerLine) + 2;

		CatalogColumns cc = ResolveColumns(headerLine);
		char delimiter = cc.Delimiter;
		int colMag = cc.Mag;
		if (colMag < 0)
		{
			throw new InvalidDataException("Required CSV column 'mag' was not found.");
		}

		int colAbsMag = cc.AbsMag;
		if (options.UseAbsoluteMagnitudeForSizing && colAbsMag < 0)
		{
			throw new InvalidDataException("Absolute-magnitude sizing was selected, but the CSV has no absolute-magnitude column (for example, 'absmag').");
		}

		int colDist = cc.Dist;
		int colPlx = cc.Plx;
		int colCi = cc.Ci;
		int colMagSrc = cc.MagSrc;
		int colHip = cc.Hip;
		int colHyg = cc.Hyg;
		int colTyc = cc.Tyc;
		int colId = cc.Id;
		int colProper = cc.Proper;
		int colBayer = cc.Bayer;
		int colCon = cc.Con;
		RaUnit raUnit = cc.RaUnit;
		int colRa = cc.Ra;
		int colDec = cc.Dec;
		bool decInRadians = cc.DecInRadians;
		int colX = cc.X;
		int colY = cc.Y;
		int colZ = cc.Z;
		bool hasRaDec = cc.HasRaDec;
		bool hasXyz = cc.HasXyz;
		if (!hasRaDec && !hasXyz)
		{
			throw new InvalidDataException("CSV must have (ra, dec) columns (or x, y, z direction columns).");
		}

		if (analysis is not null)
		{
			List<string> found = new() { hasRaDec ? "ra/dec" : "x/y/z", "mag" };
			if (colAbsMag >= 0) found.Add("absmag");
			if (colDist >= 0) found.Add("dist");
			else if (colPlx >= 0) found.Add("parallax");
			if (colCi >= 0) found.Add("ci");
			if (colMagSrc >= 0) found.Add("mag_src");
			if (colProper >= 0) found.Add("proper");
			if (colHip >= 0) found.Add("hip");
			analysis.Columns = string.Join(", ", found);
		}

		if ((options.UseMaxDistance || options.UseMinDistance) && colDist < 0 && colPlx < 0)
		{
			throw new InvalidDataException("A distance filter is enabled, but the CSV has no distance or parallax column.");
		}

		if (options.UseMaxAbsoluteMagnitude && colAbsMag < 0)
		{
			throw new InvalidDataException("The absolute-magnitude filter is enabled, but the CSV has no absolute-magnitude column.");
		}

		double maxDistPc = options.ToParsecs(options.MaxDistance);
		double minDistPc = options.ToParsecs(options.MinDistance);

		HashSet<string> alwaysInclude = options.ParseAlwaysInclude();
		HashSet<string> alwaysIncludeFound = new(StringComparer.OrdinalIgnoreCase);
		int colAlways = options.AlwaysIncludeByProperName ? colProper : colId;
		if (alwaysInclude.Count > 0 && colAlways < 0)
		{
			throw new InvalidDataException(options.AlwaysIncludeByProperName
				? "Always-include by proper name is enabled, but the CSV has no 'proper' column."
				: "Always-include by ID is enabled, but the CSV has no 'oms_id' or 'id' column.");
		}

		List<GameRecord> records = new();
		List<StarIdentityFile.Record> identities = new();
		int solCatalogId = 0;
		long lineCount = 0;
		string? line;

		while ((line = reader.ReadLine()) != null)
		{
			lineCount++;
			bytesReadSoFar += Encoding.UTF8.GetByteCount(line) + 2;
			if ((lineCount & 4095) == 0 && fileLength > 0)
			{
				Progress = Math.Min(0.95f, (float)bytesReadSoFar / fileLength);
				StatusMessage = $"Processing row {lineCount} ({records.Count} stars kept)...";
			}

			if (line.Length == 0)
			{
				continue;
			}

			if (analysis is not null)
			{
				analysis.DataRows++;
			}

			string[] cols = ParseCsvLine(line, delimiter);
			if (!TryParseDouble(cols, colMag, out double mag))
			{
				if (analysis is not null) analysis.NoMagnitude++;
				continue;
			}

			// Distance in parsecs (0 = unknown).
			double distPc = 0;
			if (TryParseDouble(cols, colDist, out double d) && d > 0)
			{
				distPc = d;
			}
			else if (TryParseDouble(cols, colPlx, out double plx) && plx > 0)
			{
				distPc = 1000.0 / plx;
			}

			// The catalog's own Sol row (distance 0) cannot be placed as a background star; Add Sol handles it.
			string proper = GetString(cols, colProper);
			if (string.Equals(proper, "Sol", StringComparison.OrdinalIgnoreCase) || (colDist >= 0 && TryParseDouble(cols, colDist, out double dz) && dz <= 1e-6))
			{
				if (string.Equals(proper, "Sol", StringComparison.OrdinalIgnoreCase))
					solCatalogId = GetInt(cols, colId);
				if (analysis is not null) analysis.SolRowsSkipped++;
				continue;
			}

			bool hasAbsMag = TryParseDouble(cols, colAbsMag, out double absMag);
			bool forced = false;
			if (alwaysInclude.Count > 0)
			{
				string key = GetString(cols, colAlways);
				if (key.Length > 0 && alwaysInclude.Contains(key))
				{
					forced = true;
					alwaysIncludeFound.Add(key);
				}
			}

			if (options.AnyFilterEnabled && !forced)
			{
				bool byApp = options.UseMaxApparentMagnitude && mag < options.MaxApparentMagnitude;
				bool byAbs = options.UseMaxAbsoluteMagnitude && hasAbsMag && absMag < options.MaxAbsoluteMagnitude;
				bool byMaxDist = options.UseMaxDistance && distPc > 0 && distPc < maxDistPc;
				bool byMinDist = options.UseMinDistance && distPc > 0 && distPc > minDistPc;
				if (analysis is not null)
				{
					if (byApp) analysis.MatchedAppMag++;
					if (byAbs) analysis.MatchedAbsMag++;
					if (byMaxDist) analysis.MatchedMaxDist++;
					if (byMinDist) analysis.MatchedMinDist++;
				}

				if (!(byApp || byAbs || byMaxDist || byMinDist))
				{
					if (analysis is not null) analysis.RejectedByFilters++;
					continue;
				}
			}

			double sizeMagnitude = mag;
			if (options.UseAbsoluteMagnitudeForSizing)
			{
				if (hasAbsMag)
				{
					sizeMagnitude = absMag;
				}
				else if (!forced)
				{
					if (analysis is not null) analysis.NoAbsMagForSizing++;
					continue;
				}
			}

			double3 dir;
			if (hasRaDec && TryParseDouble(cols, colRa, out double ra) && TryParseDouble(cols, colDec, out double dec))
			{
				double raRad = raUnit switch
				{
					RaUnit.Hours => ra * Math.PI / 12.0,
					RaUnit.Degrees => ra * Math.PI / 180.0,
					RaUnit.Radians => ra,
					_ => ra <= 24.0 ? ra * Math.PI / 12.0 : ra * Math.PI / 180.0,
				};
				double decRad = decInRadians ? dec : dec * Math.PI / 180.0;
				dir = DirectionFromRaDec(raRad, decRad);
			}
			else if (hasXyz && TryParseDouble(cols, colX, out double px) && TryParseDouble(cols, colY, out double py) && TryParseDouble(cols, colZ, out double pz))
			{
				dir = new double3(px, py, pz);
			}
			else
			{
				if (analysis is not null) analysis.NoPosition++;
				continue;
			}

			double len = dir.Length();
			if (len < 1e-12)
			{
				if (analysis is not null) analysis.NoPosition++;
				continue;
			}

			dir /= len;

			// Colour index. Tycho-2 BT-VT runs up to ~0.25 larger than B-V for red stars; B-V ~= 0.85 (BT-VT).
			double? bv = null;
			bool rowIsTycho = false;
			if (TryParseDouble(cols, colCi, out double ci))
			{
				bool isTycho;
				if (colMagSrc >= 0)
				{
					isTycho = string.Equals(GetString(cols, colMagSrc), "T", StringComparison.OrdinalIgnoreCase);
				}
				else
				{
					isTycho = GetString(cols, colTyc).Length > 0 && GetInt(cols, colHip) == 0 && GetInt(cols, colHyg) == 0;
				}

				bv = isTycho ? ci * 0.85 : ci;
				rowIsTycho = isTycho;
			}

			ComputeSizeAndColor(sizeMagnitude, bv, options, out bool visible, out byte size, out byte r, out byte g, out byte b);
			if (!visible && !forced)
			{
				if (analysis is not null) analysis.TooDim++;
				continue;
			}

			if (!visible)
			{
				// Forced star the dimming rule would cull: keep it at undimmed colour so it actually renders.
				byte4 full = bv.HasValue ? KSA.Astronomical.BvToRgb(bv.Value) : new byte4(255, 255, 255, 255);
				(r, g, b) = (full.X, full.Y, full.Z);
			}

			size = Math.Max(size, (byte)1);

			if (analysis is not null)
			{
				analysis.Kept++;
				if (forced) analysis.KeptForced++;
				if (distPc > 0)
				{
					analysis.MinDistPc = Math.Min(analysis.MinDistPc, distPc);
					analysis.MaxDistPc = Math.Max(analysis.MaxDistPc, distPc);
				}
				else
				{
					analysis.KeptWithoutDistance++;
				}

				analysis.MinMag = Math.Min(analysis.MinMag, mag);
				analysis.MaxMag = Math.Max(analysis.MaxMag, mag);
				if (proper.Length > 0) analysis.KeptNamed++;
				if (rowIsTycho) analysis.KeptTycho++;
				continue;
			}

			float invPc = distPc > 0 ? (float)(1.0 / distPc) : 0f;
			records.Add(new GameRecord
			{
				X = (float)dir.X, Y = (float)dir.Y, Z = (float)dir.Z,
				Size = size, R = r, G = g, B = b,
				InverseParsecs = invPc,
			});

			if (options.WriteSisterFile)
			{
				string name = proper;
				if (name.Length == 0)
				{
					string bayer = GetString(cols, colBayer);
					if (bayer.Length > 0)
					{
						name = (bayer + " " + GetString(cols, colCon)).Trim();
					}
				}

				identities.Add(new StarIdentityFile.Record
				{
					Hip = GetInt(cols, colHip),
					CatalogId = GetInt(cols, colId),
					DirX = (float)dir.X,
					DirY = (float)dir.Y,
					DirZ = (float)dir.Z,
					InverseParsecs = invPc,
					Magnitude = (float)mag,
					AbsoluteMagnitude = hasAbsMag ? (float)absMag : float.NaN,
					ColorIndex = bv.HasValue ? (float)bv.Value : float.NaN,
					Name = name,
				});
			}
		}

		if (analysis is not null)
		{
			analysis.SolAdded = options.AddSol;
			analysis.AlwaysIncludeRequested = alwaysInclude.Count;
			foreach (string entry in alwaysInclude)
			{
				if (!alwaysIncludeFound.Contains(entry)) analysis.AlwaysIncludeNotFound.Add(entry);
			}

			Progress = 1.0f;
			return (int)analysis.TotalWritten;
		}

		if (alwaysInclude.Count > 0)
		{
			LastAlwaysIncludeNotFound = alwaysInclude.Where(e => !alwaysIncludeFound.Contains(e)).ToList();
		}
		else
		{
			LastAlwaysIncludeNotFound = new List<string>();
		}

		if (options.AddSol)
		{
			records.Add(MakeSolRecord());
			if (options.WriteSisterFile)
			{
				var sol = MakeSolIdentity();
				sol.CatalogId = solCatalogId;
				identities.Add(sol);
			}
		}

		StatusMessage = "Writing binary...";
		WriteGameBinary(outputBinaryPath, options.MagnitudeScale, records);
		if (options.WriteSisterFile)
		{
			StarIdentityFile.Write(StarIdentityFile.GetSisterPath(outputBinaryPath), identities);
		}

		Progress = 1.0f;
		return records.Count;
	}

	private static long DirectionKey(float x, float y, float z, float invPc)
	{
		// Quantised direction + distance so stars can be matched back to their sister-file identities.
		long qx = (long)MathF.Round(x * 20000f) & 0xFFFF;
		long qy = (long)MathF.Round(y * 20000f) & 0xFFFF;
		long qz = (long)MathF.Round(z * 20000f) & 0xFFFF;
		long qd = (long)MathF.Round(MathF.Log10(Math.Max(invPc, 1e-9f)) * 1000f) & 0xFFFF;
		return (qx << 48) | (qy << 32) | (qz << 16) | qd;
	}

	// Writes the stars currently loaded in the game's renderer to a new game binary (plus optional sister file).
	internal static int ExportStars(string outputBinaryPath, IReadOnlyList<GameStarSpace.LoadedStar> stars, float magnitudeScale,
		bool addSol, bool writeSister, IReadOnlyList<string>? sourceBinaryPaths)
	{
		Dictionary<long, StarIdentityFile.Record> known = new();
		if (writeSister && sourceBinaryPaths is not null)
		{
			foreach (string source in sourceBinaryPaths)
			{
				List<StarIdentityFile.Record>? sister = StarIdentityFile.TryRead(StarIdentityFile.GetSisterPath(source));
				if (sister is null)
				{
					continue;
				}

				foreach (StarIdentityFile.Record r in sister)
				{
					known.TryAdd(DirectionKey(r.DirX, r.DirY, r.DirZ, r.InverseParsecs), r);
				}
			}
		}

		List<GameRecord> records = new(stars.Count + 1);
		List<StarIdentityFile.Record> identities = new(writeSister ? stars.Count + 1 : 0);
		bool hasSol = false;
		for (int i = 0; i < stars.Count; i++)
		{
			GameStarSpace.LoadedStar s = stars[i];
			if (s.InverseParsecs >= SolDetectInverseParsecs)
			{
				hasSol = true;
			}

			records.Add(new GameRecord
			{
				X = s.Direction.X, Y = s.Direction.Y, Z = s.Direction.Z,
				Size = Math.Min(s.Scale, MaxGameScale), R = s.R, G = s.G, B = s.B,
				InverseParsecs = s.InverseParsecs,
			});

			if (writeSister)
			{
				if (!known.TryGetValue(DirectionKey(s.Direction.X, s.Direction.Y, s.Direction.Z, s.InverseParsecs), out StarIdentityFile.Record id))
				{
					id = new StarIdentityFile.Record
					{
						Magnitude = float.NaN,
						AbsoluteMagnitude = float.NaN,
						ColorIndex = float.NaN,
						Name = string.Empty,
					};
				}

				id.DirX = s.Direction.X;
				id.DirY = s.Direction.Y;
				id.DirZ = s.Direction.Z;
				id.InverseParsecs = s.InverseParsecs;
				identities.Add(id);
			}

			if ((i & 4095) == 0 && stars.Count > 0)
			{
				Progress = 0.9f * i / stars.Count;
			}
		}

		if (addSol && !hasSol)
		{
			records.Add(MakeSolRecord());
			if (writeSister)
			{
				identities.Add(MakeSolIdentity());
			}
		}

		WriteGameBinary(outputBinaryPath, magnitudeScale, records);
		if (writeSister)
		{
			StarIdentityFile.Write(StarIdentityFile.GetSisterPath(outputBinaryPath), identities);
		}

		Progress = 1f;
		return records.Count;
	}

	public static string BuildCanonicalTomlManifest(
		float magnitudeScale = GeneratorOptions.DefaultMagnitudeScale,
		bool distanceIsParallax = true,
		float starScale = 8.0f,
		char delimiter = '|',
		float magnitudeOffset = 0.5f,
		float dimStart = 2.0f,
		float dimEnd = 0.0f,
		int raColumn = 8,
		int decColumn = 9,
		int magColumn = 5,
		int bvColumn = 40,
		float bvMinimum = 0.05f,
		int distanceColumn = 11)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("# STAR SAMPLE MANIFEST");
		sb.AppendLine("# used with the 'generatestarbinary' command");
		sb.AppendLine("# must be named the same as the imported file, but with TOML extension");
		sb.AppendLine("# below is formatted data for import of the HEASARC hip_main.dat from the ESA");
		sb.AppendLine();
		sb.AppendLine($"starScale = {starScale.ToString("G", CultureInfo.InvariantCulture)}\t\t\t# overall scale value for stars");
		sb.AppendLine($"delimiter = '{delimiter}'\t\t\t# character for column seperation in a line");
		sb.AppendLine();
		sb.AppendLine("[brightness]");
		sb.AppendLine($"magnitudeScale = {magnitudeScale.ToString("0.00", CultureInfo.InvariantCulture)}  \t# tune this smaller for tighter spread, larger for more extreme differences");
		sb.AppendLine($"magnitudeOffset = {magnitudeOffset.ToString("0.0", CultureInfo.InvariantCulture)}\t# tune this offset to shift overall brightness");
		sb.AppendLine($"dimStart = {dimStart.ToString("0.0", CultureInfo.InvariantCulture)}\t\t\t# when brightness is below this, start dimming the star");
		sb.AppendLine($"dimEnd = {dimEnd.ToString("0.0", CultureInfo.InvariantCulture)}\t\t\t# lowest dimming value");
		sb.AppendLine();
		sb.AppendLine("[rightAscension]");
		sb.AppendLine($"column = {raColumn}\t\t\t\t# column index for right ascension, in degrees");
		sb.AppendLine();
		sb.AppendLine("[declination]");
		sb.AppendLine($"column = {decColumn}\t\t\t\t# column index for declination, in degrees");
		sb.AppendLine();
		sb.AppendLine("[magnitude]");
		sb.AppendLine($"column = {magColumn}\t\t\t\t# column index for magnitude");
		sb.AppendLine();
		sb.AppendLine("[bvColor]");
		sb.AppendLine($"column = {bvColumn}\t\t\t\t# column index for the B-V color");
		sb.AppendLine($"minimum = {bvMinimum.ToString("0.00", CultureInfo.InvariantCulture)}\t\t\t# lowest color [0..1] value before stripping");
		sb.AppendLine();
		sb.AppendLine("[distance]");
		sb.AppendLine($"column = {distanceColumn}\t\t\t\t# column index for the distance value");
		sb.AppendLine($"parallax = {(distanceIsParallax ? "true" : "false")}\t\t\t# true: column is a parallax in milliarcseconds (HIP, Gaia)");
		sb.AppendLine("\t\t\t\t\t\t# false: column is a distance in parsecs");

		return sb.ToString();
	}

	public static string GenerateTomlManifest(
		string inputCsvPath,
		string outputTomlPath,
		float magnitudeScale = GeneratorOptions.DefaultMagnitudeScale,
		bool distanceIsParallax = true,
		float starScale = 8.0f,
		float magnitudeOffset = 0.5f,
		float dimStart = 2.0f,
		float dimEnd = 0.0f)
	{
		string tomlText = BuildCanonicalTomlManifest(
			magnitudeScale: magnitudeScale,
			distanceIsParallax: distanceIsParallax,
			starScale: starScale,
			delimiter: '|',
			magnitudeOffset: magnitudeOffset,
			dimStart: dimStart,
			dimEnd: dimEnd,
			raColumn: 8,
			decColumn: 9,
			magColumn: 5,
			bvColumn: 40,
			bvMinimum: 0.05f,
			distanceColumn: 11);

		// Write to outputTomlPath if specified
		if (!string.IsNullOrWhiteSpace(outputTomlPath))
		{
			string? outDir = Path.GetDirectoryName(outputTomlPath);
			if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
			{
				Directory.CreateDirectory(outDir);
			}
			File.WriteAllText(outputTomlPath, tomlText, Encoding.UTF8);
		}

		// Also write alongside input CSV (which is where generatestarbinary looks)
		if (!string.IsNullOrWhiteSpace(inputCsvPath))
		{
			string inputTomlPath = Path.ChangeExtension(inputCsvPath, ".toml");
			if (!string.Equals(inputTomlPath, outputTomlPath, StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string? inputDir = Path.GetDirectoryName(inputTomlPath);
					if (string.IsNullOrWhiteSpace(inputDir) || Directory.Exists(inputDir))
					{
						File.WriteAllText(inputTomlPath, tomlText, Encoding.UTF8);
					}
				}
				catch
				{
					// In case input directory is read-only
				}
			}
		}

		return tomlText;
	}

	public static int ExportCatalogAndManifest(
		string inputCsvPath,
		string outputDatPath,
		string outputTomlPath,
		float magnitudeScale = GeneratorOptions.DefaultMagnitudeScale,
		bool distanceIsParallax = true,
		float magCutoff = 8.795f,
		float starScale = 8.0f,
		float magnitudeOffset = 0.5f,
		float dimStart = 2.0f,
		float dimEnd = 0.0f)
	{
		if (!File.Exists(inputCsvPath))
		{
			throw new FileNotFoundException("Input CSV file does not exist.", inputCsvPath);
		}

		string? outDir = Path.GetDirectoryName(outputDatPath);
		if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
		{
			Directory.CreateDirectory(outDir);
		}

		// First, write the canonical manifest
		GenerateTomlManifest(
			inputCsvPath,
			outputTomlPath,
			magnitudeScale,
			distanceIsParallax,
			starScale,
			magnitudeOffset,
			dimStart,
			dimEnd);

		// Now export the ~98,695 stars matching the manifest columns:
		// delimiter = '|'
		// column 5: magnitude
		// column 8: right ascension (degrees)
		// column 9: declination (degrees)
		// column 11: distance / parallax
		// column 40: B-V color index
		int exportedStars = 0;
		using (FileStream inFs = File.OpenRead(inputCsvPath))
		using (StreamReader reader = new StreamReader(inFs, Encoding.UTF8))
		using (FileStream outFs = File.Create(outputDatPath))
		using (StreamWriter writer = new StreamWriter(outFs, new UTF8Encoding(false)))
		{
			string? headerLine = reader.ReadLine();
			if (headerLine == null)
			{
				return 0;
			}

			char delimiter = ',';
			if (headerLine.Contains('|')) delimiter = '|';
			else if (headerLine.Contains('\t')) delimiter = '\t';
			else if (headerLine.Contains(';') && !headerLine.Contains(',')) delimiter = ';';

			string[] headers = ParseCsvLine(headerLine, delimiter);
			Dictionary<string, int> colMap = new(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < headers.Length; i++)
			{
				string clean = headers[i].Trim('\"', ' ', '\'');
				if (!string.IsNullOrEmpty(clean) && !colMap.ContainsKey(clean))
				{
					colMap[clean] = i;
				}
			}

			int colRa = colMap.TryGetValue("ra", out int r) ? r : -1;
			int colDec = colMap.TryGetValue("dec", out int d) ? d : -1;
			int colMag = colMap.TryGetValue("mag", out int m) ? m : -1;
			int colCi = colMap.TryGetValue("ci", out int c) ? c : (colMap.TryGetValue("bv", out int bv) ? bv : -1);
			int colDist = colMap.TryGetValue("dist", out int dt) ? dt : (colMap.TryGetValue("distance", out int dist) ? dist : -1);
			int colPlx = colMap.TryGetValue("parallax", out int p) ? p : (colMap.TryGetValue("plx", out int plx) ? plx : -1);

			bool raIsHours = true;
			if (colMap.ContainsKey("deg_ra") || colMap.ContainsKey("radeg"))
			{
				raIsHours = false;
			}

			string[] rowCols = new string[41];
			string? line;
			while ((line = reader.ReadLine()) != null)
			{
				if (string.IsNullOrWhiteSpace(line)) continue;

				string[] cols = ParseCsvLine(line, delimiter);
				if (cols.Length == 0) continue;

				if (colRa < 0 || cols.Length <= colRa ||
					colDec < 0 || cols.Length <= colDec ||
					colMag < 0 || cols.Length <= colMag ||
					colCi < 0 || cols.Length <= colCi)
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(cols[colRa]) ||
					string.IsNullOrWhiteSpace(cols[colDec]) ||
					string.IsNullOrWhiteSpace(cols[colMag]) ||
					string.IsNullOrWhiteSpace(cols[colCi]))
				{
					continue;
				}

				if (!double.TryParse(cols[colRa], NumberStyles.Float, CultureInfo.InvariantCulture, out double raVal) ||
					!double.TryParse(cols[colDec], NumberStyles.Float, CultureInfo.InvariantCulture, out double decVal) ||
					!double.TryParse(cols[colMag], NumberStyles.Float, CultureInfo.InvariantCulture, out double magVal) ||
					!double.TryParse(cols[colCi], NumberStyles.Float, CultureInfo.InvariantCulture, out double ciVal))
				{
					continue;
				}

				if (magVal > magCutoff)
				{
					continue;
				}

				double raDeg = raIsHours ? (raVal * 15.0) : raVal;
				double decDeg = decVal;

				// Parallax / Distance
				double distOrPlx = 0.0;
				if (distanceIsParallax)
				{
					if (colPlx >= 0 && cols.Length > colPlx &&
						double.TryParse(cols[colPlx], NumberStyles.Float, CultureInfo.InvariantCulture, out double plxVal))
					{
						distOrPlx = plxVal;
					}
					else if (colDist >= 0 && cols.Length > colDist &&
						double.TryParse(cols[colDist], NumberStyles.Float, CultureInfo.InvariantCulture, out double distVal) && distVal > 0.0)
					{
						distOrPlx = 1000.0 / distVal;
					}
				}
				else
				{
					if (colDist >= 0 && cols.Length > colDist &&
						double.TryParse(cols[colDist], NumberStyles.Float, CultureInfo.InvariantCulture, out double distVal))
					{
						distOrPlx = distVal;
					}
					else if (colPlx >= 0 && cols.Length > colPlx &&
						double.TryParse(cols[colPlx], NumberStyles.Float, CultureInfo.InvariantCulture, out double plxVal) && plxVal > 0.0)
					{
						distOrPlx = 1000.0 / plxVal;
					}
				}

				for (int i = 0; i < 41; i++) rowCols[i] = string.Empty;
				rowCols[5] = magVal.ToString("F3", CultureInfo.InvariantCulture);
				rowCols[8] = raDeg.ToString("F6", CultureInfo.InvariantCulture);
				rowCols[9] = decDeg.ToString("F6", CultureInfo.InvariantCulture);
				rowCols[11] = distOrPlx.ToString("F4", CultureInfo.InvariantCulture);
				rowCols[40] = ciVal.ToString("F3", CultureInfo.InvariantCulture);

				writer.WriteLine(string.Join('|', rowCols));
				exportedStars++;
			}
		}

		return exportedStars;
	}

	private static byte CalculateSizeAndAdjustColors(double mag, ref byte r, ref byte g, ref byte b)
	{
		const double sizeBrightMag = -1.6;
		const double sizeDimMag = 6.8;
		const double sizeFloor = 10.0;
		const double sizeCurveGamma = 0.45;

		double brightFlux = Math.Pow(10.0, -0.4 * sizeBrightMag);
		double dimFlux = Math.Pow(10.0, -0.4 * sizeDimMag);
		double starFlux = Math.Pow(10.0, -0.4 * mag);

		double sizeNorm = (starFlux - dimFlux) / (brightFlux - dimFlux);
		sizeNorm = Math.Clamp(sizeNorm, 0.0, 1.0);

		int size = (int)Math.Round(sizeFloor + (255.0 - sizeFloor) * Math.Pow(sizeNorm, sizeCurveGamma));

		// If post-calculated size < 17, clamp size to 17 and scale RGB by 0.2
		if (size < 17)
		{
			size = 17;
			r = (byte)Math.Clamp((int)Math.Round(r * 0.2), 0, 255);
			g = (byte)Math.Clamp((int)Math.Round(g * 0.2), 0, 255);
			b = (byte)Math.Clamp((int)Math.Round(b * 0.2), 0, 255);
		}

		return (byte)Math.Clamp(size, 0, 255);
	}

	private static (byte R, byte G, byte B) BvToLinearRgb(double bv)
	{
		// Ballesteros formula
		double temperature = 4600.0 * (1.0 / (0.92 * bv + 1.7) + 1.0 / (0.92 * bv + 0.62));
		double t = temperature / 100.0;

		int r;
		if (t <= 66.0)
		{
			r = 255;
		}
		else
		{
			r = (int)Math.Clamp(Math.Round(329.698727446 * Math.Pow(t - 60.0, -0.1332047592)), 0.0, 255.0);
		}

		int g;
		if (t <= 66.0)
		{
			g = (int)Math.Clamp(Math.Round(99.4708025861 * Math.Log(t) - 161.1195681661), 0.0, 255.0);
		}
		else
		{
			g = (int)Math.Clamp(Math.Round(288.1221695283 * Math.Pow(t - 60.0, -0.0755148492)), 0.0, 255.0);
		}

		int b;
		if (t >= 66.0)
		{
			b = 255;
		}
		else if (t <= 19.0)
		{
			b = 0;
		}
		else
		{
			b = (int)Math.Clamp(Math.Round(138.5177312231 * Math.Log(t - 10.0) - 305.0447927307), 0.0, 255.0);
		}

		// Convert to linear RGB
		byte linR = ConvertToLinear(r);
		byte linG = ConvertToLinear(g);
		byte linB = ConvertToLinear(b);

		return (linR, linG, linB);
	}

	private static byte ConvertToLinear(double srgbByte)
	{
		double v = srgbByte / 255.0;
		double linear = v <= 0.04045 ? (v / 12.92) : Math.Pow((v + 0.055) / 1.055, 2.4);
		return (byte)Math.Clamp(Math.Round(linear * 255.0), 0.0, 255.0);
	}

	private static int FindColumn(Dictionary<string, int> colMap, params string[] names)
	{
		foreach (string name in names)
		{
			if (colMap.TryGetValue(name, out int idx))
			{
				return idx;
			}
		}
		return -1;
	}

	private static int GetRequiredColumn(Dictionary<string, int> colMap, string name)
	{
		if (colMap.TryGetValue(name, out int idx))
		{
			return idx;
		}
		throw new InvalidDataException($"Required CSV column '{name}' was not found.");
	}

	private static string[] ParseCsvLine(string line, char delimiter = ',')
	{
		List<string> result = new();
		bool inQuotes = false;
		StringBuilder current = new();

		for (int i = 0; i < line.Length; i++)
		{
			char c = line[i];
			if (c == '\"')
			{
				inQuotes = !inQuotes;
			}
			else if (c == delimiter && !inQuotes)
			{
				result.Add(current.ToString());
				current.Clear();
			}
			else
			{
				current.Append(c);
			}
		}
		result.Add(current.ToString());
		return result.ToArray();
	}
}
