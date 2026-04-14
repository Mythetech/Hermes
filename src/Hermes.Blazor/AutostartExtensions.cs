// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Blazor;

/// <summary>
/// Extension methods for adding autostart (launch at login) support to Hermes Blazor applications.
/// </summary>
public static class AutostartExtensions
{
    /// <summary>
    /// Enables autostart (launch at login) for this application.
    /// When enabled, the application will be registered to launch automatically at system startup.
    /// </summary>
    /// <param name="builder">The Blazor app builder.</param>
    /// <param name="appId">
    /// Optional application identifier. If null, the entry assembly name is used.
    /// </param>
    /// <param name="args">
    /// Optional command-line arguments to pass when launched at login.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static HermesBlazorAppBuilder UseAutostart(
        this HermesBlazorAppBuilder builder,
        string? appId = null,
        string[]? args = null)
    {
        if (appId is not null)
            Autostart.SetEnabled(appId, true, args);
        else
            Autostart.SetEnabled(true, args);

        return builder;
    }
}
