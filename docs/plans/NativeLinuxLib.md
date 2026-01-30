# Native Linux Library: AOT-Friendly GTK Replacement

## Executive Summary

**GtkSharp is fundamentally incompatible with AOT/trimming** due to pervasive reflection usage. Creating a trimmable library that matches Hermes's API needs is **medium complexity** with an estimated **4-5 weeks** of development effort for a senior developer.

**Recommended approach**: Create a native C wrapper library (`libHermes.Native.Linux.so`) following the proven macOS pattern already in the codebase.

---

## Why GtkSharp Isn't AOT-Friendly

GtkSharp uses reflection throughout its core:

| Issue | Location | Impact |
|-------|----------|--------|
| Dynamic P/Invoke via `FuncLoader` | Every native call | Runtime function pointer resolution |
| `Activator.CreateInstance()` | `ObjectManager.cs:45` | Dynamic object creation |
| `AppDomain.GetAssemblies()` | `GType.cs:235` | Assembly scanning |
| `DynamicInvoke()` | `Signal.cs:200` | Dynamic delegate invocation |
| `FindMembers()` / `GetCustomAttributes()` | `SignalConnector.cs`, `Object.cs` | Reflection-based discovery |
| No trimming annotations | Entire library | No `[DynamicallyAccessedMembers]` |

**Fixing GtkSharp itself would require 12-16 weeks** of work with source generators to replace all reflection - not recommended.

---

## Current Hermes Linux API Surface

The Linux backend uses ~90 GTK/WebKit API calls across:

| Category | Key APIs | Lines of C# |
|----------|----------|-------------|
| Window | `Gtk.Window`, size/position, maximize/minimize, icon | ~300 |
| WebView | `WebKit.WebView`, LoadUri, RunJavascript, custom schemes | ~350 |
| Menus | `MenuBar`, `MenuItem`, `CheckMenuItem`, accelerators | ~460 |
| Dialogs | `FileChooserDialog`, `MessageDialog` | ~190 |
| Context Menus | `Menu.Popup()` | ~140 |
| Threading | `GLib.Idle.Add()` | ~60 |
| **Total** | | **~1,640 lines** |

---

## Recommended Approach: Native C Wrapper

Create `libHermes.Native.Linux.so` that wraps GTK3 + WebKit2GTK, with thin C# `[LibraryImport]` bindings.

### Why This Approach

1. **Proven pattern** - Already works for macOS (`libHermes.Native.macOS.dylib`)
2. **Perfect AOT compatibility** - Static P/Invoke with `LibraryImport`
3. **Lower complexity** - GTK complexity stays in C, not C#
4. **Easier maintenance** - Native layer handles GObject lifecycle

### Architecture

```
┌──────────────────────────────────────────────┐
│     LinuxWindowBackend.cs (C# - AOT safe)    │
│     LinuxNativeImports.cs (LibraryImport)    │
├──────────────────────────────────────────────┤
│              P/Invoke Boundary               │
├──────────────────────────────────────────────┤
│      libHermes.Native.Linux.so (C code)      │
│  HermesWindow.c | HermesMenu.c | Dialogs.c   │
├──────────────────────────────────────────────┤
│   libgtk-3.so  |  libwebkit2gtk-4.1.so      │
└──────────────────────────────────────────────┘
```

---

## Complexity Breakdown

### Estimated Code Volume

| Component | C Code | C# Code | Total |
|-----------|--------|---------|-------|
| Window + WebView | ~1,200 | ~600 | 1,800 |
| Menu System | ~800 | ~460 | 1,260 |
| Dialogs | ~400 | ~200 | 600 |
| Context Menus | ~300 | ~140 | 440 |
| Build/Infra | ~300 | ~200 | 500 |
| **Total** | **~3,000** | **~1,600** | **~4,600** |

### Technical Challenges

