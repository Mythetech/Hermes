# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

- **Native notifications** on all platforms via `HermesApplication.Notifications` and the `INativeNotifications` DI contract in `Hermes.Blazor`. Title, body, icon, silent flag, and a `Clicked` event carrying an app-supplied `Tag` for navigation. macOS uses UNUserNotificationCenter (bundle required), Windows uses WinRT toasts through hand-rolled COM with no new packages, Linux uses `org.freedesktop.Notifications` over GDBus with no new native dependencies. Unsupported hosts are a logged no-op, never an exception
- `HermesApplication.ConfigureNotifications` for the app id, display name and icon used by the platform notification center
- `NotificationsDemo` sample with a macOS bundling script for manual verification

## [1.3.1] - 2026-09-11

### Fixed

- `Hermes.Blazor` now depends on `Microsoft.AspNetCore.Components.WebView` `11.0.0-rc.1.26425.128`. Its `FrameworkReference` to `Microsoft.AspNetCore.App` puts consuming apps on the .NET 11 shared framework, so the `Components.Web` runtime was already the RC1 build while the 10.0.12 WebView package embedded the 10.x `blazor.webview.js`. Any new JavaScript interop entry point failed at first render; `Virtualize` crashed every app that rendered one with `Blazor._internal.Virtualize.setAnchorMode is not a function`. The 1.3.0 changelog entry described this alignment, but the published package still shipped the 10.0.12 dependency
- `NU5104` is suppressed for `Hermes.Blazor` until the 11.0.0 GA package replaces the RC build

## [1.3.0] - 2026-09-09

### Changed

- Retargeted all projects to `net11.0` on the .NET 11 RC1 SDK, pinned via `global.json`
- CI resolves the SDK from `global.json` instead of a hardcoded `dotnet-version`
- `Microsoft.AspNetCore.Components.WebView` and the WebAssembly packages moved to `11.0.0-rc.1.26425.128`
- Removed the explicit `Microsoft.SourceLink.GitHub` references; the .NET SDK supplies SourceLink implicitly, and the floating `8.*` range pulled in a `Microsoft.Build.Tasks.Git` version with a published advisory (NU1902)

### Fixed

- `SingleInstanceGuard.Dispose()` no longer throws when the mutex is released from a thread that does not own it. .NET 11 raises `InvalidOperationException` where earlier runtimes raised `ApplicationException`, and only the latter was handled

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
