// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.KinematicCharacter;

using System;

using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Sample.CharacterControls;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

//=============================================================================
//=============================================================================
public struct MovingData : IEquatable<MovingData>
{
    public MovingData()
    {
        Node = null;
        Transform = new Matrix3x4();
    }

    public static bool operator ==(MovingData lhs, MovingData rhs)
    {
        return lhs.Node is not null && ReferenceEquals(lhs.Node, rhs.Node);
    }

    public static bool operator !=(MovingData lhs, MovingData rhs) => !(lhs == rhs);

    public readonly bool Equals(MovingData rhs) => this == rhs;

    public override readonly bool Equals(object? obj) => obj is MovingData rhs && Equals(rhs);

    public override readonly int GetHashCode() => Node?.GetHashCode() ?? 0;

    public Node? Node;
    public Matrix3x4 Transform;
}

//=============================================================================
//=============================================================================
/// <summary>Character component, responsible for physical movement according to controls, as well as animation.</summary>
[Reflected]
public partial class KinematicCharacter : MoveAndOrbitComponent
{
    private const float MoveForce = 0.2f;
    private const float InairMoveForce = 0.2f;
    private const float InairThresholdTime = 0.1f;

    /// <summary>Construct.</summary>
    public KinematicCharacter()
    {
        _movingData[0] = new MovingData();
        _movingData[1] = new MovingData();

        // Only the physics update event is needed: unsubscribe from the rest for optimization
        SetUpdateEventMask(UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate);
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

    public override void DelayedStart()
    {
        var collisionShape = Node.FindComponent<CollisionShape>();
        if (collisionShape is not null)
            _collisionShape = new WeakReference<CollisionShape>(collisionShape);
        var animController = Node.FindComponent<AnimationController>()!;
        _animController = new WeakReference<AnimationController>(animController);
        var kinematicController = Node.FindComponent<KinematicCharacterController>()!;
        _kinematicController = new WeakReference<KinematicCharacterController>(kinematicController);
    }

    /// <summary>Handle startup. Called by LogicComponent base class.</summary>
    public override void Start()
    {
        // Component has been inserted into its scene node. Subscribe to events now
        SubscribeToEvent<PhysicsEvents.NodeCollisionEvent>(Node, HandleNodeCollision);
        var physicsWorld = Scene!.GetComponent<PhysicsWorld>()!;
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(physicsWorld, HandlePhysicsPreStep);
        SubscribeToEvent<PhysicsEvents.PhysicsPostStepEvent>(physicsWorld, HandlePhysicsPostStep);
    }

    /// <summary>Handle physics world update. Called by LogicComponent base class.</summary>
    public override void FixedUpdate(float timeStep)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var runAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Jog_Fwd_Loop.ani");
        var idleAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Idle_Loop.ani");
        var jumpAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Jump_Loop.ani");
        var crouchAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Crouch_Fwd_Loop.ani");
        var crouchIdleAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Crouch_Idle_Loop.ani");

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
        _kinematicController!.TryGetTarget(out var kinematicController);
        _onGround = kinematicController!.OnGround;

        var isCrouching = _inputMap!.Evaluate("Crouch") != 0.0f;
        if (isCrouching)
        {
            kinematicController.Height = 0.9f;
            kinematicController.Offset = new Vector3f(0.0f, 0.45f, 0.0f);
        }
        else
        {
            kinematicController.Height = 1.8f;
            kinematicController.Offset = new Vector3f(0.0f, 0.9f, 0.0f);
        }

        // Normalize move vector so that diagonal strafing is not faster
        if (moveDir.LengthSquared > 0.0f)
            moveDir.Normalize();

        // rotate movedir
        var velocity = rot * moveDir;
        if (_onGround)
        {
            _curMoveDir = velocity;
        }
        else
        {
            // In-air direction control is limited
            _curMoveDir = _curMoveDir.Lerp(velocity, 0.03f);
        }

        kinematicController.SetWalkIncrement(_curMoveDir * (softGrounded ? MoveForce : InairMoveForce));

        if (softGrounded)
        {
            if (_isJumping)
            {
                _isJumping = false;
            }
            _isJumping = false;
            // Jump. Must release jump control between jumps
            if (_inputMap.Evaluate("Jump") != 0.0f)
            {
                _isJumping = true;
                if (_okToJump)
                {
                    _okToJump = false;
                    _jumpStarted = true;
                    kinematicController.Jump();
                }
            }
            else
            {
                _okToJump = true;
            }
        }

        _animController!.TryGetTarget(out var animController);
        var controllerVelocity = kinematicController.LinearVelocity;
        var planarSpeed = new Vector2f(controllerVelocity.X, controllerVelocity.Z).Length;
        if (_onGround)
        {
            // Play walk animation if moving on ground, otherwise fade it out
            if (softGrounded && !moveDir.ApproximatelyEquivalent(Vector3f.Zero))
            {
                animController!.PlayExistingExclusive(
                    new AnimationParameters(isCrouching ? crouchAnimation : runAnimation).Looped(), 0.2f);
                var movementAnimation = isCrouching ? crouchAnimation : runAnimation;
                animController.SetSpeed(movementAnimation.Name,
                    RootMotionAnimation.GetSpeedScale(movementAnimation, planarSpeed));
            }
            else
            {
                animController!.PlayExistingExclusive(
                    new AnimationParameters(isCrouching ? crouchIdleAnimation : idleAnimation).Looped(), 0.2f);
            }
        }
        else if (_jumpStarted)
        {
            animController!.PlayNewExclusive(new AnimationParameters(jumpAnimation).KeepOnCompletion(), 0.2f);
            _jumpStarted = false;
        }
        else
        {
            const float MaxDistance = 50.0f;
            const float SegmentDistance = 10.01f;
            var result = default(PhysicsRaycastResult);
            Scene!.GetComponent<PhysicsWorld>()!.RaycastSingleSegmented(
                ref result, new Ray(Node.Position, Vector3f.Down), MaxDistance, SegmentDistance, 0xffff);
            if (result.Body is not null && result.Distance > 0.7f)
            {
                animController!.PlayExistingExclusive(new AnimationParameters(jumpAnimation).KeepOnCompletion(), 0.2f);
            }
        }
    }

