// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Notifications;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class ToastXmlBuilderTests
{
    [Fact]
    public void Build_TitleOnly_HasLaunchAndSingleText()
    {
        var xml = ToastXmlBuilder.Build("id1", "Hello", null, null, silent: false);

        Assert.Equal(
            "<toast launch=\"id1\"><visual><binding template=\"ToastGeneric\"><text>Hello</text></binding></visual></toast>",
            xml);
    }

    [Fact]
    public void Build_WithBody_AddsSecondText()
    {
        var xml = ToastXmlBuilder.Build("id1", "Hello", "World", null, silent: false);

        Assert.Contains("<text>Hello</text><text>World</text>", xml);
    }

    [Fact]
    public void Build_EscapesMarkupInTitleBodyAndId()
    {
        var xml = ToastXmlBuilder.Build("a&b", "<b>\"x\"</b>", "it's & more", null, silent: false);

        Assert.Contains("launch=\"a&amp;b\"", xml);
        Assert.Contains("<text>&lt;b&gt;&quot;x&quot;&lt;/b&gt;</text>", xml);
        Assert.Contains("<text>it&apos;s &amp; more</text>", xml);
    }

    [Fact]
    public void Build_WithIcon_AddsAppLogoOverrideAsFileUri()
    {
        var xml = ToastXmlBuilder.Build("id1", "Hello", null, @"C:\icons\app icon.png", silent: false);

        Assert.Contains("<image placement=\"appLogoOverride\" src=\"file:///C:/icons/app%20icon.png\"/>", xml);
    }

    [Fact]
    public void Build_Silent_AddsSilentAudio()
    {
        var xml = ToastXmlBuilder.Build("id1", "Hello", null, null, silent: true);

        Assert.EndsWith("</visual><audio silent=\"true\"/></toast>", xml);
    }

    [Fact]
    public void Build_NotSilent_HasNoAudioElement()
    {
        var xml = ToastXmlBuilder.Build("id1", "Hello", null, null, silent: false);

        Assert.DoesNotContain("<audio", xml);
    }
}
