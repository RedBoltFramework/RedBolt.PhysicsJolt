// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#pragma once

#include <cstdint>
#include <SharpCpp/Marshaller.hpp>

#if defined(REDBOLT_JOLT_BINDING_PARSE)
#define _M_ARM 1
#endif

#include <Jolt/Jolt.h>
#include <Jolt/Core/Array.h>
#include <Jolt/Core/Mutex.h>
#include <Jolt/Core/Reference.h>
#include <Jolt/Core/StaticArray.h>
#include <Jolt/Math/DVec3.h>
#include <Jolt/Math/Double3.h>
#include <Jolt/Math/Float3.h>
#include <Jolt/Math/Float4.h>
#include <Jolt/Math/Mat44.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Math/Vec4.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/Shape/SubShapeID.h>
#include <Jolt/Physics/Vehicle/VehicleConstraint.h>
namespace SharpCpp
{

template <class T, class TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "JPH.Array<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "JPH"
)]] Marshaller<JPH::Array<T, TAllocator>>
    : SequenceCollectionMarshallerBase<JPH::Array<T, TAllocator>, T> {};

template <class T, class TAllocator>
struct Marshaller<JPH::Array<T, TAllocator>&>
    : OwnedHandleReferenceMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<const JPH::Array<T, TAllocator>&>
    : OwnedHandleConstReferenceMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<JPH::Array<T, TAllocator>*>
    : OwnedHandlePointerMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<const JPH::Array<T, TAllocator>*>
    : OwnedHandleConstPointerMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::Array<T, TAllocator>>
    : OwnedHandleReturnMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, JPH::uint N>
struct [[sharpcpp::managed_type(
    public_type = "JPH.StaticArray<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "JPH"
)]] Marshaller<JPH::StaticArray<T, N>>
    : IndexedSequenceCollectionMarshallerBase<JPH::StaticArray<T, N>, T> {};

template <class T, JPH::uint N>
struct Marshaller<JPH::StaticArray<T, N>&>
    : OwnedHandleReferenceMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<const JPH::StaticArray<T, N>&>
    : OwnedHandleConstReferenceMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<JPH::StaticArray<T, N>*>
    : OwnedHandlePointerMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<const JPH::StaticArray<T, N>*>
    : OwnedHandleConstPointerMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::StaticArray<T, N>>
    : OwnedHandleReturnMarshallerBase<JPH::StaticArray<T, N>> {};

struct JoltVector3Abi
{
    float x;
    float y;
    float z;
};

struct JoltVector4Abi
{
    float x;
    float y;
    float z;
    float w;
};

struct JoltDVector3Abi
{
    double x;
    double y;
    double z;
};

struct JoltQuaternionAbi
{
    float w;
    float x;
    float y;
    float z;
};

struct JoltRayAbi
{
    JoltVector3Abi origin;
    JoltVector3Abi direction;
};

// Row-major layout matching RedBolt.Math.Matrix4's field order (M00..M03, M10..M13, ...).
// Jolt's Mat44 is stored as 4 SIMD column vectors, so every element here must be read/written
// through Jolt's scalar accessors (GetColumn4/sLoadFloat4x4), never by copying the SIMD storage.
struct JoltMatrix4Abi
{
    float m00, m01, m02, m03;
    float m10, m11, m12, m13;
    float m20, m21, m22, m23;
    float m30, m31, m32, m33;
};

struct JoltRayCastResultAbi
{
    uint32_t bodyId;
    float fraction;
    uint32_t subShapeId;
};

inline JoltVector3Abi ToAbi(JPH::Vec3Arg value) noexcept
{
    return {value.GetX(), value.GetY(), value.GetZ()};
}

inline JPH::Vec3 ToCpp(const JoltVector3Abi& value) noexcept
{
    return {value.x, value.y, value.z};
}

inline JoltQuaternionAbi ToAbi(JPH::QuatArg value) noexcept
{
    return {value.GetW(), value.GetX(), value.GetY(), value.GetZ()};
}

inline JPH::Quat ToCpp(const JoltQuaternionAbi& value) noexcept
{
    return {value.x, value.y, value.z, value.w};
}

inline JoltVector3Abi ToAbi(const JPH::Float3& value) noexcept
{
    return {value.x, value.y, value.z};
}

// Named distinctly from ToCpp(const JoltVector3Abi&) above: both share the same ABI parameter
// type, and C++ cannot overload on return type alone (JPH::Vec3 vs JPH::Float3).
inline JPH::Float3 ToFloat3Cpp(const JoltVector3Abi& value) noexcept
{
    return {value.x, value.y, value.z};
}

