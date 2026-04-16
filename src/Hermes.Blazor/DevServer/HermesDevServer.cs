// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor.DevServer;

/// <summary>
/// Internal Kestrel-based dev server for Blazor hot reload support.
/// Runs only when dotnet watch is detected (or ForceDevServer is set).
/// </summary>
internal sealed class HermesDevServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly FileSystemWatcher? _cssWatcher;

    private HermesDevServer(WebApplication app, FileSystemWatcher? cssWatcher)
    {
        _app = app;
        _cssWatcher = cssWatcher;
    }

    /// <summary>
    /// The base URL the webview should navigate to (e.g., http://127.0.0.1:54321).
    /// Available after StartAsync().
    /// </summary>
    internal string BaseUrl { get; private set; } = null!;

    /// <summary>
    /// Creates and starts the internal dev server.
    /// </summary>
    [RequiresDynamicCode("Blazor Server requires dynamic code")]
    [RequiresUnreferencedCode("Blazor Server uses reflection")]
    internal static async Task<HermesDevServer> StartAsync(
        Type rootComponentType,
        Action<IServiceCollection> configureServices,
        string hostPage,
        string wwwrootPath)
    {
        const int MaxRetries = 3;
        Exception? lastException = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await TryStartAsync(rootComponentType, configureServices, hostPage, wwwrootPath);
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
            {
                lastException = ex;
                Console.WriteLine($"[Hermes] Dev server port binding failed (attempt {attempt + 1}/{MaxRetries}): {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Dev server failed to start after {MaxRetries} attempts.",
            lastException);
    }

    [RequiresDynamicCode("Blazor Server requires dynamic code")]
    [RequiresUnreferencedCode("Blazor Server uses reflection")]
    private static async Task<HermesDevServer> TryStartAsync(
        Type rootComponentType,
        Action<IServiceCollection> configureServices,
        string hostPage,
        string wwwrootPath)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Development";

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        configureServices(builder.Services);

        if (Directory.Exists(wwwrootPath))
        {
            builder.Environment.WebRootPath = wwwrootPath;
            builder.Environment.WebRootFileProvider = new PhysicalFileProvider(wwwrootPath);
        }

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        // Track SSE clients for CSS reload notifications
        var sseClients = new List<TaskCompletionSource<bool>>();
        var sseLock = new object();
        long cssVersion = 0;

        // Serve dev-only endpoints and rewrite blazor.webview.js → blazor.web.js
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Value == "/_hermes/css-reload.js")
            {
                context.Response.ContentType = "application/javascript";
                context.Response.Headers.CacheControl = "no-cache";
                await context.Response.WriteAsync(CssHotReloadScript);
                return;
            }

            if (context.Request.Path.Value?.Contains("blazor.webview.js") == true)
            {
                context.Request.Path = context.Request.Path.Value.Replace("blazor.webview.js", "blazor.web.js");
            }
            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        });
        app.UseAntiforgery();

        MapRazorComponentsReflection(app, rootComponentType);

        // SSE endpoint for CSS hot reload notifications
        app.MapGet("/_hermes/css-reload", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            var lastSeen = Interlocked.Read(ref cssVersion);

            while (!ctx.RequestAborted.IsCancellationRequested)
            {
                var tcs = new TaskCompletionSource<bool>();
                lock (sseLock) { sseClients.Add(tcs); }

                try
                {
                    await tcs.Task.WaitAsync(ctx.RequestAborted);
                    await ctx.Response.WriteAsync($"data: reload\n\n", ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
                catch (OperationCanceledException) { break; }
                finally
                {
                    lock (sseLock) { sseClients.Remove(tcs); }
                }
            }
        });

        // Serve the host page with CSS hot reload script injected.
        // We use a custom fallback handler instead of MapFallbackToFile so we can
        // inject the SSE script before </body> in the served HTML.
        var hostPagePath = Path.Combine(wwwrootPath, hostPage);
        app.MapFallback(async (HttpContext ctx) =>
        {
            if (!File.Exists(hostPagePath))
            {
                ctx.Response.StatusCode = 404;
                return;
            }

            var html = await File.ReadAllTextAsync(hostPagePath);
            html = html.Replace("</body>", CssHotReloadScript + "\n</body>", StringComparison.OrdinalIgnoreCase);

            ctx.Response.ContentType = "text/html";
            ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await ctx.Response.WriteAsync(html);
        });

        await app.StartAsync();

        var address = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("Dev server did not bind to any address.");

        // Watch wwwroot for CSS changes and notify SSE clients
        FileSystemWatcher? watcher = null;
        if (Directory.Exists(wwwrootPath))
        {
            watcher = new FileSystemWatcher(wwwrootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            void OnCssChanged(object sender, FileSystemEventArgs e)
            {
                if (!e.FullPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) return;

                Interlocked.Increment(ref cssVersion);
                List<TaskCompletionSource<bool>> clients;
                lock (sseLock)
                {
                    clients = new List<TaskCompletionSource<bool>>(sseClients);
                    sseClients.Clear();
                }
                foreach (var tcs in clients)
                {
                    tcs.TrySetResult(true);
                }
            }

            watcher.Changed += OnCssChanged;
            watcher.Created += OnCssChanged;
            watcher.Renamed += (s, e) => OnCssChanged(s, e);
        }

        var server = new HermesDevServer(app, watcher)
        {
            BaseUrl = address
        };

        Console.WriteLine($"[Hermes] Dev mode detected (dotnet watch). Starting internal dev server on {address}");
        Console.WriteLine("[Hermes] Hot reload is active. Edit .razor, .cs, or .css files and changes will apply automatically.");

        return server;
    }

    /// <summary>
    /// Calls app.MapRazorComponents&lt;T&gt;().AddInteractiveServerRenderMode() via reflection,
    /// where T is the root component type known only at runtime.
    /// This only runs in dev mode, never in AOT/trimmed production builds.
    /// </summary>
    [RequiresDynamicCode("MapRazorComponents requires dynamic code")]
    [RequiresUnreferencedCode("MapRazorComponents uses reflection")]
    private static void MapRazorComponentsReflection(WebApplication app, Type rootComponentType)
    {
        var mapMethod = typeof(RazorComponentsEndpointRouteBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "MapRazorComponents" && m.IsGenericMethod)
            .MakeGenericMethod(rootComponentType);

        var conventionBuilder = mapMethod.Invoke(null, [app])!;

        var addServerMode = typeof(ServerRazorComponentsEndpointConventionBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "AddInteractiveServerRenderMode")
            .Invoke(null, [conventionBuilder]);
    }

    /// <summary>
    /// JavaScript that connects to the SSE endpoint and reloads stylesheets
    /// when CSS files change on disk. Served at /_hermes/css-reload.js.
    /// </summary>
    private const string CssHotReloadScript = """
        (function() {
            var es = new EventSource('/_hermes/css-reload');
            es.onmessage = function() {
                document.querySelectorAll('link[rel="stylesheet"]').forEach(function(link) {
                    var url = new URL(link.href);
                    url.searchParams.set('_hr', Date.now());
                    link.href = url.toString();
                });
            };
        })();
        """;

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[Hermes] Dev server stopped.");
        _cssWatcher?.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
