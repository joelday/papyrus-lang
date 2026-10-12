vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO Orvid/Champollion
  REF 83d7f92481c797f2732fcaed016c24f9fcfb3eba
  SHA512 2ffd56fc9f5ec98444124d60e86771e7e5aa1f68d27bd561bcffe0d5b04ac98913e656a50131a28178d39231404bb2502737bf56c07592867b871f372fcdce95
  HEAD_REF master
)

# Check if one or more features are a part of a package installation.
# See /docs/maintainers/vcpkg_check_features.md for more details
vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
  INVERTED_FEATURES
  standalone CHAMPOLLION_STATIC_LIBRARY
)
vcpkg_cmake_configure(
  SOURCE_PATH "${SOURCE_PATH}"
  OPTIONS
  ${FEATURE_OPTIONS}
)

vcpkg_cmake_install()

if(${CHAMPOLLION_STATIC_LIBRARY})
  vcpkg_cmake_config_fixup(PACKAGE_NAME Champollion CONFIG_PATH lib/cmake/Champollion)
endif()

file(
  INSTALL "${SOURCE_PATH}/LICENSE"
  DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}"
  RENAME copyright)
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
