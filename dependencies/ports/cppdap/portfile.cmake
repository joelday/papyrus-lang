vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO google/cppdap
  REF 59819690ec4114b01aae73b4caf22650f029ce53
  SHA512 88e381a9d41a1510e8e0afe9d0b6fd62ab834cecbdd91a042464a1003847fdeee2c9f94c9912a9ab2538140894ec965be96dda954edff81818c42c8d0a8846c1
  HEAD_REF main
)

vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
  FEATURES
  use-nlohmann-json CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE
  use-rapidjson CPPDAP_USE_EXTERNAL_RAPIDJSON_PACKAGE
)

if(NOT CPPDAP_USE_EXTERNAL_NLOHMANN_JSON_PACKAGE AND NOT CPPDAP_USE_EXTERNAL_RAPIDJSON_PACKAGE)
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
