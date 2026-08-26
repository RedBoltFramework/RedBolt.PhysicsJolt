// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CharacterDemo;

using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

/// <summary>Character component, responsible for physical movement according to controls, as well as animation.</summary>
[Reflected]
public partial class Character : MoveAndOrbitComponent
{
    private const float MoveForce = 0.8f;
    private const float InairMoveForce = 0.02f;
    private const float BrakeForce = 0.2f;
    private const float JumpForce = 7.0f;
    private const float InairThresholdTime = 0.1f;

    /// <summary>Construct.</summary>
    public Character()
    {
        // Only the physics update event is needed: unsubscribe from the rest for optimization
        SetUpdateEventMask(UpdateEvent.UseFixedUpdate);
    }

    [Serialized("Controls Yaw")]
    private float ControlsYaw
    {
        get => GetYaw();
        set => SetYaw(value);
    }

    [Serialized("Controls Pitch")]
    private float ControlsPitch
    {
        get => GetPitch();
        set => SetPitch(value);
    }

    /// <summary>Handle startup. Called by LogicComponent base class.</summary>
    public override void Start()
    {
        // Component has been inserted into its scene node. Subscribe to events now
        SubscribeToEvent<PhysicsEvents.NodeCollisionEvent>(Node, HandleNodeCollision);
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(Scene!.GetComponent<PhysicsWorld>()!, HandlePhysicsPreStep);
    }

    /// <summary>Handle physics world update. Called by LogicComponent base class.</summary>
    public override void FixedUpdate(float timeStep)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var runAnimation = cache.Get<Animation>("ThirdParty/Quaternius/UniversalCharacter.glb.d/Animations/Jog_Fwd_Loop.ani");
        var idleAnimation = cache.Get<Animation>("ThirdParty/Quaternius/UniversalCharacter.glb.d/Animations/Idle_Loop.ani");
        var jumpAnimation = cache.Get<Animation>("ThirdParty/Quaternius/UniversalCharacter.glb.d/Animations/Jump_Loop.ani");

        /// \todo Could cache the components for faster access instead of finding them each frame
        var body = GetComponent<RigidBody>()!;
        var animCtrl = Node.FindComponent<AnimationController>()!;

        // Update the in air timer. Reset if grounded
        if (!_onGround)
            _inAirTimer += timeStep;
        else
            _inAirTimer = 0.0f;
        // When character has been in air less than 1/10 second, it's still interpreted as being on ground
        var softGrounded = _inAirTimer < InairThresholdTime;

        // Update movement & animation
        var rot = Node.Rotation;
        var moveDir = GetVelocity();
        var velocity = body.LinearVelocity;
        // Velocity on the XZ plane
        var planeVelocity = new Vector3f(velocity.X, 0.0f, velocity.Z);

        // Normalize move vector so that diagonal strafing is not faster
        if (moveDir.LengthSquared > 0.0f)
            moveDir.Normalize();

        // If in air, allow control, but slower than when on ground
        body.ApplyImpulse(rot * moveDir * (softGrounded ? MoveForce : InairMoveForce));

        if (softGrounded)
        {
            // When on ground, apply a braking force to limit maximum ground velocity
            var brakeForce = -planeVelocity * BrakeForce;
            body.ApplyImpulse(brakeForce);

            // Jump. Must release jump control between jumps
            if (_inputMap!.Evaluate("Jump") != 0.0f)
            {
                if (_okToJump)
                {
                    body.ApplyImpulse(Vector3f.Up * JumpForce);
                    _okToJump = false;
                    _onGround = false;
                    animCtrl.PlayNewExclusive(new AnimationParameters(jumpAnimation).KeepOnCompletion(), 0.2f);
                }
            }
            else
                _okToJump = true;
        }

        if (!_onGround)
        {
            animCtrl.PlayExistingExclusive(new AnimationParameters(jumpAnimation).KeepOnCompletion(), 0.2f);
        }
        else
        {
            // Play walk animation if moving on ground, otherwise fade it out
            if (softGrounded && !moveDir.ApproximatelyEquivalent(Vector3f.Zero))
                animCtrl.PlayExistingExclusive(new AnimationParameters(runAnimation).Looped(), 0.2f);
            else
                animCtrl.PlayExistingExclusive(new AnimationParameters(idleAnimation).Looped(), 0.2f);

            // Set walk animation speed proportional to velocity
            animCtrl.SetSpeed("ThirdParty/Quaternius/UniversalCharacter.glb.d/Animations/Jog_Fwd_Loop.ani",
                planeVelocity.Length * 0.3f);
        }

        // Reset grounded flag for next frame
        _onGround = false;
    }

    /// <summary>Set input map.</summary>
    public void SetInputMap(InputMap? inputMap)
    {
        _inputMap = inputMap;
    }

    /// <summary>Return input map.</summary>
    public InputMap? GetInputMap() => _inputMap;

    /// <summary>Set input map attribute.</summary>
    public void SetInputMapAttr(ResourceRef value)
    {
        SetInputMap(InputMap.Load(value.Name));
    }

    /// <summary>Return input map attribute.</summary>
    [Serialized("Input Map", DefaultSource = nameof(GetInputMapAttributeDefault), Setter = nameof(SetInputMapAttr))]
    public ResourceRef GetInputMapAttr()
    {
        return ResourceType.GetResourceRef(_inputMap, ObjectReflection<InputMap>.TypeId);
    }

    /// <summary>Handle physics collision event.</summary>
    private void HandleNodeCollision(ref PhysicsEvents.NodeCollisionEvent eventData)
    {
        // Check collision contacts and see if character is standing on ground (look for a contact that has near vertical normal)
        foreach (var contact in eventData.Contacts)
        {
            // If contact is below node center and pointing up, assume it's a ground contact
            if (contact.Position.Y < Node.Position.Y + 1.0f)
            {
                var level = contact.Normal.Y;
                if (level > 0.75f)
                    _onGround = true;
            }
        }
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedUpdate(eventData.TimeStep);
    }

    /// <summary>Grounded flag for movement.</summary>
    [Serialized("On Ground")]
    private bool _onGround;
    /// <summary>Jump flag.</summary>
    [Serialized("OK To Jump")]
    private bool _okToJump = true;
    /// <summary>In air timer. Due to possible physics inaccuracy, character can be off ground for max. 1/10 second and still be allowed to move.</summary>
    [Serialized("In Air Timer")]
    private float _inAirTimer;

    /// <summary>Input map.</summary>
    private InputMap? _inputMap;

    private static object GetInputMapAttributeDefault() => new ResourceRef(ObjectReflection<InputMap>.TypeId);
}
