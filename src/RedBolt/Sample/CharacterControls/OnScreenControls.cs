// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterControls;

using System;
using System.Collections.Generic;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;

[Flags]
internal enum OnScreenControl
{
    None = 0,
    Jump = 1 << 0,
    Crouch = 1 << 1,
    View = 1 << 2,
    Debug = 1 << 3,
    Brake = 1 << 4
}

internal sealed class OnScreenControls : RedObject
{
    private sealed class Button
    {
        public Button(OnScreenControl control, string elementName, string label)
        {
            Control = control;
            ElementName = elementName;
            Label = Encoding.UTF8.GetBytes(label);
        }

        public readonly OnScreenControl Control;
        public readonly string ElementName;
        public readonly byte[] Label;
        public bool Down;
        public bool Visible;
        public Vector2f Min;
        public Vector2f Max;
    }

    public OnScreenControls(Input input, params (OnScreenControl Control, string ElementName, string Label)[] buttons)
    {
        _input = input;
        _buttons = new Button[buttons.Length];
        for (var i = 0; i < buttons.Length; ++i)
        {
            var button = buttons[i];
            _buttons[i] = new Button(button.Control, button.ElementName, button.Label);
        }
        SubscribeToEvent<InputEvents.SDLRawInputEvent>(input, HandleRawInput);
    }

    public bool IsVisible => _input.ActiveInputDevice == InputDeviceType.Touch;

    public OnScreenControl Draw(OnScreenControl visibleControls, bool showJoystick)
    {
        const int columns = 2;
        var visibleButtonCount = 0;
        for (var i = 0; i < _buttons.Length; ++i)
        {
            _buttons[i].Visible = (visibleControls & _buttons[i].Control) != 0;
            if (_buttons[i].Visible)
                ++visibleButtonCount;
            else
                SetButtonState(_buttons[i], false);
        }

        var rows = (visibleButtonCount + columns - 1) / columns;
        var displaySize = ImGui.GetIO().DisplaySize;
        var buttonSize = displaySize.Y < 500.0f ? CompactButtonSize : ButtonSize;
        var windowSize = new Vector2f(
            columns * buttonSize.X + WindowPadding * 2.0f + ButtonSpacing,
            rows * buttonSize.Y + WindowPadding * 2.0f + Math.Max(0, rows - 1) * ButtonSpacing);
        UpdateButtonRects(displaySize - windowSize - ScreenMargin, buttonSize);

        if (!IsVisible)
        {
            ReleaseButtons();
            _movementTouchId = -1;
            return OnScreenControl.None;
        }

        if (showJoystick)
            DrawJoystick();

        if (visibleButtonCount == 0)
            return OnScreenControl.None;

        ImGui.SetNextWindowPos(displaySize - windowSize - ScreenMargin, ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.ImGuiCondAlways);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(WindowPadding, WindowPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarItemSpacing, new Vector2f(ButtonSpacing, ButtonSpacing));
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 18.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButton, ButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButtonHovered, ButtonHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColButtonActive, ButtonActiveColor);

