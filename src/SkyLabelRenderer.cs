using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace OhMyStars;

internal static class SkyLabelRenderer {
    private readonly record struct Label(float2 Position, float2 Size, ImColor8 Color, string Text, int Priority);
    private static readonly List<Label> Labels = [];
    private static readonly ScreenLabelLayout Layout = new();
    private static float2 _origin;

    internal static void Begin(float2 origin, float2 size) {
        _origin = origin;
        Labels.Clear();
        Layout.Begin(size.X, size.Y);
    }

    internal static void Reserve(float2 position, float2 size) =>
        Layout.Reserve(new(position.X - _origin.X, position.Y - _origin.Y, size.X, size.Y));

    internal static void Add(float2 position, ImColor8 color, string text, int priority = 0) =>
        Labels.Add(new(position, ImGui.CalcTextSize(text), color, text, priority));

    internal static void Draw(ImDrawListPtr drawList) {
        for(int priority = 3; priority >= 0; priority--)
            foreach(var label in Labels) {
                if(label.Priority != priority || !Layout.TryPlace(new(
                    label.Position.X - _origin.X, label.Position.Y - _origin.Y, label.Size.X, label.Size.Y), out var box))
                    continue;
                ImDrawListExtensions.AddText(drawList, _origin + new float2(box.X, box.Y), label.Color, label.Text);
            }
    }
}
