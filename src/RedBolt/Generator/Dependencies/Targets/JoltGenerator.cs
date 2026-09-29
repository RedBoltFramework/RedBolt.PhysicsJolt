// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class JoltGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "jolt",
        "RedBolt.Bindings.Jolt",
        "RedBolt.Bindings.Jolt",
        "Jolt",
        nativeLibraryName,
        "redbolt_jolt",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> DirectorRecords = new(StringComparer.Ordinal)
    {
        "JPH::ContactListener",
        "JPH::ObjectLayerFilter",
        "JPH::ObjectLayerPairFilter"
    };

    // The math classes' nested `Type` is their SIMD storage. Generation parses with SSE disabled
    // (-mno-sse, the BindingTargetGenerator policy that keeps the managed surface target-invariant)
    // and JoltMarshallers.hpp defines _M_ARM for the parse, so there `Type` is float[4]-style
    // storage while natively it is __m128 and friends; its members do not exist in the native build.
    private static readonly HashSet<string> SimdStorageRecords = new(StringComparer.Ordinal)
    {
        "JPH::Vec4::Type",
        "JPH::UVec4::Type",
        "JPH::DVec3::Type",
        "JPH::BVec16::Type"
    };

    // Internal Jolt job state (documented as internal to PhysicsSystem); its nested steps hold
    // collections of non-copyable atomics. Nested records are ignored with their parent.
    private static readonly HashSet<string> InternalRecords = new(StringComparer.Ordinal)
    {
        "JPH::PhysicsUpdateContext"
    };

    // JPH::Triangle stores its vertices in a fixed-size Float3[3] field. SharpCpp's fixed-array
    // field binding currently only supports elements that are bound record types, not a
    // projected value type with a namespace-qualified public_type (such as Float3, now mapped to
    // RedBolt.Math.Float3): it emits an invalid managed marshaller type name for the element
    // accessor. Triangle/TriangleList are not used anywhere in RedBolt; ignore them rather than
    // changing SharpCpp's generator.
    private static readonly HashSet<string> UnsupportedFixedArrayFieldRecords = new(StringComparer.Ordinal)
    {
        "JPH::Triangle"
    };

    private static readonly IReadOnlyDictionary<string, FixedWidthAliasProjection> FixedWidthAliases =
        new Dictionary<string, FixedWidthAliasProjection>(StringComparer.Ordinal)
        {
            ["JPH::uint64"] = new(BindingPrimitiveKind.UInt64, "ulong"),
            ["JPH::int64"] = new(BindingPrimitiveKind.Int64, "long")
        };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["JPH"] = "RedBolt.Bindings.Jolt"
        };

    protected override IReadOnlySet<string>? DirectorRecordCppTypeNames => DirectorRecords;

    // Jolt exposes director callbacks as well; keep the callback lifetime
    // support local to this optional wrapper rather than relying on SDL.
    protected override bool IncludeCallbackReleaseRegistration => true;

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        FixedWidthAliasNormalizer.Normalize(compilation, FixedWidthAliases);

        IgnoreRecordTree(
            compilation,
            SimdStorageRecords,
            "SIMD storage type differs between the generator parse and the native build");
        IgnoreRecordTree(compilation, InternalRecords, "internal PhysicsSystem job state");
        IgnoreRecordTree(
            compilation,
            UnsupportedFixedArrayFieldRecords,
            "fixed-size array field of a projected value type is not supported by SharpCpp's binding generator");
    }

    protected override IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options)
    {
        yield return System.IO.Path.Combine(
            options.RedBoltRoot, "src", "RedBolt", "Native", "JoltPhysics", "JoltMarshallers.hpp");
    }

    // Ignores each named record and every record nested inside it.
    private static void IgnoreRecordTree(
        BindingCompilation compilation, IReadOnlySet<string> names, string reason)
    {
        foreach (var record in compilation.Records)
        {
            foreach (var name in names)
            {
                if (record.CppQualifiedName == name ||
                    record.CppQualifiedName.StartsWith(name + "::", StringComparison.Ordinal))
                {
                    record.SetIgnoreReason(reason);
                    break;
                }
            }
        }
    }
}