| Challenge | Difficulty | Notes |
|-----------|------------|-------|
| WebKit JS bridge | Medium | Signal callbacks must stay in C |
| Custom URI schemes | Medium | Memory ownership between C/C# |
| GLib threading | Medium | `g_idle_add()` for cross-thread calls |
| webkit2gtk 4.0 vs 4.1 | Low-Medium | May need two library variants |
| GTK Window/Menu | Low | Well-documented, straightforward |
| File dialogs | Low | Simple GTK API |

### Maintenance Burden

- **Low ongoing maintenance** - GTK3 is stable, API changes rare
- **Build complexity** - Adds CMake build + native artifact management
- **Distribution** - Need to ship `.so` for common distros (or build from source)

---

## Implementation Phases

### Phase 1: Core Window + WebView (2 weeks)
- CMake build system setup
- `HermesWindow.c` with GTK window + WebKit WebView
- JavaScript bridge (`webkit_user_content_manager`)
- Custom URI scheme handler
- `LinuxNativeImports.cs` with `[LibraryImport]`
- Basic `LinuxWindowBackend.cs` using native lib

### Phase 2: Menu System (1 week)
- `HermesMenu.c` with `GtkMenuBar`
- Accelerator key parsing and binding
- Submenu support
- C# menu backend layer

### Phase 3: Dialogs + Context Menus (1 week)
- `HermesDialogs.c` (FileChooser, MessageDialog)
- `HermesContextMenu.c` (popup menus)
- C# dialog/context menu backends

### Phase 4: Integration + Testing (1 week)
- Full integration with Hermes.Blazor
- AOT compilation testing (`PublishAot=true`)
- Ubuntu 22.04 / 24.04 testing
- Wayland + X11 testing
- CI/CD pipeline updates

---

## Native Library API Design

### Required Exports (~65 functions)

**Application Lifecycle**
```c
void Hermes_App_Init(int* argc, char*** argv);
void Hermes_App_Run(void);
```

**Window Lifecycle**
```c
void* Hermes_Window_Create(const HermesWindowParams* params);
void Hermes_Window_Show(void* window);
void Hermes_Window_Close(void* window);
void Hermes_Window_WaitForClose(void* window);
void Hermes_Window_Destroy(void* window);
```

**Window Properties**
```c
void Hermes_Window_SetTitle(void* window, const char* title);
void Hermes_Window_GetSize(void* window, int* width, int* height);
void Hermes_Window_SetSize(void* window, int width, int height);
// ... ~10 more property accessors
```

**WebView Operations**
```c
void Hermes_Window_NavigateToUrl(void* window, const char* url);
void Hermes_Window_NavigateToString(void* window, const char* html);
void Hermes_Window_SendWebMessage(void* window, const char* message);
void Hermes_Window_RegisterCustomScheme(void* window, const char* scheme);
void Hermes_Window_RunJavascript(void* window, const char* script);
```

**Threading**
```c
void Hermes_Window_Invoke(void* window, InvokeCallback callback);
void Hermes_Window_BeginInvoke(void* window, InvokeCallback callback);
```

---

## Key Technical Details

### WebKit JavaScript Bridge (C Implementation)

```c
static void on_script_message_received(WebKitUserContentManager* manager,
                                        WebKitJavascriptResult* result,
                                        gpointer user_data) {
    HermesWindow* self = (HermesWindow*)user_data;
    JSCValue* value = webkit_javascript_result_get_js_value(result);
    if (jsc_value_is_string(value)) {
        char* message = jsc_value_to_string(value);
        if (self->on_web_message) {
            self->on_web_message(message);
        }
        g_free(message);
    }
}
```

### GLib Threading Integration

