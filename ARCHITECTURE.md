# Hermes - Native Desktop Framework

## Goal
Build a native desktop framework for a premium IDE with first-class native menu support, using modern C# and minimal native code.

## Key Decisions
- **Target .NET 9/10** - Modern framework, no legacy support needed
- **Minimize native code** - Windows pure C#, macOS and Linux use thin native shims
- **Dynamic plugin menus** - First-class support for runtime menu modifications

## .NET Features We Can Use
- `LibraryImport` with source generators (macOS interop)
- `file`-scoped types, required members, primary constructors
- `Span<T>`, `Memory<T>` throughout
- Generic math, static abstract interface members
- AOT compilation ready from day one

---

## Platform Strategy

| Platform | WebView | Menus | Dialogs | Native Code |
|----------|---------|-------|---------|-------------|
| **Windows** | Microsoft.Web.WebView2 (NuGet) | CsWin32 P/Invoke | CsWin32 P/Invoke | **None** |
| **Linux** | WebKitGTK via thin C shim | Thin C shim (GTK3) | Thin C shim (GTK3) | ~2,700 LOC |
| **macOS** | Thin Obj-C wrapper for WebKit | Thin Obj-C for NSMenu | Thin Obj-C for NSPanel | ~3,800 LOC |

### Key Dependencies
```xml
<!-- Windows -->
<PackageReference Include="Microsoft.Web.WebView2" Version="1.*" />
<PackageReference Include="Microsoft.Windows.CsWin32" Version="0.*" />

<!-- Linux -->
<!-- libHermes.Native.Linux.so - small C library (GTK3 + WebKitGTK), called via LibraryImport -->

<!-- macOS -->
<!-- libHermes.Native.macOS.dylib - small Objective-C library, called via LibraryImport -->
```

---

## Repository Structure

```
Hermes/
├── src/
│   ├── Hermes/                   # Core .NET library
│   │   ├── Abstractions/         # IHermesWindow, IMenuBar, etc.
│   │   ├── Platforms/
│   │   │   ├── Windows/          # WebView2 + CsWin32 (pure C#)
│   │   │   ├── Linux/            # P/Invoke to libHermes.Native.Linux.so
│   │   │   └── macOS/            # P/Invoke to libHermes.Native.macOS.dylib
│   │   ├── Menu/                 # NativeMenuBar, NativeContextMenu
│   │   └── HermesWindow.cs       # Facade over platform backends
│   │
│   ├── Hermes.Native.macOS/      # ONLY native code needed (~3,100 LOC Obj-C)
│   │   ├── HermesWindow.m        # NSWindow + WKWebView
│   │   ├── HermesMenu.m          # NSMenu
│   │   ├── HermesDialogs.m       # NSOpenPanel, NSSavePanel
│   │   └── Makefile              # Simple clang build
│   │
│   └── Hermes.Blazor/            # Blazor integration
│       ├── HermesBlazorApp.cs
│       └── HermesWebViewManager.cs
│
├── samples/
│   ├── HelloWorld/
│   ├── MenuDemo/
│   └── PluginMenuDemo/
│
└── Hermes.sln
```

---

## Menu API Design

### Core Requirements (Plugin Loading Support)
The menu system must support **runtime modifications** for dynamic plugin loading:
- Add new top-level menus after window creation
- Insert items into existing menus at runtime
- Remove menus/items when plugins unload
- Update accelerators dynamically

### C# API
```csharp
// Initial menu setup
var menuBar = window.MenuBar;

menuBar.AddMenu("File", file => file
    .AddItem("New", "file.new", item => item.WithAccelerator("Ctrl+N"))
    .AddItem("Open...", "file.open", item => item.WithAccelerator("Ctrl+O"))
    .AddSeparator()
    .AddItem("Save", "file.save", item => item.WithAccelerator("Ctrl+S")));

// Dynamic updates (state changes)
menuBar["file.save"].IsEnabled = document.IsDirty;
menuBar["view.sidebar"].IsChecked = sidebarVisible;

// PLUGIN LOADING: Add menu at runtime
public void OnPluginLoaded(IPlugin plugin)
{
    menuBar.AddMenu(plugin.MenuName, menu =>
    {
        foreach (var command in plugin.Commands)
            menu.AddItem(command.Label, command.Id);
    });

    // Or insert into existing menu
    menuBar["Tools"].InsertItem(
        afterId: "tools.options",
        label: plugin.Name,
        commandId: $"plugins.{plugin.Id}.open");
}

// PLUGIN UNLOADING: Remove menu at runtime
public void OnPluginUnloaded(IPlugin plugin)
{
    menuBar.RemoveMenu(plugin.MenuName);
    menuBar["Tools"].RemoveItem($"plugins.{plugin.Id}.open");
}

// Context menus
var contextMenu = window.CreateContextMenu();
contextMenu.AddItem("Cut", "edit.cut", item => item.WithAccelerator("Ctrl+X"));
contextMenu.Show(mouseX, mouseY);
```

