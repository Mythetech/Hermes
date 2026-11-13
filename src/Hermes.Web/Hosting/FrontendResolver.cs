// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Net;

namespace Hermes.Web.Hosting;

/// <summary>
/// Where the window's page comes from.
/// </summary>
internal abstract record FrontendSource
{
    public sealed record DevServer(Uri Url) : FrontendSource;

    public sealed record StaticFiles(string Root) : FrontendSource;

    public sealed record Missing(string Html) : FrontendSource;
}

/// <summary>
/// Picks the dev server in Development when it answers, otherwise the built frontend, otherwise a page saying what to start.
/// </summary>
internal static class FrontendResolver
{
    public static FrontendSource Resolve(
        bool isDevelopment,
        Uri? devServer,
        string staticRoot,
        TimeSpan probeTimeout,
        Func<Uri, TimeSpan, bool> probe)
    {
        if (isDevelopment && devServer is not null && probe(devServer, probeTimeout))
            return new FrontendSource.DevServer(devServer);

        if (File.Exists(Path.Combine(staticRoot, "index.html")))
            return new FrontendSource.StaticFiles(staticRoot);

        var root = WebUtility.HtmlEncode(staticRoot);
        var advice = isDevelopment && devServer is not null
            ? $"Start the dev server at <code>{WebUtility.HtmlEncode(devServer.ToString())}</code>, then restart the app, or build the frontend into <code>{root}</code>."
            : $"No frontend was found in <code>{root}</code>. Build the app in Release or run <code>dotnet publish</code> so the frontend is built and copied there.";

        return new FrontendSource.Missing(MissingPage(advice));
    }

    private static string MissingPage(string advice) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>No frontend</title>
        <style>
          body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 2rem; color: #333; }
          code { background: #f2f2f2; padding: 0.1rem 0.3rem; border-radius: 3px; }
        </style>
        </head>
        <body>
        <h1>Nothing to show yet</h1>
        <p>{{advice}}</p>
        </body>
        </html>
        """;
}
