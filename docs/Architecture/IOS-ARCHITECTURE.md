# Hermes.Mobile iOS Architecture

## Lifecycle Inversion

Desktop Hermes follows an executable-first model: C# `Main` owns the process, creates the native window via `IHermesWindowBackend`, and spins up the WKWebView itself. iOS does not allow this. The `UIApplicationDelegate` owns the app lifecycle, and the `UIWindow` / `UIViewController` owns the view hierarchy.

Hermes.Mobile inverts the relationship: the iOS shell drives the lifecycle, and Hermes becomes a library that hosts Blazor inside a `WKWebView` provided by the shell.

```
Desktop:  C# Main() → HermesWindow → IHermesWindowBackend → native window + webview
iOS:      Swift/ObjC AppDelegate → UIWindow → HermesMobileHost → WKWebView + Blazor
```

The public API mirrors the desktop builder pattern:

```csharp
var builder = HermesMobileAppBuilder.CreateDefault();
builder.RootComponents.Add<App>("#app");
var host = builder.Build();
Window = new UIWindow { RootViewController = host.RootViewController };
host.Start();
```

## Asset Serving

Blazor apps need an HTML host page, `blazor.webview.js`, CSS, and static assets served to the webview. On desktop, Hermes uses a custom URL scheme handler (`app://localhost/`) backed by `WebViewManager.TryGetResponseContent`. On iOS, this approach is broken (see Known Issues), so assets are served via an embedded HTTP server on localhost.

`EmbeddedFileServer` is a minimal `HttpListener`-based server that:
- Binds to `http://localhost:0/` (OS-assigned port)
- Serves files from `IOSAssetFileProvider`, which reads from `NSBundle.MainBundle.ResourcePath`
- Synthesizes an empty `_framework/blazor.modules.json` if it's missing from the bundle
- Falls back to `index.html` for extensionless paths (SPA routing)

The webview navigates to `http://localhost:{port}/` and loads Blazor assets over HTTP. iOS allows localhost HTTP without App Transport Security exceptions, though `NSAllowsLocalNetworking` is set in Info.plist for explicitness.

## JS-C# Bridge

The bridge protocol is identical to desktop Hermes and MAUI's BlazorWebView:

```
JS → C#:  window.external.sendMessage(json)
          → WKScriptMessageHandler ("webwindowinterop")
          → ScriptMessageHandler.DidReceiveScriptMessage
          → WebViewManager.MessageReceived

C# → JS:  WebViewManager.SendMessage(json)
          → WKWebView.EvaluateJavaScript("__dispatchMessageCallback(\"...\")")
          → window.__dispatchMessageCallback
          → registered receive callbacks
```

A `WKUserScript` injected at document-end wires up `window.external.sendMessage/receiveMessage` and calls `Blazor.start()` (the host page uses `autostart="false"`).

## Initialization Order

Several constraints force a specific initialization sequence in `HermesMobileHost`:

1. **Start `EmbeddedFileServer`** with `IOSAssetFileProvider` to get the assigned port
2. **Compute `appBaseUri`** as `http://localhost:{port}/`
3. **Register `ScriptMessageHandler`** on `WKWebViewConfiguration.UserContentController`
4. **Inject `BlazorInitScript`** as a `WKUserScript` at `AtDocumentEnd`
5. **Create `WKWebView`** with the fully configured `WKWebViewConfiguration`
6. **Create `IOSWebViewManager`** with the webview and `appBaseUri`
7. **Wire late-bound closure** so the ScriptMessageHandler can forward messages to the manager

Steps 3-4 must happen before step 5 because `WKWebView` copies its configuration at construction, ignoring later mutations. The ScriptMessageHandler and IOSWebViewManager have a circular dependency (handler needs manager, manager needs webview, webview needs config with handler), broken by a late-bound closure that captures `pendingManager`.

NSObject-bridged handlers (`ScriptMessageHandler`, `AllowAllNavigationDelegate`) are stored as instance fields on `HermesMobileHost` to prevent GC collection, which would silently stop native callbacks.

## DI Requirements

`HermesMobileAppBuilder.CreateDefault()` calls `services.AddBlazorWebView()` from the `Microsoft.AspNetCore.Components.WebView` package. This registers `NavigationManager`, `IJSRuntime`, and other services that the `WebViewManager` base class expects. Missing this call results in `No service for type 'NavigationManager' has been registered` at runtime.