### Platform Backend Interface
```csharp
public interface IMenuBackend
{
    void AddMenu(string label, int insertIndex);
    void AddItem(nint menuHandle, string id, string label, string? accelerator, MenuItemFlags flags);
    void InsertItem(nint menuHandle, string afterId, string id, string label, string? accelerator, MenuItemFlags flags);
    void RemoveMenu(string label);
    void RemoveItem(nint menuHandle, string id);
    void SetItemEnabled(nint menuHandle, string id, bool enabled);
    void SetItemChecked(nint menuHandle, string id, bool isChecked);
    void SetItemLabel(nint menuHandle, string id, string label);
    void SetItemAccelerator(nint menuHandle, string id, string accelerator);
}
```

---

## Startup Sequence

Startup overlaps the WebView's native boot with managed composition so time to
first render approaches the platform floor plus one render:

1. **UI thread (native track)**: creates a lazy handle to the static file
   provider (the manifest read itself runs on the composition worker, about
   11ms cold when it ran on the UI thread) and an `InlinedHostPage` over it,
   registers the `StartupSchemeHandler` for the app scheme,
   attaches a `WebMessageBuffer`, starts the composition worker, pays native
   application initialization explicitly via
   `IHermesWindowBackend.InitializeApplication()` (NSApplication registration on
   macOS, about 100ms cold), and creates the window. Linux issues the initial
   load before `Show()` because WebKitGTK stalls the UI process for a full
   500ms timeout when the WebView is size-allocated before its first load;
   macOS and Windows show the window and leave the initial load to `Run()`,
   because loading before `Show()` measured +57ms window-visible on CI and
   loading right after `Show()` inside `Build()` measured 31 to 45ms of
   synchronous WKWebView work moved into `Build()` with no first-render gain
   (local, 2026-09-03). It then pumps the native loop in 5ms slices through
   `RunEventLoopIteration` until the worker finishes; on fast hardware
   composition is often already complete by then and the pump runs zero
   iterations.
2. **Worker thread (managed track)**: service registration, `IHost` build, and
   dev server startup run concurrently via `Task.Run` inside
   `HermesBlazorAppBuilder.Build()`, followed by `RendererWarmup`. The worker
   touches no native state and never posts to the UI synchronization context,
   so pumping cannot deadlock on it.

On Linux, where `Build()` issues the load before `Show()`, and in the macOS and
Linux dev-server-failure fallback, the WebView requests the host page and its
script while the UI thread pumps, through the `StartupSchemeHandler`, which
answers from the `EarlyStaticContentHandler` (host page with
`blazor.webview.js` inlined, static files from the same provider, 404
otherwise) and never blocks. On macOS and Windows the default path navigates in
`Run()`, after the manager exists, so during `Build()` the pump's job there is
to let WebKit's application-level setup and WebView2's controller
initialization (whose continuations are dispatched through the pump) progress.
Windows under hot reload registers nothing for the app scheme, because an
`http://*` WebView2 filter would intercept the dev server; macOS and Linux
register the `app` scheme even under hot reload, so a dev server that fails to
start and falls back to release mode still has a handler. IPC messages the page
script sends in that window are held by the `WebMessageBuffer`. When composition
completes, `HermesWebViewManager` installs itself as the inner scheme handler
and the buffered messages are replayed into it in arrival order, all on the UI
thread with no pump in between, so nothing is delivered twice or out of order.
`Run()` adds the root components and, on macOS and Windows, issues the initial
load; on Linux the page can already be attached by then and renders
immediately.

`RunEventLoopIteration` services run loop sources on macOS
(`CFRunLoopRunInMode`, no NSEvents, so no window or input callbacks fire before
the app is ready), the default GLib main context on Linux, and the thread
message queue on Windows (a `WM_QUIT` seen during startup is re-posted for the
main loop). Hot reload and `UseFastStartup` keep the previous sequence: the dev
server's base URI is only known after composition, and fast startup defers the
window to `Run()`.

The synchronization context is installed on the UI thread before `Show()`: on
Windows, WebView2 initialization continuations capture it, and without it they
resume on thread pool threads and controller calls fail COM apartment
marshaling.

Set `HERMES_STARTUP_TRACE=1` to print `HERMES_PHASE:<name>:<ms since process
start>` lines for `app-initialized`, `window-initialized`, `window-shown`,
`navigate`, `composition-done`, `manager-ready`, `first-request`,
`first-message`, and `first-render-batch`. `first-request` precedes
`composition-done` only where the load is issued in `Build()` and composition
outlasts native init; on fast hardware `composition-done` lands before the
pump starts.

