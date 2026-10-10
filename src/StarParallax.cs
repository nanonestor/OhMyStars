using Brutal.Numerics;
using KSA;
using System.Reflection;

namespace OhMyStars;

// Experimental: moves nearby background stars with the camera (Sun-centred ecliptic frame) and
// re-derives their size/brightness from absolute magnitude at the new distance. Only stars loaded from
// a .bin carrying an "OMSP" parallax section (see tools/make_parallax_bin.py) are affected.
internal static class StarParallax
{
	internal const double MetersPerParsec = 3.0856775814913673e16;
	private const float StarSphereDistance = 19.5f;
	private const int PrefixRecordSize = 16;
	private const int OmspRecordSize = 20;
	// ~0.2 AU: far below one pixel of shift even for the nearest stars, so re-applying more often gains nothing.
	private const double MoveThresholdPc = 1e-6;

	private static readonly double BrightFlux = Math.Pow(10.0, 0.64);
	private static readonly double DimFlux = Math.Pow(10.0, -2.72);

	// Sol from athyg_32_reduced_m10.csv (absmag 4.85, B-V 0.656 -> linear RGB via the generator's formula).
	private const float SolAbsMag = 4.85f;
	private const byte SolR = 255;
	private const byte SolG = 226;
	private const byte SolB = 202;

	internal static bool Enabled = true;
	internal static float CutoffPc = DefaultCutoffPc;
	internal const float DefaultCutoffPc = 500f;
	// Stars closer to the camera than this are hidden so the game's own rendering of that star system takes over.
	internal static float HideRadiusPc = DefaultHideRadiusPc;
	internal const float DefaultHideRadiusPc = 0.1f;

	private sealed class OmspData
	{
		public required float[] Px { get; init; }
		public required float[] Py { get; init; }
		public required float[] Pz { get; init; }
		public required float[] AbsMag { get; init; }
		public required byte[] R { get; init; }
		public required byte[] G { get; init; }
		public required byte[] B { get; init; }
		public required byte[] Flags { get; init; }
	}

	private struct MappedStar
	{
		public int Slot;
		public double Px, Py, Pz;
		public double SunDistance;
		public float AbsMag;
		public byte R, G, B;
		public SpriteInstance Base;
	}

	private static readonly Dictionary<string, OmspData?> _fileCache = new(StringComparer.OrdinalIgnoreCase);
	private static readonly List<MappedStar> _pendingMap = new();
	private static MappedStar[] _stars = Array.Empty<MappedStar>();
	private static InstancedStarTechnique? _technique;
	private static int _expectedInstanceCount = -1;
	private static bool _dirty;
	private static bool _applied;
	private static double3 _lastCameraPc;
	private static FieldInfo? _instancesField;

	internal static int MappedStarCount => _stars.Length;

	internal static void MarkDirty() => _dirty = true;

	internal static void ClearMap()
	{
		_pendingMap.Clear();
		_stars = Array.Empty<MappedStar>();
		_technique = null;
		_expectedInstanceCount = -1;
		_applied = false;
	}

	internal static void Record(int slot, string path, int index)
	{
		OmspData? data = GetOmsp(path);
		if (data == null || index < 0 || index >= data.Flags.Length || (data.Flags[index] & 1) == 0)
		{
			return;
		}

		double px = data.Px[index];
		double py = data.Py[index];
		double pz = data.Pz[index];
		_pendingMap.Add(new MappedStar
		{
			Slot = slot,
			Px = px,
			Py = py,
			Pz = pz,
			SunDistance = Math.Sqrt(px * px + py * py + pz * pz),
			AbsMag = data.AbsMag[index],
			R = data.R[index],
			G = data.G[index],
			B = data.B[index]
		});
	}

	internal static void OnStarsReloaded(InstancedStarTechnique starTechnique)
	{
		try
		{
			SpriteInstance[]? instances = GetInstances(starTechnique);
			if (instances == null)
			{
				ClearMap();
				return;
			}

			int solSlot = _pendingMap.Count > 0 ? AddSolInstance(starTechnique) : -1;
			MappedStar[] stars = _pendingMap.ToArray();
			_pendingMap.Clear();
			for (int i = 0; i < stars.Length; i++)
			{
				stars[i].Base = instances[stars[i].Slot];
				if (stars[i].Slot == solSlot)
				{
					// Sol is only meaningful as a background star while parallax is active.
					stars[i].Base = new SpriteInstance { Position = stars[i].Base.Position, PackedData = 0u };
				}
			}

			_stars = stars;
			_technique = starTechnique;
			_expectedInstanceCount = starTechnique.InstanceCount[Program.MainViewport.ShaderSlot];
			_applied = false;
			_dirty = true;
			Console.WriteLine($"OhMyStars parallax: {stars.Length} stars mapped.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OhMyStars parallax: reload snapshot failed: {ex.Message}");
			ClearMap();
		}
	}