```c
static gboolean invoke_on_main(gpointer user_data) {
    InvokeContext* ctx = (InvokeContext*)user_data;
    ctx->callback();
    g_mutex_lock(&ctx->mutex);
    ctx->done = TRUE;
    g_cond_signal(&ctx->cond);
    g_mutex_unlock(&ctx->mutex);
    return G_SOURCE_REMOVE;
}

void Hermes_Window_Invoke(void* window, InvokeCallback callback) {
    InvokeContext ctx = { .callback = callback, .done = FALSE };
    g_cond_init(&ctx.cond);
    g_mutex_init(&ctx.mutex);
    g_idle_add(invoke_on_main, &ctx);
    // Wait for completion...
}
```

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| webkit2gtk version fragmentation | Medium | Medium | Ship both 4.0 and 4.1 variants |
| Wayland vs X11 differences | Low | Low | Test on both in CI |
| GObject memory leaks | Low | Medium | Careful ref counting in C |
| Build system complexity | Low | Low | Copy macOS CMake pattern |

---

## Alternative Approaches (Not Recommended)

### A. Direct P/Invoke to GTK (No Native Wrapper)
- **Effort**: 6-8 weeks
- **Why not**: GObject lifecycle management in C# is error-prone, callback marshaling is complex

### B. Fork and Fix GtkSharp
- **Effort**: 12-16 weeks (high uncertainty)
- **Why not**: Deep reflection usage requires source generators for entire GType system

### C. GTK4 with New Bindings
- **Effort**: Unknown
- **Why not**: GTK4 adoption limited, gobject-introspection has similar AOT issues

---

## Reference Implementation

**Pattern to follow (macOS native wrapper):**
- `src/Hermes/Platforms/macOS/MacNativeImports.cs` - P/Invoke declarations
- `src/Hermes/Platforms/macOS/MacWindowBackend.cs` - Native wrapper usage
- `src/Hermes.Native.macOS/Exports.h` - C API design

**Current implementation to replace:**
- `src/Hermes/Platforms/Linux/LinuxWindowBackend.cs` - 845 lines
- `src/Hermes/Platforms/Linux/LinuxMenuBackend.cs` - 457 lines

---

---

## GTK3 vs GTK4: Which to Target?

### Naming Clarification

| Name | What it is |
|------|------------|
| **GTK3 / GTK4** | Major versions of the GTK toolkit |
| **webkit2gtk-4.0 / 4.1** | WebKit API versions (both work with GTK3) |

The "4" in webkit2gtk-4.1 is unrelated to GTK4 - it's just WebKit's API version.

### Recommendation: Use GTK3

**GTK3 covers 100% of Hermes's needs.** GTK4 would require significant rework for marginal benefits:

| Feature | GTK3 | GTK4 | Migration Cost |
|---------|------|------|----------------|
| Window management | ✅ Works | Minor DPI improvements | Low |
| WebView (WebKit) | ✅ Works | No change | None |
| File/message dialogs | ✅ Works | Minor API update | Low |
| Wayland support | ⚠️ 80% (quirks) | Better native support | N/A |
| **Menu system** | **✅ Works** | **Complete redesign** | **Very High** |

**GTK4 removed `GtkMenuBar` entirely** - migrating would require 5-10 days just for the menu system.

### Distribution Support

GTK3 and GTK4 coexist on all modern distros. GTK3 is guaranteed on any Linux desktop.

**Target: Ubuntu 24.04+ only** → Use `webkit2gtk-4.1` exclusively. This simplifies the build:
- No library version detection needed
- Single `.so` variant
- Removes the current reflection-based library resolver hack

---

## Summary

| Metric | Value |
|--------|-------|
| **Complexity** | Medium |
| **Effort** | 4-5 weeks (senior developer) |
| **Risk** | Low-Medium |
| **Lines of Code** | ~4,600 (3,000 C + 1,600 C#) |
| **Maintenance** | Low (GTK3 is stable) |
| **AOT Compatible** | Yes (100%) |
| **Target Toolkit** | GTK3 + webkit2gtk-4.1 |
| **Minimum Distro** | Ubuntu 24.04+ |

The native wrapper approach is well-proven in the codebase and provides the best balance of development effort, maintainability, and AOT compatibility.
