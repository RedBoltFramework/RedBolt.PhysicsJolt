# Allowed deviations

These deviations apply to the Jolt 3D physics module extracted from RedBolt:

- The implementation adapts rbfx's Bullet-backed 3D API to the official Jolt Physics repository pinned to commit `341ac92c7a8a63a4a37729ad6eca21f9a5a51e13`.
- Jolt bindings and native callback/query helpers are generated through SharpCpp. There is no hand-written P/Invoke layer; Jolt IDs replace native Bullet pointers exposed by rbfx.
- Jolt shape casts are translational, so the rbfx end-rotation argument is retained for API compatibility but cannot affect a sweep. The unbounded Bullet plane is represented by a large bounded Jolt half-space adapter.
- `Internal Edge Utility`, `Split Impulse`, `Anisotropic Friction`, `Rolling Friction`, `Contact Threshold`, `ERP`, `CFM`, and `Max Suspension Force` remain reflected for serialization compatibility where Jolt has no equivalent per-world, per-body, per-constraint, or per-wheel control.
- `PhysicsWorld` implements the backend-neutral `IPhysicsWorld3D` contract in RedBolt.Core. The base engine, Network, and Particles modules therefore do not reference this repository.
- `PredictedKinematicController` lives in the optional `RedBolt.PhysicsJolt.Network` assembly because it combines RedBolt.Network behavior with Jolt components.
