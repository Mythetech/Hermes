// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Collections.Concurrent;
using System.Threading;

namespace Hermes.Storage;

/// <summary>
/// Static facade for opening Hermes key-value stores. Provides a zero-configuration
/// <see cref="Default"/> store and a named-store factory via <see cref="Open(string)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stores are cached per name within a process, so two callers asking for the same
/// store name receive the same in-memory instance and cannot race against two copies
/// of the same file.
/// </para>
/// <para>
/// Files are written to <c>{AppData}/Hermes/KvStore/{name}.json</c>, where
/// <c>{AppData}</c> is resolved by <see cref="AppDataDirectories.GetUserDataPath(string)"/>.
/// </para>
/// </remarks>
public static class HermesStore
{
    /// <summary>
    /// The name of the default store, used when callers do not specify one.
    /// </summary>
    public const string DefaultName = "default";

    /// <summary>
    /// Subdirectory under the user data directory where store files are written.
    /// </summary>
    internal const string StoreDirectory = "KvStore";

    private const int MaxNameLength = 64;

    private static readonly HashSet<string> s_reservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly Lock s_sync = new();
    private static readonly ConcurrentDictionary<string, IHermesKeyValueStore> s_stores =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the default key-value store. Equivalent to <c>Open(DefaultName)</c>.
    /// </summary>
    public static IHermesKeyValueStore Default => Open(DefaultName);

    /// <summary>
    /// Opens (or returns the cached instance of) the named key-value store.
    /// </summary>
    /// <param name="name">
    /// The store name. Must be 1-64 characters of <c>[a-zA-Z0-9._-]</c>, must not be
    /// <c>"."</c> or <c>".."</c>, and must not be a reserved Windows device name
    /// (CON, PRN, COM1, etc.).
    /// </param>
    /// <exception cref="ArgumentException">Thrown if the name is invalid.</exception>
    public static IHermesKeyValueStore Open(string name)
    {
        ValidateName(name);

        if (s_stores.TryGetValue(name, out var existing))
        {
            return existing;
        }

        lock (s_sync)
        {
            if (s_stores.TryGetValue(name, out existing))
            {
                return existing;
            }

            var directory = AppDataDirectories.GetUserDataPath(StoreDirectory);
            var filePath = Path.Combine(directory, $"{name}.json");
            var store = new JsonFileKeyValueStore(name, filePath);
            s_stores[name] = store;
            return store;
        }
    }

    /// <summary>
    /// For tests: removes the cached instance for the given name so the next
    /// <see cref="Open(string)"/> reads from disk fresh. Does not delete the file.
    /// </summary>
    internal static void ResetCache(string name)
    {
        s_stores.TryRemove(name, out _);
    }

    /// <summary>
    /// For tests: returns the on-disk file path for the named store.
    /// </summary>
    internal static string GetFilePath(string name)
    {
        ValidateName(name);
        var directory = AppDataDirectories.GetUserDataPath(StoreDirectory);
        return Path.Combine(directory, $"{name}.json");
    }

    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Store name cannot be empty or whitespace.", nameof(name));
        }

        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Store name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        if (name == "." || name == "..")
        {
            throw new ArgumentException(
                "Store name cannot be '.' or '..'.", nameof(name));
        }

        foreach (var c in name)
        {
            var ok = (c >= 'a' && c <= 'z')
                  || (c >= 'A' && c <= 'Z')
                  || (c >= '0' && c <= '9')
                  || c == '.'
                  || c == '_'
                  || c == '-';
            if (!ok)
            {
                throw new ArgumentException(
                    $"Store name contains invalid character '{c}'. Allowed: letters, digits, '.', '_', '-'.",
                    nameof(name));
            }
        }

        if (s_reservedWindowsNames.Contains(name))
        {
            throw new ArgumentException(
                $"Store name '{name}' is a reserved Windows device name.", nameof(name));
        }
    }
}
