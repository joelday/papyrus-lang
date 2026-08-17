# CMake wrapper for libxse/commonlibsf (upstream is xmake-only as of 6eeb9e60).
# Merges lib/commonlib-shared (REL/REX) into one static lib so find_package(CommonLibSF)
# matches the other overlay CommonLib ports.

cmake_minimum_required(VERSION 3.21)

if(TARGET CommonLibSF)
  return()
endif()

option(REX_OPTION_INI "Enables ini config support for REX." OFF)
option(REX_OPTION_JSON "Enables json config support for REX." OFF)
option(REX_OPTION_TOML "Enables toml config support for REX." OFF)
option(SFSE_SUPPORT_XBYAK "Enables trampoline support for xbyak." OFF)

project(CommonLibSF LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(GNUInstallDirs)

if(PROJECT_SOURCE_DIR STREQUAL PROJECT_BINARY_DIR)
  message(FATAL_ERROR "In-source builds are not allowed.")
endif()

find_package(spdlog CONFIG REQUIRED)
if(SFSE_SUPPORT_XBYAK)
  find_package(xbyak CONFIG REQUIRED)
endif()

set(COMMONLIB_SHARED_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/lib/commonlib-shared")

file(GLOB_RECURSE SOURCES CONFIGURE_DEPENDS
  "${CMAKE_CURRENT_SOURCE_DIR}/include/*.h"
  "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp"
  "${COMMONLIB_SHARED_ROOT}/include/*.h"
  "${COMMONLIB_SHARED_ROOT}/src/*.cpp"
)
list(FILTER SOURCES EXCLUDE REGEX ".*/SFSE/Impl/PCH\\.h$")
list(FILTER SOURCES EXCLUDE REGEX ".*/REX/PCH\\.h$")

add_library(${PROJECT_NAME} STATIC ${SOURCES})
add_library(${PROJECT_NAME}::${PROJECT_NAME} ALIAS ${PROJECT_NAME})

target_compile_definitions(
  ${PROJECT_NAME}
  PUBLIC
    WINVER=0x0A00
    _WIN32_WINNT=0x0A00
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    "$<$<BOOL:${REX_OPTION_INI}>:COMMONLIB_OPTION_INI=1>"
    "$<$<BOOL:${REX_OPTION_JSON}>:COMMONLIB_OPTION_JSON=1>"
    "$<$<BOOL:${REX_OPTION_TOML}>:COMMONLIB_OPTION_TOML=1>"
    "$<$<BOOL:${SFSE_SUPPORT_XBYAK}>:COMMONLIB_OPTION_XBYAK=1>"
    "$<$<BOOL:${SFSE_SUPPORT_XBYAK}>:SFSE_SUPPORT_XBYAK=1>"
)

target_include_directories(
  ${PROJECT_NAME}
  PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<BUILD_INTERFACE:${COMMONLIB_SHARED_ROOT}/include>
    $<INSTALL_INTERFACE:${CMAKE_INSTALL_INCLUDEDIR}>
)

target_precompile_headers(
  ${PROJECT_NAME}
  PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/include/SFSE/Impl/PCH.h"
)

target_link_libraries(
  ${PROJECT_NAME}
  PUBLIC
    spdlog::spdlog
)
if(SFSE_SUPPORT_XBYAK)
  target_link_libraries(${PROJECT_NAME} PUBLIC xbyak::xbyak)
endif()

if(MSVC)
  target_compile_options(
    ${PROJECT_NAME}
    PUBLIC
      /bigobj
      /wd4005
      /wd4068
      /wd4200
      /wd4201
      /wd4324
  )
  target_link_libraries(
    ${PROJECT_NAME}
    PUBLIC
      advapi32
      bcrypt
      dbghelp
      dxgi
      ole32
      version
      ws2_32
  )
endif()

install(
  TARGETS ${PROJECT_NAME}
  EXPORT ${PROJECT_NAME}-targets
  ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
)

install(
  EXPORT ${PROJECT_NAME}-targets
  NAMESPACE ${PROJECT_NAME}::
  DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/${PROJECT_NAME}
)

configure_file(
  ${CMAKE_CURRENT_SOURCE_DIR}/config.cmake.in
  ${CMAKE_CURRENT_BINARY_DIR}/${PROJECT_NAME}Config.cmake
  @ONLY
)

install(
  FILES ${CMAKE_CURRENT_BINARY_DIR}/${PROJECT_NAME}Config.cmake
  DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/${PROJECT_NAME}
)

install(
  DIRECTORY
    include/RE
    include/SFSE
    ${COMMONLIB_SHARED_ROOT}/include/REL
    ${COMMONLIB_SHARED_ROOT}/include/REX
  DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)
