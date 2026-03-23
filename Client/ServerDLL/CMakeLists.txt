cmake_minimum_required(VERSION 3.20)
project(MarinerServer LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(VCPKG_TARGET_TRIPLET "x64-windows-static" CACHE STRING "")

find_path(DETOURS_INCLUDE_DIRS "detours/detours.h")
find_library(DETOURS_LIBRARY detours REQUIRED)

find_package(zydis CONFIG REQUIRED)

add_library(MarinerServer SHARED
    dllmain.cpp
)

target_include_directories(MarinerServer PRIVATE ${DETOURS_INCLUDE_DIRS})
target_link_libraries(MarinerServer PRIVATE ${DETOURS_LIBRARY} Zydis::Zydis)

# Static CRT, PDB for Release
set_target_properties(MarinerServer PROPERTIES
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)
target_compile_options(MarinerServer PRIVATE "$<$<CONFIG:Release>:/Zi>")
target_link_options(MarinerServer PRIVATE "$<$<CONFIG:Release>:/DEBUG>" "$<$<CONFIG:Release>:/OPT:REF>" "$<$<CONFIG:Release>:/OPT:ICF>")
