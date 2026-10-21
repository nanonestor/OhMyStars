using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using System;

namespace OhMyStars;

// Cursor star-picking mode. While active, every left click in the 3D view (not over an ImGui window) finds the
// catalog star drawn closest to the cursor (the catalog scan happens only once per click) and marks it with an
// indicator. In continuous mode the pick is reported to the owner's callback immediately, so the user can keep
// clicking until the right star is selected. In confirm mode clicks keep re-picking a pending star, and a small\n// window offers Confirm / Cancel before reporting it.
internal static unsafe class StarPicker {
    private const float PickRadiusPx = 20f;
    private const float ClickDragTolerancePx = 4f;

    private static Action<int>? _onPicked;
    private static Func<int, bool>? _canPick;
    private static Func<float2, long?>? _customPick;
    private static Action<long>? _onCustomPicked;

    public static bool Active { get; private set; }
    // Identifies which UI started the current picking session, so each UI only shows its own state.
    public static string Owner { get; private set; } = string.Empty;
    public static int CurrentHip { get; private set; }
    public static bool NoStarNearLastClick { get; private set; }
    // Confirm mode: a picked star waits here until the user answers the confirmation window.
    public static bool ConfirmMode { get; private set; }
    public static int PendingHip { get; private set; }
    private static string _prompt = string.Empty;

    public static bool IsActiveFor(string owner) => Active && Owner == owner;

    // The owning UI must call KeepAlive every frame its picker controls are visible. If a frame passes without
    // it (window closed or collapsed, tab or mode changed), picking is cancelled automatically.
    private static bool _keptAlive;

    public static void KeepAlive(string owner) {
        if(IsActiveFor(owner))
            _keptAlive = true;
    }

    public static void Start(string owner, Action<int> onPicked, Func<int, bool>? canPick = null) {
        Cancel();
        Owner = owner;
        _onPicked = onPicked;
        _canPick = canPick;
        CurrentHip = 0;
        PendingHip = 0;
        ConfirmMode = false;
        _prompt = string.Empty;
        NoStarNearLastClick = false;
        Active = true;
        _keptAlive = true;
    }

    public static void StartCustom(string owner, Func<float2, long?> pick, Action<long> onPicked) {
        Start(owner, _ => { });
        _customPick = pick;
        _onCustomPicked = onPicked;
    }

    // Starts continuous picking where the latest picked star must be confirmed (Confirm / Cancel) before the
    // callback runs. The callback may call StartConfirm again to chain another pick.
    public static void StartConfirm(string owner, string prompt, Action<int> onConfirmed) {
        Start(owner, onConfirmed);
        ConfirmMode = true;
        _prompt = prompt;
    }

    public static void Toggle(string owner, Action<int> onPicked) {
        if(IsActiveFor(owner))
            Cancel();
        else
            Start(owner, onPicked);
    }

    public static void Cancel() {
        Active = false;
        Owner = string.Empty;
        CurrentHip = 0;
        PendingHip = 0;
        ConfirmMode = false;
        _prompt = string.Empty;
        _onPicked = null;
        _canPick = null;
        _customPick = null;
        _onCustomPicked = null;
        NoStarNearLastClick = false;
    }

    public static void UpdateOverlay(ImDrawListPtr drawList, Camera camera, double3 center, double radius) {
        if(!Active)
            return;
        if(!_keptAlive) {
            Cancel();
            return;
        }
        _keptAlive = false;

        // In confirm mode, each click replaces the pending star until the user confirms it.
        if(ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !ImGui.GetIO().WantCaptureMouse &&
            !ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow)) {
            float2 drag = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, -1f);
            if(drag.X * drag.X + drag.Y * drag.Y <= ClickDragTolerancePx * ClickDragTolerancePx) {
                float2 mouse = ImGui.GetIO().MousePos;
                if(_customPick is not null) {
                    long? id = _customPick(mouse);
                    NoStarNearLastClick = !id.HasValue;
                    if(id.HasValue)
                        _onCustomPicked?.Invoke(id.Value);
                } else if(SkyCulturesRenderer.TryPickStar(camera, center, radius, mouse, PickRadiusPx, out int hip, _canPick)) {
                    CurrentHip = hip;
                    NoStarNearLastClick = false;
                    if(ConfirmMode)
                        PendingHip = hip;
                    else
                        _onPicked?.Invoke(hip);
                } else {
                    NoStarNearLastClick = true;
                }
            }
        }

        if(CurrentHip <= 0)
            return;

        if(!SkyCulturesRenderer.TryGetStarScreen(CurrentHip, camera, center, radius, GameStarSpace.GetObserverPc(camera), out float2 screen))
            return;

        // Indicator: a ring around the star plus an arrow coming in from the upper-left with the name.
        ImColor8 outline = new ImColor8(0, 0, 0, 220);
        ImColor8 color = new ImColor8(0, 255, 140, 255);
        ImDrawListExtensions.AddCircle(drawList, screen, 11f, outline, 0, 4f);
        ImDrawListExtensions.AddCircle(drawList, screen, 11f, color, 0, 2f);

        float2 tip = screen + new float2(-13f, -13f);
        float2 tail = screen + new float2(-60f, -60f);
        ImDrawListExtensions.AddLine(drawList, tail, tip, outline, 4f);
        ImDrawListExtensions.AddLine(drawList, tail, tip, color, 2f);
        ImDrawListExtensions.AddTriangleFilled(drawList, tip, tip + new float2(-10f, -2f), tip + new float2(-2f, -10f), color);

        string name = SkyCulturesRenderer.BackgroundName(CurrentHip);
        float textHeight = ImGui.CalcTextSize(name).Y;
        float2 labelPosition = tail + new float2(-4f, -textHeight - 10f);
        if(name.Length > 0)
            SkyLabelRenderer.Add(labelPosition, color, name, 3);
        _pendingScreen = screen;
    }

    private static float2 _pendingScreen;

    // Accepts the pending star in confirm mode: ends this pick and reports the star to the callback.
    public static void ConfirmPending() {
        if(!Active || !ConfirmMode || PendingHip <= 0)
            return;
        int hip = PendingHip;
        Action<int>? callback = _onPicked;
        Cancel();
        callback?.Invoke(hip);
    }

    // Confirmation window for confirm mode. Must be drawn outside the HUD window, which has NoInputs.
    public static void DrawWindows() {
        if(!Active || !ConfirmMode)
            return;

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
        if(PendingHip > 0)
            ImGui.SetNextWindowPos(_pendingScreen + new float2(24f, 16f), ImGuiCond.Always, (float2?)null);
        if(!ImGui.Begin("Oh My Stars - Star Picker", flags)) {
            ImGui.End();
            return;
        }

        if(PendingHip <= 0) {
            ImGui.Text(_prompt);
            ImGui.TextColored(in ConsoleStyle.TextMuted, NoStarNearLastClick ? "No star near the last click - try again." : "Left-click near a star in the sky.");
            if(ImGui.Button("Cancel"))
                Cancel();
        } else {
            ImGui.Text($"{_prompt}: {SkyCulturesRenderer.DescribeStar(PendingHip)}");
            ImGui.TextColored(in ConsoleStyle.TextMuted, NoStarNearLastClick ? "No star near the last click." : "Click again to pick a different star.");
            if(ImGui.Button("Confirm"))
                ConfirmPending();
            ImGui.SameLine();
            if(ImGui.Button("Cancel"))
                Cancel();
        }
        ImGui.End();
    }
}
