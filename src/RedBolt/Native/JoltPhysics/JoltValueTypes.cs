// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Bindings.Jolt;

using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct JoltRayCastResult
{
    public uint BodyId;
    public float Fraction;
    public uint SubShapeId;
}

/// <summary>Projection of JPH::UVec4: four unsigned components, passed by value.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct JoltUInt4(uint x, uint y, uint z, uint w) : IEquatable<JoltUInt4>
{
    public uint X = x;
    public uint Y = y;
    public uint Z = z;
    public uint W = w;

    public readonly bool Equals(JoltUInt4 other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override readonly bool Equals(object? obj) => obj is JoltUInt4 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public static bool operator ==(JoltUInt4 left, JoltUInt4 right) => left.Equals(right);

    public static bool operator !=(JoltUInt4 left, JoltUInt4 right) => !left.Equals(right);

    public override readonly string ToString() => $"({X}, {Y}, {Z}, {W})";
}

/// <summary>
/// Projection of JPH::DMat44: a 3x3 float rotation/scale part (three column vectors, W unused)
/// plus a double-precision translation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct JoltDMatrix4(
    RedBolt.Math.Vector4f column0,
    RedBolt.Math.Vector4f column1,
    RedBolt.Math.Vector4f column2,
    RedBolt.Math.BaseRealVector3<double> translation) : IEquatable<JoltDMatrix4>
{
    public RedBolt.Math.Vector4f Column0 = column0;
    public RedBolt.Math.Vector4f Column1 = column1;
    public RedBolt.Math.Vector4f Column2 = column2;
    public RedBolt.Math.BaseRealVector3<double> Translation = translation;

    public static JoltDMatrix4 Identity => new(
        new RedBolt.Math.Vector4f(1.0f, 0.0f, 0.0f, 0.0f),
        new RedBolt.Math.Vector4f(0.0f, 1.0f, 0.0f, 0.0f),
        new RedBolt.Math.Vector4f(0.0f, 0.0f, 1.0f, 0.0f),
        new RedBolt.Math.BaseRealVector3<double>());

    public readonly bool Equals(JoltDMatrix4 other) =>
        Column0 == other.Column0 && Column1 == other.Column1 && Column2 == other.Column2 &&
        Translation == other.Translation;

    public override readonly bool Equals(object? obj) => obj is JoltDMatrix4 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Column0, Column1, Column2, Translation);

    public static bool operator ==(JoltDMatrix4 left, JoltDMatrix4 right) => left.Equals(right);

    public static bool operator !=(JoltDMatrix4 left, JoltDMatrix4 right) => !left.Equals(right);
}
