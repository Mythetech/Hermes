// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor.DevServer;

/// <summary>
/// Internal Kestrel-based dev server for Blazor hot reload support.
/// Serves static files (HTML, CSS, JS) over HTTP so the webview can load them
/// with no-cache headers, enabling CSS hot reload. Rendering and interactivity
/// are handled by the WebViewManager through the native bridge (blazor.webview.js),
/// not by Blazor Server — this is purely a static file server with an SSE endpoint.
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
    /// </summary>
    internal string BaseUrl { get; private set; } = null!;

    /// <summary>
    /// Creates and starts the internal dev server.
    /// </summary>
    internal static async Task<HermesDevServer> StartAsync(
        string hostPage,
        string wwwrootPath)
    {
        const int MaxRetries = 3;
        Exception? lastException = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await TryStartAsync(hostPage, wwwrootPath);
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

    private static async Task<HermesDevServer> TryStartAsync(
        string hostPage,
        string wwwrootPath)
    {
        // Resolve the source wwwroot directory from the static web assets manifest.
        // dotnet watch serves static files from source directories, not the build
        // output. The manifest's ContentRoots[0] is the project's source wwwroot.
        // We use it for both static file serving and the CSS file watcher so edits
        // to the source files are detected and served immediately.
        var sourceWwwroot = ResolveSourceWwwroot(wwwrootPath);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Environment.WebRootPath = sourceWwwroot;

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        // Track SSE clients for CSS reload notifications
        var sseClients = new List<TaskCompletionSource<bool>>();
        var sseLock = new object();
        long cssVersion = 0;

        // Intercept the host page request BEFORE UseStaticFiles to inject the
        // CSS hot reload script. WebViewManager navigates to /{hostPage} (e.g.,
        // /index.html), which UseStaticFiles would serve directly from disk
        // without the script injection.
        var hostPagePath = Path.Combine(sourceWwwroot, hostPage);
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value?.TrimStart('/') ?? "";
            if (path.Equals(hostPage, StringComparison.OrdinalIgnoreCase)
                || path == string.Empty)
            {
                if (File.Exists(hostPagePath))
                {
                    var html = await File.ReadAllTextAsync(hostPagePath);
                    html = html.Replace("</body>", CssHotReloadScriptTag + "\n</body>", StringComparison.OrdinalIgnoreCase);

                    context.Response.ContentType = "text/html";
                    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                    await context.Response.WriteAsync(html);
                    return;
                }
            }
            await next();
        });

        // Serve static files from the source wwwroot with no-cache headers
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        });

        // Serve _framework/ files (blazor.webview.js, blazor.modules.json) from the
        // static web assets manifest. These live in NuGet package directories, not
        // wwwroot. Parse the build-time manifest to find the physical path.
        var frameworkAssetsPath = FindFrameworkAssetsPath();
        if (frameworkAssetsPath is not null)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(frameworkAssetsPath),
                RequestPath = "/_framework",
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                }
            });
        }

        // SSE endpoint for CSS hot reload notifications
        app.MapGet("/_hermes/css-reload", async (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

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

        await app.StartAsync();

        var address = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("Dev server did not bind to any address.");

        // Watch the source wwwroot for CSS changes and notify SSE clients
        FileSystemWatcher? watcher = null;
        if (Directory.Exists(sourceWwwroot))
        {
            watcher = new FileSystemWatcher(sourceWwwroot)
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
    /// Resolves the source wwwroot directory from the static web assets manifest.
    /// The build output wwwroot is a snapshot; dotnet watch serves from source
    /// directories listed in the manifest. Falls back to the build output path.
    /// </summary>
    private static string ResolveSourceWwwroot(string buildOutputWwwroot)
    {
        var entryName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryName is null) return buildOutputWwwroot;

        var manifestPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"{entryName}.staticwebassets.runtime.json");

        if (!File.Exists(manifestPath)) return buildOutputWwwroot;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
            var contentRoots = doc.RootElement.GetProperty("ContentRoots")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();

            // ContentRoots[0] is the project's source wwwroot directory
            if (contentRoots.Length > 0 && Directory.Exists(contentRoots[0]))
            {
                return contentRoots[0].TrimEnd('/');
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hermes] Warning: Failed to resolve source wwwroot: {ex.Message}");
        }

        return buildOutputWwwroot;
    }

    /// <summary>
    /// Parses the static web assets manifest to find the physical directory that
    /// serves _framework/ files (blazor.webview.js). The manifest maps virtual paths
    /// to content roots — _framework children point to the WebView NuGet package.
    /// </summary>
    private static string? FindFrameworkAssetsPath()
    {
        var entryName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryName is null) return null;

        var manifestPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"{entryName}.staticwebassets.runtime.json");

        if (!File.Exists(manifestPath)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
            var contentRoots = doc.RootElement.GetProperty("ContentRoots")
                .EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();

            // Find the _framework node and get its first child's content root
            var root = doc.RootElement.GetProperty("Root");
            if (root.TryGetProperty("Children", out var children)
                && children.TryGetProperty("_framework", out var framework)
                && framework.TryGetProperty("Children", out var fwChildren))
            {
                // Get the content root index from any child asset
                foreach (var child in fwChildren.EnumerateObject())
                {
                    if (child.Value.TryGetProperty("Asset", out var asset)
                        && asset.TryGetProperty("ContentRootIndex", out var indexEl))
                    {
                        var idx = indexEl.GetInt32();
                        if (idx >= 0 && idx < contentRoots.Length && Directory.Exists(contentRoots[idx]))
                        {
                            return contentRoots[idx];
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hermes] Warning: Failed to parse static web assets manifest: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Script tag injected into the host page to enable CSS hot reload via SSE.
    /// Connects to the /_hermes/css-reload SSE endpoint and cache-busts all
    /// stylesheet hrefs when a CSS file change is detected.
    /// </summary>
    private const string CssHotReloadScriptTag = """
        <script>
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
        </script>
        """;

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[Hermes] Dev server stopped.");
        _cssWatcher?.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
