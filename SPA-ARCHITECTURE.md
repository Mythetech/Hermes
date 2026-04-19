# Hermes - SPA Framework Support

## Goal
Enable frontend SPA frameworks (React, Svelte, vanilla JS, and future frameworks) to build native desktop apps with Hermes, using a shared interop bridge for bidirectional communication between JavaScript and .NET.

## Key Decisions
- **Framework-agnostic core** - `@hermes/bridge` provides universal JS↔.NET communication; framework adapters are thin wrappers
- **Vite-based toolchain** - All SPA samples use Vite for dev server and production builds
- **AOT-safe serialization** - JSON source generation (`JsonSerializerContext`) throughout, no reflection
- **Platform-transparent hosting** - `StaticFileHost` abstracts scheme differences (`http://` on Windows, `app://` on macOS/Linux)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│                   SPA Frontend                       │
│  ┌──────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ Vanilla  │  │ @hermes/react│  │ @hermes/svelte│  │
│  │   JS     │  │  (useInvoke) │  │   (stores)    │  │
│  └────┬─────┘  └──────┬───────┘  └──────┬────────┘  │
│       │               │                 │            │
│       └───────────┬───┘─────────────────┘            │
│             ┌─────┴──────┐                           │
│             │@hermes/bridge│ ← core JS bridge        │
│             └─────┬──────┘                           │
├───────────────────┼──────────────────────────────────┤
│          window.external                             │
│         (sendMessage / receiveMessage)               │
├───────────────────┼──────────────────────────────────┤
│             ┌─────┴──────┐                           │
│             │InteropBridge│ ← C# message processor   │
│             └─────┬──────┘                           │
│       ┌───────────┼───────────────┐                  │
│  ┌────┴────┐ ┌────┴─────┐ ┌──────┴──────┐           │
│  │Handlers │ │  Events  │ │StaticFileHost│           │
│  │(invoke) │ │(push/sub)│ │ (asset srv)  │           │
│  └─────────┘ └──────────┘ └─────────────┘           │
│                .NET Host (Hermes.Web)                 │
└──────────────────────────────────────────────────────┘
```

---

## Package Structure

### NPM Packages (Bridge Layer)

```
packages/
├── hermes-bridge/          # @hermes/bridge - core JS↔.NET communication
│   ├── src/
│   │   ├── bridge.ts       # HermesBridge class, message dispatch
│   │   ├── types.ts        # InvokeOptions, HermesExternal interface
│   │   └── index.ts        # Public exports
│   ├── tsconfig.json       # ESM output
│   └── tsconfig.cjs.json   # CJS output (dual-publish)
│
├── hermes-react/           # @hermes/react - React hooks
│   ├── src/
│   │   ├── useInvoke.ts    # useInvoke<T> hook
│   │   └── index.ts
│   └── package.json        # peer: @hermes/bridge, react
│
└── hermes-svelte/          # @hermes/svelte - Svelte stores
    ├── src/
    │   ├── stores.ts       # hermesConnected, createInvokeStore, createEventStore
    │   └── index.ts
    └── package.json        # peer: @hermes/bridge, svelte
```

### NuGet Package

```
src/Hermes.Web/             # Mythetech.Hermes.Web
├── Hermes.Web.csproj       # Depends on Mythetech.Hermes (core)
├── HermesWebApp.cs          # App facade (Run, Bridge access)
├── HermesWebAppBuilder.cs   # Fluent builder API
├── Hosting/
│   ├── StaticFileHost.cs    # SPA-aware file server
│   └── MimeTypes.cs         # Extension → MIME mapping
└── Interop/
    ├── InteropBridge.cs     # Bidirectional message processor
    ├── InteropBridgeOptions.cs  # Fluent handler registration
    └── InteropMessage.cs    # JSON envelope types + source gen context
```

---

## Application Startup Flow

```
Program.cs
  ├─ HermesWindow.Prewarm()              # Optional platform pre-warming
  ├─ HermesWebAppBuilder.Create(args)
  ├─ builder.ConfigureWindow(opts => {})  # Title, size, DevTools, etc.
  ├─ builder.UseStaticFiles("wwwroot")    # OR builder.UseDevServer(url)
  ├─ builder.UseSpaFallback()             # Route extensionless paths → index.html
  ├─ builder.UseInteropBridge(bridge => {})
  ├─ builder.Build()
  │   ├─ Creates HermesWindow
  │   ├─ Registers custom scheme handler (StaticFileHost)
  │   ├─ Sets navigation URL (platform-specific scheme)
  │   ├─ Creates InteropBridge (if configured)
  │   └─ Returns HermesWebApp
  └─ app.Run()                            # Blocks until window closes
