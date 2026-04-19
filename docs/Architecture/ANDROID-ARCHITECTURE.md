# Hermes.Mobile.Android Architecture

Android host for Hermes Blazor apps. Hosts Blazor inside an Android `WebView` using `ShouldInterceptRequest` + `TryGetResponseContent` for asset serving and `addJavascriptInterface` for the JS-C# bridge.

## Overview

Like the iOS PoC, the Android PoC inverts the desktop lifecycle: the native Android `Activity` owns the process, and Hermes is a hosted library.

```
Desktop:  C# Main() -> HermesWindow -> IHermesWindowBackend -> native window
Android:  Activity.OnCreate() -> HermesMobileAndroidBuilder -> HermesMobileAndroidHost -> WebView
iOS:      AppDelegate.FinishedLaunching() -> HermesMobileAppBuilder -> HermesMobileHost -> WKWebView
```

## Asset Serving

| | iOS | Android |
|---|---|---|
| **Mechanism** | Embedded `HttpListener` on localhost | `ShouldInterceptRequest` + `TryGetResponseContent` |
| **Why** | `WKURLSchemeHandler` broken (.NET iOS registrar regression) | `WebViewAssetLoader` path mismatches with Blazor's `Navigate("/")` |
| **Host page loading** | `HttpListener` serves HTML | `LoadDataWithBaseURL` injects HTML directly |
| **Sub-resource loading** | `HttpListener` serves files | `ShouldInterceptRequest` resolves via `IFileProvider` |
| **Asset origin** | `http://localhost:{port}/` | `https://0.0.0.0/` (synthetic) |
| **Base href** | `/` | `/` |

The initial host page is loaded via `LoadDataWithBaseURL` to avoid real network requests to the synthetic origin. All sub-resource requests (JS, CSS, static assets) are intercepted by `HermesWebViewClient.ShouldInterceptRequest`, which delegates to `AndroidWebViewManager.ResolveRequest` using the base `WebViewManager.TryGetResponseContent`. This mirrors MAUI's approach and the iOS PoC's `ResolveRequest` pattern.

`ShouldOverrideUrlLoading` intercepts internal URL navigations to prevent full-page reloads, keeping Blazor's client-side router in control.

## JS-C# Bridge

| | iOS | Android |
|---|---|---|
| **JS to C#** | `WKScriptMessageHandler` + `ProtocolAdoption` workaround | `addJavascriptInterface` + `[JavascriptInterface]` |
| **C# to JS** | `WKWebView.EvaluateJavaScript()` | `WebView.EvaluateJavascript()` |
| **Init script injection** | `WKUserScript` at document-end | `EvaluateJavascript()` on `OnPageFinished` |
| **Workarounds needed** | Static registrar + `class_addProtocol` P/Invoke | None |

The `JsBridge` class is registered via `webView.AddJavascriptInterface(bridge, "HermesBridge")`. JavaScript calls `HermesBridge.postMessage(message)` to send messages to C#. The `[JavascriptInterface]` attribute marks the method as callable from JS. `PostMessage` is called on a WebView background thread, so it dispatches to the main thread via `Handler(Looper.MainLooper)` before forwarding to the Blazor pipeline. Similarly, `SendMessage` (C# to JS) dispatches `EvaluateJavascript` to the main thread when called off-thread.

The init script is guarded against double execution (`OnPageFinished` can fire multiple times) to prevent resetting Blazor's registered message callbacks.

## Component Mapping

