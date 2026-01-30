# Hermes

A modern, AOT-compatible native desktop framework for .NET 10.

[![NuGet](https://img.shields.io/nuget/v/Mythetech.Hermes)](https://www.nuget.org/packages/Mythetech.Hermes)
[![License](https://img.shields.io/badge/license-Elastic%20License%202.0-blue)](LICENSE)

## Overview

Hermes is a cross-platform desktop framework that enables .NET developers to create native desktop applications with web-based UIs. It provides native windows, menus, dialogs, and WebView integration across Windows, macOS, and Linux.

Designed for .NET 10 and modern development workflows, Hermes prioritizes AOT compatibility, minimal native code, and runtime performance. Unlike frameworks that rely on large C++ codebases, Hermes uses pure C# on Windows (via CsWin32 source generators) and thin native shims on macOS and Linux—totaling approximately 3,800 lines of native code compared to 12,300+ in similar frameworks.

Hermes builds upon patterns established by [Photino](https://github.com/nickg/Photino). See [THIRD_PARTY_LICENSES](THIRD_PARTY_LICENSES/) for attribution.

## Features

- **Native Windows and WebView** — Platform-native windows with embedded WebView2 (Windows), WebKitGTK (Linux), and WKWebView (macOS)
- **Native Menus** — Full menu bar support with keyboard accelerators and runtime modification for plugin systems
- **Context Menus** — Native right-click menus with screen coordinate positioning
- **File Dialogs** — Native open, save, and folder selection dialogs with file filters
- **AOT Compatible** — Designed for Native AOT from day one using `LibraryImport` instead of `DllImport`
- **Blazor Integration** — First-class support for Blazor applications via `Hermes.Blazor`
- **Cross-Platform** — Single codebase targeting Windows, macOS, and Linux
- **Minimal Dependencies** — Pure C# on Windows; thin native layers only where required

## Platform Support

| Platform | Runtime | WebView | Status |
|----------|---------|---------|--------|
| Windows 10/11 | .NET 10 | WebView2 | Supported |
| macOS 12+ | .NET 10 | WKWebView | Supported |
| Linux (x64) | .NET 10 | WebKitGTK 4.x | Supported |

## Getting Started

### Installation

```shell
dotnet add package Mythetech.Hermes
```

For Blazor applications:

```shell
dotnet add package Mythetech.Hermes.Blazor
```

### Basic Example

```csharp
using Hermes;

var window = new HermesWindow()
    .SetTitle("My Application")
    .SetSize(1024, 768)
    .Center()
    .Load("https://example.com");

window.WaitForClose();
```

### Blazor Example

```csharp
using Hermes;
using Hermes.Blazor;

HermesWindow.Prewarm();

var builder = HermesBlazorAppBuilder.CreateDefault(args);

builder.ConfigureWindow(options =>
{
    options.Title = "My Blazor App";
    options.Width = 1024;
    options.Height = 768;
    options.CenterOnScreen = true;
});

builder.RootComponents.Add<App>("#app");

var app = builder.Build();
app.Run();
```

## Native Menus

Hermes provides a fluent API for building native menus with full support for runtime modification—enabling dynamic plugin loading scenarios.

```csharp
// Build menus with fluent API
window.MenuBar
    .AddMenu("File", file =>
    {
        file.AddItem("New", "file.new", item => item.WithAccelerator("Ctrl+N"))
            .AddItem("Open...", "file.open", item => item.WithAccelerator("Ctrl+O"))
            .AddSeparator()
            .AddItem("Save", "file.save", item => item.WithAccelerator("Ctrl+S"))
            .AddItem("Exit", "file.exit");
    })
    .AddMenu("Edit", edit =>
    {
        edit.AddItem("Undo", "edit.undo", item => item.WithAccelerator("Ctrl+Z"))
            .AddItem("Redo", "edit.redo", item => item.WithAccelerator("Ctrl+Y"))
            .AddSeparator()
            .AddItem("Cut", "edit.cut", item => item.WithAccelerator("Ctrl+X"))
            .AddItem("Copy", "edit.copy", item => item.WithAccelerator("Ctrl+C"))
            .AddItem("Paste", "edit.paste", item => item.WithAccelerator("Ctrl+V"));
    });

// Handle menu clicks
window.MenuBar.ItemClicked += itemId =>
{
    if (itemId == "file.exit")
        window.Close();
};

// Runtime modification for plugins
window.MenuBar.AddMenu("Plugins", plugins =>
{
    plugins.AddItem("My Plugin", "plugins.myplugin");
});
```

Accelerators automatically translate between platforms—`Ctrl+S` on Windows/Linux becomes `Cmd+S` on macOS.

## Architecture

Hermes minimizes native code by leveraging platform-specific .NET capabilities:

| Platform | Approach | Native Code |
|----------|----------|-------------|
| Windows | CsWin32 source generators + WebView2 NuGet | None |
| macOS | Thin Objective-C shim with `LibraryImport` | ~2,400 LOC |
| Linux | Thin C shim with `LibraryImport` | ~1,400 LOC |

This architecture provides:
- **Easier maintenance** — Most logic lives in C#, not platform-specific native code
- **AOT compatibility** — `LibraryImport` generates marshalling code at compile time
- **Reduced build complexity** — No C++ toolchain required on Windows

For detailed technical information, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Packages

| Package | Description |
|---------|-------------|
| [Mythetech.Hermes](https://www.nuget.org/packages/Mythetech.Hermes) | Core framework |
| [Mythetech.Hermes.Blazor](https://www.nuget.org/packages/Mythetech.Hermes.Blazor) | Blazor integration |

## Documentation

- [Architecture Overview](ARCHITECTURE.md) — Technical design and platform strategy
- [Platform Differences](PLATFORM-DIFFERENCES.md) — Cross-platform behavior notes
- [Roadmap](ROADMAP.md) — Planned features and enhancements

## License

Hermes is source-available under the [Elastic License 2.0](LICENSE).

**Free for:** non-commercial use, educational use, open source projects, and commercial use under $1M annual revenue. See [COMMERCIAL.md](COMMERCIAL.md) for details.

This project incorporates patterns from Photino. See [THIRD_PARTY_LICENSES](THIRD_PARTY_LICENSES/) for attribution details.
