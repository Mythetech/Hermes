# Performance Review #1: Startup Hot Path Optimization

**Date**: 2026-01-26
**Scope**: Initial architecture review for Hermes startup optimization
**Status**: Pre-implementation analysis

---

## Executive Summary

This review analyzes the photino.Native, photino.NET, and photino.Blazor codebases to identify startup hot path optimizations for Hermes. Key findings indicate potential for significant improvements through WebView2 pre-warming, elimination of nested async callbacks, removal of reflection in Blazor, and reduction of macOS native code from 1,677 LOC to ~500 LOC.

---

## 1. Current photino.Native Analysis

### 1.1 Windows Implementation Issues

**File**: `photino.Native/Photino.Windows.cpp` (1,495 lines)

**Hot Path Bottleneck - Nested Async Callbacks (lines 1116-1233)**:
```cpp
// Current pattern - deeply nested async
CreateCoreWebView2EnvironmentWithOptions(...)
  -> callback: CreateCoreWebView2Controller(...)
    -> callback: Configure settings, navigate
```

**Problems identified**:
- WebView2 environment creation blocks window display
- No opportunity for parallel initialization
- Capture variables in nested callbacks create lifetime issues
- HRESULT errors shown via MessageBox (blocks UI)

**String Memory Leaks (lines 99-170)**:
```cpp
wchar_t* ToUTF16String(const char* str) {
    std::wstring* wstr = new std::wstring(...);  // Never freed
    return (wchar_t*)wstr->c_str();              // Dangling pointer risk
}
```

### 1.2 Linux Implementation Issues

**File**: `photino.Native/Photino.Linux.cpp` (900+ lines)

**Deprecated WebKit API (line 585)**:
```cpp
webkit_web_view_run_javascript(...)  // Deprecated
// Blocks with busy-wait:
while (!completed) {
    g_main_context_iteration(NULL, TRUE);  // Busy wait
}
```

**Recommendation**: Use `webkit_web_view_evaluate_javascript()` with async callback.

### 1.3 macOS Implementation Issues

**Files**: 8 Objective-C files totaling 1,677 lines

| File | Lines | Consolidation Target |
|------|-------|---------------------|
| Photino.Mac.mm | 1,202 | ~300 LOC |
| Photino.Mac.Dialog.mm | 232 | ~100 LOC |
| Photino.Mac.UiDelegate.mm | 100 | ~60 LOC |
| Photino.Mac.WindowDelegate.mm | 38 | Inline |
| Photino.Mac.UrlSchemeHandler.mm | 37 | ~30 LOC |
| Others | ~68 | Inline |
| **Total** | **1,677** | **~500 LOC** |

**JSON Preference Parsing Overhead**:
```objc
// Current: Runtime JSON parsing via nlohmann/json
for (auto& pref : json["preferences"]) {
    [prefs setValue:... forKey:...];  // N iterations
}
```

**Recommendation**: Pass preferences as bitmask in blittable struct.

---

## 2. photino.NET P/Invoke Analysis

**File**: `photino.NET/PhotinoDllImports.cs`

### 2.1 Mixed P/Invoke Patterns

```csharp
// Modern (good) - 60+ functions:
[LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)]
static partial void Photino_SetTitle(IntPtr instance, string title);

// Legacy (requires runtime marshalling) - 4 functions:
[DllImport(DLL_NAME, ...)]
static extern IntPtr Photino_ctor(ref PhotinoNativeParameters parameters);
```

**Comment at line 24-25**: "LibraryImport has limitations with user-defined types (struct parameters)"

**Recommendation**: .NET 10 may support `LibraryImport` for ref struct parameters. Otherwise, use blittable struct design.

### 2.2 PhotinoNativeParameters Struct (27 fields, ~230 bytes)

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct PhotinoNativeParameters {
    public int Size;  // Runtime validation
    public IntPtr Title;
    public IntPtr StartUrl;
    // ... 24 more fields
}
```

**Validation overhead**: Size check at native entry prevents version mismatch but adds runtime cost.

**Recommendation**: Eliminate size field, use compile-time struct layout verification.

---

## 3. photino.Blazor Analysis

**Critical Files**:
- `PhotinoSynchronizationContext.cs` - Reflection-based thread access
- `PhotinoWebViewManager.cs` - Blocking message send
- `ServiceCollectionExtensions.cs` - Eager service initialization

### 3.1 Reflection in SynchronizationContext (CRITICAL)

**File**: `PhotinoSynchronizationContext.cs`, lines 54-58

```csharp
// Current: Reflection on every context creation
_uiThreadId = (int)typeof(PhotinoWindow)
    .GetField("_managedThreadId", BindingFlags.NonPublic | BindingFlags.Instance)!
    .GetValue(_window)!;

_invokeMethodInfo = typeof(PhotinoWindow)
    .GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)!;
