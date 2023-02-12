vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO google/cppdap
  REF 0a340c6d71ca00893ca1aefea38f3504e6755196
  SHA512 14f8d0438678eb715f171b95ed9733485bdb681bc174642f196b7665aad3157b500ba6e6abccc5adcb8ec17aeffa94ae562dcbd31248244d9c8f4a11b97fc2ea
  HEAD_REF main
)

vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
  FEATURES
    use-nlohmann-json CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE
    use-rapidjson CPPDAP_USE_EXTERNAL_RAPIDJSON_PACKAGE
)

if (NOT CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE AND NOT CPPDAP_USE_EXTERNAL_RAPIDJSON_PACKAGE)
  message(FATAL_ERROR "Must set either \"use-nlohmann-json\" or \"use-rapidjson\" feature.")
elseif(CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE AND CPPDAP_USE_EXTERNAL_RAPIDJSON_PACKAGE)
  message(FATAL_ERROR "Cannot set both \"use-nlohmann-json\" and \"use-rapidjson\" feature.")
endif()

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
