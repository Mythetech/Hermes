// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Hermes.Blazor.WebView;
using Hermes.Blazor.WebView.Services;
using Microsoft.JSInterop;
using Xunit;

namespace Hermes.Tests.Web.WebView;

public class WebViewJSRuntimeTests
{
    [Fact]
    public void FrameworkResolvers_ComeBeforeAnyReflectionFallback()
    {
        var runtime = new WebViewJSRuntime();

        var chain = runtime.ReadJsonSerializerOptions().TypeInfoResolverChain;

        Assert.IsType<DotNetObjectReferenceTypeInfoResolver>(chain[0]);
        Assert.IsType<HermesWebViewJsonContext>(chain[1]);
    }

    [Fact]
    public void DotNetObjectReference_ResolvesToTheRuntimeConverter_NotAGeneratedObjectContract()
    {
        var runtime = new WebViewJSRuntime();

        var typeInfo = runtime.ReadJsonSerializerOptions().GetTypeInfo(typeof(DotNetObjectReference<HermesRendererInteropMethods>));

        // The JSRuntime registers a converter factory for DotNetObjectReference<T> that
        // assigns the id JavaScript calls back with. The generated contract must defer to it,
        // otherwise the reference would be serialized as an object with a Value property.
        Assert.Contains("DotNetObjectReferenceJsonConverter", typeInfo.Converter.GetType().Name);
    }

    [Fact]
    public void JsonElement_ResolvesWithoutReflection()
    {
        var runtime = new WebViewJSRuntime();

        var typeInfo = runtime.ReadJsonSerializerOptions().GetTypeInfo(typeof(JsonElement));

        Assert.Equal(typeof(JsonElement), typeInfo.Type);
    }

    [Fact]
    public void InvokeVoidAsync_WritesTheBeginInvokeJSEnvelope()
    {
        var sent = new List<string>();
        var runtime = new WebViewJSRuntime();
        runtime.AttachToWebView(new IpcSender(new InlineDispatcher(), sent.Add, _ => { }));

        var call = runtime.InvokeVoidAsync("Blazor._internal.navigationManager.scrollToElement", "top");

        Assert.False(call.IsCompleted);
        Assert.Equal("""__bwv:["BeginInvokeJS",2,"Blazor._internal.navigationManager.scrollToElement","[\u0022top\u0022]",3,0,1]""", Assert.Single(sent));
    }

    [Fact]
    public async Task InvokeBeforeAttach_IsVisibleAsAFault()
    {
        var runtime = new WebViewJSRuntime();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await runtime.InvokeVoidAsync("f"));

        Assert.Contains("outside of a WebView context", ex.Message);
    }
}
