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

    public sealed record Missing(string Html, string Advice) : FrontendSource;
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

        var advice = isDevelopment && devServer is not null
            ? $"Start the dev server at {devServer}, then restart the app. Or build the frontend and rebuild the app, which copies it into {staticRoot}."
            : $"No frontend was found in {staticRoot}. Build the app in Release or run dotnet publish so the frontend is built and copied there.";

        return new FrontendSource.Missing(MissingPage(WebUtility.HtmlEncode(advice)), advice);
    }

    private static string MissingPage(string advice) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8">
        <title>No frontend</title>
        <style>
          body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 2rem; color: #333; }
        </style>
        </head>
        <body>
        <h1>Nothing to show yet</h1>
        <p>{{advice}}</p>
        </body>
        </html>
        """;
}