Orderings that look tempting but measured slower, do not revisit without new
evidence: deferring navigation into the message loop (about 65ms slower,
WebKit needs the early native load request), creating the WKWebView before
NSApplication initialization (32ms slower in a standalone spike, WebKit's
process spawn only progresses while the run loop is serviced), and issuing the
macOS load inside `Build()` right after `Show()` (31 to 45ms of synchronous
WKWebView work moved into `Build()`, no first-render gain, measured
2026-09-03). The UI thread pumps instead of blocking on the composition join
so WebKit's process launch and WebView2's initialization can progress whenever
composition outlasts native init; locally the join measured close to zero.

Threading contract for contributors: native object creation and access belong
on the UI thread only; pure managed composition may run on workers.

---

## Implementation Phases

### Phase 1: Project Scaffolding
- [x] Set up Hermes repo structure
- [x] Create Hermes.sln with Hermes.csproj, Hermes.Blazor.csproj
- [x] Add NuGet references (WebView2, CsWin32, GtkSharp)
- [x] Create platform abstractions (IHermesWindowBackend, IMenuBackend)

### Phase 2: Windows Backend (Pure C#)
- [x] Implement WindowsWindowBackend using Microsoft.Web.WebView2
- [x] Implement WindowsMenuBackend using CsWin32 (CreateMenu, AppendMenu, etc.)
- [x] Implement WindowsDialogBackend using CsWin32 (GetOpenFileName, etc.)
- [x] Handle message loop and threading
- [x] Verify window + WebView works

### Phase 3: Linux Backend (Pure C#)
- [x] Implement LinuxWindowBackend using GtkSharp
- [x] Implement LinuxMenuBackend using GtkSharp menus
- [x] Implement LinuxDialogBackend using GtkSharp dialogs
- [x] WebView via WebKitGTKSharp or thin P/Invoke if needed
- [x] Verify on Linux

### Phase 4: macOS Backend (Thin Native Layer)
- [x] Create Hermes.Native.macOS (Objective-C, ~500 LOC)
- [x] Create LibraryImport bindings in C#
- [x] Implement MacWindowBackend calling native library
- [x] Verify on macOS

### Phase 5: Menu System
- [x] Create NativeMenuBar facade over platform backends
- [x] Implement runtime add/insert/remove for plugin support
- [x] Accelerator parsing in C#
- [x] Context menu support
- [x] State management (enabled, checked) in C#

### Phase 6: Blazor & Polish
- [x] Implement Hermes.Blazor
- [x] Create samples (HelloWorld, MenuDemo, PluginMenuDemo)
- [x] Test AOT compilation
- [x] CI/CD setup

---

## Files to Create

### Core Abstractions (src/Hermes/)
```
Abstractions/IHermesWindowBackend.cs
Abstractions/IMenuBackend.cs
Abstractions/IDialogBackend.cs
HermesWindow.cs
HermesWindowOptions.cs
Menu/NativeMenuBar.cs
Menu/NativeMenuItem.cs
Menu/NativeContextMenu.cs
Menu/Accelerator.cs
```

### Windows Backend (src/Hermes/Platforms/Windows/)
```
WindowsWindowBackend.cs
WindowsMenuBackend.cs
WindowsDialogBackend.cs
NativeMethods.txt
```

### Linux Backend (src/Hermes/Platforms/Linux/)
```
LinuxWindowBackend.cs
LinuxWebViewBackend.cs
LinuxMenuBackend.cs
LinuxDialogBackend.cs
```

### macOS Backend
```
src/Hermes.Native.macOS/
├── HermesWindow.m
├── HermesMenu.m
├── HermesDialogs.m
├── Exports.h
└── Makefile

src/Hermes/Platforms/macOS/
├── MacWindowBackend.cs
├── MacMenuBackend.cs
└── MacDialogBackend.cs
```

### Blazor Layer (src/Hermes.Blazor/)
```
HermesBlazorApp.cs
HermesBlazorAppBuilder.cs
HermesWebViewManager.cs
HermesDispatcher.cs
```

---

## Estimated Scope

| Component | Estimated LOC | Language |
|-----------|---------------|----------|
| Core abstractions & HermesWindow | ~500 | C# |
| Windows backend | ~800 | C# |
| Linux backend | ~600 | C# |
| macOS native | ~3,100 | Objective-C |
| Linux native | ~1,800 | C |
| macOS backend (C# interop) | ~300 | C# |
| Menu system | ~400 | C# |
| Blazor integration | ~600 | C# |
| **Total** | **~8,100** | Mostly C# |