inline JoltVector4Abi ToAbi(JPH::Vec4Arg value) noexcept
{
    return {value.GetX(), value.GetY(), value.GetZ(), value.GetW()};
}

inline JPH::Vec4 ToCpp(const JoltVector4Abi& value) noexcept
{
    return {value.x, value.y, value.z, value.w};
}

inline JoltVector4Abi ToAbi(const JPH::Float4& value) noexcept
{
    return {value.x, value.y, value.z, value.w};
}

// Named distinctly from ToCpp(const JoltVector4Abi&): same ABI parameter type, different
// return type (JPH::Vec4 vs JPH::Float4).
inline JPH::Float4 ToFloat4Cpp(const JoltVector4Abi& value) noexcept
{
    return {value.x, value.y, value.z, value.w};
}

inline JoltDVector3Abi ToAbi(JPH::DVec3Arg value) noexcept
{
    return {value.GetX(), value.GetY(), value.GetZ()};
}

inline JPH::DVec3 ToCpp(const JoltDVector3Abi& value) noexcept
{
    return {value.x, value.y, value.z};
}

inline JoltDVector3Abi ToAbi(const JPH::Double3& value) noexcept
{
    return {value.x, value.y, value.z};
}

// Named distinctly from ToCpp(const JoltDVector3Abi&) for the same reason as ToFloat3Cpp.
inline JPH::Double3 ToDouble3Cpp(const JoltDVector3Abi& value) noexcept
{
    return {value.x, value.y, value.z};
}

inline JoltMatrix4Abi ToAbi(JPH::Mat44Arg value) noexcept
{
    // Each column is a SIMD Vec4; extract its scalar rows via the accessors rather than
    // reinterpreting the SIMD storage, then scatter them into row-major ABI slots.
    JPH::Vec4 col0 = value.GetColumn4(0);
    JPH::Vec4 col1 = value.GetColumn4(1);
    JPH::Vec4 col2 = value.GetColumn4(2);
    JPH::Vec4 col3 = value.GetColumn4(3);
    return {
        col0.GetX(), col1.GetX(), col2.GetX(), col3.GetX(),
        col0.GetY(), col1.GetY(), col2.GetY(), col3.GetY(),
        col0.GetZ(), col1.GetZ(), col2.GetZ(), col3.GetZ(),
        col0.GetW(), col1.GetW(), col2.GetW(), col3.GetW()};
}

inline JPH::Mat44 ToCpp(const JoltMatrix4Abi& value) noexcept
{
    // Reassemble as 4 column Float4s (row values across each column) and load through
    // sLoadFloat4x4, which is the accessor-based way to populate the SIMD column storage.
    const JPH::Float4 columns[4] = {
        JPH::Float4(value.m00, value.m10, value.m20, value.m30),
        JPH::Float4(value.m01, value.m11, value.m21, value.m31),
        JPH::Float4(value.m02, value.m12, value.m22, value.m32),
        JPH::Float4(value.m03, value.m13, value.m23, value.m33)};
    return JPH::Mat44::sLoadFloat4x4(columns);
}

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.BaseRealVector3<float>",
    managed_marshaller = "global::RedBolt.Math.Vector3fMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::Vec3>
{
    using cpp_type = JPH::Vec3;
    using abi_type = JoltVector3Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Vec3&> : ProjectedValueConstReferenceMarshallerBase<JPH::Vec3> {};

template <>
struct Marshaller<JPH::Vec3&> : ProjectedValueReferenceMarshallerBase<JPH::Vec3> {};

// Mutable Vec3 output arrays (GJKClosestPoint::GetClosestPointsSimplex) bind as
// Span<BaseRealVector3<float>>; the element count crosses the ABI in-band. JoltGenerator allow-lists the functions that take this
// projection and sets the per-parameter minimum span length; all other Vec3 pointers stay ignored.
template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.Span<global::RedBolt.Math.BaseRealVector3<float>>",
    managed_marshaller = "global::SharpCpp.Support.SpanAbiMarshaller<>"
)]] Marshaller<JPH::Vec3*> : ProjectedValueSpanMarshallerBase<JPH::Vec3> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Quaternion",
    suppress_emission = true
)]] Marshaller<JPH::Quat>
{
    using cpp_type = JPH::Quat;
    using abi_type = JoltQuaternionAbi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Quat&> : ProjectedValueConstReferenceMarshallerBase<JPH::Quat> {};

template <>
struct Marshaller<JPH::Quat&> : ProjectedValueReferenceMarshallerBase<JPH::Quat> {};

// Float3 is deliberately projected to its own RedBolt.Math.Float3 type rather than reusing
// BaseRealVector3<float> (Vec3's projection). Vec3 is 16-byte-aligned SIMD storage while Float3
// is a tightly packed 12-byte POD; JPH::Array<Vec3> and JPH::Array<Float3> are distinct native
// collections with different element strides, and SharpCpp's generic Array<T> dispatches to the
// correct native entry points by the managed element type, so the two must stay distinct C# types.
template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Float3",
    suppress_emission = true
)]] Marshaller<JPH::Float3>
{
    using cpp_type = JPH::Float3;
    using abi_type = JoltVector3Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToFloat3Cpp(value); }
};