```

**Line 255 - Reflection invoke on every dispatch**:
```csharp
_invokeMethodInfo.Invoke(_window, new object[] { callback });
```

**Impact**:
- Not AOT-compatible
- Allocation per invoke (object[] boxing)
- Reflection overhead on hot path

**Recommendation**: Add interface to `IHermesWindowBackend`:
```csharp
public interface IUIThreadDispatcher {
    int UIThreadId { get; }
    bool CheckAccess();
    void Invoke(Action action);
    void BeginInvoke(Action action);
}
```

### 3.2 Blocking Message Send (CRITICAL)

**File**: `PhotinoWebViewManager.cs`, lines 91-95

```csharp
protected override void SendMessage(string message) {
    while (!_channel.Writer.TryWrite(message))
        Thread.Sleep(200);  // BLOCKING!
}
```

**Impact**: Blocks calling thread for 200ms per failed write attempt.

**Recommendation**: Use bounded channel with async wait:
```csharp
public ValueTask SendAsync(string message) {
    if (_channel.Writer.TryWrite(message))
        return ValueTask.CompletedTask;
    return SendSlowAsync(message);
}
```

### 3.3 No Buffer Pooling

**File**: `PhotinoWebViewManager.cs`, lines 62-84

Custom scheme responses allocate new `MemoryStream` per request.

**Recommendation**: Use `ArrayPool<byte>.Shared` with pooled response stream.

---

## 4. Hermes Current State

### 4.1 Well-Designed Abstractions

**IHermesWindowBackend** (132 lines) - Clean interface:
- Lifecycle: `Initialize()`, `Show()`, `Close()`, `WaitForClose()`
- Properties: `Title`, `Size`, `Position`, `IsMaximized`, `IsMinimized`
- WebView: `NavigateToUrl()`, `NavigateToString()`, `SendWebMessage()`, `RegisterCustomScheme()`
- Threading: `Invoke()`
- Events: `Closing`, `Resized`, `Moved`, `FocusIn`, `FocusOut`, `WebMessageReceived`

### 4.2 Project Configuration (Correct)

**Hermes.csproj**:
```xml
<TargetFramework>net10.0</TargetFramework>
<IsAotCompatible>true</IsAotCompatible>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>

<!-- Platform-specific packages already declared -->
<PackageReference Include="Microsoft.Web.WebView2" Version="1.*" />
<PackageReference Include="Microsoft.Windows.CsWin32" Version="0.*" />
<PackageReference Include="GtkSharp" Version="3.*" />
```

### 4.3 HermesWindow Facade (489 lines)

Lazy initialization pattern already in place:
```csharp
private void EnsureInitialized() {
    if (_initialized) return;
    _backend.Initialize(_options);
    _initialized = true;
}
```

**Gap identified**: No pre-warming mechanism for WebView2 environment.

---

## 5. Recommended Optimizations

### 5.1 Windows: WebView2 Pre-warming

**New file**: `src/Hermes/Platforms/Windows/WebView2EnvironmentPool.cs`

```csharp
internal sealed class WebView2EnvironmentPool {
    private static readonly Lazy<WebView2EnvironmentPool> _instance = new(...);
    private CoreWebView2Environment? _sharedEnvironment;
    private TaskCompletionSource<CoreWebView2Environment>? _initTask;

    public void BeginPrewarm() {
        _ = GetOrCreateEnvironmentAsync();  // Fire and forget
    }

    public async ValueTask<CoreWebView2Environment> GetOrCreateEnvironmentAsync() {
        if (_sharedEnvironment is not null) return _sharedEnvironment;
        // ... async initialization with double-check locking
    }
}
```

**Usage in HermesWindow**:
```csharp
public static void Prewarm() {
    if (OperatingSystem.IsWindows())
        WebView2EnvironmentPool.Instance.BeginPrewarm();
}
```

### 5.2 Windows: CsWin32 NativeMethods.txt

**New file**: `src/Hermes/Platforms/Windows/NativeMethods.txt`

```
CreateWindowExW
RegisterClassExW
DefWindowProcW
DestroyWindow
ShowWindow
UpdateWindow
GetClientRect
SetWindowPos
GetMessageW
TranslateMessage
DispatchMessageW
PostQuitMessage
PostMessageW
SetThreadDpiAwarenessContext
GetDpiForWindow
MonitorFromWindow
GetMonitorInfoW
```

### 5.3 Linux: Modern WebKit APIs

**Replace deprecated API**:
```csharp
// Old (photino):
webkit_web_view_run_javascript(webview, js, null, null, null);

// New (Hermes):
_webview.EvaluateJavascript(js, null, null, (webview, result) => { });
```

### 5.4 macOS: Blittable Struct Design

**C struct**:
```c
typedef struct {
    int32_t x, y, width, height;
    int32_t minWidth, minHeight, maxWidth, maxHeight;
    uint32_t flags;  // Bit 0: Resizable, Bit 1: Chromeless, ...
} HermesWindowConfig;
```

**C# LibraryImport**:
```csharp
[LibraryImport("Hermes.Native", EntryPoint = "Hermes_WindowCreate")]
internal static partial nint WindowCreate(
    in HermesWindowConfig config,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? title,
    // ... other params
);
```

### 5.5 Blazor: IUIThreadDispatcher Interface

**Extend IHermesWindowBackend**:
```csharp
public interface IHermesWindowBackend : IDisposable {
    // Existing members...

