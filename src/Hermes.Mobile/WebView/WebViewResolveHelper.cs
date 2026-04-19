// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Mobile.WebView;

public static class WebViewResolveHelper
{
    public static WebViewResponse ToResponse(
        int statusCode, Stream content, IDictionary<string, string> headers, string url)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        content.Dispose();

        var contentType = headers.TryGetValue("Content-Type", out var ct)
            ? ct
            : MimeTypeLookup.GetContentType(url);

        return new WebViewResponse
        {
            StatusCode = statusCode,
            Body = ms.ToArray(),
            ContentType = contentType
        };
    }
}
