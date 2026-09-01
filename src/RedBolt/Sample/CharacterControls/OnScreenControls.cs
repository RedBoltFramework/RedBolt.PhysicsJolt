// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterControls;

using System;

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
    Brake = 1 << 4,
    Fire = 1 << 5
}

/// <summary>
/// Contextual touch controls shared by the Jolt samples. The input map is borrowed and remains owned by
/// the sample or scene component that created it.
/// </summary>
internal sealed class OnScreenControls : IDisposable
{
    private readonly record struct Button(OnScreenControl Control, string Name, string Action);

    public OnScreenControls(InputMap inputMap,
        params (OnScreenControl Control, string Action, string Label)[] buttons)
    {
        _inputMap = inputMap;
        _input = inputMap.GetSubsystem<Input>()!;
        _overlay = new InputOverlay(inputMap);
        _overlay.AddJoystick("Movement", "Forward", "Back", "Left", "Right");
        _overlay.AddButton("Exit", "Exit", null, "BACK", new Vector2f(0.0f, 0.0f),
            new Vector2f(12.0f, 12.0f), InputOverlayAlignment.TopLeft, new Vector2f(52.0f, 40.0f));

        _buttons = new Button[buttons.Length];
        for (var i = 0; i < buttons.Length; ++i)
        {
            var button = buttons[i];
            var name = $"Action{i}";
            _buttons[i] = new Button(button.Control, name, button.Action);
            var column = i % 2;
            var row = i / 2;
            _overlay.AddButton(name, button.Action, null, button.Label,
                new Vector2f(1.0f, 1.0f), new Vector2f(-12.0f - column * 84.0f, -12.0f - row * 84.0f),
                InputOverlayAlignment.BottomRight);
        }
    }

    public bool IsVisible => _input.ActiveInputDevice == InputDeviceType.Touch;

    public OnScreenControl Draw(OnScreenControl visibleControls, bool showJoystick)
    {
        _overlay.SetJoystickVisible("Movement", showJoystick);
        var pressed = OnScreenControl.None;
        foreach (var button in _buttons)
        {
            var visible = (visibleControls & button.Control) != 0;
            _overlay.SetButtonVisible(button.Name, visible);
            if (visible && _inputMap.EvaluatePressed(button.Action))
                pressed |= button.Control;
        }
        return pressed;
    }

    public void Dispose() => _overlay.Dispose();

    private readonly Input _input;
    private readonly InputMap _inputMap;
    private readonly InputOverlay _overlay;
    private readonly Button[] _buttons;
}

internal static class SampleInputMaps
{
    public static InputMap Create(bool movement = false)
    {
        var inputMap = new InputMap();
        inputMap.MapKeyboardKey("Exit", Scancode.Escape);
        inputMap.MapControllerButton("Exit", ControllerButton.Back);
        if (movement)
            MapMovement(inputMap);
        return inputMap;
    }

    public static void MapMovement(InputMap inputMap)
    {
        MapDirection(inputMap, "Forward", Scancode.W, Scancode.Up, ControllerButton.DPadUp,
            ControllerAxis.LeftY, -1.0f);
        MapDirection(inputMap, "Back", Scancode.S, Scancode.Down, ControllerButton.DPadDown,
            ControllerAxis.LeftY, 1.0f);
        MapDirection(inputMap, "Left", Scancode.A, Scancode.Left, ControllerButton.DPadLeft,
            ControllerAxis.LeftX, -1.0f);
        MapDirection(inputMap, "Right", Scancode.D, Scancode.Right, ControllerButton.DPadRight,
            ControllerAxis.LeftX, 1.0f);
    }

    public static void MapDPad(InputMap inputMap)
    {
        inputMap.MapControllerButton("Forward", ControllerButton.DPadUp);
        inputMap.MapControllerButton("Back", ControllerButton.DPadDown);
        inputMap.MapControllerButton("Left", ControllerButton.DPadLeft);
        inputMap.MapControllerButton("Right", ControllerButton.DPadRight);
    }

    public static void MapButton(InputMap inputMap, string action, Scancode scancode,
        ControllerButton controllerButton)
    {
        inputMap.MapKeyboardKey(action, scancode);
        inputMap.MapControllerButton(action, controllerButton);
    }

    private static void MapDirection(InputMap inputMap, string action, Scancode primary, Scancode alternate,
        ControllerButton dPad, ControllerAxis axis, float pressed)
    {
        inputMap.MapKeyboardKey(action, primary);
        inputMap.MapKeyboardKey(action, alternate);
        inputMap.MapControllerButton(action, dPad);
        inputMap.MapControllerAxis(action, axis, 0.0f, pressed);
    }
}
