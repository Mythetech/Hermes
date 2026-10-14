// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DataPackageValidatorTests
{
    private static readonly byte[] s_png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private static string AbsolutePath(string name) => Path.Combine(Path.GetTempPath(), name);

    [Fact]
    public void Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => DataPackageValidator.ValidateForWrite(null!));

    [Fact]
    public void Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => DataPackageValidator.ValidateForWrite(new HermesDataPackage()));

    [Fact]
    public void FullPackage_IsValid() =>
        DataPackageValidator.ValidateForWrite(new HermesDataPackage
        {
            Text = "a",
            Html = "<b>a</b>",
            Png = new ReadOnlyMemory<byte>(s_png),
            Files = [AbsolutePath("a.txt")],
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-hermes-test"] = new byte[] { 1 } },
        });

    [Fact]
    public void NonPng_Throws() =>
        Assert.Throws<ArgumentException>(() => DataPackageValidator.ValidateForWrite(
            new HermesDataPackage { Png = new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3 }) }));

    [Fact]
    public void EmptyFiles_Throws() =>
        Assert.Throws<ArgumentException>(() => DataPackageValidator.ValidateForWrite(
            new HermesDataPackage { Files = [] }));

    [Theory]
    [InlineData("relative/file.txt")]
    [InlineData("")]
    public void NonAbsoluteFile_Throws(string path) =>
        Assert.Throws<ArgumentException>(() => DataPackageValidator.ValidateForWrite(
            new HermesDataPackage { Files = [path] }));

    [Theory]
    [InlineData("text/plain")]
    [InlineData("TEXT/HTML")]
    [InlineData("image/png")]
    [InlineData("text/uri-list")]
    [InlineData("nosubtype")]
    [InlineData("a/b/c")]
    [InlineData("application/x test")]
    [InlineData("/x")]
    public void InvalidOrStandardCustomType_Throws(string mimeType) =>
        Assert.Throws<ArgumentException>(() => DataPackageValidator.ValidateForWrite(new HermesDataPackage
        {
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { [mimeType] = new byte[] { 1 } },
        }));
}
