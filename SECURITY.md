# Hermes Security

This document describes the security architecture of Hermes and provides guidance for building secure applications with the framework.

## Security Architecture

### WebView Sandbox Delegation

Hermes delegates all web content rendering and JavaScript execution to platform-native WebView engines:

| Platform | WebView Engine | Sandbox |
|----------|----------------|---------|
| Windows | WebView2 (Chromium/Edge) | Chromium multi-process sandbox |
| macOS | WKWebView | WebKit process isolation |
| Linux | WebKit2GTK | WebKit process isolation |

The framework does not implement custom JavaScript engines, parsers, or network stacks. Security updates to WebView engines are delivered through OS updates.

### Trust Boundaries

```
┌─────────────────────────────────────────────────────────┐
│  .NET Application (Hermes + Blazor)                     │
│  ┌─────────────────────────────────────────────────┐    │
│  │ Application Code / Blazor Components            │    │
│  └───────────────────┬─────────────────────────────┘    │
└──────────────────────┼──────────────────────────────────┘
                       │
            ┌──────────┴──────────┐
            ↓                     ↓
   [WebMessage Channel]    [Custom Scheme Handler]
            │                     │
┌───────────▼─────────────────────▼───────────────────────┐
│  WebView Process (OS-level sandbox)                      │
│  ┌─────────────────────────────────────────────────┐    │
│  │ JavaScript / Web Content                         │    │
│  └─────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

**Key boundaries:**
- JavaScript ↔ .NET: Messages passed as strings via `window.external.sendMessage()` / `WebMessageReceived`
- Custom Schemes: HTTP-like requests intercepted by .NET handler, responses returned as streams
- Native P/Invoke: Platform backends communicate with native libraries for window management

### What Hermes Does NOT Do

The framework intentionally excludes:
- **Direct network requests** - All HTTP/WebSocket handled by WebView engine
- **Credential storage** - Applications must implement their own secure storage
- **Process spawning** - No `Process.Start()` or shell execution in the core library
- **File system access** - Beyond window state persistence in standard app data directories

## Configuration for Production

### DevTools

Developer tools are **disabled by default** (`DevToolsEnabled = false`). This prevents end users from inspecting application internals, modifying DOM, or executing arbitrary JavaScript.

If you explicitly enable DevTools for debugging:
```csharp
window.SetDevToolsEnabled(true); // Only for development
```

Ensure this is disabled in release builds.

### Context Menu

The browser context menu is enabled by default. For kiosk-mode or locked-down applications, disable it:

```csharp
window.SetContextMenuEnabled(false);
```

This prevents users from accessing "Inspect Element" or other browser-provided menu items.

### Custom Scheme Handlers

If you register custom scheme handlers:

```csharp
window.RegisterCustomScheme("myscheme", (url) => {
    // IMPORTANT: Validate the URL path
    var uri = new Uri(url);
    var path = uri.AbsolutePath;

    // Prevent path traversal
    if (path.Contains(".."))
        return (null, null);

    // Only serve from allowed directory
    var safePath = Path.GetFullPath(Path.Combine(AllowedRoot, path.TrimStart('/')));
    if (!safePath.StartsWith(AllowedRoot))
        return (null, null);

    // Serve the file
    return (File.OpenRead(safePath), GetContentType(path));
});
```

## Known Considerations

### CORS Headers on Custom Schemes

Custom scheme responses include permissive CORS headers (`Access-Control-Allow-Origin: *`). This is **by design** - restrictive CORS policies block legitimate requests for third-party Blazor component assets. Razor Class Libraries from NuGet packages (served via `_content/{PackageName}/`) require cross-origin access to load correctly.

Since custom schemes serve local application content only (your app's wwwroot and RCL assets), cross-origin access from external sites is not a concern in typical usage.

**Guidance:** Do not load untrusted external content via iframes or fetch from external origins in your WebView. If you must load external content, implement application-level validation.

### WebMessage Size

Individual WebMessage size is not limited by the framework. Applications should validate and limit message sizes in their Blazor components to prevent memory exhaustion.

### Static Asset Provider

Static web assets are resolved from a compile-time manifest. The manifest paths are trusted because they're generated at build time, not from user input.

### Window State Persistence

Window position and size are stored in plaintext JSON at platform-specific locations:
- Windows: `%LOCALAPPDATA%\Hermes\WindowState\`
- macOS: `~/Library/Application Support/Hermes/WindowState/`
- Linux: `~/.local/share/Hermes/WindowState/`

This data reveals application usage patterns but contains no credentials or sensitive content.

## Secure Development Practices

### Secrets Management

**Never** store secrets in `appsettings.json` or embed them in source code. Use:
- Environment variables for deployment secrets
- Platform-specific secure storage (Windows Credential Manager, macOS Keychain, etc.)
- User-prompted credentials stored in memory only

### Input Validation

Treat all WebMessage input as untrusted:

```csharp
backend.WebMessageReceived += (message) => {
    // Validate JSON structure
    if (!TryParseMessage(message, out var command))
        return;

    // Validate command type
    if (!AllowedCommands.Contains(command.Type))
        return;

    // Validate parameters
    if (!ValidateParameters(command))
        return;

    // Process
    HandleCommand(command);
};
```

### Content Security Policy

Add CSP headers to your HTML to restrict script execution:

```html
<meta http-equiv="Content-Security-Policy"
      content="default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline';">
```

For Blazor applications, `'unsafe-eval'` is required for WebAssembly.

### Keep WebView Updated

On Windows, ensure the WebView2 Runtime is updated. Hermes uses the Evergreen distribution which auto-updates, but enterprise deployments may use Fixed Version which requires manual updates.

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.x     | Yes       |

## Reporting a Vulnerability

If you discover a security vulnerability in Hermes, please report it responsibly:

1. **Email:** security@mythetech.com
2. **Include:** Description, reproduction steps, potential impact
3. **Response:** We will acknowledge within 48 hours
4. **Timeline:** We aim to release fixes within 30 days of confirmed vulnerabilities

Please do not open public issues for security vulnerabilities.

## Third-Party Dependencies

Hermes relies on these security-relevant dependencies:

| Dependency | Purpose | Updates |
|------------|---------|---------|
| WebView2 (Windows) | Browser engine | Microsoft auto-updates |
| WKWebView (macOS) | Browser engine | Apple OS updates |
| WebKit2GTK (Linux) | Browser engine | Distribution packages |
| System.Text.Json | JSON parsing | .NET runtime updates |

See [THIRD_PARTY_LICENSES](THIRD_PARTY_LICENSES/) for full license information.
