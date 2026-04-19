// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Mobile;

public interface IMobileBuilder<THost> where THost : IMobileHost
{
    IServiceCollection Services { get; }
    RootComponentCollection RootComponents { get; }
    IMobileBuilder<THost> UseHostPage(string hostPage);

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    THost Build();
}
