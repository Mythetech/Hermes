// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;

namespace Hermes.Storage;

/// <summary>
/// A small persistent key-value store for application settings, preferences,
/// and lightweight session-bridging state.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be thread-safe within a single process. They are
/// not designed to coordinate writes across processes; multi-instance applications
/// should pair this with <see cref="Hermes.SingleInstance.SingleInstanceGuard"/>.
/// </para>
/// <para>
/// Stores are typed at the call site via generic <c>Get</c>/<c>Set</c> methods. Values
/// must be JSON-serializable using <c>System.Text.Json</c>. Each entry is stored
/// independently so a deserialization failure on one key (e.g. due to a struct shape
/// change) does not affect other keys.
/// </para>
/// </remarks>
public interface IHermesKeyValueStore
{
    /// <summary>
    /// Gets the name of this store. Stores are isolated from one another by name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a snapshot of the keys currently in the store. Safe to enumerate
    /// without holding any lock.
    /// </summary>
    IReadOnlyCollection<string> Keys { get; }

    /// <summary>
    /// Returns <c>true</c> if a value (including <c>null</c>) is stored under the given key.
    /// </summary>
    bool Contains(string key);

    /// <summary>
    /// Tries to get the value stored under <paramref name="key"/>, deserialized as <typeparamref name="T"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the key exists and its stored value successfully deserializes to
    /// <typeparamref name="T"/>; otherwise <c>false</c>. A deserialization failure is treated
    /// as "not present" and is logged as a warning, but does not throw.
    /// </returns>
    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// Gets the value stored under <paramref name="key"/>, or <c>default(T)</c> if the
    /// key is missing or its stored value cannot be deserialized as <typeparamref name="T"/>.
    /// </summary>
    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    T? Get<T>(string key);

    /// <summary>
    /// Gets the value stored under <paramref name="key"/>, or <paramref name="defaultValue"/>
    /// if the key is missing or its stored value cannot be deserialized as <typeparamref name="T"/>.
    /// </summary>
    [RequiresUnreferencedCode("JSON deserialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON deserialization may require runtime code generation.")]
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> and immediately
    /// persists the entire store to disk. Replaces any existing value.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization may require types preserved by trimming.")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation.")]
    void Set<T>(string key, T value);

    /// <summary>
    /// Removes the value stored under <paramref name="key"/> and persists the change.
    /// </summary>
    /// <returns><c>true</c> if the key was present and removed; <c>false</c> if it was missing.</returns>
    bool Remove(string key);

    /// <summary>
    /// Removes all entries from the store and persists the empty state.
    /// </summary>
    void Clear();
}