	// The star bins never contain the Sun; add it so it shows up as a background star once the camera leaves the
	// solar system (it is hidden by the near-camera cull while inside it).
	private static int AddSolInstance(InstancedStarTechnique starTechnique)
	{
		int countBefore = starTechnique.InstanceCount[Program.MainViewport.ShaderSlot];
		starTechnique.AddInstance(Program.MainViewport, new float3(1f, 0f, 0f), 17, new byte4(SolR, SolG, SolB, byte.MaxValue), StarSphereDistance);
		if (starTechnique.InstanceCount[Program.MainViewport.ShaderSlot] <= countBefore)
		{
			return -1;
		}

		_pendingMap.Add(new MappedStar
		{
			Slot = countBefore,
			AbsMag = SolAbsMag,
			R = SolR,
			G = SolG,
			B = SolB
		});
		return countBefore;
	}

	internal static bool IsActive => Enabled && _stars.Length > 0;

	internal static double3 GetCameraPc(Camera camera) => camera.PositionEcl * (1.0 / MetersPerParsec);

	// True when the camera is far enough from the Sun that the Sun is drawn as a background star.
	internal static bool IsCameraOutsideSolarSystem(Camera camera)
	{
		if (!IsActive)
		{
			return false;
		}

		double3 c = GetCameraPc(camera);
		double hide = Math.Max(HideRadiusPc, 1e-9);
		return c.X * c.X + c.Y * c.Y + c.Z * c.Z > hide * hide;
	}

	internal static void Update()
	{
		InstancedStarTechnique? technique = _technique;
		if (technique == null || _stars.Length == 0)
		{
			return;
		}

		try
		{
			if (!Enabled)
			{
				if (_applied)
				{
					Restore(technique);
				}

				return;
			}

			Camera? camera = Program.GetMainCamera();
			if (camera == null)
			{
				return;
			}

			double3 cameraPc = camera.PositionEcl * (1.0 / MetersPerParsec);
			double3 delta = cameraPc - _lastCameraPc;
			bool moved = delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z > MoveThresholdPc * MoveThresholdPc;
			if (!moved && !_dirty && _applied)
			{
				return;
			}

			SpriteInstance[]? instances = GetValidatedInstances(technique);
			if (instances == null)
			{
				return;
			}

			_lastCameraPc = cameraPc;
			_dirty = false;
			Apply(instances, cameraPc);
			_applied = true;
			PushToGpu(technique);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OhMyStars parallax: update failed, disabling: {ex.Message}");
			Enabled = false;
		}
	}

	private static void Apply(SpriteInstance[] instances, double3 cameraPc)
	{
		double cutoff = CutoffPc;
		bool limited = cutoff > 0d;
		double hideRadius = Math.Max(0d, HideRadiusPc);
		double hideRadiusSq = hideRadius * hideRadius;
		int additiveOffset = StarsEditWindow.RenderDataAdditiveOffset;
		int sizeFloor = StarsEditWindow.SizeFloorThreshold;
		float redScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.RedMultiplier;
		float greenScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.GreenMultiplier;
		float blueScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.BlueMultiplier;

		MappedStar[] stars = _stars;
		for (int i = 0; i < stars.Length; i++)
		{
			ref MappedStar star = ref stars[i];
			double dx = star.Px - cameraPc.X;
			double dy = star.Py - cameraPc.Y;
			double dz = star.Pz - cameraPc.Z;
			double distanceSq = dx * dx + dy * dy + dz * dz;
			if (distanceSq <= hideRadiusSq || distanceSq < 1e-18)
			{
				instances[star.Slot] = new SpriteInstance { Position = star.Base.Position, PackedData = 0u };
				continue;
			}

			if (limited && star.SunDistance > cutoff)
			{
				instances[star.Slot] = star.Base;
				continue;
			}

			double distance = Math.Sqrt(distanceSq);

			double apparentMag = star.AbsMag + 5.0 * Math.Log10(distance) - 5.0;
			ComputeSizeAndColor(apparentMag, star.R, star.G, star.B, out byte size, out byte r, out byte g, out byte b);

			byte scaleByte = StarsEditWindow.TransformScaleByte(size);
			if (scaleByte < sizeFloor)
			{
				scaleByte = 0;
			}

			byte4 color = new byte4(
				StarsEditWindow.TransformRenderByte(r, redScale, additiveOffset),
				StarsEditWindow.TransformRenderByte(g, greenScale, additiveOffset),
				StarsEditWindow.TransformRenderByte(b, blueScale, additiveOffset),
				byte.MaxValue);

			double inv = StarSphereDistance / distance;
			instances[star.Slot] = new SpriteInstance
			{
				Position = new float3((float)(dx * inv), (float)(dy * inv), (float)(dz * inv)),
				PackedData = Pack(color, scaleByte)
			};
		}
	}

