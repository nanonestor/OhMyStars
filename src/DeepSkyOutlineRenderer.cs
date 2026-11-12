using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using static OhMyStars.DeepSkyOutlineGeometry;

namespace OhMyStars;

internal static class DeepSkyOutlineRenderer {
    private sealed class CachedContour(DeepSkyOutlines.Contour contour) {
        public readonly DeepSkyCatalog.Entry Entry = contour.Entry;
        public readonly double3[] Directions = contour.Points.Select(point =>
            StarDirectionConverter.RaDecToDirection(point.RaHours, point.DecDegrees)).ToArray();
        public readonly double3[] Aligned = new double3[contour.Points.Length];
        public readonly double3[] Positions = new double3[contour.Points.Length];
        public readonly ClipPoint[] Clip = new ClipPoint[contour.Points.Length];
        public double3 Center;
        public double3 AlignedCenter;
        public double BoundsRadius;
        private float3 _color = new(float.NaN);
        public ImColor8 Color;

        public void UpdateColor(DeepSkySettings.TypeStyle style) {
            if(_color.X == style.Red && _color.Y == style.Green && _color.Z == style.Blue)
                return;
            _color = new(style.Red, style.Green, style.Blue);
            Color = StellariumRenderer.ToLineColor(_color, 0.8f);
        }
    }

    private sealed record Loaded(CachedContour[] Contours);
    private static Task<Loaded>? _loading;
    private static Loaded? _loaded;
    private static string? _error;
    private static (float X, float Y, float Z)? _alignment;
    private static int _catalogMask = -1;
    private static readonly List<CachedContour> Eligible = [];
    private static readonly List<Target> Targets = [];
    private static readonly List<ClipPoint> WorkA = [];
    private static readonly List<ClipPoint> WorkB = [];
    private static float2 _viewportOrigin;
    private static int2 _framebuffer;
    private static Camera? _pickCamera;
    private static double3 _pickCenter;
    private static double _pickRadius;

    private static void Load(DeepSkyCatalog catalog) {
        if(_loaded is not null || _error is not null)
            return;
        _loading ??= Task.Run(() => {
            var source = DeepSkyOutlines.Load(ModPaths.Combine("catalogs", "OpenNGC", "outlines", "outlines_stellarium.dat"), catalog);
            var contours = source.Contours.Select(contour => new CachedContour(contour)).ToArray();
            foreach(var contour in contours) {
                foreach(var direction in contour.Directions)
                    contour.Center += direction;
                contour.Center /= contour.Directions.Length;
                foreach(var direction in contour.Directions)
                    contour.BoundsRadius = Math.Max(contour.BoundsRadius, VectorMath.Length(direction - contour.Center));
                contour.BoundsRadius += 1e-6;
            }
            string? notice = source.UnmatchedNames.Length == 0 ? null :
                $"OpenNGC outlines: {source.UnmatchedNames.Length} named regions have no unique drawable catalog match and are omitted: {string.Join(", ", source.UnmatchedNames)}.";
            Console.WriteLine($"OhMyStars: loaded {contours.Length} matched OpenNGC contours from {source.SourceContourCount} source contours.");
            if(notice is not null)
                Console.WriteLine($"OhMyStars: {notice}");
            return new Loaded(contours);
        });
        if(!_loading.IsCompleted)
            return;
        if(_loading.IsFaulted) {
            _error = _loading.Exception!.GetBaseException().Message;
            Console.WriteLine($"OhMyStars: could not load nebula outlines: {_loading.Exception}");
            return;
        }
        _loaded = _loading.GetAwaiter().GetResult();
        Targets.EnsureCapacity(_loaded.Contours.Length);
    }

    public static void DrawStatus(DeepSkyCatalog catalog) {
        Load(catalog);
        if(_error is not null) {
            ImGui.TextWrapped($"Nebula outline load failed: {_error}");
            if(ImGui.Button("Retry nebula outlines")) {
                _loading = null;
                _error = null;
            }
        } else if(_loaded is null) {
            ImGui.Text("Loading nebula outlines...");
        }
    }

    public static void ClearTargets() {
        Targets.Clear();
        _pickCamera = null;
    }

