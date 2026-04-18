// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Mobile.WebView;

internal static class MimeTypeLookup
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".json"] = "application/json",
        [".webmanifest"] = "application/manifest+json",
        [".map"] = "application/json",
        [".wasm"] = "application/wasm",
        [".dll"] = "application/octet-stream",
        [".pdb"] = "application/octet-stream",
        [".blat"] = "application/octet-stream",
        [".dat"] = "application/octet-stream",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".txt"] = "text/plain",
    };

    public static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return Map.TryGetValue(ext, out var ct) ? ct : "application/octet-stream";
    }
}
