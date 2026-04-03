# Hermes Roadmap

This document tracks planned features and improvements for the Hermes framework.

---

## High Priority

### System Tray / Status Bar Support

**Status:** Not started
**Platforms:** All

Native system tray integration for background applications.

| Platform | API                                          |
| -------- | -------------------------------------------- |
| macOS    | NSStatusItem / NSStatusBar                   |
| Windows  | Shell_NotifyIcon (NOTIFYICONDATA)            |
| Linux    | libappindicator / GtkStatusIcon (deprecated) |

**Features needed:**

- [ ] Show/hide tray icon
- [ ] Set icon image
- [ ] Set tooltip
- [ ] Tray icon click handling
- [ ] Context menu on tray icon

---

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

**Status:** Not started
**Platforms:** All

Programmatic clipboard access for copy/paste operations.

| Platform | API                                                 |
| -------- | --------------------------------------------------- |
| macOS    | NSPasteboard                                        |
| Windows  | OpenClipboard / GetClipboardData / SetClipboardData |
| Linux    | GtkClipboard                                        |

**Features needed:**

- [ ] Get/set text
- [ ] Get/set HTML
- [ ] Get/set image
- [ ] Clipboard change monitoring

---

### Hermes.Web - JavaScript/TypeScript SPA Support

**Status:** Research complete, ready for implementation
**Platforms:** All

Enable React, Vue, Angular, Svelte and other JS/TS frameworks as an alternative to Blazor. The core infrastructure already supports this - the `window.external` bridge is injected at the native level.

**Research & API Design:** [docs/plans/SpaAlternatives.md](docs/plans/SpaAlternatives.md)

**New package:** `Hermes.Web` providing:

- [ ] Static file serving with SPA fallback (`UseStaticFiles()`, `UseSpaFallback()`)
- [ ] Dev server proxy for Vite/Webpack HMR (`UseDevServer()`)
- [ ] Auto-detect mode (dev server if running, else static files)
- [ ] Type-safe C# ↔ JS interop bridge (`InteropBridge`)
- [ ] TypeScript definitions package (`@hermes/bridge`)
- [ ] Sample apps (React, Vue, Angular, Svelte)

**Enterprise value:**

- Broader developer pool (React/Vue/Angular >> Blazor adoption)
- Electron migration path for existing web apps
- Smaller bundle size (no Blazor runtime)
- Mature JS tooling ecosystem (Vite, ESLint, etc.)

**Licensing consideration:** Potential hybrid model - free for npm/JS usage, commercial license for C# integration. Positions Hermes as premium alternative to Photino with proper native OS support.

---

### Single Instance Support

**Status:** Complete
**Platforms:** All
**Complexity:** Low

Ensures only one instance of the application runs at a time, with command-line arg forwarding from second instances to the first. Uses cross-platform .NET APIs (named Mutex + named pipes), no native code required.

**API (non-Blazor):**

```csharp
using var guard = HermesApplication.SingleInstance("my-app-id");
if (!guard.IsFirstInstance)
{
    guard.NotifyFirstInstance(args);
    return;
}

guard.SecondInstanceLaunched += secondArgs =>
{
    window.Invoke(() => { /* bring to front, handle args */ });
};
```

**API (Blazor):**

```csharp
var builder = HermesBlazorAppBuilder.CreateDefault(args);
builder.SingleInstance("my-app-id", guard =>
{
    guard.SecondInstanceLaunched += secondArgs =>
    {
        // Bring window to front, handle args
    };
});
```

**Features:**

- [x] Detect existing instance
- [x] Callback when second instance launches
- [x] Pass command line args to existing instance

---

### Opener (External App Launcher)

**Status:** Complete
**Platforms:** All
**Complexity:** Low

Open files, folders, and URLs in their default applications. Uses `Process.Start` with `UseShellExecute = true` for cross-platform support, with platform-specific handling for `RevealInFileManager`.

**API:**

```csharp
HermesApplication.OpenUrl("https://example.com");
HermesApplication.OpenFile("/path/to/file.pdf");
HermesApplication.RevealInFileManager("/path/to/file.txt");
```

**Features:**

- [x] Open URL in default browser (http/https only)
- [x] Open file in default application
- [x] Reveal file/folder in file manager

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

### Key-Value Store

**Status:** Not started
**Platforms:** All
**Complexity:** Medium

Simple persistent storage for app settings and preferences.

**Proposed API:**

```csharp
var store = HermesStore.Open("settings");
store.Set("theme", "dark");
var theme = store.Get<string>("theme");
await store.SaveAsync();
```

**Features needed:**

- [ ] Get/set typed values
- [ ] Automatic persistence to app data directory
- [ ] JSON-based storage

---

### Autostart (Launch at Login)

**Status:** Not started
**Platforms:** All
**Complexity:** Medium

Register application to launch at system startup.

| Platform | API                                     |
| -------- | --------------------------------------- |
| Windows  | Registry HKCU\...\Run or Task Scheduler |
| macOS    | SMAppService (macOS 13+) or Login Items |
| Linux    | XDG autostart (~/.config/autostart/)    |

**Proposed API:**

```csharp
HermesApplication.SetAutostart(enabled: true);
bool isEnabled = HermesApplication.AutostartEnabled;
```

**Features needed:**

- [ ] Enable/disable autostart
- [ ] Query current state
- [ ] Pass launch arguments

---

## Low Priority

### macOS-Specific

- [ ] **Touch Bar support** - For MacBook Pro
- [x] **Dock menu customization** - Right-click dock icon menu
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

---

## Contributing

When implementing a feature from this roadmap:

1. Create a new interface in `src/Hermes/Abstractions/` (e.g., `ITrayBackend.cs`)
2. Implement platform backends in `src/Hermes/Platforms/{Windows,Linux,macOS}/`
3. For macOS, add native code in `src/Hermes.Native.macOS/` if needed
4. Add tests in `tests/Hermes.Tests/`
5. Update `PLATFORM-DIFFERENCES.md` with any platform-specific behavior
6. Move the item to "Completed" in this file
