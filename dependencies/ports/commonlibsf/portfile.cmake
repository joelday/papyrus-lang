# Pin: libxse/commonlibsf @ 6eeb9e60 (main, 2026-07-24).
# Why this pin, not Starfield-Reverse-Engineering/CommonLibSF:
#   - Design (Appendix C / P5.04a) names libxse/commonlibsf as the maintained successor.
#   - SRE/CommonLibSF is archived and still CMake-based; its Address Library IDs predate
#     the 1.15.216 remap (Nexus 3256). Using those IDs would silently miss every hook.
#   - libxse dropped CMake for xmake + the libxse/commonlib-shared submodule. This overlay
#     vendors the submodule at the gitlink recorded on that commit and injects a CMake
#     wrapper so the existing vcpkg/MSBuild DebugServer can consume the package.
# Do not invent Address Library IDs in this port; see docs/debug/sfse-spike.md.

vcpkg_from_github(
        OUT_SOURCE_PATH SOURCE_PATH
        REPO libxse/commonlibsf
        REF 6eeb9e60590db21b6f983d487b6b2d51a0fb6726
        SHA512 de27fcc4f24c33a0bacf91aff0ccca3b930be1f00ebf53e2e0a83a1a74b438528ce8dd7ce96de180205250936d83da67d8b714ebe3d6fd32de0f29d016556c10
        HEAD_REF main
)

# Submodule gitlink at the pin above: lib/commonlib-shared -> 5470284e964d5510aa001dca3e0bb5548b6356a4
vcpkg_from_github(
        OUT_SOURCE_PATH SHARED_SOURCE_PATH
        REPO libxse/commonlib-shared
        REF 5470284e964d5510aa001dca3e0bb5548b6356a4
        SHA512 55e2883d0d500c9be50daccbc9c1c60afba9ce024812feb6d571b429ee77a83e13a0ed07142f69d68b563081f02fe550416f156dc4a71f2e070db9a62fe3e262
        HEAD_REF main
)

file(REMOVE_RECURSE "${SOURCE_PATH}/lib/commonlib-shared")
file(COPY "${SHARED_SOURCE_PATH}/" DESTINATION "${SOURCE_PATH}/lib/commonlib-shared")

# Upstream is xmake-only; ship a CMake build so vcpkg_cmake_* matches the other CommonLib ports.
file(COPY "${CMAKE_CURRENT_LIST_DIR}/CMakeLists.txt" DESTINATION "${SOURCE_PATH}")
file(COPY "${CMAKE_CURRENT_LIST_DIR}/config.cmake.in" DESTINATION "${SOURCE_PATH}")

vcpkg_check_features(
        OUT_FEATURE_OPTIONS FEATURE_OPTIONS
        FEATURES
            xbyak SFSE_SUPPORT_XBYAK
)

vcpkg_cmake_configure(
        SOURCE_PATH "${SOURCE_PATH}"
        OPTIONS
            ${FEATURE_OPTIONS}
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup(PACKAGE_NAME CommonLibSF CONFIG_PATH lib/cmake/CommonLibSF)
vcpkg_copy_pdbs()

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")

file(INSTALL "${CMAKE_CURRENT_LIST_DIR}/usage" DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/COPYING" "${SOURCE_PATH}/EXCEPTIONS")
