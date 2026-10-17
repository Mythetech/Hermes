// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

/// <summary>
/// Live drops by ID, each owned by the window it landed on.
/// </summary>
internal sealed class DropRegistry
{
    public const int MaxDropsPerOwner = 64;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static DropRegistry Shared { get; } = new(TimeProvider.System);

    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _drops = new(StringComparer.Ordinal);
    private long _nextOrder;

    public DropRegistry(TimeProvider time)
    {
        _time = time;
    }

    public HermesDrop Register(object owner, double x, double y, HermesDataPackage data)
    {
        var drop = new HermesDrop { Id = CreateId(), X = x, Y = y, Data = data };
        var now = _time.GetUtcNow();

        lock (_lock)
        {
            RemoveWhere(entry => now - entry.Created >= Lifetime);

            var owned = _drops.Values.Where(entry => ReferenceEquals(entry.Owner, owner)).OrderBy(entry => entry.Order).ToList();
            foreach (var entry in owned.Take(owned.Count - MaxDropsPerOwner + 1))
                _drops.Remove(entry.Drop.Id);

            _drops[drop.Id] = new Entry(owner, drop, now, _nextOrder++);
        }

        return drop;
    }

    /// <summary>Finds a live drop; a null owner matches any window.</summary>
    public bool TryGet(string? id, object? owner, [NotNullWhen(true)] out HermesDrop? drop)
    {
        drop = null;
        if (string.IsNullOrEmpty(id))
            return false;

        lock (_lock)
        {
            if (!_drops.TryGetValue(id, out var entry))
                return false;

            if (_time.GetUtcNow() - entry.Created >= Lifetime)
            {
                _drops.Remove(id);
                return false;
            }

            if (owner is not null && !ReferenceEquals(entry.Owner, owner))
                return false;

            drop = entry.Drop;
            return true;
        }
    }

    public void Release(object owner)
    {
        lock (_lock)
            RemoveWhere(entry => ReferenceEquals(entry.Owner, owner));
    }

    private void RemoveWhere(Func<Entry, bool> predicate)
    {
        foreach (var entry in _drops.Values.Where(predicate).ToList())
            _drops.Remove(entry.Drop.Id);
    }

    private static string CreateId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    private sealed record Entry(object Owner, HermesDrop Drop, DateTimeOffset Created, long Order);
}
