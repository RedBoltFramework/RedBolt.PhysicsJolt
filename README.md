# RedBolt.PhysicsJolt

Optional Jolt 3D physics for [RedBolt](https://github.com/RedBoltFramework/RedBolt). The repository owns the generated Jolt bindings, native wrapper, managed physics assembly, optional networking integration, and its samples.

The source build expects RedBolt in `../RedBolt` by default. Override `RedBoltRoot` when the repositories use a different layout.

```sh
dotnet build RedBolt.PhysicsJolt.slnx --configuration Debug
```

Desktop, WebAssembly, and Android samples are included under `src/RedBolt/Sample`. Generated and native outputs remain below the `build/` directory beside the engine directory.

Tests remain in the sibling `RedBolt.Tests` repository. See [building](doc/BUILDING.md), [package production](packaging/nuget/README.md), and the [physics-specific deviations](doc/ALLOWED_DEVIATIONS.md).
