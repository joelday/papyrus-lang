vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO nikitalita/cppdap
  REF d1b698f67abc0da9af072e9d054e2761481c4f86
  SHA512 fe12ec3bc3de1f02e6669fd637b86bb07c9878fc012d1d7d28efeb3471e6936956e89de6b31325c70ff86b20bb45627c654f99c92ad276d8512838a19aef17f8
  HEAD_REF cmake-install
)
# Check if one or more features are a part of a package installation.
# See /docs/maintainers/vcpkg_check_features.md for more details
vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
  FEATURES
    use-nlohmann-json CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE
    rapidjson CPPDAP_USE_EXTERNAL_RAPID_JSON_PACKAGE
)

vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
    OPTIONS
        ${FEATURE_OPTIONS}
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup(PACKAGE_NAME cppdap CONFIG_PATH lib/cmake/cppdap)
file(
        INSTALL "${SOURCE_PATH}/LICENSE"
        DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}"
        RENAME copyright)
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
