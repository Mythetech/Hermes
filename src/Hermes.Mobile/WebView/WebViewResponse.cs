// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Mobile.WebView;

public sealed class WebViewResponse
{
    public required int StatusCode { get; init; }
    public required byte[] Body { get; init; }
    public required string ContentType { get; init; }

    public static WebViewResponse NotFound => new()
    {
        StatusCode = 404,
        Body = Array.Empty<byte>(),
        ContentType = string.Empty
    };
}
