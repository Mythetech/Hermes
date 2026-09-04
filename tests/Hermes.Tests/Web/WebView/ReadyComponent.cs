// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// The same render tree the stage 0 harness used, so its render batch can be pinned against
/// the bytes upstream produced for it: one button with a click handler and the text READY.
/// </summary>
public sealed class ReadyComponent : ComponentBase
{
    public static int AfterRenderCount;
    public static int Clicks;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => Clicks++));
        builder.AddContent(2, "READY");
        builder.CloseElement();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            AfterRenderCount++;
        }
    }

    public static void Reset()
    {
        AfterRenderCount = 0;
        Clicks = 0;
    }
}
