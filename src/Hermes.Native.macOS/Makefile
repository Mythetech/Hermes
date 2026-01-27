# Hermes.Native.macOS Makefile
# Builds a universal (arm64 + x86_64) dynamic library

# Use xcrun to find the correct compiler and SDK
CC = xcrun clang

# Frameworks
FRAMEWORKS = -framework Cocoa -framework WebKit -framework UniformTypeIdentifiers

# Compiler flags
# Note: Some SDK versions have format_arg issues in headers, suppress these warnings
CFLAGS = -Wall -fPIC -O2 -Wno-format-nonliteral
OBJCFLAGS = $(CFLAGS) -fobjc-arc

# Minimum macOS version (macOS 12 Monterey or later)
MIN_VERSION = -mmacosx-version-min=12.0

# Architecture flags
ARCH_ARM64 = -arch arm64
ARCH_X86_64 = -arch x86_64

# Source files
SOURCES = HermesWindow.m \
          HermesWindowDelegate.m \
          HermesUrlSchemeHandler.m \
          HermesUiDelegate.m \
          HermesMenu.m \
          HermesDialogs.m \
          HermesContextMenu.m \
          Exports.m

# Object directories
OBJ_ARM64 = obj/arm64
OBJ_X86_64 = obj/x86_64

# Output files
OUTPUT_ARM64 = lib/arm64/libHermes.Native.macOS.dylib
OUTPUT_X86_64 = lib/x86_64/libHermes.Native.macOS.dylib
OUTPUT_UNIVERSAL = lib/libHermes.Native.macOS.dylib

# Default target
.PHONY: all clean arm64 x86_64 universal

all: universal

# Create directories
$(OBJ_ARM64):
	@mkdir -p $(OBJ_ARM64)

$(OBJ_X86_64):
	@mkdir -p $(OBJ_X86_64)

lib/arm64:
	@mkdir -p lib/arm64

lib/x86_64:
	@mkdir -p lib/x86_64

lib:
	@mkdir -p lib

# Compile arm64 objects
$(OBJ_ARM64)/%.o: %.m | $(OBJ_ARM64)
	$(CC) $(OBJCFLAGS) $(ARCH_ARM64) $(MIN_VERSION) -c $< -o $@

# Compile x86_64 objects
$(OBJ_X86_64)/%.o: %.m | $(OBJ_X86_64)
	$(CC) $(OBJCFLAGS) $(ARCH_X86_64) $(MIN_VERSION) -c $< -o $@

# Build arm64 dylib
arm64: $(addprefix $(OBJ_ARM64)/, $(SOURCES:.m=.o)) | lib/arm64
	$(CC) $(OBJCFLAGS) $(ARCH_ARM64) $(MIN_VERSION) -dynamiclib \
		-install_name @rpath/libHermes.Native.macOS.dylib \
		$(FRAMEWORKS) \
		$^ -o $(OUTPUT_ARM64)
	@echo "Built arm64 library: $(OUTPUT_ARM64)"

# Build x86_64 dylib
x86_64: $(addprefix $(OBJ_X86_64)/, $(SOURCES:.m=.o)) | lib/x86_64
	$(CC) $(OBJCFLAGS) $(ARCH_X86_64) $(MIN_VERSION) -dynamiclib \
		-install_name @rpath/libHermes.Native.macOS.dylib \
		$(FRAMEWORKS) \
		$^ -o $(OUTPUT_X86_64)
	@echo "Built x86_64 library: $(OUTPUT_X86_64)"

# Create universal binary
universal: arm64 x86_64 | lib
	lipo -create $(OUTPUT_ARM64) $(OUTPUT_X86_64) -output $(OUTPUT_UNIVERSAL)
	@echo "Built universal library: $(OUTPUT_UNIVERSAL)"
	@file $(OUTPUT_UNIVERSAL)

# Clean build artifacts
clean:
	rm -rf obj lib
	@echo "Cleaned build artifacts"

# Install to system (optional)
install: universal
	cp $(OUTPUT_UNIVERSAL) /usr/local/lib/
	@echo "Installed to /usr/local/lib/"

# Show help
help:
	@echo "Hermes.Native.macOS Build System"
	@echo ""
	@echo "Targets:"
	@echo "  all       - Build universal binary (default)"
	@echo "  arm64     - Build arm64 only"
	@echo "  x86_64    - Build x86_64 only"
	@echo "  universal - Build universal binary"
	@echo "  clean     - Remove build artifacts"
	@echo "  install   - Install to /usr/local/lib/"
	@echo "  help      - Show this help"
