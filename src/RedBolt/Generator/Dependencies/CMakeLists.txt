# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

if(NOT REDBOLT_DEPENDENCIES_GENERATOR_DLL)
    set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
        "${PROJECT_SOURCE_DIR}/build/bin/$<CONFIG>/host/tools/RedBolt.PhysicsJolt.Generator.Dependencies.dll"
        CACHE FILEPATH "RedBolt.PhysicsJolt dependency generator assembly")
endif()

file(GLOB_RECURSE REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cs"
    "${CMAKE_CURRENT_SOURCE_DIR}/*.csproj"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/BindingTargetGenerator.cs"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/FixedWidthAliasNormalizer.cs"
    "${REDBOLT_ROOT}/src/RedBolt/Generator/Dependencies/GeneratorOptions.cs")

set(REDBOLT_DEPENDENCIES_GENERATOR_INPUTS "${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}" CACHE INTERNAL "RedBolt.PhysicsJolt generator inputs")

set(REDBOLT_JOLT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/jolt" CACHE INTERNAL "Jolt binding output")
set(REDBOLT_JOLT_BINDINGS_STEM "RedBolt.Bindings.Jolt" CACHE INTERNAL "Jolt binding stem")
set(REDBOLT_JOLT_BINDINGS_CS "${REDBOLT_JOLT_GENERATED_DIR}/${REDBOLT_JOLT_BINDINGS_STEM}.Bindings.g.cs" CACHE INTERNAL "Jolt generated C# binding")
set(REDBOLT_JOLT_BINDINGS_CPP "${REDBOLT_JOLT_GENERATED_DIR}/${REDBOLT_JOLT_BINDINGS_STEM}.Bindings.g.cpp" CACHE INTERNAL "Jolt generated native binding")
set(REDBOLT_JOLT_BINDINGS_JSON "${REDBOLT_JOLT_GENERATED_DIR}/${REDBOLT_JOLT_BINDINGS_STEM}.json" CACHE INTERNAL "Jolt binding metadata")
set(REDBOLT_JOLT_PARSE_HEADER "${REDBOLT_JOLT_GENERATED_DIR}/JoltAll.hpp" CACHE INTERNAL "Jolt parse header")
set(REDBOLT_JOLT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/JoltPhysics" CACHE INTERNAL "RedBolt Jolt support include directory" FORCE)
set(REDBOLT_JOLT_SUPPORT_HEADER "${REDBOLT_JOLT_SUPPORT_INCLUDE_DIR}/JoltMarshallers.hpp" CACHE INTERNAL "RedBolt Jolt marshaller header")
set(REDBOLT_BINDING_INCLUDE_DIRS "${REDBOLT_SHARPCPP_ROOT}/include" CACHE INTERNAL "SharpCpp binding includes")
file(MAKE_DIRECTORY "${REDBOLT_JOLT_GENERATED_DIR}")

file(CONFIGURE OUTPUT "${REDBOLT_JOLT_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <JoltMarshallers.hpp>
#include <Jolt/RegisterTypes.h>
#include <Jolt/Core/Factory.h>
#include <Jolt/Core/JobSystemThreadPool.h>
#include <Jolt/Core/TempAllocator.h>
#include <Jolt/Geometry/GJKClosestPoint.h>
#include <Jolt/Physics/Body/Body.h>
#include <Jolt/Physics/Body/BodyCreationSettings.h>
#include <Jolt/Physics/Body/MassProperties.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayerInterfaceMask.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayerInterfaceTable.h>
#include <Jolt/Physics/Collision/BroadPhase/ObjectVsBroadPhaseLayerFilterMask.h>
#include <Jolt/Physics/Collision/BroadPhase/ObjectVsBroadPhaseLayerFilterTable.h>
#include <Jolt/Physics/Collision/CollideShape.h>
#include <Jolt/Physics/Collision/ContactListener.h>
#include <Jolt/Physics/Collision/CollisionCollectorImpl.h>
#include <Jolt/Physics/Collision/NarrowPhaseQuery.h>
#include <Jolt/Physics/Collision/ShapeCast.h>
#include <Jolt/Physics/Collision/ObjectLayerPairFilterMask.h>
#include <Jolt/Physics/Collision/ObjectLayerPairFilterTable.h>
#include <Jolt/Physics/Collision/Shape/BoxShape.h>
#include <Jolt/Physics/Collision/Shape/CapsuleShape.h>
#include <Jolt/Physics/Collision/Shape/ConvexHullShape.h>
#include <Jolt/Physics/Collision/Shape/CylinderShape.h>
#include <Jolt/Physics/Collision/Shape/HeightFieldShape.h>
#include <Jolt/Physics/Collision/Shape/MeshShape.h>
#include <Jolt/Physics/Collision/Shape/RotatedTranslatedShape.h>
#include <Jolt/Physics/Collision/Shape/ScaledShape.h>
#include <Jolt/Physics/Collision/Shape/SphereShape.h>
#include <Jolt/Physics/Collision/Shape/StaticCompoundShape.h>
#include <Jolt/Physics/Collision/Shape/TaperedCylinderShape.h>
#include <Jolt/Physics/Collision/Shape/TriangleShape.h>
#include <Jolt/Physics/Collision/TransformedShape.h>
#include <Jolt/Physics/Character/Character.h>
#include <Jolt/Physics/Constraints/HingeConstraint.h>
#include <Jolt/Physics/Constraints/PointConstraint.h>
#include <Jolt/Physics/Constraints/SixDOFConstraint.h>
#include <Jolt/Physics/Constraints/SwingTwistConstraint.h>
#include <Jolt/Physics/PhysicsStepListener.h>
#include <Jolt/Physics/SoftBody/SoftBodyCreationSettings.h>
#include <Jolt/Physics/Vehicle/VehicleCollisionTester.h>
#include <Jolt/Physics/Vehicle/VehicleConstraint.h>
#include <Jolt/Physics/Vehicle/WheeledVehicleController.h>
#include <Jolt/Physics/PhysicsSystem.h>

namespace JPH
{
using RedBoltAllHitCastRayCollector [[sharpcpp::materialize]] = AllHitCollisionCollector<CastRayCollector>;
using RedBoltClosestCastShapeCollector [[sharpcpp::materialize]] = ClosestHitCollisionCollector<CastShapeCollector>;
using RedBoltAllHitCollideShapeCollector [[sharpcpp::materialize]] = AllHitCollisionCollector<CollideShapeCollector>;

static_assert(sizeof(RedBoltAllHitCastRayCollector) > 0);
static_assert(sizeof(RedBoltClosestCastShapeCollector) > 0);
static_assert(sizeof(RedBoltAllHitCollideShapeCollector) > 0);
}
]=] @ONLY)
