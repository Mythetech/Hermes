// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Infrastructure;

/// <summary>
/// Decides which requests and web messages belong to the app on Windows, where the app is served at http://localhost/.
/// </summary>
internal static class AppOriginPolicy
{
    public const string WindowsAppHost = "localhost";

    /// <summary>The WebView2 request filter for a scheme registered on the window.</summary>
    public static string RequestFilter(string scheme) =>
        IsWebScheme(scheme) ? $"{scheme}://{WindowsAppHost}/*" : $"{scheme}://*";

    /// <summary>Whether a request for a registered scheme is the app's own, rather than another origin's.</summary>
    public static bool IsAppRequest(Uri uri) =>
        !IsWebScheme(uri.Scheme)
        || (uri.IsDefaultPort && string.Equals(uri.Host, WindowsAppHost, StringComparison.OrdinalIgnoreCase));

    /// <summary>The Access-Control-Allow-Origin value for an app response: the request's own origin.</summary>
    public static string CorsOrigin(Uri uri) => uri.GetLeftPart(UriPartial.Authority);

    /// <summary>Whether two URLs share a scheme, host and port.</summary>
    public static bool IsSameOrigin(string? first, string? second)
    {
        // Identical URLs share an origin even when Uri can't parse them, such as a data: URL over its length limit.
        if (first is not null && string.Equals(first, second, StringComparison.Ordinal))
            return true;

        if (!Uri.TryCreate(first, UriKind.Absolute, out var a) || !Uri.TryCreate(second, UriKind.Absolute, out var b))
            return false;

        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;
    }

    private static bool IsWebScheme(string scheme) =>
        string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
