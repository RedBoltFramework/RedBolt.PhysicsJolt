// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

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
        IgnoreSimdVectorPointerFunctions(compilation);
    }

    // JPH::Vec3 is 16-byte SIMD natively but projects to the 12-byte JoltVector3Abi
    // (JoltMarshallers.hpp), so a raw Vec3 pointer cannot be reinterpret_cast between the two
    // layouts the way the generic Marshaller<const T*> fallback requires; that fallback
    // static_asserts on exactly this mismatch. Jolt uses such pointers for arrays
    // (ConvexHullShapeSettings inPoints, MotionProperties::ResetSleepTestSpheres), so a
    // single-element pointer marshaller would be wrong too. Vec3&/const Vec3& stay bound through
    // ProjectedValue(Const)ReferenceMarshallerBase, and array overloads (JPH::Array<Vec3>) remain.
    //
    // Every function with a Vec3/RVec3 pointer parameter is therefore ignored by default. The one
    // exception is SimdVectorOutputArrays, an explicit allow-list of fixed-size output arrays
    // keyed by qualified function name and parameter name. A mutable Vec3* binds as
    // Span<BaseRealVector3<float>> through Marshaller<JPH::Vec3*> (JoltMarshallers.hpp), and the
    // listed minimum length (the number of elements native code writes) is enforced by the
    // generated wrapper with an ArgumentException. A function is only bound when every Vec3
    // pointer parameter is mutable and listed; const Vec3* parameters and anything unlisted keep
    // the whole function ignored. RVec3 is Vec3 because JPH_DOUBLE_PRECISION is not set.
    private static readonly HashSet<string> SimdVectorPointees = new(StringComparer.Ordinal)
    {
        "JPH::Vec3",
        "JPH::RVec3"
    };

    private static readonly Dictionary<string, Dictionary<string, int>> SimdVectorOutputArrays =
        new(StringComparer.Ordinal)
        {
            // Body::GetSleepTestPoints (3) and PlaneShape::GetVertices (4) also take Vec3 output
            // arrays but are private, so they are not bound.
            // Copies mNumPoints (at most 4, the size of mY/mP/mQ) simplex points.
            ["JPH::GJKClosestPoint::GetClosestPointsSimplex"] = new(StringComparer.Ordinal)
            {
                ["outY"] = 4,
                ["outP"] = 4,
                ["outQ"] = 4
            }
        };

    private static void IgnoreSimdVectorPointerFunctions(BindingCompilation compilation)
    {
        foreach (var function in compilation.Functions.Where(static function =>
            function.Parameters.Any(static parameter => IsSimdVectorPointer(parameter.CppTypeName))))
        {
            if (TryConfigureSimdVectorOutputArrays(function))
                continue;

            function.SetIgnoreReason(
                "raw Vec3 pointer is an array whose native SIMD layout does not match the " +
                "projected JoltVector3Abi; use the JPH::Array<Vec3> overload instead");
        }
    }

    private static bool TryConfigureSimdVectorOutputArrays(FunctionBinding function)
    {
        if (!SimdVectorOutputArrays.TryGetValue(function.CppQualifiedName, out var minimumLengths))
            return false;

        var pointerParameters = function.Parameters
            .Where(static parameter => IsSimdVectorPointer(parameter.CppTypeName))
            .ToList();
        if (!pointerParameters.All(parameter =>
                !IsConstPointer(parameter.CppTypeName) && minimumLengths.ContainsKey(parameter.NativeName)))
        {
            return false;
        }

        foreach (var parameter in pointerParameters)
            parameter.SetMetadata(MetadataKeys.MinimumSpanLength, minimumLengths[parameter.NativeName]);
        return true;
    }

    private static bool IsConstPointer(string cppTypeName) =>
        cppTypeName.Trim().StartsWith("const ", StringComparison.Ordinal);

    private static bool IsSimdVectorPointer(string cppTypeName)
    {
        var name = cppTypeName.Trim();
        if (!name.EndsWith('*'))
            return false;
        name = name.TrimEnd('*').Trim();
        if (name.StartsWith("const ", StringComparison.Ordinal))
            name = name["const ".Length..].Trim();
        return SimdVectorPointees.Contains(name);
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