## iOS App Bundle Structure

Static assets must be included as `BundleResource` items in the app project's csproj with `LogicalName` mappings that preserve the expected URL paths:

```
wwwroot/
  index.html                              ← app's host page
  _framework/blazor.webview.js            ← from Microsoft.AspNetCore.Components.WebView NuGet
  _framework/blazor.modules.json          ← from same NuGet (or synthesized by file server)
  _content/Shared.App/css/app.css          ← from Shared.App RCL wwwroot
```

The `$(PkgMicrosoft_AspNetCore_Components_WebView)` MSBuild property (via `GeneratePathProperty="true"` on the PackageReference) resolves the NuGet package path for `blazor.webview.js`.

## Known Issues and Workarounds

### WKURLSchemeHandler Not Invoked (.NET iOS Registrar Regression)

**Issue:** `WKURLSchemeHandler` registered via `SetUrlSchemeHandler` is never invoked by `WKWebView`. The handler registers correctly, `ProtocolAdoption.Ensure` confirms protocol conformance, but `StartUrlSchemeTask` is never called. Navigation lands on `about:blank`.

**Root cause:** .NET iOS 26.2 dynamic registrar does not emit Obj-C protocol conformance metadata for managed classes implementing protocol interfaces. Related: dotnet/macios#23002, dotnet/maui#32894.

**Workaround:** Embedded HTTP server on localhost for asset serving. The scheme handler code (`AppSchemeHandler.cs`) is retained for when the upstream fix lands.

### WKScriptMessageHandler Requires Static Registrar + Runtime Protocol Adoption

**Issue:** `WKScriptMessageHandler` conformance is not recognized by WKWebView when using the default dynamic registrar.

**Workaround:** Two-part fix:
1. `<Registrar>static</Registrar>` in the app csproj forces static Obj-C class/protocol metadata emission
2. `ProtocolAdoption.Ensure<T>()` calls `class_addProtocol` via P/Invoke at type initialization to register protocol conformance at runtime

```csharp
[DllImport("/usr/lib/libobjc.dylib", EntryPoint = "class_addProtocol")]
private static extern byte class_addProtocol(IntPtr cls, IntPtr protocol);
```

This is applied to `ScriptMessageHandler`, `AllowAllNavigationDelegate`, and `AppSchemeHandler`.

### iOS Linker Strips Blazor Component Constructors

**Issue:** The iOS linker (ILLink) trims constructors from Blazor component types because they're only instantiated via reflection. This causes `A suitable constructor for type 'MainLayout' could not be located` at runtime.

**Workaround:** Add `TrimmerRootAssembly` entries for assemblies containing Blazor components:

```xml
<TrimmerRootAssembly Include="Shared.App" />
<TrimmerRootAssembly Include="Hermes.Mobile" />
<TrimmerRootAssembly Include="Microsoft.AspNetCore.Components.WebView" />
```

### ibtool LaunchScreen.storyboard Failure

**Issue:** `ibtool` fails with "iOS 26.2 Platform Not Installed" when compiling `LaunchScreen.storyboard`, even with Xcode and the iOS SDK installed.

**Workaround:** Replace the storyboard with a `UILaunchScreen` dictionary in Info.plist:

```xml
<key>UILaunchScreen</key>
<dict>
    <key>UIColorName</key>
    <string>systemBackgroundColor</string>
</dict>
```

## Dependencies

`Hermes.Mobile` depends on:
- `Hermes.Contracts` (plugin interfaces like `IClipboard`)
- `Microsoft.AspNetCore.Components.WebView` (WebViewManager base class, `AddBlazorWebView()`)

It does **not** depend on:
- `Hermes` or `Hermes.Blazor` (both assume desktop window backends)
- `Microsoft.AspNetCore.Components.WebView.Maui` (avoids the MAUI dependency chain)
- Any native Obj-C/Swift code beyond the .NET iOS bindings

## Future Work

- **WKURLSchemeHandler revival:** Once the macios registrar regression is fixed, replace the embedded HTTP server with the scheme handler for a cleaner, no-network-listener approach
- **Shared message pump:** Extract the bounded-channel pattern from desktop `HermesWebViewManager` into a shared `Hermes.Blazor.Core` for both desktop and mobile
- **Android head:** Same lifecycle inversion pattern, targeting `WebView` via .NET Android bindings
- **NativeAOT validation:** `PublishAot=true` publish has not been tested yet
