#!/bin/bash
# Build script for Hermes.Native.Linux

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"

echo "Building Hermes.Native.Linux..."

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake ..

# Build
make -j$(nproc)

echo ""
echo "Build complete!"
echo "Library: $SCRIPT_DIR/lib/libHermes.Native.Linux.so"
