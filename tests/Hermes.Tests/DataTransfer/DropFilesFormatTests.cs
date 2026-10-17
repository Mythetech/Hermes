// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.Text;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DropFilesFormatTests
{
    [Fact]
    public void Build_ThenParse_RoundTrips()
    {
        string[] paths = [@"C:\one.txt", @"C:\dir with space\twö 世界.png"];

        Assert.Equal(paths, DropFilesFormat.Parse(DropFilesFormat.Build(paths)));
    }

    [Fact]
    public void Build_WritesAWideHeaderAndADoubleNulTerminator()
    {
        var data = DropFilesFormat.Build([@"C:\a"]);

        Assert.Equal(20, BinaryPrimitives.ReadInt32LittleEndian(data));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16)));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, data[^4..]);
    }

    [Fact]
    public void Parse_ReadsAnsiLists()
    {
        var header = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(header, 20);
        var data = header.Concat(Encoding.Latin1.GetBytes("C:\\a.txt\0C:\\b.txt\0\0")).ToArray();

        Assert.Equal(new[] { @"C:\a.txt", @"C:\b.txt" }, DropFilesFormat.Parse(data));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public void Parse_TooShort_ReturnsEmpty(int length) =>
        Assert.Empty(DropFilesFormat.Parse(new byte[length]));
}