    // Add for Blazor threading:
    int UIThreadId { get; }
    bool CheckAccess();
    void BeginInvoke(Action action);
}
```

### 5.6 Blazor: Non-Blocking Message Pump

```csharp
internal sealed class MessagePump : IAsyncDisposable {
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(1024) {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask SendAsync(string message) {
        return _channel.Writer.TryWrite(message)
            ? ValueTask.CompletedTask
            : _channel.Writer.WriteAsync(message);
    }
}
```

---

## 6. Performance Metrics

### 6.1 P/Invoke Call Reduction (macOS)

| Phase | photino | Hermes |
|-------|---------|--------|
| Window creation | 8 calls | 1 call |
| WebView setup | 12 calls | (included) |
| Preference config | 10+ calls | (included) |
| **Total init** | **30+** | **1** |

### 6.2 Allocation Reduction (Blazor)

| Operation | photino | Hermes |
|-----------|---------|--------|
| Thread ID access | Reflection + boxing | Direct property |
| Invoke dispatch | object[] allocation | Direct call |
| Message send | Potential Thread.Sleep | ValueTask (no alloc) |
| Scheme response | New MemoryStream | ArrayPool rental |

### 6.3 Native Code Reduction (macOS)

| Component | photino | Hermes |
|-----------|---------|--------|
| Window/WebView | 1,202 LOC | ~300 LOC |
| Dialogs | 232 LOC | ~100 LOC |
| Delegates | 172 LOC | ~60 LOC inline |
| Other | 71 LOC | ~40 LOC |
| **Total** | **1,677** | **~500** |

---

## 7. Implementation Checklist

### Phase 1: Windows Backend Core
- [ ] Create `NativeMethods.txt` for CsWin32
- [ ] Implement `WindowsWindowBackend.Initialize()` with CsWin32 calls
- [ ] Implement window message loop in `WaitForClose()`
- [ ] Add lock-free invoke queue with `WM_USER` message

### Phase 2: WebView2 Integration
- [ ] Create `WebView2EnvironmentPool` singleton
- [ ] Add `HermesWindow.Prewarm()` static method
- [ ] Implement `AttachWebViewAsync()` using pre-warmed environment
- [ ] Wire up `WebMessageReceived` and custom scheme handlers

### Phase 3: Linux Backend
- [ ] Implement `LinuxWindowBackend` with GtkSharp
- [ ] Use modern `EvaluateJavascript()` API
- [ ] Implement `UserContentManager` for script injection
- [ ] Add async custom scheme handling

### Phase 4: Blazor Optimization
- [ ] Add `UIThreadId`, `CheckAccess()`, `BeginInvoke()` to interface
- [ ] Implement in all backends
- [ ] Create `HermesSynchronizationContext` without reflection
- [ ] Implement `MessagePump` with bounded channel

### Phase 5: macOS Native Layer
- [ ] Design blittable struct API (header file)
- [ ] Consolidate Objective-C into single ~500 LOC file
- [ ] Create LibraryImport declarations in C#
- [ ] Implement `MacWindowBackend`
- [ ] Create Makefile for universal binary

---

## 8. Verification Plan

1. **Startup timing**: Measure time from `Main()` to first paint
2. **Allocation profiling**: Use `dotnet-trace` to count allocations during init
3. **AOT build**: Verify `dotnet publish -p:PublishAot=true` succeeds
4. **Platform matrix**: Test on Windows 11, Ubuntu 24.04, macOS 15

---

## Appendix A: photino Source File References

| File | Purpose | Key Lines |
|------|---------|-----------|
| `Photino.Windows.cpp` | Windows impl | 1116-1233 (WebView2 init) |
| `Photino.Linux.cpp` | Linux impl | 585 (deprecated webkit API) |
| `Photino.Mac.mm` | macOS impl | 200-400 (JSON prefs) |
| `PhotinoDllImports.cs` | P/Invoke | 24-25 (LibraryImport limitation) |
| `PhotinoSynchronizationContext.cs` | Blazor threading | 54-58, 255 (reflection) |
| `PhotinoWebViewManager.cs` | Blazor WebView | 91-95 (Thread.Sleep) |

## Appendix B: Hermes Source File References

| File | Purpose | Status |
|------|---------|--------|
| `IHermesWindowBackend.cs` | Backend interface | Complete |
| `IMenuBackend.cs` | Menu interface | Complete |
| `IDialogBackend.cs` | Dialog interface | Complete |
| `HermesWindow.cs` | Facade | Complete, needs Prewarm() |
| `HermesWindowOptions.cs` | Config | Complete |
| `Hermes.csproj` | Project | Complete |
| `WindowsWindowBackend.cs` | Windows impl | Stub only |
