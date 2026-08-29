// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterControls;

using RedBolt.Graphics;
using RedBolt.Math;

internal static class RootMotionAnimation
{
    private const string RootLinearVelocityMetadata = "RootLinearVelocity";

    public static float GetSpeedScale(Animation animation, float planarSpeed)
    {
        var rootVelocity = animation.GetMetadata(RootLinearVelocityMetadata).GetVector3();
        var rootPlanarSpeed = new Vector2f(rootVelocity.X, rootVelocity.Z).Length;
        return rootPlanarSpeed > MathDefs.Epsilon ? planarSpeed / rootPlanarSpeed : 1.0f;
    }
}
