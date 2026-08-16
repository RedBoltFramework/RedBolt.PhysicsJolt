// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsJolt;

using RedBolt.Math;

/// <summary>Collision shape type.</summary>
public enum ShapeType
{
    Box = 0,
    Sphere,
    StaticPlane,
    Cylinder,
    Capsule,
    Cone,
    TriangleMesh,
    ConvexHull,
    Terrain,
    GImpactMesh
}

/// <summary>Rigid body collision event signaling mode.</summary>
public enum CollisionEventMode
{
    Never = 0,
    Active,
    Always
}

/// <summary>Physics raycast result.</summary>
public struct PhysicsRaycastResult : IEquatable<PhysicsRaycastResult>
{
    /// <summary>Test for equality.</summary>
    public static bool operator ==(PhysicsRaycastResult left, PhysicsRaycastResult right) => !(left != right);
    /// <summary>Test for inequality.</summary>
    public static bool operator !=(PhysicsRaycastResult left, PhysicsRaycastResult right) =>
        left.Position != right.Position || left.Normal != right.Normal || left.Distance != right.Distance ||
        !ReferenceEquals(left.Body, right.Body);

    public readonly bool Equals(PhysicsRaycastResult other) => this == other;
    public override readonly bool Equals(object? obj) => obj is PhysicsRaycastResult other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Position, Normal, Distance, Body);

    /// <summary>Hit rigid body.</summary>
    public RigidBody? Body;
    /// <summary>Hit position in world space.</summary>
    public Vector3f Position;
    /// <summary>Hit normal in world space.</summary>
    public Vector3f Normal;
    /// <summary>Distance from the ray origin.</summary>
    public float Distance;
    /// <summary>Normalized hit distance in the requested ray interval.</summary>
    public float HitFraction;
}

/// <summary>Delayed world transform assignment for parented rigid bodies.</summary>
public struct DelayedWorldTransform
{
    /// <summary>Rigid body.</summary>
    public RigidBody? RigidBody;
    /// <summary>Parent rigid body.</summary>
    public RigidBody? ParentRigidBody;
    /// <summary>New world position.</summary>
    public Vector3f WorldPosition;
    /// <summary>New world rotation.</summary>
    public Quaternion WorldRotation;
}

/// <summary>Physics contact point reported as part of a collision manifold.</summary>
public struct PhysicsContact
{
    /// <summary>Contact position in world space.</summary>
    public Vector3f Position;
    /// <summary>Contact normal in world space, pointing from body A toward body B.</summary>
    public Vector3f Normal;
    /// <summary>Signed contact distance. Negative values indicate penetration.</summary>
    public float Distance;
    /// <summary>Applied impulse. Jolt does not expose the solved impulse in contact callbacks.</summary>
    public float Impulse;
}
