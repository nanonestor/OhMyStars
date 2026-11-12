namespace OhMyStars;

internal static class DeepSkyOutlineGeometry {
    internal readonly record struct Point(double X, double Y);
    internal readonly record struct ClipPoint(double X, double Y, double W) {
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(W);
        public Point Ndc => new(X / W, Y / W);
        public static ClipPoint Lerp(ClipPoint a, ClipPoint b, double t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.W + (b.W - a.W) * t);
    }
    internal readonly record struct Target(long Id, ClipPoint[] Vertices);

    private static double Distance(ClipPoint point, int plane) => plane switch {
        0 => point.W - 1e-8,
        1 => point.X + point.W,
        2 => point.W - point.X,
        3 => point.Y + point.W,
        4 => point.W - point.Y,
        _ => throw new ArgumentOutOfRangeException(nameof(plane))
    };

    public static bool ClipSegment(ClipPoint a, ClipPoint b, out double start, out double end) {
        start = 0;
        end = 1;
        if(!a.IsFinite || !b.IsFinite)
            return false;
        for(int plane = 0; plane < 5; plane++) {
            double da = Distance(a, plane), db = Distance(b, plane);
            if(da < 0 && db < 0)
                return false;
            if((da < 0) != (db < 0)) {
                double t = da / (da - db);
                if(da < 0)
                    start = Math.Max(start, t);
                else
                    end = Math.Min(end, t);
                if(start > end)
                    return false;
            }
        }
        return true;
    }

    // Clip only for hit-testing. Drawing uses original segments, never synthetic viewport-border edges.
    public static bool Contains(ReadOnlySpan<ClipPoint> vertices, Point mouse, List<ClipPoint> workA,
        List<ClipPoint> workB, out double area) {
        area = 0;
        if(!double.IsFinite(mouse.X) || !double.IsFinite(mouse.Y) || mouse.X is < -1 or > 1 || mouse.Y is < -1 or > 1)
            return false;
        workA.Clear();
        foreach(var vertex in vertices) {
            if(!vertex.IsFinite)
                return false;
            workA.Add(vertex);
        }
        var source = workA;
        var destination = workB;
        for(int plane = 0; plane < 5; plane++) {
            if(source.Count < 3)
                return false;
            destination.Clear();
            ClipPoint previous = source[^1];
            double previousDistance = Distance(previous, plane);
            foreach(var current in source) {
                double currentDistance = Distance(current, plane);
                if((previousDistance < 0) != (currentDistance < 0))
                    destination.Add(ClipPoint.Lerp(previous, current, previousDistance / (previousDistance - currentDistance)));
                if(currentDistance >= 0)
                    destination.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }
            (source, destination) = (destination, source);
        }
        if(source.Count < 3)
            return false;
        bool inside = false, onEdge = false;
        Point a = source[^1].Ndc;
        foreach(var vertex in source) {
            Point b = vertex.Ndc;
            area += a.X * b.Y - b.X * a.Y;
            double cross = (mouse.X - a.X) * (b.Y - a.Y) - (mouse.Y - a.Y) * (b.X - a.X);
            if(Math.Abs(cross) <= 1e-10 && mouse.X >= Math.Min(a.X, b.X) - 1e-10 &&
                mouse.X <= Math.Max(a.X, b.X) + 1e-10 && mouse.Y >= Math.Min(a.Y, b.Y) - 1e-10 &&
                mouse.Y <= Math.Max(a.Y, b.Y) + 1e-10)
                onEdge = true;
            if((a.Y > mouse.Y) != (b.Y > mouse.Y) &&
                mouse.X < (b.X - a.X) * (mouse.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
            a = b;
        }
        area = Math.Abs(area) * 0.5;
        return area > 1e-12 && (inside || onEdge);
    }

    public static long? Pick(long? ringId, IReadOnlyList<Target> targets, Point mouse, List<ClipPoint> workA, List<ClipPoint> workB) {
        if(ringId.HasValue)
            return ringId;
        long? selected = null;
        double smallest = double.PositiveInfinity;
        for(int i = 0; i < targets.Count; i++) {
            var target = targets[i];
            if(Contains(target.Vertices, mouse, workA, workB, out double area) &&
                (area < smallest || area == smallest && (!selected.HasValue || target.Id < selected.Value))) {
                selected = target.Id;
                smallest = area;
            }
        }
        return selected;
    }
}