        var pressed = OnScreenControl.None;
        var open = true;
        if (ImGui.Begin("##On-screen controls"u8, ref open,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
                ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
                ImGuiWindowFlags.ImGuiWindowFlagsNoBackground))
        {
            var visibleIndex = 0;
            for (var i = 0; i < _buttons.Length; ++i)
            {
                var button = _buttons[i];
                if ((visibleControls & button.Control) == 0)
                    continue;
                if (visibleIndex % columns != 0)
                    ImGui.SameLine();

                ImGui.Button(button.Label, buttonSize);
                button.Visible = true;
                button.Min = ImGui.GetItemRectMin();
                button.Max = ImGui.GetItemRectMax();
                var down = IsTouched(button);
                if (down && !button.Down)
                    pressed |= button.Control;
                SetButtonState(button, down);
                ++visibleIndex;
            }
        }
        ImGui.End();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
        return pressed;
    }

    private void DrawJoystick()
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        var radius = MathDefs.Clamp(MathF.Min(displaySize.X, displaySize.Y) * 0.105f, 48.0f, 78.0f);
        var center = new Vector2f(radius + ScreenMargin.X, displaySize.Y - radius - ScreenMargin.Y);
        Vector2f? touchPosition = null;

        if (_movementTouchId >= 0)
        {
            if (_touches.TryGetValue(_movementTouchId, out var touch))
                touchPosition = ToLogical(touch, displaySize);
            else
                _movementTouchId = -1;
        }
        if (_movementTouchId < 0)
        {
            foreach (var touch in _touches)
            {
                if (touch.Value.X >= 0.5f)
                    continue;
                _movementTouchId = touch.Key;
                _movementTouchOrigin = touch.Value;
                touchPosition = ToLogical(touch.Value, displaySize);
                break;
            }
        }

        var marker = center;
        var active = touchPosition.HasValue;
        if (active)
        {
            center = ToLogical(_movementTouchOrigin, displaySize);
            var delta = touchPosition!.Value - center;
            if (delta.Length > radius)
                delta *= radius / delta.Length;
            marker = center + delta;
        }

        using var drawList = ImGui.GetForegroundDrawList();
        drawList.AddCircleFilled(center, radius + 8.0f,
            ImGui.GetColorU32(active ? JoystickGlowActive : JoystickGlow));
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(JoystickBackground));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(JoystickBorder), 0, 2.0f);
        drawList.AddCircle(center, radius * 0.5f, ImGui.GetColorU32(JoystickGuide));
        drawList.AddLine(center - new Vector2f(radius * 0.72f, 0.0f), center + new Vector2f(radius * 0.72f, 0.0f),
            ImGui.GetColorU32(JoystickGuide));
        drawList.AddLine(center - new Vector2f(0.0f, radius * 0.72f), center + new Vector2f(0.0f, radius * 0.72f),
            ImGui.GetColorU32(JoystickGuide));
        drawList.AddCircleFilled(marker, radius * 0.28f, ImGui.GetColorU32(JoystickKnobGlow));
        drawList.AddCircleFilled(marker, radius * 0.18f, ImGui.GetColorU32(JoystickKnob));
        drawList.AddCircleFilled(marker, radius * 0.055f, ImGui.GetColorU32(JoystickKnobHighlight));
    }

    private bool IsTouched(Button button)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        foreach (var touch in _buttonTouches)
        {
            if (touch.Value == button && _touches.TryGetValue(touch.Key, out var position)
                && Contains(button, ToLogical(position, displaySize)))
                return true;
        }
        return false;
    }

    private void HandleRawInput(ref InputEvents.SDLRawInputEvent args)
    {
        var @event = SdlEvent.CreateManagedWrapper(args.SDLEvent, ownsHandle: false);
        var eventType = (SdlEventType)@event.Type;
        if (eventType is not (SdlEventType.SdlFingerdown or SdlEventType.SdlFingermotion or SdlEventType.SdlFingerup))
            return;

        var touchId = @event.TfingerFingerId.GetHashCode();
        var captured = _buttonTouches.ContainsKey(touchId);
        if (eventType != SdlEventType.SdlFingerup)
            _touches[touchId] = new Vector2f(@event.TfingerX, @event.TfingerY);

        if (captured)
        {
            args.Consumed = true;
        }
        else if (eventType == SdlEventType.SdlFingerdown && IsVisible)
        {
            var displaySize = ImGui.GetIO().DisplaySize;
            var position = new Vector2f(@event.TfingerX * displaySize.X, @event.TfingerY * displaySize.Y);
            for (var i = 0; i < _buttons.Length; ++i)
            {
                if (_buttons[i].Visible && Contains(_buttons[i], position))
                {
                    captured = true;
                    _buttonTouches.Add(touchId, _buttons[i]);
                    break;
                }
            }
            args.Consumed = captured;
        }

        if (eventType == SdlEventType.SdlFingerup)
        {
            _touches.Remove(touchId);
            _buttonTouches.Remove(touchId);
        }
    }

    private void ReleaseButtons()
    {
        for (var i = 0; i < _buttons.Length; ++i)
            SetButtonState(_buttons[i], false);
    }

    private void UpdateButtonRects(Vector2f windowPosition, Vector2f buttonSize)
    {
        var visibleIndex = 0;
        for (var i = 0; i < _buttons.Length; ++i)
        {
            var button = _buttons[i];
            if (!button.Visible)
                continue;
            var column = visibleIndex % 2;
            var row = visibleIndex / 2;
            button.Min = windowPosition + new Vector2f(
                WindowPadding + column * (buttonSize.X + ButtonSpacing),
                WindowPadding + row * (buttonSize.Y + ButtonSpacing));
            button.Max = button.Min + buttonSize;
            ++visibleIndex;
        }
    }

    private void SetButtonState(Button button, bool down)
    {
        if (button.Down == down)
            return;

        button.Down = down;
        _input.SetScreenButton(button.ElementName, down);
    }

    private static bool Contains(Button button, Vector2f position) =>
        position.X >= button.Min.X && position.X <= button.Max.X
        && position.Y >= button.Min.Y && position.Y <= button.Max.Y;

    private static Vector2f ToLogical(Vector2f normalizedPosition, Vector2f displaySize) =>
        new(normalizedPosition.X * displaySize.X, normalizedPosition.Y * displaySize.Y);

    private static readonly Vector2f ButtonSize = new(92.0f, 60.0f);
    private static readonly Vector2f CompactButtonSize = new(78.0f, 50.0f);
    private static readonly Vector2f ScreenMargin = new(14.0f, 14.0f);
    private const float WindowPadding = 7.0f;
    private const float ButtonSpacing = 7.0f;
    private static readonly Vector4f ButtonColor = new(0.11f, 0.16f, 0.26f, 0.82f);
    private static readonly Vector4f ButtonHoveredColor = new(0.16f, 0.38f, 0.48f, 0.92f);
    private static readonly Vector4f ButtonActiveColor = new(0.15f, 0.7f, 0.78f, 0.96f);
    private static readonly Vector4f JoystickGlow = new(0.08f, 0.12f, 0.24f, 0.42f);
    private static readonly Vector4f JoystickGlowActive = new(0.08f, 0.75f, 0.84f, 0.22f);
    private static readonly Vector4f JoystickBackground = new(0.035f, 0.055f, 0.105f, 0.72f);
    private static readonly Vector4f JoystickBorder = new(0.35f, 0.5f, 0.68f, 0.72f);
    private static readonly Vector4f JoystickGuide = new(0.28f, 0.38f, 0.58f, 0.45f);
    private static readonly Vector4f JoystickKnobGlow = new(0.1f, 0.95f, 1.0f, 0.2f);
    private static readonly Vector4f JoystickKnob = new(0.18f, 0.92f, 1.0f, 0.92f);
    private static readonly Vector4f JoystickKnobHighlight = new(0.9f, 1.0f, 1.0f, 1.0f);
    private readonly Input _input;
    private readonly Button[] _buttons;
    private readonly Dictionary<int, Vector2f> _touches = [];
    private readonly Dictionary<int, Button> _buttonTouches = [];
    private int _movementTouchId = -1;
    private Vector2f _movementTouchOrigin;
}
