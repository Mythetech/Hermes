using System.Runtime.Versioning;
using Microsoft.Web.WebView2.Core;

namespace Hermes.Platforms.Windows;

/// <summary>
/// Pre-warms and pools a shared WebView2 environment for faster window creation.
/// Thread-safe singleton with lazy initialization.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WebView2EnvironmentPool
{
    private static readonly Lazy<WebView2EnvironmentPool> s_instance =
        new(() => new WebView2EnvironmentPool(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static WebView2EnvironmentPool Instance => s_instance.Value;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private CoreWebView2Environment? _sharedEnvironment;
    private Task<CoreWebView2Environment>? _initTask;
    private string? _userDataFolder;

    private WebView2EnvironmentPool() { }

    /// <summary>
    /// Begin pre-warming on a background thread. Call early in app startup.
    /// Fire-and-forget - exceptions are logged but not thrown.
    /// </summary>
    public void BeginPrewarm(string? userDataFolder = null)
    {
        _userDataFolder = userDataFolder;
        _ = GetOrCreateEnvironmentAsync();
    }

    /// <summary>
    /// Get the pre-warmed environment, or create one if not yet ready.
    /// Returns the shared environment instance.
    /// </summary>
    public async ValueTask<CoreWebView2Environment> GetOrCreateEnvironmentAsync(
        string? userDataFolder = null)
    {
        // Fast path: environment already created
        if (_sharedEnvironment is not null)
            return _sharedEnvironment;

        // Check if initialization is in progress
        var existingTask = _initTask;
        if (existingTask is not null)
            return await existingTask.ConfigureAwait(false);

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (_sharedEnvironment is not null)
                return _sharedEnvironment;

            // Check again if another thread started initialization
            if (_initTask is not null)
            {
                _initLock.Release();
                return await _initTask.ConfigureAwait(false);
            }

            // Start initialization
            _initTask = CreateEnvironmentAsync(userDataFolder ?? _userDataFolder);
            _sharedEnvironment = await _initTask.ConfigureAwait(false);
            return _sharedEnvironment;
        }
        finally
        {
            if (_initLock.CurrentCount == 0)
                _initLock.Release();
        }
    }

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync(string? userDataFolder)
    {
        userDataFolder ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hermes",
            "WebView2");

        Directory.CreateDirectory(userDataFolder);

        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: null).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets whether the environment has been pre-warmed and is ready.
    /// </summary>
    public bool IsReady => _sharedEnvironment is not null;
}