	// Mirrors tools/make_parallax_bin.py (and the original star bin generator) size/dim rule.
	private static void ComputeSizeAndColor(double mag, byte baseR, byte baseG, byte baseB, out byte size, out byte r, out byte g, out byte b)
	{
		double flux = Math.Pow(10.0, -0.4 * mag);
		double norm = Math.Clamp((flux - DimFlux) / (BrightFlux - DimFlux), 0.0, 1.0);
		int s = (int)Math.Round(10.0 + 245.0 * Math.Pow(norm, 0.45), MidpointRounding.ToEven);
		r = baseR;
		g = baseG;
		b = baseB;
		if (s < 17)
		{
			s = 17;
			r = (byte)Math.Round(baseR * 0.2, MidpointRounding.ToEven);
			g = (byte)Math.Round(baseG * 0.2, MidpointRounding.ToEven);
			b = (byte)Math.Round(baseB * 0.2, MidpointRounding.ToEven);
		}

		size = (byte)Math.Clamp(s, 0, 255);
	}

	// Same packing as InstancedStarTechnique.AddInstance.
	private static uint Pack(byte4 color, byte scale)
	{
		uint c = (uint)color;
		return ((c & 0xFFu) << 24) | (((c >> 8) & 0xFFu) << 16) | (((c >> 16) & 0xFFu) << 8) | scale;
	}

	private static void Restore(InstancedStarTechnique technique)
	{
		_applied = false;
		SpriteInstance[]? instances = GetValidatedInstances(technique);
		if (instances == null)
		{
			return;
		}

		MappedStar[] stars = _stars;
		for (int i = 0; i < stars.Length; i++)
		{
			instances[stars[i].Slot] = stars[i].Base;
		}

		PushToGpu(technique);
	}

	private static void PushToGpu(InstancedStarTechnique technique)
	{
		foreach (IViewport viewport in ViewportRegistry.Views)
		{
			technique.UpdateInstanceBuffer(viewport, 0);
		}
	}

	private static SpriteInstance[]? GetValidatedInstances(InstancedStarTechnique technique)
	{
		int slot = Program.MainViewport.ShaderSlot;
		if (technique.InstanceCount[slot] != _expectedInstanceCount)
		{
			// Stars were reloaded by something other than StarsEditWindow.ApplySelection; mapping is stale.
			Console.WriteLine("OhMyStars parallax: star instances changed externally, mapping cleared.");
			ClearMap();
			return null;
		}

		SpriteInstance[]? instances = GetInstances(technique);
		if (instances == null || instances.Length < _expectedInstanceCount)
		{
			ClearMap();
			return null;
		}

		return instances;
	}

	private static SpriteInstance[]? GetInstances(InstancedStarTechnique technique)
	{
		_instancesField ??= typeof(InstancedRenderTechnique<SpriteInstance>).GetField("Instances", BindingFlags.Instance | BindingFlags.NonPublic);
		if (_instancesField?.GetValue(technique) is not SpriteInstance[][] all)
		{
			return null;
		}

		int slot = Program.MainViewport.ShaderSlot;
		return slot >= 0 && slot < all.Length ? all[slot] : null;
	}

	private static OmspData? GetOmsp(string path)
	{
		if (_fileCache.TryGetValue(path, out OmspData? cached))
		{
			return cached;
		}

		OmspData? data = null;
		try
		{
			data = ReadOmsp(path);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OhMyStars parallax: failed to read OMSP data from '{path}': {ex.Message}");
		}

		_fileCache[path] = data;
		return data;
	}

	private static OmspData? ReadOmsp(string path)
	{
		using FileStream stream = File.OpenRead(path);
		using BinaryReader reader = new BinaryReader(stream);
		if (stream.Length < sizeof(int))
		{
			return null;
		}

		long count = reader.ReadInt32();
		long sectionStart = sizeof(int) + count * PrefixRecordSize;
		if (count <= 0 || sectionStart + 16 > stream.Length)
		{
			return null;
		}

		stream.Position = sectionStart;
		if (reader.ReadByte() != (byte)'O' || reader.ReadByte() != (byte)'M' || reader.ReadByte() != (byte)'S' || reader.ReadByte() != (byte)'P')
		{
			return null;
		}

		int version = reader.ReadInt32();
		int prefixCount = reader.ReadInt32();
		int extraCount = reader.ReadInt32();
		if (version != 1 || prefixCount != count || extraCount < 0)
		{
			return null;
		}

		int n = prefixCount;
		if (stream.Position + (long)(prefixCount + extraCount) * OmspRecordSize > stream.Length)
		{
			return null;
		}

		OmspData data = new OmspData
		{
			Px = new float[n],
			Py = new float[n],
			Pz = new float[n],
			AbsMag = new float[n],
			R = new byte[n],
			G = new byte[n],
			B = new byte[n],
			Flags = new byte[n]
		};

		for (int i = 0; i < n; i++)
		{
			data.Px[i] = reader.ReadSingle();
			data.Py[i] = reader.ReadSingle();
			data.Pz[i] = reader.ReadSingle();
			data.AbsMag[i] = reader.ReadSingle();
			data.R[i] = reader.ReadByte();
			data.G[i] = reader.ReadByte();
			data.B[i] = reader.ReadByte();
			data.Flags[i] = reader.ReadByte();
		}

		Console.WriteLine($"OhMyStars parallax: loaded OMSP data for {n} stars from '{Path.GetFileName(path)}'.");
		return data;
	}
}