    public override void FixedPostUpdate(float timeStep)
    {
        if (_movingData[0] == _movingData[1])
        {
            // TODO: Implement riding on platforms
            // Matrix3x4 delta = movingData_[0].transform_ * movingData_[1].transform_.Inverse();
            //
            // // add delta
            // Vector3 kPos;
            // Quaternion kRot;
            // kinematicController_->GetTransform(kPos, kRot);
            // Matrix3x4 matKC(kPos, kRot, Vector3::ONE);
            //
            // // update
            // matKC = delta * matKC;
            // kinematicController_->SetTransform(matKC.Translation(), matKC.Rotation());
            //
            // // update yaw control (directly rotates char)
            // controls_.yaw_ += delta.Rotation().YawAngle();
        }

        // shift and clear
        _movingData[1] = _movingData[0];
        _movingData[0].Node = null;
    }

    public void SetOnMovingPlatform(RigidBody? platformBody)
    {
        //onMovingPlatform_ = (platformBody != NULL);
        //platformBody_ = platformBody;
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

    private bool IsNodeMovingPlatform(Node? node)
    {
        // TODO: Implement riding on platforms
        return false;
        // if (node == 0)
        // {
        //     return false;
        // }
        //
        // const Variant& var = node->GetVar(StringHash("IsMovingPlatform"));
        // return (var != Variant::EMPTY && var.GetBool());
    }

    private void NodeOnMovingPlatform(Node? node)
    {
        if (!IsNodeMovingPlatform(node))
        {
            return;
        }

        _movingData[0].Node = node;
        _movingData[0].Transform = node!.GetWorldTransform();
    }

    /// <summary>Handle physics collision event.</summary>
    private void HandleNodeCollision(ref PhysicsEvents.NodeCollisionEvent eventData)
    {
        // Check collision contacts and see if character is standing on ground (look for a contact that has near vertical normal)

        // possible moving platform trigger volume
        if (eventData.OtherBody!.IsTrigger)
        {
            NodeOnMovingPlatform(eventData.OtherNode);
        }
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedUpdate(eventData.TimeStep);
    }

    private void HandlePhysicsPostStep(ref PhysicsEvents.PhysicsPostStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedPostUpdate(eventData.TimeStep);
    }

    /// <summary>Grounded flag for movement.</summary>
    [Serialized("On Ground")]
    protected bool _onGround;
    /// <summary>Jump flag.</summary>
    [Serialized("OK To Jump")]
    protected bool _okToJump = true;
    /// <summary>In air timer. Due to possible physics inaccuracy, character can be off ground for max. 1/10 second and still be allowed to move.</summary>
    [Serialized("In Air Timer")]
    protected float _inAirTimer;

    // extra vars
    protected Vector3f _curMoveDir = Vector3f.Zero;
    protected bool _isJumping;
    protected bool _jumpStarted;

    protected WeakReference<CollisionShape>? _collisionShape;
    protected WeakReference<AnimationController>? _animController;
    protected WeakReference<KinematicCharacterController>? _kinematicController;

    // moving platform data
    protected readonly MovingData[] _movingData = new MovingData[2];
    /// <summary>Input map.</summary>
    protected InputMap? _inputMap;

    private static object GetInputMapAttributeDefault() => new ResourceRef(ObjectReflection<InputMap>.TypeId);
}
