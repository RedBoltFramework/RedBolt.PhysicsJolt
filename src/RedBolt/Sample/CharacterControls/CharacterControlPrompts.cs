// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterControls;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
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

    public void Draw(bool showCrouch, bool showDebug)
    {
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

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, IconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static readonly Vector2f IconSize = new(24.0f, 24.0f);
    private readonly Texture2D[] _textures;
}
