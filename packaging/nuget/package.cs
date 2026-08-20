// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#:project ../../../RedBolt/eng/RedBolt.ModuleBuild/Packaging/RedBolt.ExternalModule.Packaging.csproj

using System.Runtime.CompilerServices;
using RedBolt.ExternalModule.Packaging;

return ExternalModulePackager.Run(args, new ExternalModulePackageManifest
{
    Name = "RedBolt.PhysicsJolt",
    BasePackages = ["RedBolt.Core", "RedBolt.Network", "SharpCpp.Std", "SharpCpp.Support"],
    RequireBaseRuntime = true,
    SupportedRids = ["linux-x64", "win-x64", "android-x64", "android-arm64", "browser-wasm"],
    DefaultRids = ["linux-x64"],
    BuildSteps =
    [
        new("Native/JoltPhysics/RedBolt.Bindings.Jolt.csproj", BuildWhen.Linux, true),
        new("PhysicsJolt/RedBolt.PhysicsJolt.csproj", BuildWhen.Linux, true, "-p:BuildProjectReferences=false"),
        new("PhysicsJolt.Network/RedBolt.PhysicsJolt.Network.csproj", BuildWhen.Linux, true, "-p:BuildProjectReferences=false"),
        new("PhysicsJolt/RedBolt.PhysicsJolt.csproj", BuildWhen.Windows, false,
            "-p:RedBoltPlatform=win-x64"),
        new("Sample/Web/RedBolt.Sample.Web.csproj", BuildWhen.Web, false, "-p:RedBoltEnablePlatforms=web"),
        new("Sample/Android/RedBolt.Sample.Android.csproj", BuildWhen.Android, false,
            "--framework", "net10.0-android", "-p:RedBoltEnablePlatforms=android", "-p:RedBoltEnableArchitectures=all")
    ],
    ManagedPackages =
    [
        new(
            "RedBolt.PhysicsJolt",
            "Jolt 3D physics integration for RedBolt.",
            [
                new("build/bin/Release/linux-x64/core/RedBolt.PhysicsJolt.dll", "lib/net10.0/RedBolt.PhysicsJolt.dll"),
                new("build/bin/Release/linux-x64/extern/RedBolt.Bindings.Jolt.dll", "lib/net10.0/RedBolt.Bindings.Jolt.dll")
            ],
            [
                new("RedBolt.Core", PackageVersions.RedBolt),
                new("SharpCpp.Std", PackageVersions.RedBolt),
                new("SharpCpp.Support", PackageVersions.RedBolt)
            ]),
        new(
            "RedBolt.PhysicsJolt.Network",
            "Jolt-aware networking helpers for RedBolt.",
            [new("build/bin/Release/linux-x64/core/RedBolt.PhysicsJolt.Network.dll", "lib/net10.0/RedBolt.PhysicsJolt.Network.dll")],
            [
                new("RedBolt.PhysicsJolt", PackageVersions.Module),
                new("RedBolt.Network", PackageVersions.RedBolt)
            ])
    ],
    RuntimePackage = new(
        "RedBolt.PhysicsJolt.Runtime",
        "Jolt",
        "*RedBolt.Native.Jolt*",
        "build/native/{rid}/lib/Release/wasm/RedBolt.PhysicsJolt.Native.Web.a",
        [
            new("RedBolt.PhysicsJolt", PackageVersions.Module),
            new("RedBolt.Core.Runtime.{rid}", PackageVersions.RedBolt)
        ]),
    Consumer = new(
        "RedBolt.PhysicsJolt.Runtime.linux-x64",
        """
        using System;
        using RedBolt.PhysicsJolt;
        Console.WriteLine(typeof(PhysicsWorld).FullName);
        """,
        OnlyWithLinuxRuntime: true)
}, SourcePath());

static string SourcePath([CallerFilePath] string path = "") => path;
