# PhysicsJolt porting checklist

This inventory moved from the base RedBolt repository with the optional 3D physics module.

## Module

- [x] Generated SharpCpp Jolt binding target and `RedBolt.Native.Jolt` wrapper
- [x] Physics definitions, events, registration, math/ABI projections, and deterministic Jolt lifetime
- [x] `PhysicsWorld` fixed-step simulation, interpolation, contacts, collision filtering, debug drawing, and complete ray/sweep/overlap query surface
- [x] `CollisionGeometryDataCache` and model, convex, heightfield, plane, primitive, and dynamic-concave shape construction
- [x] `RigidBody` static, dynamic, kinematic, trigger, transforms, velocities, forces, impulses, damping, CCD, mass batching, and collision layers
- [x] Point, hinge, slider, and swing-twist constraints with collision suppression
- [x] Kinematic character controller, trigger animator, raycast vehicle, and wheel components
- [x] Backend-neutral `IPhysicsWorld3D` integration without a base-engine dependency on Jolt
- [x] Optional `RedBolt.PhysicsJolt.Network` prediction/reconciliation controller

## Samples and distribution

- [x] Physics, RaycastVehicle, CharacterDemo, KinematicCharacter, SceneReplication, and AdvancedNetworking desktop samples
- [x] Aggregate WebAssembly sample picker
- [x] Aggregate Android x64/arm64 sample picker
- [x] Linux x64 and Windows x64 native wrappers
- [x] Managed, runtime-specific, and networking-integration NuGet package definitions
- [x] Tests remain in `RedBolt.Tests`
