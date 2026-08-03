// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.RegularExpressions;

namespace Hermes.Blazor;

/// <summary>
/// Rewrites the served host page so blazor.webview.js is embedded inline
/// instead of fetched as a separate request. This removes a request round
/// trip through the custom scheme handler and starts JS parsing as soon as
/// the HTML arrives, which measurably shortens time to first render.
/// </summary>
internal static partial class HostPageInliner
{
    private const string BlazorWebViewAsset = "_framework/blazor.webview.js";

    [GeneratedRegex("""<script\s+src=["']_framework/blazor\.webview\.js["']\s*>\s*</script>""",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlazorScriptTag();

    public static string Inline(string html, Func<string, string?> assetResolver)
    {
        var match = BlazorScriptTag().Match(html);
        if (!match.Success)
            return html;

        var script = assetResolver(BlazorWebViewAsset);
        if (script is null)
            return html;

        return html.Remove(match.Index, match.Length)
            .Insert(match.Index, $"<script>{script}</script>");
    }
}
