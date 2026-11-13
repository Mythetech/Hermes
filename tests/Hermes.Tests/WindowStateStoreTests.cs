// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Text.Json;
using Hermes.Diagnostics;
using Hermes.Storage;
using Xunit;

namespace Hermes.Tests;

public sealed class WindowStateStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("hermes-window-state-");

    public void Dispose() => _directory.Delete(recursive: true);

    /// <summary>
    /// Two copies of an app write the same window state file when they close together.
    /// </summary>
    [Fact]
    public void ConcurrentSaves_FromTwoStoresOnOneFile_NeverFailAndLeaveValidJson()
    {
        var path = Path.Combine(_directory.FullName, "App.json");
        var stores = new[] { new WindowStateStore(path), new WindowStateStore(path) };
        var failures = new ConcurrentQueue<string>();
        void OnError(string message, Exception? exception)
        {
            if (message.Contains(_directory.FullName, StringComparison.Ordinal))
                failures.Enqueue(message);
        }

        HermesLogger.ErrorObserved += OnError;
        try
        {
            Parallel.For(0, stores.Length, i =>
            {
                for (var n = 0; n < 200; n++)
                    stores[i].SaveState($"store{i}-{n}", new WindowState { X = n, Y = n, Width = 800, Height = 600 });
            });
        }
        finally
        {
            HermesLogger.ErrorObserved -= OnError;
        }

        Assert.Empty(failures);
        Assert.NotNull(JsonSerializer.Deserialize<Dictionary<string, WindowState>>(File.ReadAllText(path)));
        Assert.Empty(_directory.GetFiles("*.tmp"));
    }
}
