# This is temporary until my changes are merged upstream

vcpkg_from_github(
        OUT_SOURCE_PATH SOURCE_PATH
        REPO nikitalita/CommonLibSSE
        REF c1e41a52c09e130ab45a5b3e6c40fd3fea184156
        SHA512 fdbcd39b9d4de00081c00e8d1d72dc40e301481d98c3404ecb539fefe03e695b78345d624141e6d4e630c4ef2155d12bf9ecea0bdcec5dbb551704cd4ec0c50f
        HEAD_REF nikita-ng-working
)

# set(SOURCE_PATH "F:/workspace/skyrim-mod-workspace/CommonLibSSE-NG")
vcpkg_configure_cmake(
        SOURCE_PATH "${SOURCE_PATH}"
        PREFER_NINJA
        OPTIONS -DBUILD_TESTS=off -DSKSE_SUPPORT_XBYAK=on
)

vcpkg_install_cmake()
vcpkg_cmake_config_fixup(PACKAGE_NAME CommonLibSSE CONFIG_PATH lib/cmake)
vcpkg_copy_pdbs()

file(GLOB CMAKE_CONFIGS "${CURRENT_PACKAGES_DIR}/share/CommonLibSSE/CommonLibSSE/*.cmake")
file(INSTALL ${CMAKE_CONFIGS} DESTINATION "${CURRENT_PACKAGES_DIR}/share/CommonLibSSE")
file(INSTALL "${SOURCE_PATH}/cmake/CommonLibSSE.cmake" DESTINATION "${CURRENT_PACKAGES_DIR}/share/CommonLibSSE")

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/share/CommonLibSSE/CommonLibSSE")

file(
        INSTALL "${SOURCE_PATH}/LICENSE"
        DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}"
        RENAME copyright)
