// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Bindings.Jolt;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct JoltRayCastResult
{
    public uint BodyId;
    public float Fraction;
    public uint SubShapeId;
}