| iOS | Android | Purpose |
|-----|---------|---------|
| `HermesMobileHost` | `HermesMobileAndroidHost` | Orchestrator |
| `HermesMobileAppBuilder` | `HermesMobileAndroidBuilder` | Builder pattern |
| `IOSWebViewManager` | `AndroidWebViewManager` | `WebViewManager` subclass |
| `EmbeddedFileServer` | `ShouldInterceptRequest` (in WebViewClient) | Asset serving |
| `IOSAssetFileProvider` | `AndroidAssetFileProvider` | `IFileProvider` implementation |
| `ScriptMessageHandler` | `JsBridge` | JS-to-C# bridge |
| `BlazorInitScript` | `BlazorInitScript` | Init script (different JS) |
| `AllowAllNavigationDelegate` | `HermesWebViewClient` | Asset interception, navigation control |
| (none) | `HermesWebChromeClient` | Console log forwarding |
| `IOSDispatcher` | `AndroidDispatcher` | Main thread marshalling |
| `IOSClipboard` | `AndroidClipboard` | `IClipboard` plugin |
| `ProtocolAdoption` | (not needed) | Obj-C runtime workaround |
| `AppSchemeHandler` | (not needed) | Custom scheme (future iOS) |
| `MimeTypeLookup` | (not needed) | Content-Type resolved by `TryGetResponseContent` headers |

## Project Structure

```
src/Hermes.Mobile.Android/
    Hermes.Mobile.Android.csproj    net10.0-android, min API 24
    HermesMobileAndroidBuilder.cs   Builder + RootComponentCollection
    HermesMobileAndroidHost.cs      Orchestrator, creates WebView + wires manager
    WebView/
        AndroidWebViewManager.cs    Extends WebViewManager
        BlazorInitScript.cs         JS bridge setup + Blazor.start()
        JsBridge.cs                 [JavascriptInterface] for JS-to-C# messages
        HermesWebViewClient.cs      Asset interception + init script on page load
        HermesWebChromeClient.cs    Console message forwarding
        AndroidAssetFileProvider.cs IFileProvider over AssetManager
    Threading/
        AndroidDispatcher.cs        Handler(Looper.MainLooper) dispatcher
    Plugins/
        AndroidClipboard.cs         IClipboard via ClipboardManager

samples/Shared/Shared.Android/
    Shared.Android.csproj           App project, references lib + Shared.App
    MainActivity.cs                 Entry point, creates host
    MainApplication.cs              [Application] class
    AndroidManifest.xml             App config
    Resources/
        values/styles.xml           Material theme
        wwwroot/index.html          Host page
```

## Build Requirements

- .NET 10 SDK with Android workload: `dotnet workload install android`
- Android SDK (installed via Android Studio or `dotnet android sdk install`)
- JDK (Android Studio bundles JBR)
- Environment variables:
  ```
  export ANDROID_HOME=$HOME/Library/Android/sdk
  export JAVA_HOME="/Applications/Android Studio.app/Contents/jbr/Contents/Home"
  ```

## Build Commands

```bash
# Build library
dotnet build src/Hermes.Mobile.Android/Hermes.Mobile.Android.csproj

# Build sample app
dotnet build samples/Shared/Shared.Android/Shared.Android.csproj

# Deploy to emulator/device
dotnet build samples/Shared/Shared.Android/Shared.Android.csproj -t:Run

# Debug with Chrome DevTools
# Open chrome://inspect in Chrome, the WebView appears as inspectable target
```

## Known Limitations

- No back button navigation handling (see Next Steps)
- No deep linking or intent handling
- No runtime permission handling for clipboard on Android 13+
- `AndroidAssetFileProvider` reports `Length = -1` since `AssetManager` doesn't expose file sizes
- Trimmer warnings from Blazor source generator (IL2111, IL2110) are benign, same as iOS

## Next Steps

1. **Back button navigation** - Override `OnBackPressed()` in Activity, call `webView.GoBack()` if `webView.CanGoBack()`. Critical Android UX expectation.
2. **Deep linking / intent handling** - Register URL schemes via intent filters in `AndroidManifest.xml`
3. **Status bar theming** - Match status bar color to Blazor app theme dynamically
4. **Soft keyboard handling** - Configure `windowSoftInputMode` for proper input field behavior
5. **Runtime permissions** - Request `android.permission.READ_CLIPBOARD` on Android 13+ (API 33+)
6. **Rename Hermes.Mobile to Hermes.Mobile.iOS** - Consolidate naming after both PoCs are proven
7. **Extract shared Hermes.Mobile** - Common abstractions (builder pattern, `RootComponentCollection`) once patterns stabilize
