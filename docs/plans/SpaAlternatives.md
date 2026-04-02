# Hermes.Web - JavaScript/TypeScript SPA Support

## Overview

Hermes.Web enables React, Vue, Angular, Svelte, and other JS/TS frameworks as alternatives to Blazor for building desktop UIs with Hermes. The core infrastructure already supports this: the `window.external` bridge is injected at the native WebView level, and the custom scheme handler pattern from Hermes.Blazor provides a proven model for content hosting.

**Status:** Research complete, ready for implementation

## Package Structure

**NuGet:** `Mythetech.Hermes.Web`

- Depends on `Mythetech.Hermes` (core only, no Blazor dependency)
- Targets `net10.0`

**npm:** `@hermes/bridge`

- Vanilla TypeScript, zero dependencies
- Framework-agnostic, adapter-ready (SvelteKit-inspired adapter model)

## C# API Design

```csharp
// Minimal example
var builder = HermesWebAppBuilder.CreateDefault(args);

builder.ConfigureWindow(options =>
{
    options.Title = "My React App";
    options.Width = 1280;
    options.Height = 720;
});

// Production: serve built assets
builder.UseStaticFiles("dist");   // explicit path, or empty for wwwroot/
builder.UseSpaFallback();         // index.html fallback for client-side routing

// Development option 1: connect to already-running dev server
builder.UseDevServer("http://localhost:5173");

// Development option 2: let Hermes manage the dev server process
builder.RunDevServer(dev =>
{
    dev.Command = "npm run dev";
    dev.Port = 5173;
    dev.WorkingDirectory = "./frontend";              // optional, defaults to project root
    dev.StartupTimeout = TimeSpan.FromSeconds(30);    // optional
});

// Interop bridge
builder.UseInteropBridge(bridge =>
{
    bridge.Register("greet", (string name) => $"Hello, {name}!");
});

var app = builder.Build();
app.Run();
```

## TypeScript API Design (`@hermes/bridge`)

```typescript
import { bridge } from '@hermes/bridge';

// Invoke C# method, returns a promise
const greeting = await bridge.invoke<string>('greet', 'World');

// Listen for C# -> JS messages
bridge.on('event-name', (data: unknown) => { ... });

// Send message to C#
bridge.send('event-name', { key: 'value' });
```

> **Feedback on the API design is welcomed.** This is an early design and we're open to community input on the shape of both the C# and TypeScript APIs.

## Architecture

### Project Layout

```
Hermes.Web/
├── HermesWebAppBuilder.cs        # Builder, configures window + content source
├── HermesWebApp.cs               # Runs message loop, owns window lifecycle
├── Hosting/
│   ├── StaticFileHost.cs         # Serves files via custom scheme handler
│   ├── SpaFallbackMiddleware.cs  # Routes unknown paths to index.html
│   ├── DevServerProxy.cs        # Proxies requests to external dev server (UseDevServer)
│   └── DevServerProcess.cs      # Launches + monitors dev server process (RunDevServer)
├── Interop/
│   ├── InteropBridge.cs          # Dispatches JSON messages to registered handlers
│   └── InteropBridgeOptions.cs   # Registration API for handlers
└── Mythetech.Hermes.Web.csproj
```

### Content Flow

**Production mode (static files):**

1. WebView navigates to base URI (`app://localhost/` on macOS/Linux, `http://localhost/` on Windows)
2. Custom scheme handler receives request
3. `StaticFileHost` resolves path against configured directory (explicit path or `wwwroot/`)
4. If file not found and SPA fallback enabled, serves `index.html`
5. Returns stream + content type to WebView

**Dev server mode (`UseDevServer`):**

1. WebView navigates directly to the dev server URL (e.g. `http://localhost:5173`)
2. HMR, live reload, etc. work natively since the WebView hits the real server
3. Interop bridge still works via injected/imported script

**Managed dev server (`RunDevServer`):**

1. `HermesWebApp.Run()` spawns the dev server process
2. Polls the port with HTTP GET every 500ms until 200 response or `StartupTimeout` expires
3. Once ready, navigates WebView to the URL
4. On app close, kills the dev server process tree (not just parent, since `npm run dev` spawns child processes)
   - Windows: `taskkill /T /PID` for process tree
   - macOS/Linux: kill process group

**Error handling:**

- `UseDevServer`: if server isn't reachable, shows an error page in the WebView with instructions
- `RunDevServer`: if process fails to start or times out, surfaces the error with stderr output

### Interop Bridge Protocol

```
JS: bridge.invoke('greet', 'World')
  -> JSON: {"type":"invoke","id":"abc123","method":"greet","args":["World"]}
  -> window.external.sendMessage(json)
  -> WebMessageReceived event on backend
  -> InteropBridge deserializes, finds handler, executes
  -> Result JSON: {"type":"result","id":"abc123","value":"Hello, World!"}
  -> SendWebMessage(json) back to WebView
  -> JS promise resolves with "Hello, World!"
```

Errors follow the same path with `{"type":"error","id":"abc123","message":"..."}` so the JS promise rejects.

### Bridge Injection in Dev Server Mode

When using a dev server, the interop bridge script needs to reach the WebView since it won't come from the static file host. Two approaches under consideration:

1. **Script tag approach:** Developer adds `<script>` or imports `@hermes/bridge` in their app. The npm package sets up the `window.external` listener on import. Explicit and doesn't fight with framework hydration.

2. **Auto-injection:** Inject via `WebView.ExecuteScriptAsync` after page load. Zero setup for the developer but may conflict with framework hydration timing.

> **Decision pending**, leaning toward the script tag approach. In the context of a desktop app, auto-injection is convenient but developers may not want injected scripts in their production builds.

## Static File Resolution

- `UseStaticFiles()` with no argument defaults to `wwwroot/` (familiar to .NET developers)
- `UseStaticFiles("dist")` uses an explicit path (accommodates JS framework output directories like `dist/`, `build/`, `out/`)
- Path traversal prevention via canonical path validation (matching existing Hermes.Blazor approach)

## Future Direction

### Path to Typed RPC (v2)

v1 ships with string-based JSON invoke (`bridge.invoke('method', ...args)`). The architecture is designed to grow toward typed RPC:

- The `InteropBridge.Register()` API captures method names and parameter types at registration time
- A future source generator could read these registrations and emit TypeScript type definitions
- Generator would output `.d.ts` files (or full `.ts` wrappers) as a build artifact
- This brings Tauri-like type safety without requiring Rust knowledge

### Framework Adapters (SvelteKit-inspired)

`@hermes/bridge` stays vanilla and dependency-free. Framework-specific adapters published separately:

- `@hermes/bridge-react` - `useHermesBridge()` hook, `useInvoke()` for queries
- `@hermes/bridge-vue` - `useHermes()` composable
- `@hermes/bridge-svelte` - Svelte stores or runes wrapping bridge events

Adapters are thin wrappers and community-contribution friendly.

### Sample Apps

- `samples/HelloWorld/` - minimal Vanilla + Vite example (ships with initial release)
- `samples/ReactHelloWorld/` - minimal React + Vite example (planned for initial release)
- Additional framework samples added over time

## Enterprise Value

- Broader developer pool (React/Vue/Angular adoption >> Blazor)
- Electron migration path for existing web apps
- Smaller bundle size (no Blazor runtime)
- Mature JS tooling ecosystem (Vite, ESLint, Prettier, etc.)