```

### Typical Program.cs

```csharp
HermesWindow.Prewarm();
var builder = HermesWebAppBuilder.Create(args);

builder.ConfigureWindow(opts =>
{
    opts.Title = "My SPA App";
    opts.Width = 800;
    opts.Height = 600;
    opts.DevToolsEnabled = true;
});

var isDev = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";

if (isDev)
{
    builder.UseDevServer("http://localhost:5173");
}
else
{
    builder.UseStaticFiles("frontend/dist");
    builder.UseSpaFallback();
}

builder.UseInteropBridge(bridge =>
{
    bridge.Register<string, string>("greet", name => $"Hello, {name}!");
    bridge.Register("getRuntime", () => $".NET {Environment.Version}");
    bridge.Register("getPlatform", () => Environment.OSVersion.Platform.ToString());
});

var app = builder.Build();
app.Run();
```

---

## Interop Bridge

### Message Envelopes

All communication uses JSON envelopes with a `type` discriminator:

| Direction | Type | Fields | Purpose |
|-----------|------|--------|---------|
| JS → C# | `invoke` | `id`, `method`, `args` | Call a registered .NET handler |
| C# → JS | `result` | `id`, `value` | Return value from invoke |
| C# → JS | `error` | `id`, `message` | Error from invoke |
| Either | `event` | `name`, `data` | Fire-and-forget notification |

```json
// Invoke (JS → C#)
{"type": "invoke", "id": "uuid", "method": "greet", "args": ["World"]}

// Result (C# → JS)
{"type": "result", "id": "uuid", "value": "Hello, World!"}

// Error (C# → JS)
{"type": "error", "id": "uuid", "message": "Method not found: foo"}

// Event (either direction)
{"type": "event", "name": "tick", "data": 42}
```

### JSON Source Generation

All envelope types use `JsonSerializerContext` for AOT safety:

```csharp
[JsonSerializable(typeof(InteropEnvelope))]
[JsonSerializable(typeof(ResultEnvelope))]
[JsonSerializable(typeof(ErrorEnvelope))]
[JsonSerializable(typeof(EventEnvelope))]
internal partial class InteropJsonContext : JsonSerializerContext { }
```

### C# Registration API

```csharp
// Sync handlers
bridge.Register(string method, Func<object?> handler)
bridge.Register<TResult>(string method, Func<TResult> handler)
bridge.Register<TArg, TResult>(string method, Func<TArg, TResult> handler)

// Async handlers
bridge.RegisterAsync<TResult>(string method, Func<Task<TResult>> handler)
bridge.RegisterAsync<TArg, TResult>(string method, Func<TArg, Task<TResult>> handler)

// Events: subscribe to JS events
bridge.On(string eventName, Action handler)
bridge.On<T>(string eventName, Action<T> handler)

// Events: push to JS
bridge.Send(string eventName, object? data = null)
```

### Invoke Flow (JS → C#)

```
JS: bridge.invoke('greet', 'World')
  → JSON: {type: "invoke", id: "abc-123", method: "greet", args: ["World"]}
  → window.external.sendMessage(json)
    → Platform WebView delivers to .NET
      → InteropBridge.OnWebMessageReceived(json)
        → Deserialize InvokeEnvelope
        → Look up "greet" in _invokeHandlers
        → Execute handler("World") → "Hello, World!"
        → Serialize ResultEnvelope {type: "result", id: "abc-123", value: "Hello, World!"}
        → backend.SendWebMessage(json)
          → window.external.receiveMessage(json)
            → JS Promise resolves with "Hello, World!"
```

### Event Flow (C# → JS)

```
C#: bridge.Send("tick", 42)
  → JSON: {type: "event", name: "tick", data: 42}
  → backend.SendWebMessage(json)
    → window.external.receiveMessage(json)
      → Bridge dispatches to listeners registered via bridge.on("tick", cb)
```

### Error Handling

| Scenario | Behavior |
|----------|----------|
| Unknown method | C# sends error envelope, JS Promise rejects |
| Handler throws | Exception caught, error envelope sent, JS Promise rejects |
| Non-JSON message | Silently ignored with console warning |
| No Hermes runtime | `bridge.isHermes` returns false, invokes fail gracefully |

---

## JavaScript Bridge API

### Core Bridge (`@hermes/bridge`)

```typescript
import { bridge } from '@hermes/bridge';

