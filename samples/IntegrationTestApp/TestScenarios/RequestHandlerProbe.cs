// Copyright (c) Mythetech. Licensed under the MIT License.
namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Serves a 3x2 PNG at /hermes-test/pixel.png through a mapped request handler, and remembers the URL it was asked for.
/// </summary>
public static class RequestHandlerProbe
{
    public const string Path = "/hermes-test";
    public const int Width = 3;
    public const int Height = 2;

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAYAAACddGYaAAAAEUlEQVR4nGM4oaHxH4YZkDkAoS0Miz+GyrYAAAAASUVORK5CYII=");

    private static volatile string? _lastUrl;

    public static string? LastUrl => _lastUrl;

    public static (Stream? Content, string? ContentType) Handle(string url)
    {
        _lastUrl = url;
        return new Uri(url).AbsolutePath.EndsWith("/pixel.png", StringComparison.Ordinal)
            ? (new MemoryStream(Png), "image/png")
            : (null, null);
    }
}
