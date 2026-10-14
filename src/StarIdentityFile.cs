using System.Text;

namespace OhMyStars;

// The "sister" file written next to a game star binary (<name>.bin -> <name>.ohmystars.bin;
// the mod's default OhMyStars_stars.bin -> OhMyStars__stars_IDs.bin).
// Records are in exactly the same order as the game file, so record i describes game star i.
// Layout: "OMSI" magic, int version, int count, then per record:
//   int hip, int catalogId, float dirX, dirY, dirZ, float invPc, float mag, float absMag, float ci, string name
internal static class StarIdentityFile
{
	internal const string Extension = ".ohmystars.bin";
	private const uint Magic = 0x49534D4F; // "OMSI" little-endian
	private const int Version = 1;

	internal struct Record
	{
		public int Hip;
		public int CatalogId;
		public float DirX;
		public float DirY;
		public float DirZ;
		public float InverseParsecs;
		public float Magnitude;
		public float AbsoluteMagnitude;
		public float ColorIndex;
		public string Name;
	}

	// The mod's default binary pairs with a specially named identity file instead of "<name>.ohmystars.bin".
	internal const string DefaultBinaryName = "OhMyStars_stars.bin";
	internal const string DefaultIdentityName = "OhMyStars__stars_IDs.bin";

	internal static string GetSisterPath(string gameBinaryPath)
	{
		string directory = Path.GetDirectoryName(gameBinaryPath) ?? string.Empty;
		if (string.Equals(Path.GetFileName(gameBinaryPath), DefaultBinaryName, StringComparison.OrdinalIgnoreCase))
		{
			return Path.Combine(directory, DefaultIdentityName);
		}

		string stem = Path.GetFileNameWithoutExtension(gameBinaryPath);
		if (stem.EndsWith(GameSuffix, StringComparison.OrdinalIgnoreCase))
		{
			return Path.Combine(directory, stem + IdsSuffixAfterStars + ".bin");
		}

		// Legacy naming for binaries that don't follow the "<name>_stars.bin" convention.
		return Path.Combine(directory, stem + Extension);
	}

	internal const string GameSuffix = "_stars";
	internal const string IdentitySuffix = "_stars_IDs";
	private const string IdsSuffixAfterStars = "_IDs";

	internal static bool IsSisterFile(string path)
	{
		return path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
			|| path.EndsWith(IdentitySuffix + ".bin", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(Path.GetFileName(path), DefaultIdentityName, StringComparison.OrdinalIgnoreCase);
	}

	internal static void Write(string path, IReadOnlyList<Record> records)
	{
		using FileStream stream = new(path, FileMode.Create, FileAccess.Write);
		using BinaryWriter writer = new(stream, Encoding.UTF8);
		writer.Write(Magic);
		writer.Write(Version);
		writer.Write(records.Count);
		foreach (Record r in records)
		{
			writer.Write(r.Hip);
			writer.Write(r.CatalogId);
			writer.Write(r.DirX);
			writer.Write(r.DirY);
			writer.Write(r.DirZ);
			writer.Write(r.InverseParsecs);
			writer.Write(r.Magnitude);
			writer.Write(r.AbsoluteMagnitude);
			writer.Write(r.ColorIndex);
			writer.Write(r.Name ?? string.Empty);
		}
	}

	internal static List<Record>? TryRead(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}

		try
		{
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
			using BinaryReader reader = new(stream, Encoding.UTF8);
			if (reader.ReadUInt32() != Magic)
			{
				return null;
			}

			int version = reader.ReadInt32();
			if (version != Version)
			{
				Console.WriteLine($"OhMyStars: unsupported identity file version {version} in {path}");
				return null;
			}

			int count = reader.ReadInt32();
			List<Record> records = new(Math.Max(0, count));
			for (int i = 0; i < count; i++)
			{
				records.Add(new Record
				{
					Hip = reader.ReadInt32(),
					CatalogId = reader.ReadInt32(),
					DirX = reader.ReadSingle(),
					DirY = reader.ReadSingle(),
					DirZ = reader.ReadSingle(),
					InverseParsecs = reader.ReadSingle(),
					Magnitude = reader.ReadSingle(),
					AbsoluteMagnitude = reader.ReadSingle(),
					ColorIndex = reader.ReadSingle(),
					Name = reader.ReadString(),
				});
			}

			return records;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OhMyStars: failed to read identity file {path}: {ex.Message}");
			return null;
		}
	}
}