// Detect environment
if (bridge.isHermes) { /* running in Hermes window */ }

// Invoke .NET method
const greeting = await bridge.invoke<string>('greet', 'World');

// Invoke with options
const result = await bridge.invoke<string>('slowOp', { timeout: 5000 }, arg1);

// Subscribe to .NET events (returns unsubscribe function)
const unsub = bridge.on<number>('tick', (seconds) => {
    console.log(`Tick: ${seconds}`);
});

// Push event to .NET
bridge.send('userAction', { type: 'click', target: 'button' });

// Cleanup
unsub();
```

### React Adapter (`@hermes/react`)

```typescript
import { useInvoke } from '@hermes/react';

function GreetCard() {
    const [name, setName] = useState('World');
    const { data, loading, error, invoke } = useInvoke<string>('greet');

    return (
        <div>
            <input value={name} onChange={e => setName(e.target.value)} />
            <button onClick={() => invoke(name)}>Greet</button>
            {loading && <span>Loading...</span>}
            {error && <span>Error: {error.message}</span>}
            {data && <p>{data}</p>}
        </div>
    );
}
```

**Hook lifecycle:**
1. On mount, invokes the method once (no args) to populate initial data
2. `invoke(...args)` triggers explicit calls with arguments
3. `refetch()` re-invokes with the last-used arguments
4. Prevents state updates on unmounted components via `mountedRef`

### Svelte Adapter (`@hermes/svelte`)

```svelte
<script>
    import { hermesConnected, createInvokeStore, createEventStore } from '@hermes/svelte';

    const greet = createInvokeStore<string>('greet', 'World');
    const tick = createEventStore<number>('tick');
</script>

