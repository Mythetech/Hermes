// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;
using System.Text.Json;
using Hermes.Blazor;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class HermesDropEventArgsTests
{
    // Blazor deserializes event args with web defaults: camelCase, case-insensitive.
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_ResolvesDataFromTheRegistry()
    {
        var package = new HermesDataPackage { Files = [Path.Combine(Path.GetTempPath(), "a.txt")] };
        var drop = DropRegistry.Shared.Register(new object(), 3, 4, package);

        var args = JsonSerializer.Deserialize<HermesDropEventArgs>(
            $$"""{"dropId":"{{drop.Id}}","x":3,"y":4,"files":[{"name":"a.txt","size":12,"type":"text/plain"}],"types":["Files"]}""",
            s_web)!;

        Assert.Equal(drop.Id, args.DropId);
        Assert.Equal(3, args.X);
        Assert.Equal(4, args.Y);
        Assert.Equal(new HermesDropFile("a.txt", 12, "text/plain"), Assert.Single(args.Files));
        Assert.Equal(new[] { "Files" }, args.Types);
        Assert.Same(package, args.Data);
    }

    [Fact]
    public void Deserialize_UnknownId_GivesAnEmptyPackage()
    {
        var args = JsonSerializer.Deserialize<HermesDropEventArgs>(
            """{"dropId":"0123456789abcdef0123456789abcdef","x":0,"y":0}""", s_web)!;

        Assert.True(args.Data.IsEmpty);
        Assert.Empty(args.Files);
    }

    [Fact]
    public void Deserialize_IgnoresDataSuppliedByThePage()
    {
        var args = JsonSerializer.Deserialize<HermesDropEventArgs>(
            """{"dropId":"","data":{"files":["/etc/passwd"]}}""", s_web)!;

        Assert.True(args.Data.IsEmpty);
    }

    [Fact]
    public void EventHandler_IsRegisteredAsOnHermesDrop()
    {
        var attribute = typeof(Hermes.Blazor.EventHandlers).GetCustomAttribute<EventHandlerAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("onhermesdrop", attribute.AttributeName);
        Assert.Equal(typeof(HermesDropEventArgs), attribute.EventArgsType);
    }
}
