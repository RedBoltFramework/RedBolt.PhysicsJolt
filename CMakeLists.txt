# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

cmake_minimum_required(VERSION 3.28)

project(RedBoltPhysicsJolt VERSION 0.1.0 LANGUAGES C CXX)

set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build dependencies statically" FORCE)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_SCAN_FOR_MODULES OFF)

if(EMSCRIPTEN)
    add_compile_options(-mbulk-memory)
endif()
if(ANDROID)
    add_compile_options(-fsigned-char)
    add_link_options("-Wl,-z,max-page-size=16384")
endif()

# LLVM-MinGW's COFF linker supports whole-archive, but not the GNU ld
# push-state/pop-state pair emitted by CMake's default feature definition.
if(MINGW)
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE
        "LINKER:--whole-archive"
        "<LINK_ITEM>"
        "LINKER:--no-whole-archive")
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE_SUPPORTED TRUE)
endif()

set(REDBOLT_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../RedBolt" CACHE PATH "RedBolt source root")
set(REDBOLT_SHARPCPP_ROOT "${REDBOLT_ROOT}/extern/SharpCpp" CACHE PATH "SharpCpp source root")
set(REDBOLT_DEPENDENCY_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern" CACHE PATH "RedBolt.PhysicsJolt dependency cache")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-physics-jolt-native")
set(REDBOLT_RUNTIME_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/build/bin/$<CONFIG>/linux-x64/extern/runtimes/linux-x64/native" CACHE PATH "Native runtime output")
set(REDBOLT_PDB_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/build/bin/$<CONFIG>/linux-x64/extern/pdb" CACHE PATH "Native PDB output")

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

list(PREPEND CMAKE_MODULE_PATH "${REDBOLT_ROOT}/cmake/Modules")
set(CPM_DONT_UPDATE_MODULE_PATH ON CACHE BOOL "Use RedBolt CMake modules" FORCE)
set(CPM_SOURCE_CACHE "${REDBOLT_DEPENDENCY_ROOT}" CACHE PATH "CPM source cache" FORCE)
unset(CPM_DIRECTORY CACHE)
unset(CPM_VERSION CACHE)
include("${REDBOLT_ROOT}/cmake/CPM.cmake")
set(FETCHCONTENT_BASE_DIR "${CMAKE_BINARY_DIR}/deps")

include("${REDBOLT_ROOT}/cmake/RedBoltCPM.cmake")
include("${REDBOLT_ROOT}/cmake/RedBoltSharpCpp.cmake")

set(OBJECT_LAYER_BITS 32 CACHE STRING "Use 16 collision layer and mask bits" FORCE)
set(JPH_BUILD_SHARED_LIBS OFF CACHE BOOL "Build Jolt statically" FORCE)
set(OVERRIDE_CXX_FLAGS OFF CACHE BOOL "Keep RedBolt compiler flags" FORCE)
set(INTERPROCEDURAL_OPTIMIZATION OFF CACHE BOOL "Disable Jolt LTO" FORCE)
set(ENABLE_ALL_WARNINGS OFF CACHE BOOL "Disable Jolt warnings as errors" FORCE)
set(ENABLE_INSTALL OFF CACHE BOOL "Disable Jolt installation" FORCE)
set(ENABLE_OBJECT_STREAM OFF CACHE BOOL "Disable Jolt object streams" FORCE)
set(CPP_RTTI_ENABLED ON CACHE BOOL "Enable RTTI for generated bindings" FORCE)
set(DEBUG_RENDERER_IN_DEBUG_AND_RELEASE OFF CACHE BOOL "Disable Jolt debug renderer" FORCE)
set(DEBUG_RENDERER_IN_DISTRIBUTION OFF CACHE BOOL "Disable Jolt debug renderer" FORCE)
set(PROFILER_IN_DEBUG_AND_RELEASE OFF CACHE BOOL "Disable Jolt profiler" FORCE)
set(PROFILER_IN_DISTRIBUTION OFF CACHE BOOL "Disable Jolt profiler" FORCE)
set(JPH_USE_DX12 OFF CACHE BOOL "Disable Jolt DirectX compute" FORCE)
set(JPH_USE_VK OFF CACHE BOOL "Disable Jolt Vulkan compute" FORCE)
set(JPH_USE_MTL OFF CACHE BOOL "Disable Jolt Metal compute" FORCE)
set(JPH_USE_CPU_COMPUTE OFF CACHE BOOL "Disable Jolt CPU compute" FORCE)
foreach(_redbolt_jolt_feature SSE4_1 SSE4_2 AVX AVX2 AVX512 LZCNT TZCNT F16C FMADD)
    set(USE_${_redbolt_jolt_feature} OFF CACHE BOOL "Build portable Jolt binaries" FORCE)
endforeach()
redbolt_cpm_add_package(
    NAME JoltPhysics
    GIT_REPOSITORY https://github.com/jrouwe/JoltPhysics.git
    GIT_TAG 341ac92c7a8a63a4a37729ad6eca21f9a5a51e13
    CUSTOM_CACHE_KEY "."
    SOURCE_SUBDIR Build
    PATCHES "${CMAKE_CURRENT_LIST_DIR}/extern/JoltPhysics.patch"
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_JOLT_SOURCE_ROOT "${JoltPhysics_SOURCE_DIR}" CACHE INTERNAL "Jolt Physics source root")
redbolt_require_path("${REDBOLT_JOLT_SOURCE_ROOT}/Build/CMakeLists.txt" "Jolt Physics build")
set_target_properties(Jolt PROPERTIES POSITION_INDEPENDENT_CODE ON)
set(REDBOLT_JOLT_INCLUDE_DIR "${REDBOLT_JOLT_SOURCE_ROOT}" CACHE INTERNAL "Jolt Physics include directory")
set(REDBOLT_JOLT_LIBRARY Jolt CACHE INTERNAL "RedBolt Jolt Physics target")
set(REDBOLT_JOLT_NATIVE_LIBRARY_NAME "RedBolt.Native.Jolt" CACHE INTERNAL "Jolt native library name")

set(REDBOLT_BINDING_CXX_COMPILER "${CMAKE_CXX_COMPILER}")
set(_redbolt_jolt_binding_defines)
if(CMAKE_CONFIGURATION_TYPES)
    list(APPEND _redbolt_jolt_binding_defines "$<$<NOT:$<CONFIG:Debug>>:NDEBUG>")
elseif(CMAKE_BUILD_TYPE MATCHES "^(Release|Distribution|ReleaseASAN|ReleaseUBSAN|ReleaseTSAN|ReleaseCoverage)$")
    list(APPEND _redbolt_jolt_binding_defines NDEBUG)
endif()
set(REDBOLT_BINDING_DEFINES ${_redbolt_jolt_binding_defines})

add_subdirectory(src/RedBolt/Generator/Dependencies)
add_subdirectory(src/RedBolt/Native/JoltPhysics)

if(EMSCRIPTEN)
    set(_redbolt_jolt_library_type STATIC)
else()
    set(_redbolt_jolt_library_type SHARED)
endif()
add_library(redbolt-physics-jolt-native-jolt ${_redbolt_jolt_library_type} "${REDBOLT_JOLT_BINDINGS_CPP}")
add_dependencies(redbolt-physics-jolt-native-jolt redbolt-generate-jolt-bindings)
target_link_libraries(redbolt-physics-jolt-native-jolt PRIVATE SharpCpp::NativeSupport)
if(NOT EMSCRIPTEN)
    target_link_libraries(redbolt-physics-jolt-native-jolt PRIVATE "$<LINK_LIBRARY:WHOLE_ARCHIVE,Jolt>")
endif()
target_include_directories(redbolt-physics-jolt-native-jolt PRIVATE
    "${REDBOLT_JOLT_INCLUDE_DIR}"
    "${REDBOLT_JOLT_SUPPORT_INCLUDE_DIR}")
target_compile_definitions(redbolt-physics-jolt-native-jolt PRIVATE REDBOLT_NATIVE_BUILD JPH_OBJECT_LAYER_BITS=32)
if(MINGW)
    target_compile_options(redbolt-physics-jolt-native-jolt PRIVATE "-Wa,-mbig-obj")
endif()
set_target_properties(redbolt-physics-jolt-native-jolt PROPERTIES
    OUTPUT_NAME "RedBolt.Native.Jolt"
    RUNTIME_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}"
    LIBRARY_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
    WINDOWS_EXPORT_ALL_SYMBOLS ON
    C_VISIBILITY_PRESET default)
if(CMAKE_CONFIGURATION_TYPES)
    foreach(_redbolt_jolt_configuration IN LISTS CMAKE_CONFIGURATION_TYPES)
        string(TOUPPER "${_redbolt_jolt_configuration}" _redbolt_jolt_configuration_upper)
        set_target_properties(redbolt-physics-jolt-native-jolt PROPERTIES
            RUNTIME_OUTPUT_DIRECTORY_${_redbolt_jolt_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}"
            LIBRARY_OUTPUT_DIRECTORY_${_redbolt_jolt_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}")
    endforeach()
endif()
if(WIN32)
    set_target_properties(redbolt-physics-jolt-native-jolt PROPERTIES PREFIX "")
endif()

if(NOT EMSCRIPTEN)
    file(GENERATE
        OUTPUT "${REDBOLT_GENERATED_ROOT}/native-modules/JOLT.json"
        CONTENT "{\n  \"module\": \"JOLT\",\n  \"managedSelector\": \"RedBolt.Bindings.Jolt\",\n  \"nativeLibrary\": \"RedBolt.Native.Jolt\",\n  \"requires\": [],\n  \"requirementClosure\": [],\n  \"artifacts\": [\"$<TARGET_FILE_NAME:redbolt-physics-jolt-native-jolt>\"]\n}\n")
endif()

add_custom_target(redbolt-physics-jolt-native-all DEPENDS redbolt-physics-jolt-native-jolt)
add_custom_target(redbolt-physics-jolt-android-native DEPENDS redbolt-physics-jolt-native-jolt)

if(EMSCRIPTEN)
    set(REDBOLT_PHYSICS_JOLT_WASM_ARCHIVE "${CMAKE_BINARY_DIR}/lib/Release/wasm/RedBolt.PhysicsJolt.Native.Web.a")
    set(REDBOLT_PHYSICS_JOLT_WASM_MRI "${CMAKE_BINARY_DIR}/redbolt-physics-jolt-wasm-archives.mri")
    file(GENERATE OUTPUT "${REDBOLT_PHYSICS_JOLT_WASM_MRI}" CONTENT
        "create ${REDBOLT_PHYSICS_JOLT_WASM_ARCHIVE}\naddlib $<TARGET_FILE:redbolt-physics-jolt-native-jolt>\naddlib $<TARGET_FILE:Jolt>\nsave\nend\n")
    add_custom_command(
        OUTPUT "${REDBOLT_PHYSICS_JOLT_WASM_ARCHIVE}"
        COMMAND "${CMAKE_COMMAND}"
            "-DREDBOLT_EMSCRIPTEN_ENV=${_redbolt_emscripten_environment}"
            "-DREDBOLT_ARCHIVER=${CMAKE_AR}"
            "-DREDBOLT_MRI_FILE=${REDBOLT_PHYSICS_JOLT_WASM_MRI}"
            "-DREDBOLT_OUTPUT_ARCHIVE=${REDBOLT_PHYSICS_JOLT_WASM_ARCHIVE}"
            -P "${REDBOLT_ROOT}/cmake/MergeStaticArchives.cmake"
        DEPENDS redbolt-physics-jolt-native-jolt Jolt
        VERBATIM)
    add_custom_target(redbolt-physics-jolt-wasm-native DEPENDS "${REDBOLT_PHYSICS_JOLT_WASM_ARCHIVE}")
endif()
