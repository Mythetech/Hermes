// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Hermes.Storage;
using Xunit;

namespace Hermes.Tests;

[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code, AOT not required.")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code, AOT not required.")]
public class KeyValueStoreTests
{
    private sealed record UserPrefs(string Font, int Size);

    private sealed record EvolvedUserPrefs(string Font, int Size, string Theme);

    private static (IHermesKeyValueStore Store, string Name) NewStore()
    {
        var name = $"test-{Guid.NewGuid():N}";
        return (HermesStore.Open(name), name);
    }

    private static void Cleanup(string name)
    {
        try
        {
            HermesStore.ResetCache(name);
            var path = HermesStore.GetFilePath(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void Set_then_Get_roundtrips_primitive()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("theme", "dark");
            store.Set("fontSize", 14);

            Assert.Equal("dark", store.Get<string>("theme"));
            Assert.Equal(14, store.Get<int>("fontSize"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Set_then_Get_roundtrips_complex_object()
    {
        var (store, name) = NewStore();
        try
        {
            var prefs = new UserPrefs("Inter", 14);
            store.Set("prefs", prefs);

            var loaded = store.Get<UserPrefs>("prefs");
            Assert.Equal(prefs, loaded);
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Get_returns_default_for_missing_key()
    {
        var (store, name) = NewStore();
        try
        {
            Assert.Null(store.Get<string>("missing"));
            Assert.Equal(0, store.Get<int>("missing"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Get_with_fallback_returns_fallback_for_missing_key()
    {
        var (store, name) = NewStore();
        try
        {
            Assert.Equal("light", store.Get<string>("theme", "light"));
            Assert.Equal(12, store.Get<int>("fontSize", 12));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void TryGet_returns_false_for_missing_key()
    {
        var (store, name) = NewStore();
        try
        {
            var found = store.TryGet<string>("nope", out var value);
            Assert.False(found);
            Assert.Null(value);
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Remove_returns_true_for_existing_key_and_false_for_missing()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("k", "v");
            Assert.True(store.Remove("k"));
            Assert.False(store.Contains("k"));
            Assert.False(store.Remove("k"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Clear_removes_all_keys()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("a", 1);
            store.Set("b", 2);
            store.Set("c", 3);

            store.Clear();

            Assert.Empty(store.Keys);
            Assert.False(store.Contains("a"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Set_persists_immediately_to_disk()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("theme", "dark");

            var path = HermesStore.GetFilePath(name);
            Assert.True(File.Exists(path));

            var json = File.ReadAllText(path);
            Assert.Contains("\"version\": 1", json);
            Assert.Contains("\"theme\": \"dark\"", json);
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void New_store_instance_loads_persisted_data()
    {
        var name = $"test-{Guid.NewGuid():N}";
        try
        {
            var first = HermesStore.Open(name);
            first.Set("theme", "dark");
            first.Set("prefs", new UserPrefs("Inter", 14));

            // Force a fresh in-memory instance, but keep the file on disk.
            HermesStore.ResetCache(name);

            var second = HermesStore.Open(name);
            Assert.Equal("dark", second.Get<string>("theme"));
            Assert.Equal(new UserPrefs("Inter", 14), second.Get<UserPrefs>("prefs"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Get_returns_default_when_value_fails_to_deserialize()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("theme", "dark"); // string

            // Read as int -- should fail to deserialize and return default(int).
            var asInt = store.Get<int>("theme");
            Assert.Equal(0, asInt);

            // TryGet should return false in the same situation.
            Assert.False(store.TryGet<int>("theme", out var _));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Other_keys_remain_readable_when_one_key_fails_to_deserialize()
    {
        var (store, name) = NewStore();
        try
        {
            store.Set("badKey", "not-a-number");
            store.Set("goodKey", 42);

            Assert.False(store.TryGet<int>("badKey", out _));
            Assert.Equal(42, store.Get<int>("goodKey"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Corrupt_file_degrades_to_empty_store()
    {
        var name = $"test-{Guid.NewGuid():N}";
        try
        {
            // Write garbage directly to the file before opening the store.
            var path = HermesStore.GetFilePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{this is not valid json");

            var store = HermesStore.Open(name);
            Assert.Empty(store.Keys);

            // Store must still be writable after a corrupt-file degrade.
            store.Set("recovered", true);
            Assert.True(store.Get<bool>("recovered"));
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Open_with_same_name_returns_same_instance()
    {
        var name = $"test-{Guid.NewGuid():N}";
        try
        {
            var a = HermesStore.Open(name);
            var b = HermesStore.Open(name);
            Assert.Same(a, b);
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    [InlineData("foo:bar")]
    [InlineData("CON")]
    [InlineData("prn")]
    [InlineData("COM1")]
    public void Open_with_invalid_name_throws(string name)
    {
        Assert.Throws<ArgumentException>(() => HermesStore.Open(name));
    }

    [Fact]
    public void Open_with_too_long_name_throws()
    {
        var tooLong = new string('a', 65);
        Assert.Throws<ArgumentException>(() => HermesStore.Open(tooLong));
    }

    [Fact]
    public void Concurrent_writes_from_multiple_threads_are_safe()
    {
        var (store, name) = NewStore();
        try
        {
            const int writes = 200;
            Parallel.For(0, writes, i =>
            {
                store.Set($"key-{i}", i);
            });

            for (int i = 0; i < writes; i++)
            {
                Assert.Equal(i, store.Get<int>($"key-{i}"));
            }
        }
        finally
        {
            Cleanup(name);
        }
    }

    [Fact]
    public void Default_store_is_addressable()
    {
        // Use a unique key on the default store rather than a unique name,
        // since Default is shared. Clean up only that key.
        var key = $"test-default-{Guid.NewGuid():N}";
        try
        {
            HermesStore.Default.Set(key, "value");
            Assert.Equal("value", HermesStore.Default.Get<string>(key));
        }
        finally
        {
            HermesStore.Default.Remove(key);
        }
    }
}