template <>
struct Marshaller<const JPH::Float3&> : ProjectedValueConstReferenceMarshallerBase<JPH::Float3> {};

template <>
struct Marshaller<JPH::Float3&> : ProjectedValueReferenceMarshallerBase<JPH::Float3> {};

// Vec4/Float4 both project to the blittable RedBolt.Math.Vector4f and DVec3/Double3 to
// BaseRealVector3<double>, crossing the ABI as scalar structs (JoltVector4Abi/JoltDVector3Abi)
// filled through the accessors, never by copying the native (SIMD) storage. Raw pointers to the
// SIMD types (Vec4*, DVec3*) are ignored by JoltGenerator; see SimdVectorPointees.
template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Vector4f",
    managed_marshaller = "global::RedBolt.Math.Vector4fMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::Vec4>
{
    using cpp_type = JPH::Vec4;
    using abi_type = JoltVector4Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Vec4&> : ProjectedValueConstReferenceMarshallerBase<JPH::Vec4> {};

template <>
struct Marshaller<JPH::Vec4&> : ProjectedValueReferenceMarshallerBase<JPH::Vec4> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Vector4f",
    managed_marshaller = "global::RedBolt.Math.Vector4fMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::Float4>
{
    using cpp_type = JPH::Float4;
    using abi_type = JoltVector4Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToFloat4Cpp(value); }
};

template <>
struct Marshaller<const JPH::Float4&> : ProjectedValueConstReferenceMarshallerBase<JPH::Float4> {};

template <>
struct Marshaller<JPH::Float4&> : ProjectedValueReferenceMarshallerBase<JPH::Float4> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.BaseRealVector3<double>",
    managed_marshaller = "global::RedBolt.Math.Vector3dMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::DVec3>
{
    using cpp_type = JPH::DVec3;
    using abi_type = JoltDVector3Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::DVec3&> : ProjectedValueConstReferenceMarshallerBase<JPH::DVec3> {};

template <>
struct Marshaller<JPH::DVec3&> : ProjectedValueReferenceMarshallerBase<JPH::DVec3> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.BaseRealVector3<double>",
    managed_marshaller = "global::RedBolt.Math.Vector3dMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::Double3>
{
    using cpp_type = JPH::Double3;
    using abi_type = JoltDVector3Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToDouble3Cpp(value); }
};

template <>
struct Marshaller<const JPH::Double3&> : ProjectedValueConstReferenceMarshallerBase<JPH::Double3> {};

template <>
struct Marshaller<JPH::Double3&> : ProjectedValueReferenceMarshallerBase<JPH::Double3> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Matrix4",
    suppress_emission = true
)]] Marshaller<JPH::Mat44>
{
    using cpp_type = JPH::Mat44;
    using abi_type = JoltMatrix4Abi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Mat44&> : ProjectedValueConstReferenceMarshallerBase<JPH::Mat44> {};

template <>
struct Marshaller<JPH::Mat44&> : ProjectedValueReferenceMarshallerBase<JPH::Mat44> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "uint",
    suppress_emission = true
)]] Marshaller<JPH::BodyID>
{
    using cpp_type = JPH::BodyID;
    using abi_type = uint32_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetIndexAndSequenceNumber(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return JPH::BodyID(value); }
};

template <>
struct Marshaller<const JPH::BodyID&> : ProjectedValueConstReferenceMarshallerBase<JPH::BodyID> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "byte",
    suppress_emission = true
)]] Marshaller<JPH::BroadPhaseLayer>
{
    using cpp_type = JPH::BroadPhaseLayer;
    using abi_type = uint8_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetValue(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return JPH::BroadPhaseLayer(value); }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "uint",
    suppress_emission = true
)]] Marshaller<JPH::SubShapeID>
{
    using cpp_type = JPH::SubShapeID;
    using abi_type = uint32_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetValue(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        JPH::SubShapeID result;
        result.SetValue(value);
        return result;
    }
};