    public static unsafe void Draw(ImDrawListPtr draw, Camera camera, double3 center, double radius,
        DeepSkyCatalog catalog, DeepSkySettings settings, bool collectTargets, long? selectedId) {
        if(!settings.ShowNebulaOutlines)
            return;
        Load(catalog);
        if(_loaded is null)
            return;
        var alignment = (StellariumRenderer.alignmentRotationXDegrees, StellariumRenderer.alignmentRotationYDegrees,
            StellariumRenderer.alignmentRotationZDegrees);
        if(_alignment != alignment) {
            foreach(var contour in _loaded.Contours) {
                contour.AlignedCenter = StellariumRenderer.ApplyAlignment(contour.Center);
                for(int i = 0; i < contour.Directions.Length; i++)
                    contour.Aligned[i] = StellariumRenderer.ApplyAlignment(contour.Directions[i]);
            }
            _alignment = alignment;
        }
        int catalogMask = 0;
        foreach(var (kind, enabled) in settings.Catalogs)
            if(enabled)
                catalogMask |= 1 << (int)kind;
        if(_catalogMask != catalogMask) {
            Eligible.Clear();
            foreach(var contour in _loaded.Contours)
                foreach(var (kind, enabled) in settings.Catalogs)
                    if(enabled && contour.Entry.InCatalog(kind)) {
                        Eligible.Add(contour);
                        break;
                    }
            _catalogMask = catalogMask;
        }
        ImGuiViewport* viewport = ImGui.GetMainViewport();
        _viewportOrigin = viewport == null ? new(0, 0) : viewport->Pos;
        _framebuffer = camera.FramebufferSize;
        _pickCamera = collectTargets ? camera : null;
        _pickCenter = center;
        _pickRadius = radius;
        foreach(var contour in Eligible) {
            if(camera.IsSphereOutsideFov(center + contour.AlignedCenter * radius, contour.BoundsRadius * radius))
                continue;
            contour.UpdateColor(settings.Styles[contour.Entry.Type]);
            for(int i = 0; i < contour.Aligned.Length; i++) {
                double3 position = center + contour.Aligned[i] * radius;
                contour.Positions[i] = position;
                double4 clip = camera.EgoToClipDouble(position);
                contour.Clip[i] = new(clip.X, clip.Y, clip.W);
            }
            if(collectTargets)
                Targets.Add(new(contour.Entry.Id, contour.Clip));
            for(int i = 0; i < contour.Clip.Length; i++) {
                int next = (i + 1) % contour.Clip.Length;
                ClipPoint a = contour.Clip[i], b = contour.Clip[next];
                if(!ClipSegment(a, b, out double start, out double end))
                    continue;
                double3 delta = contour.Positions[next] - contour.Positions[i];
                double3 worldA = contour.Positions[i] + delta * start;
                double3 worldB = contour.Positions[i] + delta * end;
                if(!StellariumRenderer.IsVisibleFromCamera(worldA) ||
                    !StellariumRenderer.IsVisibleFromCamera((worldA + worldB) * 0.5) ||
                    !StellariumRenderer.IsVisibleFromCamera(worldB))
                    continue;
                float2 screenA = ToScreen(ClipPoint.Lerp(a, b, start).Ndc);
                float2 screenB = ToScreen(ClipPoint.Lerp(a, b, end).Ndc);
                ImDrawListExtensions.AddLine(draw, screenA, screenB, contour.Color, selectedId == contour.Entry.Id ? 2f : 1f);
            }
        }
    }

    private static float2 ToScreen(Point ndc) => _viewportOrigin + new float2(
        (float)((ndc.X + 1) * 0.5 * _framebuffer.X), (float)((ndc.Y + 1) * 0.5 * _framebuffer.Y));

    public static long? Pick(long? ringId, float2 mouse) {
        if(ringId.HasValue)
            return ringId;
        if(_pickCamera is null || Targets.Count == 0 || _framebuffer.X <= 0 || _framebuffer.Y <= 0)
            return null;
        float2 local = mouse - _viewportOrigin;
        Point ndc = new(2 * (double)local.X / _framebuffer.X - 1, 2 * (double)local.Y / _framebuffer.Y - 1);
        Ray ray = _pickCamera.ScreenToEgoRay(local);
        double3 relative = ray.Origin - _pickCenter;
        double projection = double3.Dot(relative, ray.Direction);
        double discriminant = projection * projection - double3.Dot(relative, relative) + _pickRadius * _pickRadius;
        if(discriminant < 0)
            return null;
        double distance = -projection + Math.Sqrt(discriminant);
        if(distance <= 0 || !StellariumRenderer.IsVisibleFromCamera(ray.Origin + ray.Direction * distance))
            return null;
        return DeepSkyOutlineGeometry.Pick(null, Targets, ndc, WorkA, WorkB);
    }
}
