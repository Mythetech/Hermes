// Copyright (c) Mythetech. Licensed under the MIT License.
#if LINUX
using System.Runtime.Versioning;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.Linux;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("linux")]
public sealed class LinuxClipboardBackendTests
{
    [Fact]
    public void Read_BeforeGtkIsInitialized_ThrowsInvalidOperation()
    {
        using var backend = new LinuxClipboardBackend(isReady: () => false);

        var ex = Assert.Throws<InvalidOperationException>(() => backend.Read(HermesDataFormats.Text, []));
        Assert.Contains("GTK", ex.Message);
        Assert.Throws<InvalidOperationException>(() => backend.Write(new HermesDataPackage { Text = "a" }));
        Assert.Throws<InvalidOperationException>(() => backend.GetFormats());
    }

    [Fact]
    public void StartMonitoring_BeforeGtkIsInitialized_ThrowsInvalidOperation()
    {
        using var backend = new LinuxClipboardBackend(isReady: () => false);

        Assert.Throws<InvalidOperationException>(backend.StartMonitoring);
    }
}
#endif
