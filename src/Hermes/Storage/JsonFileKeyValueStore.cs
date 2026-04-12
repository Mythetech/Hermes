// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using Hermes.Diagnostics;

namespace Hermes.Storage;

/// <summary>
/// Write-through, JSON-on-disk implementation of <see cref="IHermesKeyValueStore"/>.
/// One file per named store. Atomic writes via temp-file-then-rename.
/// </summary>
internal sealed class JsonFileKeyValueStore : IHermesKeyValueStore
{
    /// <summary>
    /// Current on-disk format version. Files written by this version use this number.
    /// Unknown versions are treated as "start fresh".
    /// </summary>
    internal const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Allow JsonNode round-tripping without surprises.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly Lock _sync = new();
    private readonly string _filePath;
    private Dictionary<string, JsonNode?> _entries = new(StringComparer.Ordinal);
    private bool _loaded;

    public JsonFileKeyValueStore(string name, string filePath)
    {
        Name = name;
        _filePath = filePath;
    }

    public string Name { get; }

    public IReadOnlyCollection<string> Keys
    {
        get
        {
            lock (_sync)
            {
                EnsureLoaded();
                return new List<string>(_entries.Keys);
            }
        }
    }

    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            EnsureLoaded();
            return _entries.ContainsKey(key);
        }
    }

    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            EnsureLoaded();

            if (!_entries.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            try
            {
                value = node is null ? default : node.Deserialize<T>(s_jsonOptions);
                return true;
            }
            catch (Exception ex)
            {
                HermesLogger.Warning(
                    $"Hermes KV store '{Name}': failed to deserialize key '{key}' as {typeof(T).Name}: {ex.Message}");
                value = default;
                return false;
            }
        }
    }

    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    public T? Get<T>(string key)
    {
        return TryGet<T>(key, out var value) ? value : default;
    }

    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    public T Get<T>(string key, T defaultValue)
    {
        if (TryGet<T>(key, out var value) && value is not null)
        {
            return value;
        }
        return defaultValue;
    }

    [RequiresUnreferencedCode("JSON serialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    public void Set<T>(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            EnsureLoaded();
            _entries[key] = JsonSerializer.SerializeToNode(value, s_jsonOptions);
            PersistToDisk();
        }
    }

    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_sync)
        {
            EnsureLoaded();
            if (!_entries.Remove(key))
            {
                return false;
            }
            PersistToDisk();
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            EnsureLoaded();
            if (_entries.Count == 0)
            {
                return;
            }
            _entries.Clear();
            PersistToDisk();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        _entries = LoadFromDisk();
        _loaded = true;
    }

    private Dictionary<string, JsonNode?> LoadFromDisk()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            }

            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            }

            var root = JsonNode.Parse(json) as JsonObject;
            if (root is null)
            {
                HermesLogger.Warning(
                    $"Hermes KV store '{Name}': file '{_filePath}' is not a JSON object, starting fresh.");
                return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            }

            var version = root["version"]?.GetValue<int?>() ?? 0;
            if (version != CurrentVersion)
            {
                HermesLogger.Warning(
                    $"Hermes KV store '{Name}': unknown file version {version} (expected {CurrentVersion}), starting fresh.");
                return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            if (root["entries"] is JsonObject entries)
            {
                foreach (var kvp in entries)
                {
                    // JsonNode children belong to a parent; detach via DeepClone
                    // so we can safely re-serialize them under a new envelope.
                    result[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            HermesLogger.Warning(
                $"Hermes KV store '{Name}': failed to load '{_filePath}': {ex.Message}. Starting fresh.");
            return new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        }
    }

    private void PersistToDisk()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entriesObject = new JsonObject();
            foreach (var kvp in _entries)
            {
                // Each node may already be parented (from a previous PersistToDisk),
                // so deep-clone before adding to the new envelope.
                entriesObject[kvp.Key] = kvp.Value?.DeepClone();
            }

            var envelope = new JsonObject
            {
                ["version"] = CurrentVersion,
                ["entries"] = entriesObject
            };

            var json = envelope.ToJsonString(s_jsonOptions);

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            HermesLogger.Error(
                $"Hermes KV store '{Name}': failed to persist to '{_filePath}': {ex.Message}", ex);
            // Do not rethrow; persistence failures should not crash the caller.
        }
    }
}
