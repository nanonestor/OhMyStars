namespace OhMyStars;

internal sealed class ScreenLabelLayout {
    internal readonly record struct Box(float X, float Y, float Width, float Height);
    private const int CellSize = 64;
    private readonly Dictionary<(int X, int Y), List<Box>> _cells = [];
    private float _width, _height;

    internal void Begin(float width, float height) {
        _width = width;
        _height = height;
        foreach(var cell in _cells.Values)
            cell.Clear();
    }

    internal void Reserve(Box box) {
        if(!float.IsFinite(box.X) || !float.IsFinite(box.Y) ||
            !float.IsFinite(box.Width) || !float.IsFinite(box.Height) ||
            box.Width <= 0 || box.Height <= 0 || box.X >= _width || box.Y >= _height ||
            box.X + box.Width <= 0 || box.Y + box.Height <= 0)
            return;
        float right = Math.Min(_width, box.X + box.Width), bottom = Math.Min(_height, box.Y + box.Height);
        box = new(Math.Max(0, box.X), Math.Max(0, box.Y), right - Math.Max(0, box.X), bottom - Math.Max(0, box.Y));
        for(int y = (int)MathF.Floor(box.Y / CellSize); y <= (int)MathF.Floor((box.Y + box.Height) / CellSize); y++)
            for(int x = (int)MathF.Floor(box.X / CellSize); x <= (int)MathF.Floor((box.X + box.Width) / CellSize); x++) {
                if(!_cells.TryGetValue((x, y), out var cell))
                    _cells[(x, y)] = cell = [];
                cell.Add(box);
            }
    }

    internal bool TryPlace(Box preferred, out Box placed) {
        placed = default;
        if(!float.IsFinite(preferred.X) || !float.IsFinite(preferred.Y) ||
            !float.IsFinite(preferred.Width) || !float.IsFinite(preferred.Height) ||
            preferred.Width <= 0 || preferred.Height <= 0 ||
            preferred.Width > _width - 4 || preferred.Height > _height - 4)
            return false;
        for(int ring = 0; ring <= 4; ring++)
            for(int direction = 0; direction < (ring == 0 ? 1 : 8); direction++) {
                float angle = direction * MathF.PI / 4;
                float x = Math.Clamp(preferred.X + MathF.Cos(angle) * ring * 24, 2, _width - preferred.Width - 2);
                float y = Math.Clamp(preferred.Y + MathF.Sin(angle) * ring * (preferred.Height + 4), 2, _height - preferred.Height - 2);
                Box candidate = new(x, y, preferred.Width, preferred.Height);
                if(Overlaps(candidate))
                    continue;
                Reserve(candidate);
                placed = candidate;
                return true;
            }
        return false;
    }

    private bool Overlaps(Box box) {
        for(int y = (int)MathF.Floor((box.Y - 3) / CellSize); y <= (int)MathF.Floor((box.Y + box.Height + 3) / CellSize); y++)
            for(int x = (int)MathF.Floor((box.X - 3) / CellSize); x <= (int)MathF.Floor((box.X + box.Width + 3) / CellSize); x++)
                if(_cells.TryGetValue((x, y), out var cell))
                    foreach(var other in cell)
                        if(box.X < other.X + other.Width + 3 && box.X + box.Width + 3 > other.X &&
                            box.Y < other.Y + other.Height + 3 && box.Y + box.Height + 3 > other.Y)
                            return true;
        return false;
    }
}