{#if $hermesConnected}
    <p>Greeting: {$greet.data}</p>
    <p>Seconds: {$tick ?? 0}</p>
{:else}
    <p>Not connected to Hermes</p>
{/if}
```

**Store types:**
- `hermesConnected: Readable<boolean>` — whether running inside Hermes
- `createInvokeStore<T>(method, ...args): Readable<InvokeState<T>>` — auto-invokes on creation, exposes `{ data, loading, error }`
- `createEventStore<T>(eventName, initialValue?): Readable<T | undefined>` — reactive store that updates on each event push from .NET

---

## Static File Hosting

### StaticFileHost

Serves frontend build output from disk with SPA routing support.

**Features:**
- Resolves files relative to a configurable root path (default: `wwwroot`)
- SPA fallback: extensionless paths return `index.html`
- Query string stripping for cache-busted URLs
- Directory traversal prevention
- Automatic MIME type detection

**Supported MIME types:**

| Category | Extensions |
|----------|------------|
| Web | `.html`, `.css`, `.js`, `.mjs`, `.json`, `.xml`, `.svg` |
| Images | `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, `.avif`, `.ico` |
| Fonts | `.woff`, `.woff2`, `.ttf`, `.otf`, `.eot` |
| Media | `.webm`, `.mp4`, `.mp3`, `.ogg`, `.wav` |
| Other | `.wasm`, `.map`, `.txt` |

### Platform Scheme Differences

| Platform | Scheme | WebView | Why |
|----------|--------|---------|-----|
| Windows | `http://localhost/` | WebView2 | Standard HTTP works natively |
| macOS | `app://localhost/` | WKWebView | Custom scheme required, must pre-register before `Initialize` |
| Linux | `app://localhost/` | WebKitGTK | Custom scheme required |

The builder handles this transparently:

```csharp
// In HermesWebAppBuilder.Build()
if (OperatingSystem.IsWindows())
    window.RegisterCustomScheme("http", staticFileHost.HandleRequest);
else
    window.RegisterCustomScheme("app", staticFileHost.HandleRequest);

var baseUri = OperatingSystem.IsWindows()
    ? "http://localhost/"
    : "app://localhost/";
window.Load(baseUri);
```

macOS/Linux must pre-register the `app` scheme even when using a dev server, because WKWebView/WebKitGTK require scheme registration before initialization.

### Dev Server vs Production

Using an environment variable (recommended):

```csharp
var isDev = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development";

if (isDev)
{
    builder.UseDevServer("http://localhost:5173");
}
else
{
    builder.UseStaticFiles("frontend/dist");
    builder.UseSpaFallback();
}
```

Alternatively, using compiler directives:

```csharp
#if DEBUG
builder.UseDevServer("http://localhost:5173");
#else
builder.UseStaticFiles("frontend/dist");
builder.UseSpaFallback();
#endif
```

---

## Builder API

`HermesWebAppBuilder` provides a fluent configuration API:

```csharp
var builder = HermesWebAppBuilder.Create(args);

builder
    .ConfigureWindow(opts => { /* HermesWindowOptions */ })
    .UseStaticFiles("frontend/dist")    // Set static file root
    .UseSpaFallback()                    // Enable SPA routing
    .UseInteropBridge(bridge =>          // Register .NET handlers
    {
        bridge.Register("method", () => result);
        bridge.RegisterAsync("async", () => Task.FromResult(result));
        bridge.On("event", () => { });
    });

var app = builder.Build();
app.Run();
```

All configuration is deferred until `Build()`, which creates the window, registers schemes, and wires up the bridge in a single pass.

---

## Adding a New Framework Adapter

The bridge pattern is designed for extension. Adding support for a new JS framework (e.g., Vue, Angular, Solid) requires only a new NPM package.

### 1. Create the package

```
packages/hermes-<framework>/
├── package.json
├── tsconfig.json
├── tsconfig.cjs.json        # CJS output for Node compatibility
├── src/
│   ├── index.ts              # Public exports
│   └── <framework-api>.ts    # Framework-specific wrappers
└── dist/                     # Built output (ESM + CJS + types)
```

### 2. Peer-depend on the core bridge

```json
{
  "name": "@hermes/<framework>",
  "peerDependencies": {
    "@hermes/bridge": ">=1.0.0-preview.1",
    "<framework>": "^X.0.0"
  }
}
```

### 3. Wrap bridge calls in framework idioms

The adapter's job is to map `bridge.invoke()` and `bridge.on()` into the framework's reactive primitives:

| Framework | Invoke pattern | Event pattern |
|-----------|---------------|---------------|
| React | `useInvoke<T>()` hook with `data/loading/error` state | `useEffect` + `bridge.on()` |
| Svelte | `createInvokeStore<T>()` readable store | `createEventStore<T>()` readable store |
| Vue 3 | `useInvoke<T>()` composable with `ref()` | `useEvent<T>()` composable |
| Solid | `createInvoke<T>()` with signals | `createEvent<T>()` with signals |

### 4. Export and document

```typescript
export { useInvoke } from './composable.js';
export type { UseInvokeResult } from './composable.js';
```

No changes to the C# side are needed. The .NET `InteropBridge` is framework-agnostic; all framework adapters communicate through the same `window.external` message channel.

---

## Sample Apps

Three samples demonstrate the full stack:

| Sample | Framework | Bridge Package | Key Patterns |
|--------|-----------|---------------|--------------|
| `samples/WebHelloWorld/` | Vanilla TS | `@hermes/bridge` | Direct `bridge.invoke()` calls |
| `samples/ReactHelloWorld/` | React + TS | `@hermes/react` | `useInvoke` hook, component state |
| `samples/SvelteHelloWorld/` | Svelte + TS | `@hermes/svelte` | Reactive stores, C#→JS event push |

All samples share the same C# backend pattern (greet, getRuntime, getPlatform handlers) and use Vite for both development and production builds.

---

## Test Coverage

Tests are located in `tests/Hermes.Tests/Web/`:

| Test Class | Coverage |
|------------|----------|
| `InteropBridgeTests` | Send events, invoke registered methods (sync/async), error envelopes for unknown methods and handler exceptions, event subscription, message detachment, non-JSON resilience |
| `StaticFileHostTests` | Root path resolution, nested files, MIME detection, directory traversal prevention, query string stripping, URL scheme parsing (`http`, `app`, relative), SPA fallback for extensionless paths |
| `HermesWebAppBuilderTests` | Builder creation, fluent chaining, configuration accumulation |

---

## Design Principles

- **No reflection** — JSON source generation and explicit handler registration keep the bridge AOT-safe and trimmable
- **Graceful degradation** — `bridge.isHermes` lets frontend code detect the runtime; all bridge methods fail cleanly outside Hermes
- **Lazy initialization** — The JS bridge defers `window.external` lookup until first use, avoiding errors when loaded in a browser
- **Memory-safe cleanup** — Event listeners return unsubscribe functions; React hooks track mount state; Svelte stores use framework lifecycle
- **Platform transparency** — Scheme differences are handled entirely in the builder; app code never sees `http://` vs `app://`
