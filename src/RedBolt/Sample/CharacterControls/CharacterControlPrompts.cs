// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterControls;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using Widgets = RedBolt.SystemUI.Widgets;

internal sealed class CharacterControlPrompts
{
    public CharacterControlPrompts(ResourceCache resources, Input input)
    {
        _textures =
        [
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_f.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_shift.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_m.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_escape.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/generic_joystick.png"),
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/touch_swipe_move.png")
        ];
        _onScreenControls = new OnScreenControls(input,
            (OnScreenControl.Jump, "Button1", "JUMP"),
            (OnScreenControl.Crouch, "Button2", "CROUCH"),
            (OnScreenControl.View, "View", "VIEW"),
            (OnScreenControl.Debug, "Debug", "DEBUG"));
    }

    public ControlActions Draw(bool showCrouch, bool showDebug)
    {
        var visibleControls = OnScreenControl.Jump | OnScreenControl.View;
        if (showCrouch)
            visibleControls |= OnScreenControl.Crouch;
        if (showDebug)
            visibleControls |= OnScreenControl.Debug;

        if (_onScreenControls.IsVisible)
        {
            DrawPrompt(_textures[10], "Drag left side to move"u8);
            DrawPrompt(_textures[11], "Drag right side to rotate camera"u8);
            var pressed = _onScreenControls.Draw(visibleControls, true);
            return new ControlActions(
                (pressed & OnScreenControl.View) != 0,
                (pressed & OnScreenControl.Debug) != 0);
        }

        _onScreenControls.Draw(visibleControls, true);

        for (var i = 0; i < 3; ++i)
        {
            ImGui.Image(_textures[i], IconSize);
            ImGui.SameLine();
        }
        DrawPrompt(_textures[3], "Move"u8);
        DrawPrompt(_textures[4], "Hold right mouse to rotate camera"u8);
        DrawPrompt(_textures[5], "Jump"u8);
        if (showCrouch)
            DrawPrompt(_textures[7], "Crouch"u8);
        DrawPrompt(_textures[6], "Toggle 1st/3rd person"u8);
        if (showDebug)
            DrawPrompt(_textures[8], "Toggle physics debug geometry"u8);
        DrawPrompt(_textures[9], "Exit"u8);
        return default;
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, IconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static readonly Vector2f IconSize = new(24.0f, 24.0f);
    private readonly Texture2D[] _textures;
    private readonly OnScreenControls _onScreenControls;
}

internal readonly record struct ControlActions(bool ViewPressed, bool DebugPressed);
