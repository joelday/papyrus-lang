vcpkg_from_github(
  OUT_SOURCE_PATH SOURCE_PATH
  REPO libxse/commonlibf4
  REF 739a2ed87d28f4aac1d50527fae104a5eed91f69
  SHA512 81fa310de227226dccfc7750d0631c0aff17d0f3d7cc8d73db5f6671c2c297eb7545d4b28b2990281261e802256b3313f25cda580b2b3c767ef21e7e8681f396
  HEAD_REF main
)

if(VCPKG_HOST_IS_WINDOWS AND VCPKG_TARGET_IS_WINDOWS AND NOT VCPKG_TARGET_IS_MINGW AND DEFINED $ENV{VisualStudioVersion})
  set(MSVC_RUNTIME_VCPKG_CONFIG_ARG "-DCMAKE_MSVC_RUNTIME_LIBRARY=\"MultiThreaded$<$<CONFIG:Debug>:Debug>DLL\"")

  # 2022 requires `/external:anglebrackets` to build with the above argument
  if($ENV{VisualStudioVersion} STREQUAL "17.0")
    set(CXX_FLAGS_VCPKG_CONFIG_ARG "-DCMAKE_CXX_FLAGS=\"/EHsc /MP /W4 /WX /external:anglebrackets /external:W0\"")
  else()
    set(CXX_FLAGS_VCPKG_CONFIG_ARG "-DCMAKE_CXX_FLAGS=\"/EHsc /MP /W4 /WX /external:W0\"")
  endif()
else()
  set(MSVC_RUNTIME_VCPKG_CONFIG_ARG "")
  set(CXX_FLAGS_VCPKG_CONFIG_ARG "")
endif()

vcpkg_cmake_configure(
  SOURCE_PATH "${SOURCE_PATH}"
  OPTIONS
    -DF4SE_SUPPORT_XBYAK=ON
    -DCMAKE_EXE_LINKER_FLAGS_RELEASE="/DEBUG:FASTLINK"
    ${MSVC_RUNTIME_VCPKG_CONFIG_ARG}
    ${CXX_FLAGS_VCPKG_CONFIG_ARG}
)

vcpkg_cmake_install()
vcpkg_copy_pdbs()
vcpkg_cmake_config_fixup(
  PACKAGE_NAME CommonLibF4
  CONFIG_PATH "lib/cmake/CommonLibF4"
)
file(GLOB CMAKE_CONFIGS "${CURRENT_PACKAGES_DIR}/share/CommonLibF4/CommonLibF4/*.cmake")
file(INSTALL ${CMAKE_CONFIGS} DESTINATION "${CURRENT_PACKAGES_DIR}/share/CommonLibF4")

file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/share/CommonLibF4/CommonLibF4")

# file(MAKE_DIRECTORY "${CURRENT_PACKAGES_DIR}/include/CommonLibF4")
# file(RENAME "${CURRENT_PACKAGES_DIR}/include/F4SE" "${CURRENT_PACKAGES_DIR}/include/CommonLibF4/F4SE")
# file(RENAME "${CURRENT_PACKAGES_DIR}/include/RE" "${CURRENT_PACKAGES_DIR}/include/CommonLibF4/RE")
# file(RENAME "${CURRENT_PACKAGES_DIR}/include/REL" "${CURRENT_PACKAGES_DIR}/include/CommonLibF4/REL")
file(
  INSTALL "${SOURCE_PATH}/LICENSE"
  DESTINATION "${CURRENT_PACKAGES_DIR}/share/${PORT}"
  RENAME copyright)
