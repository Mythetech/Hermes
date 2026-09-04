// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Hermes.Tests.Web;

/// <summary>
/// The SDK writes every content root in the runtime manifest with a trailing directory
/// separator. Resolving an asset against such a root must succeed, otherwise the framework
/// script is never served from the package and the page never boots.
/// </summary>
public class StaticWebAssetsFileProviderTests : IDisposable
{
    private readonly string _appName = "SwaProbe" + Guid.NewGuid().ToString("N");
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "hermes-swa-" + Guid.NewGuid().ToString("N"));
    private readonly string _manifestPath;

    public StaticWebAssetsFileProviderTests()
    {
        Directory.CreateDirectory(_contentRoot);
        File.WriteAllText(Path.Combine(_contentRoot, "blazor.webview.js"), "// framework script");

        // Content root written the way the SDK writes it, with the trailing separator.
        var contentRootWithSeparator = _contentRoot + Path.DirectorySeparatorChar;
        var manifest = $$"""
        {
          "ContentRoots": ["{{contentRootWithSeparator.Replace("\\", "\\\\")}}"],
          "Root": {
            "Children": {
              "_framework": {
                "Children": {
                  "blazor.webview.js": { "Children": null, "Asset": { "ContentRootIndex": 0, "SubPath": "blazor.webview.js" }, "Patterns": null }
                },
                "Asset": null,
                "Patterns": null
              }
            },
            "Asset": null,
            "Patterns": null
          }
        }
        """;
        _manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{_appName}.staticwebassets.runtime.json");
        File.WriteAllText(_manifestPath, manifest);
    }

    [Fact]
    public void Asset_UnderAContentRootWithTrailingSeparator_IsFound()
    {
        var provider = StaticWebAssetsFileProvider.Create(_appName, new NullFileProvider());

        var file = provider.GetFileInfo("_framework/blazor.webview.js");

        Assert.True(file.Exists, "The framework script must resolve through the manifest content root.");
        Assert.Equal("blazor.webview.js", file.Name);
    }

    [Fact]
    public void Asset_OutsideTheContentRoot_IsRejected()
    {
        var provider = StaticWebAssetsFileProvider.Create(_appName, new NullFileProvider());

        var file = provider.GetFileInfo("_framework/../../etc/passwd");

        Assert.False(file.Exists);
    }

    public void Dispose()
    {
        File.Delete(_manifestPath);
        Directory.Delete(_contentRoot, recursive: true);
    }
}
