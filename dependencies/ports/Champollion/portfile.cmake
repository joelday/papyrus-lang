vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO Orvid/Champollion
  REF v1.1.0
  SHA512 a95a2f82f27da2ac71fed156e32b824ef2897400c64cf3fd2221a367b61a6cc5bd224706868a9dc8ddd5f6c0054a10943d759cc170bebadca42a58384f8d0eef
  HEAD_REF master
)
# Check if one or more features are a part of a package installation.
# See /docs/maintainers/vcpkg_check_features.md for more details
vcpkg_check_features(OUT_FEATURE_OPTIONS FEATURE_OPTIONS
  INVERTED_FEATURES
	  standalone   CHAMPOLLION_STATIC_LIBRARY
)
vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}"
		OPTIONS
      ${FEATURE_OPTIONS}
)

vcpkg_cmake_install()
if (${CHAMPOLLION_STATIC_LIBRARY})
  vcpkg_cmake_config_fixup(PACKAGE_NAME Champollion CONFIG_PATH lib/cmake/Champollion)
endif()
file(
        INSTALL "${SOURCE_PATH}/LICENSE"
        DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}"
        RENAME copyright)
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
