using Brutal.Numerics;
using KSA;
using System.Reflection;

namespace OhMyStars;

// Bridges the mod to the game's built-in 3D star system (InstancedStarTechnique since v2026.10.X.5597).
// Each star is stored as a unit direction from the Sun plus an inverse distance in parsecs; the shader moves
// it with the observer: shifted = normalize(direction - observerPc * inverseParsecs). Overlays (asterism
// lines, labels, pointers) use the same formula so they stay locked to the rendered stars.
internal static class GameStarSpace
{
	internal const double MetersPerParsec = 3.0856775814913673e16;
	internal const double ParsecsPerMeter = 3.2407792887121616E-17;
	internal const double LightYearsPerParsec = 3.26156377695;

	// Matches the shader: a star this close to the observer (in unit-direction space) collapses and is skipped.
	private const double CollapseLength = 1e-3;

	// Beyond this the observer is treated as having left the Solar System, so the Sun becomes a background star.
	internal const double OutsideSolarSystemPc = 0.02;

	private static FieldInfo? _techniqueField;
	private static FieldInfo? _loadedField;
	private static FieldInfo? _loadedCountField;
	private static FieldInfo? _maxInstancesField;

	internal readonly struct LoadedStar
	{
		public readonly float3 Direction;
		public readonly float InverseParsecs;
		public readonly byte Scale;
		public readonly byte R;
		public readonly byte G;
		public readonly byte B;

		public LoadedStar(float3 direction, float inverseParsecs, byte scale, byte r, byte g, byte b)
		{
			Direction = direction;
			InverseParsecs = inverseParsecs;
			Scale = scale;
			R = r;
			G = g;
			B = b;
		}
	}

	internal static InstancedStarTechnique? GetTechnique()
	{
		_techniqueField ??= typeof(Program).GetField("_starStarTechnique", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		return _techniqueField?.GetValue(null) as InstancedStarTechnique;
	}

	internal static double3 GetObserverPc(Camera camera)
	{
		return InstancedStarTechnique.ObserverOverrideParsecs ?? camera.PositionEcl * ParsecsPerMeter;
	}

	internal static bool IsObserverOutsideSolarSystem(Camera camera)
	{
		double3 c = GetObserverPc(camera);
		return c.X * c.X + c.Y * c.Y + c.Z * c.Z > OutsideSolarSystemPc * OutsideSolarSystemPc;
	}

	// Applies the game's observer shift. Static stars (inverseParsecs <= 0) are returned unchanged.
	internal static double3 Shift(double3 direction, float inverseParsecs, double3 observerPc, out bool hidden)
	{
		hidden = false;
		if (inverseParsecs <= 0f)
		{
			return direction;
		}

		double3 shifted = direction - observerPc * inverseParsecs;
		double length = shifted.Length();
		if (length < CollapseLength)
		{
			hidden = true;
			return direction;
		}

		return shifted / length;
	}

	private static void EnsureFields()
	{
		_loadedField ??= typeof(InstancedStarTechnique).GetField("_loaded", BindingFlags.Instance | BindingFlags.NonPublic);
		_loadedCountField ??= typeof(InstancedStarTechnique).GetField("_loadedCount", BindingFlags.Instance | BindingFlags.NonPublic);
		_maxInstancesField ??= typeof(InstancedRenderTechnique<SpriteInstance>).GetField("_maxInstances", BindingFlags.Instance | BindingFlags.NonPublic);
	}

	internal static int GetLoadedCount(InstancedStarTechnique technique)
	{
		EnsureFields();
		return _loadedCountField?.GetValue(technique) is int count ? count : 0;
	}

	internal static int GetMaxInstances(InstancedStarTechnique technique)
	{
		EnsureFields();
		return _maxInstancesField?.GetValue(technique) is int value ? value : int.MaxValue;
	}

	internal static bool IsLoadedBufferFull(InstancedStarTechnique technique)
	{
		EnsureFields();
		int count = GetLoadedCount(technique);
		if (_loadedField?.GetValue(technique) is SpriteInstance[] loaded && count >= loaded.Length)
		{
			return true;
		}

		return count >= GetMaxInstances(technique);
	}

	// Clears the master star list so stars can be re-added with AddStar (follow with RefreshMasks).
	internal static void ResetLoaded(InstancedStarTechnique technique)
	{
		EnsureFields();
		_loadedCountField?.SetValue(technique, 0);
	}

	// Replaces the readonly master array when the capacity changes, keeping existing entries.
	internal static void ResizeLoaded(InstancedStarTechnique technique, int capacity)
	{
		EnsureFields();
		if (_loadedField is null || _loadedField.GetValue(technique) is not SpriteInstance[] old || old.Length == capacity)
		{
			return;
		}

		SpriteInstance[] resized = new SpriteInstance[capacity];
		int count = Math.Min(GetLoadedCount(technique), capacity);
		Array.Copy(old, resized, Math.Min(count, old.Length));
		_loadedField.SetValue(technique, resized);
		_loadedCountField?.SetValue(technique, count);
	}

	// Re-runs the game's star-system masking, which also copies the master list into the GPU instance buffers.
	internal static void RefreshMasks()
	{
		try
		{
			Program.MaskStars(Universe.Stars);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"OhMyStars: could not refresh star masks: {ex.Message}");
		}
	}

	internal static List<LoadedStar> GetLoadedStars(InstancedStarTechnique technique)
	{
		EnsureFields();
		List<LoadedStar> result = new();
		if (_loadedField?.GetValue(technique) is not SpriteInstance[] loaded)
		{
			return result;
		}

		int count = Math.Min(GetLoadedCount(technique), loaded.Length);
		result.Capacity = count;
		for (int i = 0; i < count; i++)
		{
			SpriteInstance s = loaded[i];
			uint packed = s.PackedData;
			result.Add(new LoadedStar(
				s.Direction,
				s.InverseParsecs,
				(byte)(packed & 0xFF),
				(byte)((packed >> 24) & 0xFF),
				(byte)((packed >> 16) & 0xFF),
				(byte)((packed >> 8) & 0xFF)));
		}

		return result;
	}
}
