cmake_minimum_required(VERSION 3.20)
project(MarinerClient LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(VCPKG_TARGET_TRIPLET "x64-windows-static" CACHE STRING "")

find_path(DETOURS_INCLUDE_DIRS "detours/detours.h")
find_library(DETOURS_LIBRARY detours REQUIRED)

find_package(zydis CONFIG REQUIRED)

add_library(MarinerClient SHARED
    dllmain.cpp
)

target_include_directories(MarinerClient PRIVATE ${DETOURS_INCLUDE_DIRS})
target_link_libraries(MarinerClient PRIVATE ${DETOURS_LIBRARY} Zydis::Zydis)

set_target_properties(MarinerClient PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)
