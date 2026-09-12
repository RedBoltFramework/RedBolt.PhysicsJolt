# RedBolt.PhysicsJolt workspace instructions

This repository contains the optional Jolt 3D physics module extracted from the sibling RedBolt repository.

- RedBolt is expected at `../RedBolt` unless `RedBoltRoot` is supplied; tests remain in `../RedBolt.Tests`.
- Do not introduce a dependency on RedBolt.2D or RedBolt.PhysicsBox2D.
- Preserve the public `RedBolt.PhysicsJolt`, `RedBolt.PhysicsJolt.Network`, and `RedBolt.Bindings.Jolt` assemblies and namespaces.
- Use generated SharpCpp bindings for Jolt. Do not add manual P/Invoke bindings.
- Keep Linux x64, Windows x64, WebAssembly, Android x64, and Android arm64 working in Debug and Release.
- Follow the monorepo `doc/VERIFICATION.md`: build affected projects and run focused tests; validate affected platform samples and packages when their behavior or integration changes. Run the full supported matrix for release validation or when requested.
