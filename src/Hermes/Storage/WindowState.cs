// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Storage;

/// <summary>
/// Represents the persisted state of a window.
/// </summary>
public sealed class WindowState
{
    /// <summary>
    /// Window X position in screen coordinates.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Window Y position in screen coordinates.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Window width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Window height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Whether the window was maximized.
    /// </summary>
    public bool IsMaximized { get; set; }
}
