// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Android.Content;
using Hermes.Contracts.Plugins;
using Hermes.Mobile.Android.Plugins;
using Hermes.Mobile.Android.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Mobile.Android;

public sealed class HermesMobileAndroidBuilder : IMobileBuilder<HermesMobileAndroidHost>
{
    private readonly Context _context;
    private string _hostPage = "wwwroot/index.html";

    private HermesMobileAndroidBuilder(Context context)
    {
        _context = context;
        Services = new ServiceCollection();
        Services.AddBlazorWebView();
        Services.AddSingleton<IClipboard>(new AndroidClipboard(context));
    }

    public static HermesMobileAndroidBuilder CreateDefault(Context context) => new(context);

    public IServiceCollection Services { get; }

    public RootComponentCollection RootComponents { get; } = new();

    IMobileBuilder<HermesMobileAndroidHost> IMobileBuilder<HermesMobileAndroidHost>.UseHostPage(string hostPage)
    {
        _hostPage = hostPage;
        return this;
    }

    public HermesMobileAndroidBuilder UseHostPage(string hostPage)
    {
        _hostPage = hostPage;
        return this;
    }

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public HermesMobileAndroidHost Build()
    {
        var provider = Services.BuildServiceProvider();

        var contentRoot = Path.GetDirectoryName(_hostPage) ?? string.Empty;
        var hostPageRelative = Path.GetRelativePath(
            string.IsNullOrEmpty(contentRoot) ? "." : contentRoot,
            _hostPage);

        var fileProvider = new AndroidAssetFileProvider(_context.Assets!, contentRoot);

        var components = RootComponents.GetComponents()
            .Select(c => (c.Type, c.Selector))
            .ToList();

        return new HermesMobileAndroidHost(_context, provider, fileProvider, hostPageRelative, components);
    }
}
