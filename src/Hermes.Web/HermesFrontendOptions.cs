// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Web;

/// <summary>
/// Options for <see cref="HermesWebAppBuilder.UseFrontend"/>.
/// </summary>
public sealed class HermesFrontendOptions
{
    /// <summary>
    /// Folder holding the built frontend. A relative path is resolved against the app's base directory.
    /// </summary>
    public string StaticFilesPath { get; set; } = "wwwroot";

    /// <summary>
    /// Serves index.html for paths without a file extension, so client-side routes survive a reload.
    /// </summary>
    public bool SpaFallback { get; set; } = true;

    /// <summary>
    /// How long to wait for the dev server to accept a connection before serving the built frontend instead.
    /// </summary>
    public TimeSpan DevServerProbeTimeout { get; set; } = TimeSpan.FromMilliseconds(500);
}