template <>
struct Marshaller<const JPH::SubShapeID&> : ProjectedValueConstReferenceMarshallerBase<JPH::SubShapeID> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Ray",
    suppress_emission = true
)]] Marshaller<JPH::RRayCast>
{
    using cpp_type = JPH::RRayCast;
    using abi_type = JoltRayAbi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return {ToAbi(value.mOrigin), ToAbi(value.mDirection)};
    }
    cpp_type ConvertToCpp(const abi_type& value) noexcept
    {
        return {ToCpp(value.origin), ToCpp(value.direction)};
    }
};

template <>
struct Marshaller<const JPH::RRayCast&> : ProjectedValueConstReferenceMarshallerBase<JPH::RRayCast> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Bindings.Jolt.JoltRayCastResult",
    suppress_emission = true
)]] Marshaller<JPH::RayCastResult>
{
    using cpp_type = JPH::RayCastResult;
    using abi_type = JoltRayCastResultAbi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return {value.mBodyID.GetIndexAndSequenceNumber(), value.mFraction, value.mSubShapeID2.GetValue()};
    }
    cpp_type ConvertToCpp(const abi_type& value) noexcept
    {
        JPH::RayCastResult result;
        result.mBodyID = JPH::BodyID(value.bodyId);
        result.mFraction = value.fraction;
        result.mSubShapeID2.SetValue(value.subShapeId);
        return result;
    }
};

template <>
struct Marshaller<JPH::RayCastResult&> : ProjectedValueReferenceMarshallerBase<JPH::RayCastResult> {};

template <class T>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true
)]] Marshaller<JPH::Ref<T>>
{
    using cpp_type = JPH::Ref<T>;
    using abi_type = T*;

    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return cpp_type(value); }
};

template <class T>
struct Marshaller<const JPH::Ref<T>&>
{
    using cpp_type = const JPH::Ref<T>&;
    using abi_type = T*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        value_ = value;
        return value_;
    }

private:
    JPH::Ref<T> value_{};
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::Ref<T>> : Marshaller<JPH::Ref<T>>
{
    using typename Marshaller<JPH::Ref<T>>::abi_type;
    using typename Marshaller<JPH::Ref<T>>::cpp_type;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<const JPH::Ref<T>&> : Marshaller<const JPH::Ref<T>&>
{
    using typename Marshaller<const JPH::Ref<T>&>::abi_type;
    using typename Marshaller<const JPH::Ref<T>&>::cpp_type;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true
)]] Marshaller<JPH::RefConst<T>>
{
    using cpp_type = JPH::RefConst<T>;
    using abi_type = const T*;

    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return cpp_type(value); }
};

template <class T>
struct Marshaller<const JPH::RefConst<T>&>
{
    using cpp_type = const JPH::RefConst<T>&;
    using abi_type = const T*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        value_ = value;
        return value_;
    }

private:
    JPH::RefConst<T> value_{};
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::RefConst<T>> : Marshaller<JPH::RefConst<T>>
{
    using typename Marshaller<JPH::RefConst<T>>::abi_type;
    using typename Marshaller<JPH::RefConst<T>>::cpp_type;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        const auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<const JPH::RefConst<T>&> : Marshaller<const JPH::RefConst<T>&>
{
    using typename Marshaller<const JPH::RefConst<T>&>::abi_type;
    using typename Marshaller<const JPH::RefConst<T>&>::cpp_type;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        const auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.IntPtr",
    suppress_emission = true
)]] Marshaller<std::shared_mutex*>
{
    using cpp_type = std::shared_mutex*;
    using abi_type = void*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value; }
    cpp_type ConvertToCpp(abi_type value) noexcept { return static_cast<cpp_type>(value); }
};

template <>
struct Marshaller<JPH::VehicleConstraint> : detail::DefaultMarshallerBase<JPH::VehicleConstraint>
{
    using cpp_type = JPH::VehicleConstraint;
    using abi_type = JPH::VehicleConstraint*;

    // PhysicsStepListener is a secondary base, so its address may differ from
    // the VehicleConstraint address and must be adjusted before crossing the ABI.
    [[sharpcpp::expose_member]]
    static void* GetPhysicsStepListenerHandle(cpp_type& self) noexcept
    {
        return static_cast<JPH::PhysicsStepListener*>(&self);
    }
};

} // namespace SharpCpp
