// Copyright (c) Mythetech. Licensed under the MIT License.
using Bunit;
using Hermes.Blazor.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeRootTests : BunitContext
{
    private readonly SmokeHarness _harness = new();

    public SmokeRootTests()
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        session.MarkMilestone(SmokeSession.WindowShownMilestone);
        Services.AddSingleton(new HermesSmokeRuntime(session));
    }

    [Fact]
    public void RendersTheWrappedComponent_WithItsParameters()
    {
        var cut = Render<SmokeRoot>(parameters => parameters
            .Add(p => p.ComponentType, typeof(Greeting))
            .Add(p => p.ComponentParameters, new Dictionary<string, object> { [nameof(Greeting.Name)] = "Hermes" }));

        Assert.Contains("Hello Hermes", cut.Markup);
    }

    [Fact]
    public void ReportsTheFirstRender_AndRunsToAVerdict()
    {
        var cut = Render<SmokeRoot>(parameters => parameters.Add(p => p.ComponentType, typeof(Greeting)));

        cut.WaitForAssertion(() => Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]));
        Assert.Contains("HERMES_SMOKE_MILESTONE: first-render 0ms", _harness.Lines);
    }

    [Fact]
    public void CreateParameters_CarriesTheTypeAndParameters()
    {
        var parameters = SmokeRoot.CreateParameters(typeof(Greeting), new Dictionary<string, object?> { ["Name"] = "Hermes" });

        Assert.Equal(typeof(Greeting), parameters[nameof(SmokeRoot.ComponentType)]);
        var inner = Assert.IsAssignableFrom<IDictionary<string, object>>(parameters[nameof(SmokeRoot.ComponentParameters)]);
        Assert.Equal("Hermes", inner["Name"]);
    }

    private sealed class Greeting : ComponentBase
    {
        [Parameter]
        public string? Name { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, $"Hello {Name}");
    }
}
