// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes;
using Hermes.Abstractions;

namespace Hermes.Blazor;

/// <summary>
/// Provides platform detection for Blazor components.
/// Inject this service to check the current platform without directly accessing HermesWindow.
/// </summary>
public interface IHermesPlatformService
{
    /// <summary>
    /// Gets whether the app is running on macOS.
    /// </summary>
    bool IsMacOS { get; }

    /// <summary>
    /// Gets whether the app is running on Windows.
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// Gets whether the app is running on Linux.
    /// </summary>
    bool IsLinux { get; }

    /// <summary>
    /// Gets the current platform.
    /// </summary>
    HermesPlatform Platform { get; }
}

internal sealed class HermesPlatformService : IHermesPlatformService
{
    private readonly HermesWindow _window;

    public HermesPlatformService(HermesWindow window)
    {
        _window = window;
    }

    public bool IsMacOS => _window.Platform == HermesPlatform.macOS;
    public bool IsWindows => _window.Platform == HermesPlatform.Windows;
    public bool IsLinux => _window.Platform == HermesPlatform.Linux;
    public HermesPlatform Platform => _window.Platform;
}
