# GTA Classic Perspective Lock - Android native build
# Builds SDL2, OpenAL Soft, Box2D and the (perspective-lock patched) Carnage3D engine into libmain.so
cmake_minimum_required(VERSION 3.18)
project(gtaperspectivelock C CXX)

set(ENGINES_DIR "${CMAKE_CURRENT_LIST_DIR}/../../../engines" CACHE PATH "Folder holding carnage3d/ and android-deps/")
get_filename_component(ENGINES_DIR "${ENGINES_DIR}" ABSOLUTE)
set(C3D_DIR  "${ENGINES_DIR}/carnage3d")
set(DEPS_DIR "${ENGINES_DIR}/android-deps")

# SDL2 (window, GLES context, input, Android glue)
set(SDL_SHARED ON CACHE BOOL "" FORCE)
set(SDL_STATIC OFF CACHE BOOL "" FORCE)
set(SDL_TEST OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
add_subdirectory("${DEPS_DIR}/SDL2" SDL2)

# OpenAL Soft (audio)
set(ALSOFT_UTILS OFF CACHE BOOL "" FORCE)
set(ALSOFT_EXAMPLES OFF CACHE BOOL "" FORCE)
set(ALSOFT_TESTS OFF CACHE BOOL "" FORCE)
set(ALSOFT_INSTALL OFF CACHE BOOL "" FORCE)
set(ALSOFT_INSTALL_CONFIG OFF CACHE BOOL "" FORCE)
set(ALSOFT_INSTALL_HRTF_DATA OFF CACHE BOOL "" FORCE)
set(ALSOFT_INSTALL_AMBDEC_PRESETS OFF CACHE BOOL "" FORCE)
set(ALSOFT_NO_CONFIG_UTIL ON CACHE BOOL "" FORCE)
add_subdirectory("${DEPS_DIR}/openal-soft" openal-soft)

# Box2D (physics, bundled with Carnage3D)
set(BOX2D_BUILD_UNIT_TESTS OFF CACHE BOOL "" FORCE)
set(BOX2D_BUILD_TESTBED OFF CACHE BOOL "" FORCE)
set(BOX2D_BUILD_DOCS OFF CACHE BOOL "" FORCE)
add_subdirectory("${C3D_DIR}/third_party/Box2D" box2d)

# Carnage3D engine (+ Android GLFW-on-SDL2 platform layer)
# src/CMakeLists.txt sets CARNAGE3D_SRC with PARENT_SCOPE, so load it from a function
function(c3d_load_sources)
    include("${C3D_DIR}/src/CMakeLists.txt")
endfunction()
c3d_load_sources()
list(LENGTH CARNAGE3D_SRC C3D_SRC_COUNT)
message(STATUS "Carnage3D sources: ${C3D_SRC_COUNT}")
if(C3D_SRC_COUNT LESS 50)
    message(FATAL_ERROR "Carnage3D source list not loaded")
endif()
add_library(main SHARED ${CARNAGE3D_SRC} "${C3D_DIR}/src/android/glfw_sdl_shim.cpp")
set_target_properties(main PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED ON)
target_compile_options(main PRIVATE -w -fno-strict-aliasing)
target_include_directories(main PRIVATE
    "${C3D_DIR}/third_party/Box2D/include"
    "${DEPS_DIR}/glm"
    "${DEPS_DIR}/openal-soft/include")
target_link_libraries(main PRIVATE SDL2 OpenAL box2d GLESv3 EGL android log m)
