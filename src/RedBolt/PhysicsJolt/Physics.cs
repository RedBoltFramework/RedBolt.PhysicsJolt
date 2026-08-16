// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsJolt;

using RedBolt.Core;

public static class Physics
{
    /// <summary>Register physics library objects.</summary>
    public static void RegisterPhysicsLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RegisterAssembly(typeof(Physics).Assembly);
    }
}
