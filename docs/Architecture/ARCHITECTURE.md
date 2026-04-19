# Hermes - Native Desktop Framework

## Goal
Build a native desktop framework for a premium IDE with first-class native menu support, using modern C# and minimal native code.

## Key Decisions
- **Target .NET 9/10** - Modern framework, no legacy support needed
- **Minimize native code** - Windows/Linux pure C#, macOS only native layer
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
| **Linux** | GtkSharp + WebKitGTKSharp | GtkSharp | GtkSharp | **None** |
| **macOS** | Thin Obj-C wrapper for WebKit | Thin Obj-C for NSMenu | Thin Obj-C for NSPanel | ~3,100 LOC |

### Key Dependencies
```xml
<!-- Windows -->
<PackageReference Include="Microsoft.Web.WebView2" Version="1.*" />
<PackageReference Include="Microsoft.Windows.CsWin32" Version="0.*" />

<!-- Linux -->
<PackageReference Include="GtkSharp" Version="3.*" />

<!-- macOS -->
<!-- Hermes.Native.dylib - small Objective-C library -->
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
│   │   │   ├── Linux/            # GtkSharp (pure C#)
│   │   │   └── macOS/            # P/Invoke to Hermes.Native.dylib
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
