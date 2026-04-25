# Hermes Roadmap

This document tracks planned features and improvements for the Hermes framework.

---

## High Priority

### Native Notifications

**Status:** Not started
**Platforms:** All

Desktop notifications that integrate with each platform's notification center.

| Platform | API                                                                 |
| -------- | ------------------------------------------------------------------- |
| macOS    | NSUserNotificationCenter / UNUserNotificationCenter (macOS 10.14+)  |
| Windows  | ToastNotificationManager (Windows 10+) or Shell_NotifyIcon balloons |
| Linux    | libnotify / D-Bus org.freedesktop.Notifications                     |

**Features needed:**

- [ ] Show simple notification (title + body)
- [ ] Notification with icon
- [ ] Notification click handling
- [ ] Action buttons
- [ ] Notification categories/channels

---

### Clipboard API

**Status:** In progress (text complete)
**Platforms:** All

Programmatic clipboard access for copy/paste operations.

| Platform | API                                                 |
| -------- | --------------------------------------------------- |
| macOS    | NSPasteboard                                        |
| Windows  | OpenClipboard / GetClipboardData / SetClipboardData |
| Linux    | GtkClipboard                                        |

**Features needed:**

- [x] Get/set text
- [ ] Get/set HTML
- [ ] Get/set image
- [ ] Clipboard change monitoring

---

### Hermes.Web - JavaScript/TypeScript SPA Support

**Status:** In progress (core implemented, packaging remaining)
**Platforms:** All

Enable React, Vue, Angular, Svelte and other JS/TS frameworks as an alternative to Blazor. The core infrastructure already supports this - the `window.external` bridge is injected at the native level.

**Research & API Design:** [docs/plans/SpaAlternatives.md](docs/plans/SpaAlternatives.md)

**New package:** `Hermes.Web` providing:

- [x] Static file serving with SPA fallback (`UseStaticFiles()`, `UseSpaFallback()`)
- [x] Dev server proxy for Vite/Webpack HMR (`UseDevServer()`)
- [x] Auto-detect mode (dev server if running, else static files)
- [x] Type-safe C# ↔ JS interop bridge (`InteropBridge`)
- [ ] TypeScript definitions package (`@hermes/bridge`)
- [ ] Sample apps (React, Vue, Angular, Svelte)

**Enterprise value:**

- Broader developer pool (React/Vue/Angular >> Blazor adoption)
- Electron migration path for existing web apps
- Smaller bundle size (no Blazor runtime)
- Mature JS tooling ecosystem (Vite, ESLint, etc.)

---

## Medium Priority

### Global Hotkeys

**Status:** Not started
**Platforms:** All

Register keyboard shortcuts that work even when the app is not focused.

| Platform | API                                      |
| -------- | ---------------------------------------- |
| macOS    | CGEventTap or Carbon RegisterEventHotKey |
| Windows  | RegisterHotKey                           |
| Linux    | XGrabKey (X11)                           |

---

### Drag & Drop

**Status:** WebView only
**Platforms:** All

Native drag and drop support for files and data.

**Features needed:**

- [ ] Drop files onto window
- [ ] Drag files from window
- [ ] Custom drag data types

---

### Window Transparency

**Status:** Not started
**Platforms:** All

Transparent and translucent window backgrounds for overlay/HUD applications.

---

## Low Priority

### macOS-Specific

- [ ] **Touch Bar support** - For MacBook Pro
- [ ] **Handoff/Continuity** - Apple ecosystem integration
- [ ] **Native full screen mode** - macOS full screen with separate space

### Windows-Specific

- [ ] **Jump Lists** - Taskbar right-click recent items
- [ ] **Taskbar progress** - Progress indicator in taskbar icon
- [ ] **Window frame customization** - Custom title bar drawing

### Linux-Specific

- [ ] **Wayland support** - Currently X11-focused via GTK
- [ ] **XDG Desktop Entry** - Proper .desktop file integration
- [ ] **D-Bus integration** - For system services

---

## Completed

- [x] Window management (all platforms)
- [x] Chromeless windows / custom titlebar (all platforms, recommended for polished cross-platform UX)
- [x] WebView integration (all platforms)
- [x] Native menus with accelerators
- [x] Context menus
- [x] File/folder dialogs
- [x] Message dialogs
- [x] Custom URL schemes
- [x] JavaScript bridge (window.external)
- [x] Windows accelerator enforcement
- [x] Cross-platform error logging
- [x] OS information (platform, version, architecture, locale)
- [x] Window state persistence (position, size, maximized state)
- [x] Crash reporting callback
- [x] System tray / status bar support (all platforms)
- [x] Key-value store (persistent, type-safe, JSON-backed)
- [x] Blazor hot reload via internal dev server (`dotnet watch` auto-detected, CSS hot reload via SSE)
- [x] Single instance support (all platforms)
- [x] Opener / external app launcher (all platforms)
- [x] Autostart / launch at login (all platforms)
- [x] macOS dock menu customization

---

## Contributing

When implementing a feature from this roadmap:

1. Create a new interface in `src/Hermes/Abstractions/` (e.g., `ITrayBackend.cs`)
2. Implement platform backends in `src/Hermes/Platforms/{Windows,Linux,macOS}/`
3. For macOS, add native code in `src/Hermes.Native.macOS/` if needed
4. Add tests in `tests/Hermes.Tests/`
5. Update `PLATFORM-DIFFERENCES.md` with any platform-specific behavior
6. Move the item to "Completed" in this file
