# RedBolt.PhysicsJolt workspace instructions

This repository contains the optional Jolt 3D physics module extracted from the sibling RedBolt repository.

- RedBolt is expected at `../RedBolt` unless `RedBoltRoot` is supplied; tests remain in `../RedBolt.Tests`.
- Do not introduce a dependency on RedBolt.2D or RedBolt.PhysicsBox2D.
- Preserve the public `RedBolt.PhysicsJolt`, `RedBolt.PhysicsJolt.Network`, and `RedBolt.Bindings.Jolt` assemblies and namespaces.
- Use generated SharpCpp bindings for Jolt. Do not add manual P/Invoke bindings.
- Keep Linux x64, Windows x64, WebAssembly, Android x64, and Android arm64 working in Debug and Release.
- Run build invocations sequentially. Internal build parallelism may use at most four jobs, and only when no Android emulator or QEMU process is running.
- Build the smallest affected project and run focused PhysicsJolt tests after implementation changes.
- At completion, build all supported platform/configuration cells, create and validate packages, run focused tests, and review a captured sample screenshot.
