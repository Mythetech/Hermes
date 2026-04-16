# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- **Blazor hot reload** zero-config dev server for `dotnet watch` support, with CSS hot reload via SSE and automatic environment detection
- Cross-platform native desktop framework for .NET 10
- **Window management** with native window creation, sizing, positioning, and state persistence
- **WebView integration** WebView2 (Windows), WebKit (macOS), WebKitGTK (Linux)
- **Blazor integration** via `Hermes.Blazor` package with full component support
- **Native menus** menu bars, context menus, app menus, and dock menus (macOS)
- **Keyboard accelerators** with cross-platform key translation
- **Native dialogs** open file, save file, and message dialogs on all platforms
- **Custom URL scheme handlers** for intercepting WebView navigation
- **Custom titlebar support** with chromeless window mode and Blazor titlebar component
- **Window state persistence** remembers size and position across sessions
- **AOT compilation** support from day one
- **Native Linux library** (C/GTK) for menus, dialogs, and window management
- **Native macOS library** (Objective-C) for menus, dialogs, dock menus, and window management
- **Integration testing infrastructure** with `Hermes.Testing` package
- CI/CD with multi-platform builds, smoke tests, and NuGet publishing
- Documentation: README, ARCHITECTURE, SECURITY, COMMERCIAL, PLATFORM-DIFFERENCES, ROADMAP

### Fixed
- Window state saved at 1x1 pixel no longer used on restore, falls back to defaults
- GC handle crash on macOS and Linux when windows are collected
- Copy/paste support on macOS
- Blazor static asset content type handling and cache-busted asset resolution
- Windows DPI awareness and titlebar drag behavior
- Linux WebKitGTK 4.1 migration for broader distro support
