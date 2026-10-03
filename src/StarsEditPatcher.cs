using Brutal.Numerics;
using HarmonyLib;
using KSA;
using System.Reflection;

namespace OhMyStars;

[HarmonyPatch]
internal static class StarsEditPatcher
{
    private static Harmony? _harmony;

    private static int _requestedStarCapacity;

    private static int _prefixSinceLastApply;

    private static int _droppedSinceLastApply;

    private static int _isApplyCycleActive;

    private static FieldInfo? _maxInstancesField;

    private static FieldInfo? _instancesField;

    private static FieldInfo? _instanceBufferField;

    private static FieldInfo? _instanceMemoryField;

    private static MethodInfo? _createInstanceBufferMethod;

    internal static bool IsPatched => _harmony is not null;

    internal static int PrefixSinceLastApply => _prefixSinceLastApply;

    internal static int DroppedSinceLastApply => _droppedSinceLastApply;

    internal static int RequestedStarCapacity => Volatile.Read(ref _requestedStarCapacity);

    internal static void SetRequestedStarCapacity(int requestedCapacity)
    {
        requestedCapacity = Math.Max(requestedCapacity, 1);
        Interlocked.Exchange(ref _requestedStarCapacity, requestedCapacity);
    }

    internal static void BeginApplyCycle()
    {
        Interlocked.Exchange(ref _prefixSinceLastApply, 0);
        Interlocked.Exchange(ref _droppedSinceLastApply, 0);
        Interlocked.Exchange(ref _isApplyCycleActive, 1);
    }

    internal static void EndApplyCycle()
    {
        Interlocked.Exchange(ref _isApplyCycleActive, 0);
    }

    internal static void Patch()
    {
        if (_harmony is not null)
        {
            return;
        }

        _harmony = new Harmony("nanonestor.ohmystars.starsedit");
        // Only this class: PatchAll would re-apply OhMyStars' other patch classes a second time.
        _harmony.CreateClassProcessor(typeof(StarsEditPatcher)).Patch();
        PatchInstancedStarTechniqueConstructor(_harmony);
        _prefixSinceLastApply = 0;
        _droppedSinceLastApply = 0;
        _isApplyCycleActive = 0;
        Console.WriteLine("StarsEdit Harmony patch applied.");
    }

    internal static void Unload()
    {
        if (_harmony is null)
        {
            return;
        }

        _harmony.UnpatchAll(_harmony.Id);
        _harmony = null;
        Console.WriteLine("StarsEdit Harmony unpatched.");
    }

    [HarmonyPatch(typeof(InstancedStarTechnique), nameof(InstancedStarTechnique.AddInstance),
        new[] { typeof(IViewport), typeof(float3), typeof(byte), typeof(byte4), typeof(float) })]
    [HarmonyPrefix]
    private static bool InstancedStarTechniqueAddInstancePrefix(InstancedStarTechnique __instance, IViewport viewport, ref byte scale, ref byte4 color)
    {
        bool isActiveCycle = Volatile.Read(ref _isApplyCycleActive) == 1;

        if (IsBufferFull(__instance, viewport))
        {
            if (isActiveCycle)
            {
                Interlocked.Increment(ref _droppedSinceLastApply);
            }

            return false;
        }

        int additiveOffset = StarsEditWindow.RenderDataAdditiveOffset;
        byte transformedScale = StarsEditWindow.TransformScaleByte(scale);
        if (transformedScale < StarsEditWindow.SizeFloorThreshold)
        {
            transformedScale = 0;
        }

        scale = transformedScale;

        float redScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.RedMultiplier;
        float greenScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.GreenMultiplier;
        float blueScale = StarsEditWindow.ColorMultiplier * StarsEditWindow.BlueMultiplier;

        color = new byte4(
                StarsEditWindow.TransformRenderByte(color.X, redScale, additiveOffset),
                StarsEditWindow.TransformRenderByte(color.Y, greenScale, additiveOffset),
                StarsEditWindow.TransformRenderByte(color.Z, blueScale, additiveOffset),
                color.W);

        if (isActiveCycle)
        {
            Interlocked.Increment(ref _prefixSinceLastApply);
        }

        return true;
    }

