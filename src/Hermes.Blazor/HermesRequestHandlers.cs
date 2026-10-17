// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Hermes.Blazor;

/// <summary>
/// The app's request handlers, mapped to paths under the app origin. The longest matching path wins.
/// </summary>
internal sealed class HermesRequestHandlers
{
    private static readonly PathString[] ReservedPaths = ["/_framework", "/_content", "/_hermes"];

    private readonly List<Mapping> _mappings = new();
    private Route[] _routes = [];

    public bool IsEmpty => _mappings.Count == 0;

    public void Map(string path, Func<IServiceProvider, Func<string, (Stream? Content, string? ContentType)>> resolve)
    {
        var normalized = Normalize(path);
        if (_mappings.Any(m => m.Path.Equals(normalized)))
            throw new InvalidOperationException($"A request handler is already mapped to '{normalized}'.");

        _mappings.Add(new Mapping(normalized, resolve));
    }

    public void Attach(IServiceProvider services)
    {
        _routes = _mappings
            .OrderByDescending(m => m.Path.Value!.Length)
            .Select(m => new Route(m.Path, m.Resolve(services)))
            .ToArray();
    }

    public bool TryHandle(PathString path, string url, out (Stream? Content, string? ContentType) response)
    {
        foreach (var route in _routes)
        {
            if (!path.StartsWithSegments(route.Path))
                continue;

            response = Invoke(route, url);
            return true;
        }

        response = default;
        return false;
    }

    private static (Stream? Content, string? ContentType) Invoke(Route route, string url)
    {
        try
        {
            return route.Handler(url);
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"The request handler mapped to '{route.Path}' failed, so the request was answered with 404.", ex);
            return (null, null);
        }
    }

    private static PathString Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!path.StartsWith('/') || path.IndexOfAny(['?', '#']) >= 0)
            throw new ArgumentException($"'{path}' is not a path. Map a path that starts with '/', such as '/media'.", nameof(path));

        var normalized = new PathString(path.TrimEnd('/'));
        if (!normalized.HasValue)
            throw new ArgumentException("The root path serves the app, so a request handler cannot be mapped to it.", nameof(path));

        if (ReservedPaths.Any(reserved => normalized.StartsWithSegments(reserved)))
            throw new ArgumentException($"'{path}' is reserved for Blazor and Hermes assets.", nameof(path));

        return normalized;
    }

    private sealed record Mapping(PathString Path, Func<IServiceProvider, Func<string, (Stream? Content, string? ContentType)>> Resolve);

    private sealed record Route(PathString Path, Func<string, (Stream? Content, string? ContentType)> Handler);
}
