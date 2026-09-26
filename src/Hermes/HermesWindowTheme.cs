// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes;

/// <summary>
/// The light or dark appearance used for a window's native chrome and its WebView.
/// </summary>
/// <remarks>
/// Values are passed to native code as integers and must stay in sync with
/// the WindowTheme enum in the macOS native library.
/// </remarks>
public enum HermesWindowTheme
{
    /// <summary>
    /// Follow the operating system's light or dark setting.
    /// </summary>
    System = 0,

    /// <summary>
    /// Always use the light appearance.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Always use the dark appearance.
    /// </summary>
    Dark = 2
}
