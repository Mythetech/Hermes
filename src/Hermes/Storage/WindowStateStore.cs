// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Hermes.Diagnostics;

namespace Hermes.Storage;

/// <summary>
/// Manages window state persistence to the application data directory.
/// Thread-safe singleton for application-wide window state management.
/// </summary>
public sealed class WindowStateStore
{
    private static readonly Lazy<WindowStateStore> s_instance =
        new(() => new WindowStateStore(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the window state store.
    /// </summary>
    public static WindowStateStore Instance => s_instance.Value;

    private const int MaxReplaceAttempts = 10;
    private const int ReplaceRetryDelayMilliseconds = 5;

    private readonly object _lock = new();
    private Dictionary<string, WindowState>? _cache;
    private string? _filePath;

    private WindowStateStore()
    {
    }

    internal WindowStateStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Points the store at another file before it has loaded one, so a test run never writes the user's data.</summary>
    internal void UseFileForTest(string filePath)
    {
        lock (_lock)
        {
            if (_cache is not null)
                throw new InvalidOperationException("The window state store has already loaded its file.");

            _filePath = filePath;
        }
    }

    /// <summary>
    /// Tries to load window state for the given key.
    /// </summary>
    /// <param name="key">The window state key.</param>
    /// <param name="state">The loaded state, or null if not found.</param>
    /// <returns>True if state was found; otherwise, false.</returns>
    public bool TryGetState(string key, out WindowState? state)
    {
        lock (_lock)
        {
            EnsureLoaded();

            if (_cache!.TryGetValue(key, out var cached))
            {
                state = cached;
                return true;
            }

            state = null;
            return false;
        }
    }

    /// <summary>
    /// Saves window state for the given key.
    /// </summary>
    /// <param name="key">The window state key.</param>
    /// <param name="state">The state to save.</param>
    public void SaveState(string key, WindowState state)
    {
        lock (_lock)
        {
            EnsureLoaded();
            _cache![key] = state;
            PersistToDisk();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null)
            return;

        _filePath ??= GetFilePath();
        _cache = LoadFromDisk();
    }

    private static string GetFilePath()
    {
        var appName = AppDataDirectories.GetApplicationName();
        var directory = AppDataDirectories.GetUserDataPath("WindowState");
        return Path.Combine(directory, $"{appName}.json");
    }

    private Dictionary<string, WindowState> LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, WindowState>();
            }

            var json = File.ReadAllText(_filePath);
            var result = JsonSerializer.Deserialize(json, WindowStateJsonContext.Default.DictionaryStringWindowState);
            return result ?? new Dictionary<string, WindowState>();
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Failed to load window state from {_filePath}: {ex.Message}");
            return new Dictionary<string, WindowState>();
        }
    }

    private void PersistToDisk()
    {
        if (_filePath is null || _cache is null)
            return;

        // A temp file per write keeps the rename atomic when two copies of the app save at once.
        var tempPath = $"{_filePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_cache, WindowStateJsonContext.Default.DictionaryStringWindowState);

            File.WriteAllText(tempPath, json);
            ReplaceWithRetry(tempPath, _filePath);

            HermesLogger.Info($"Saved window state to {_filePath}");
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Failed to save window state to {_filePath}: {ex.Message}");
            DeleteQuietly(tempPath);
            // Don't rethrow - window closing should not fail due to state persistence
        }
    }

    // Windows denies replacing a file while another process has it open or is replacing it too.
    private static void ReplaceWithRetry(string sourcePath, string destinationPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < MaxReplaceAttempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(attempt * ReplaceRetryDelayMilliseconds);
            }
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
