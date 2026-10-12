// Copyright (c) Mythetech. Licensed under the MIT License.
namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// A page-scoped service that finishes disposing on another thread, as UI library services do.
/// <see cref="TeardownProbe.Report"/> requires it, so a dispose that cannot get past it fails the run.
/// </summary>
public sealed class PageTeardownProbe : IAsyncDisposable
{
    private static volatile bool _disposed;

    public static bool Disposed => _disposed;

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        _disposed = true;
    }
}
