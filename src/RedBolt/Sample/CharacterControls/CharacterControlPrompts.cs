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
    public CharacterControlPrompts(ResourceCache resources)
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
            resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_escape.png")
        ];
    }

    public void Draw(bool showCrouch, bool showDebug, bool isMobile)
    {
        if (isMobile)
        {
            ImGui.TextUnformatted("Drag left half to move"u8);
            ImGui.TextUnformatted("Drag right half to look"u8);
            ImGui.TextUnformatted("Use the action buttons below"u8);
            return;
        }

        for (var i = 0; i < 3; ++i)
        {
            ImGui.Image(_textures[i], IconSize);
            ImGui.SameLine();
        }
        DrawPrompt(_textures[3], "Move"u8);
        DrawPrompt(_textures[4], "Hold / drag touch to rotate camera"u8);
        DrawPrompt(_textures[5], "Jump"u8);
        if (showCrouch)
            DrawPrompt(_textures[7], "Crouch"u8);
        DrawPrompt(_textures[6], "Toggle 1st/3rd person"u8);
        if (showDebug)
            DrawPrompt(_textures[8], "Toggle physics debug geometry"u8);
        DrawPrompt(_textures[9], "Exit"u8);
    }

    public MobileControlActions DrawMobileControls(Input input, bool showCrouch, bool showDebug)
    {
        var displaySize = ImGui.GetIO().DisplaySize;
        const int columns = 2;
        var rows = showCrouch || showDebug ? 2 : 1;
        var windowSize = new Vector2f(columns * MobileButtonSize.X + 28.0f, rows * MobileButtonSize.Y + 28.0f);
        ImGui.SetNextWindowPos(displaySize - windowSize - new Vector2f(8.0f, 8.0f), ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.ImGuiCondAlways);

        var open = true;
        var viewPressed = false;
        var debugPressed = false;
        input.SetScreenButton("Button1", false);
        input.SetScreenButton("Button2", false);
        if (ImGui.Begin("##Mobile character controls"u8, ref open,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
                ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.Button("JUMP"u8, MobileButtonSize);
            input.SetScreenButton("Button1", ImGui.IsItemActive() || IsTouched(input));
            ImGui.SameLine();
            var viewClicked = ImGui.Button("VIEW"u8, MobileButtonSize);
            var viewTouchDown = IsTouched(input);
            viewPressed = (viewTouchDown || viewClicked) && !_viewTouchDown;
            _viewTouchDown = viewTouchDown;

            if (showCrouch)
            {
                ImGui.Button("CROUCH"u8, MobileButtonSize);
                input.SetScreenButton("Button2", ImGui.IsItemActive() || IsTouched(input));
            }

            if (showDebug)
            {
                if (showCrouch)
                    ImGui.SameLine();
                var debugClicked = ImGui.Button("DEBUG"u8, MobileButtonSize);
                var debugTouchDown = IsTouched(input);
                debugPressed = (debugTouchDown || debugClicked) && !_debugTouchDown;
                _debugTouchDown = debugTouchDown;
            }
            else
                _debugTouchDown = false;
        }
        ImGui.End();
        return new MobileControlActions(viewPressed, debugPressed);
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, IconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static bool IsTouched(Input input)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var framebufferScale = ImGui.GetIO().DisplayFramebufferScale;
        var scaleX = framebufferScale.X > 0.0f ? framebufferScale.X : 1.0f;
        var scaleY = framebufferScale.Y > 0.0f ? framebufferScale.Y : 1.0f;
        for (var i = 0u; i < input.GetNumTouches(); ++i)
        {
            var touch = input.GetTouch(i);
            if (!touch.HasValue)
                continue;
            var position = new Vector2f(touch.Value.Position.X / scaleX, touch.Value.Position.Y / scaleY);
            if (position.X >= min.X && position.X <= max.X && position.Y >= min.Y && position.Y <= max.Y)
                return true;
        }
        return false;
    }

    private static readonly Vector2f IconSize = new(24.0f, 24.0f);
    private static readonly Vector2f MobileButtonSize = new(76.0f, 52.0f);
    private readonly Texture2D[] _textures;
    private bool _viewTouchDown;
    private bool _debugTouchDown;
}

internal readonly record struct MobileControlActions(bool ViewPressed, bool DebugPressed);
