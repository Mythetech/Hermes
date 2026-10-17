// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor;

/// <summary>
/// Serves the requests under a path mapped with <see cref="HermesBlazorAppBuilder.MapRequestHandler{THandler}(string)"/>.
/// </summary>
public interface IHermesRequestHandler
{
    /// <summary>
    /// Returns the content for <paramref name="url"/>, or a null stream to answer 404.
    /// A null content type is inferred from the path's extension.
    /// </summary>
    (Stream? Content, string? ContentType) HandleRequest(string url);
}
