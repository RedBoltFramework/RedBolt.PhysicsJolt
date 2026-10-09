# RedBolt.PhysicsJolt NuGet packages

RedBolt.PhysicsJolt versions independently from RedBolt, but every package records the exact base RedBolt version. Produce the matching base feed first in `<build root>/package/nuget/RedBolt/out` (`<build root>` is the `build/` directory beside the engine directory; the packaging script reads `REDBOLT_BUILD_ROOT` to override it).

Create Linux x64 packages from Release outputs, building the add-on first:

```sh
dotnet run --file packaging/nuget/package.cs -- create --rids linux-x64
```

Use `--no-build` when all selected outputs already exist. Select additional prebuilt targets with a comma-separated list such as `--rids linux-x64,win-x64,android-x64,android-arm64,browser-wasm`. Override dependency version discovery only for an intentional local feed with `--redbolt-version VERSION`.

The command writes independently versioned packages below `<build root>/package/nuget/RedBolt.PhysicsJolt/out`:

- `RedBolt.PhysicsJolt`
- `RedBolt.PhysicsJolt.Network`
- `RedBolt.PhysicsJolt.Runtime.<rid>`

The runtime facade brings in the managed physics package, the matching `RedBolt.Core.Runtime.<rid>` package, the Jolt binding assembly, and `RedBolt.Native.Jolt` for the selected RID. The networking package is optional and pins both `RedBolt.PhysicsJolt` and `RedBolt.Network`.

Validate package contents, exact dependency versions, and a package-only consumer with:

```sh
dotnet run --file packaging/nuget/package.cs -- validate --rids linux-x64
```