    private static void PatchInstancedStarTechniqueConstructor(Harmony harmony)
    {
        ConstructorInfo? ctor = typeof(InstancedStarTechnique)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(static c =>
            {
                ParameterInfo[] p = c.GetParameters();
                return p.Length >= 5 && p[4].ParameterType == typeof(int);
            });

        if (ctor is null)
        {
            Console.WriteLine("StarsEdit could not find InstancedStarTechnique constructor to patch.");
            return;
        }

        harmony.Patch(ctor, prefix: new HarmonyMethod(typeof(StarsEditPatcher), nameof(InstancedStarTechniqueCtorPrefix)));
    }

    private static void InstancedStarTechniqueCtorPrefix(ref int __4)
    {
        int currentMax = __4;
        int requestedMax = RequestedStarCapacity;
        if (requestedMax > 0 && requestedMax != currentMax)
        {
            __4 = requestedMax;
            Console.WriteLine($"StarsEdit set star capacity from {currentMax} to {requestedMax}.");
        }
    }

    internal static int TryAdaptCapacityNow(InstancedStarTechnique starTechnique, int requestedCapacity)
    {
        int currentCapacity = GetMaxInstances(starTechnique);
        if (requestedCapacity <= 0 || requestedCapacity == currentCapacity)
        {
            return currentCapacity;
        }

        try
        {
            Type baseType = typeof(InstancedRenderTechnique<SpriteInstance>);
            _maxInstancesField ??= baseType.GetField("_maxInstances", BindingFlags.Instance | BindingFlags.NonPublic);
            _instancesField ??= baseType.GetField("Instances", BindingFlags.Instance | BindingFlags.NonPublic);
            _instanceBufferField ??= typeof(InstancedStarTechnique).GetField("_instanceBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
            _instanceMemoryField ??= typeof(InstancedStarTechnique).GetField("_instanceMemory", BindingFlags.Instance | BindingFlags.NonPublic);
            _createInstanceBufferMethod ??= typeof(InstancedStarTechnique).GetMethod("CreateInstanceBuffer", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_maxInstancesField is null || _instancesField is null || _createInstanceBufferMethod is null)
            {
                return currentCapacity;
            }

            if (_instancesField.GetValue(starTechnique) is not SpriteInstance[][] oldInstances)
            {
                return currentCapacity;
            }

            SpriteInstance[][] resized = new SpriteInstance[oldInstances.Length][];
            int[] counts = starTechnique.InstanceCount;
            for (int i = 0; i < oldInstances.Length; i++)
            {
                SpriteInstance[] source = oldInstances[i] ?? Array.Empty<SpriteInstance>();
                SpriteInstance[] target = new SpriteInstance[requestedCapacity];
                int copyCount = Math.Min(source.Length, target.Length);
                if (copyCount > 0)
                {
                    Array.Copy(source, target, copyCount);
                }

                resized[i] = target;

                if (i < counts.Length && counts[i] > requestedCapacity)
                {
                    counts[i] = requestedCapacity;
                }
            }

            object? oldMemory = _instanceMemoryField?.GetValue(starTechnique);
            oldMemory?.GetType().GetMethod("Unmap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(oldMemory, null);

            object? oldBuffer = _instanceBufferField?.GetValue(starTechnique);
            if (oldBuffer is IDisposable disposableBuffer)
            {
                disposableBuffer.Dispose();
            }
            else
            {
                oldBuffer?.GetType().GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(oldBuffer, null);
            }

            _maxInstancesField.SetValue(starTechnique, requestedCapacity);
            _instancesField.SetValue(starTechnique, resized);
            _createInstanceBufferMethod.Invoke(starTechnique, null);

            int adapted = GetMaxInstances(starTechnique);
            if (adapted == requestedCapacity)
            {
                Console.WriteLine($"StarsEdit adapted star capacity from {currentCapacity} to {adapted}.");
            }

            return adapted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StarsEdit could not adapt star capacity at runtime: {ex.Message}");
            return currentCapacity;
        }
    }

    private static bool IsBufferFull(InstancedStarTechnique starTechnique, IViewport viewport)
    {
        int[] instanceCount = starTechnique.InstanceCount;
        if (viewport.ShaderSlot < 0 || viewport.ShaderSlot >= instanceCount.Length)
        {
            return false;
        }

        int maxInstances = GetMaxInstances(starTechnique);
        return instanceCount[viewport.ShaderSlot] >= maxInstances;
    }

    private static int GetMaxInstances(InstancedStarTechnique starTechnique)
    {
        _maxInstancesField ??= typeof(InstancedRenderTechnique<SpriteInstance>).GetField("_maxInstances", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_maxInstancesField?.GetValue(starTechnique) is int value)
        {
            return value;
        }

        return int.MaxValue;
    }
}
