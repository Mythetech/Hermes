// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hosting;
using Xunit;

namespace Hermes.Tests.Hosting;

public class HostingAssemblyTests
{
    [Fact]
    public void HostingAssembly_DoesNotReferenceAspNetCoreOrBlazor()
    {
        var references = typeof(HermesHostRunner).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain("Hermes.Blazor", references);
    }
}
