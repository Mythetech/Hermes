// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

public sealed class AppIdValidatorTests
{
    [Theory]
    [InlineData("com.mythetech.Horizon")]
    [InlineData("dev.hermes.Sample_App")]
    [InlineData("org.example.my-app")]
    [InlineData("a.b")]
    public void IsValid_AcceptsReverseDnsNames(string appId) =>
        Assert.True(AppIdValidator.IsValid(appId));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Horizon")]
    [InlineData("com..mythetech")]
    [InlineData(".com.mythetech")]
    [InlineData("com.mythetech.")]
    [InlineData("com.1mythetech")]
    [InlineData("com.mythe tech")]
    [InlineData("com.mythetech/Horizon")]
    public void IsValid_RejectsOtherNames(string? appId) =>
        Assert.False(AppIdValidator.IsValid(appId));

    [Fact]
    public void IsValid_RejectsNamesLongerThan255() =>
        Assert.False(AppIdValidator.IsValid("com." + new string('a', 252)));

    [Theory]
    [InlineData("Horizon")]
    [InlineData("com.1mythetech")]
    public void HermesApplicationAppId_RejectsAnInvalidName(string appId) =>
        Assert.Throws<ArgumentException>(() => HermesApplication.AppId = appId);
}
