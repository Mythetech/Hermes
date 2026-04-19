// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Web.Hosting;

internal static class MimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".mjs"] = "application/javascript",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".eot"] = "application/vnd.ms-fontobject",
        [".wasm"] = "application/wasm",
        [".map"] = "application/json",
        [".txt"] = "text/plain",
        [".webm"] = "video/webm",
        [".mp4"] = "video/mp4",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".avif"] = "image/avif",
    };

    public static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path);
        return Map.GetValueOrDefault(ext, "application/octet-stream");
    }
}
