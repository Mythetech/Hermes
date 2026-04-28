# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-04-28

### Added
- Cross-platform native desktop framework for .NET 10
- **Window management** with native window creation, sizing, positioning, and state persistence
- **WebView integration** across WebView2 (Windows), WebKit (macOS), and WebKitGTK (Linux)
- **Blazor integration** via `Hermes.Blazor` package with full component support and dependency injection
- **SPA framework support** via `Hermes.Web` (preview) with React, Svelte, and vanilla JS integration
- **Blazor hot reload** with zero-config dev server for `dotnet watch`, CSS hot reload via SSE, and automatic environment detection
- **Native menus** including menu bars, context menus, app menus, and dock menus (macOS)
- **Keyboard accelerators** with cross-platform key translation (Cmd/Ctrl, Alt/Option, Meta/Win)
- **Native dialogs** for open file, save file, and message dialogs on all platforms
- **System tray / status icon** support on all platforms with context menus and click handling
- **Custom URL scheme handlers** for intercepting WebView navigation
- **Custom titlebar support** with chromeless window mode and Blazor titlebar component
- **Window state persistence** that remembers size, position, and maximized state across sessions
- **JavaScript interop bridge** for bidirectional communication between native and web layers
- **Clipboard API** with both static and DI-based access patterns
- **Key-value store** for persistent JSON-backed application storage
- **Single instance** enforcement with argument forwarding to existing instance
- **Autostart** registration on Windows (Registry), macOS (LaunchAgent), and Linux (XDG Desktop Entry)
- **External opener** for launching URLs, files, and folders in the OS default handler
- **Crash reporting hooks** with exception context, platform info, and session tracking
- **Session management** with unique session IDs and metadata
- **Window close cancellation** with async confirmation support
- **Licensing** with Ed25519 signature validation (Elastic License 2.0)
- **AOT compilation** support from day one
- **Native Linux library** (C/GTK) for menus, dialogs, and window management
- **Native macOS library** (Objective-C) for menus, dialogs, dock menus, and window management
- **Integration testing infrastructure** with `Hermes.Testing` package providing mock backends, recording, and assertions
- CI/CD with multi-platform builds, smoke tests, and NuGet publishing

### Fixed
- Window state saved at 1x1 pixel no longer used on restore, falls back to defaults
- GC handle crash on macOS and Linux when windows are collected
- Copy/paste support on macOS
- Blazor static asset content type handling and cache-busted asset resolution
- Windows DPI awareness and titlebar drag behavior
- Linux WebKitGTK 4.1 migration for broader distro support
