// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsJolt;

using System;

using RedBolt.Scene;

public static class PhysicsEvents
{
    public struct PhysicsPreUpdateEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
    }

    public struct PhysicsPostUpdateEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
        public float Overtime;
    }

    public struct PhysicsPreStepEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
        public long? NetworkFrame;
    }

    public struct PhysicsPostStepEvent
    {
        public PhysicsWorld World;
        public float TimeStep;
    }

    public struct PhysicsCollisionStartEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
        /// <summary>Borrowed contact view, valid until the next physics simulation step.</summary>
        public ArraySegment<PhysicsContact> Contacts;
    }

    public struct PhysicsCollisionEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
        /// <summary>Borrowed contact view, valid until the next physics simulation step.</summary>
        public ArraySegment<PhysicsContact> Contacts;
    }

    public struct PhysicsCollisionEndEvent
    {
        public PhysicsWorld World;
        public Node NodeA;
        public Node NodeB;
        public RigidBody? BodyA;
        public RigidBody? BodyB;
        public bool Trigger;
    }

    public struct NodeCollisionStartEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
        /// <summary>Borrowed contact view, valid until the next physics simulation step.</summary>
        public ArraySegment<PhysicsContact> Contacts;
    }

    public struct NodeCollisionEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
        /// <summary>Borrowed contact view, valid until the next physics simulation step.</summary>
        public ArraySegment<PhysicsContact> Contacts;
    }

    public struct NodeCollisionEndEvent
    {
        public RigidBody? Body;
        public Node OtherNode;
        public RigidBody? OtherBody;
        public bool Trigger;
    }
}
