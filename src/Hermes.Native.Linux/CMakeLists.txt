# Hermes.Native.Linux CMake Build
# Builds libHermes.Native.Linux.so for GTK3 + WebKit2GTK integration

cmake_minimum_required(VERSION 3.16)
project(Hermes.Native.Linux VERSION 1.0.0 LANGUAGES C)

# Require GTK3 and WebKit2GTK 4.1
find_package(PkgConfig REQUIRED)
pkg_check_modules(GTK3 REQUIRED gtk+-3.0)
pkg_check_modules(WEBKIT2GTK REQUIRED webkit2gtk-4.1)
pkg_check_modules(SOUP REQUIRED libsoup-3.0)

# Source files
set(SOURCES
    HermesWindow.c
    HermesMenu.c
    HermesDialogs.c
    HermesContextMenu.c
    Exports.c
)

# Create shared library
add_library(Hermes.Native.Linux SHARED ${SOURCES})

# Include directories
target_include_directories(Hermes.Native.Linux PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${GTK3_INCLUDE_DIRS}
    ${WEBKIT2GTK_INCLUDE_DIRS}
    ${SOUP_INCLUDE_DIRS}
)

# Link libraries
target_link_libraries(Hermes.Native.Linux PRIVATE
    ${GTK3_LIBRARIES}
    ${WEBKIT2GTK_LIBRARIES}
    ${SOUP_LIBRARIES}
)

# Compiler flags
target_compile_options(Hermes.Native.Linux PRIVATE
    ${GTK3_CFLAGS_OTHER}
    ${WEBKIT2GTK_CFLAGS_OTHER}
    ${SOUP_CFLAGS_OTHER}
    -Wall
    -Wextra
    -O2
    -fPIC
)

# Set output name and properties
set_target_properties(Hermes.Native.Linux PROPERTIES
    OUTPUT_NAME "Hermes.Native.Linux"
    PREFIX "lib"
    SOVERSION 1
    VERSION ${PROJECT_VERSION}
)

# Copy library to lib/ directory after build
add_custom_command(TARGET Hermes.Native.Linux POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/lib
    COMMAND ${CMAKE_COMMAND} -E copy
        $<TARGET_FILE:Hermes.Native.Linux>
        ${CMAKE_CURRENT_SOURCE_DIR}/lib/libHermes.Native.Linux.so
    COMMENT "Copying library to lib/ directory"
)

# Install target
install(TARGETS Hermes.Native.Linux
    LIBRARY DESTINATION lib
)

# Print configuration info
message(STATUS "GTK3 version: ${GTK3_VERSION}")
message(STATUS "WebKit2GTK version: ${WEBKIT2GTK_VERSION}")
message(STATUS "libsoup version: ${SOUP_VERSION}")
