// Copyright (c) Mythetech. Licensed under the MIT License.
#if LINUX
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Platforms.Linux;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("linux")]
public sealed class LinuxNativeParamsTests
{
    // HermesTypes.h asserts the same offsets and size when the native library compiles.
    [Fact]
    public void WindowParams_MatchTheNativeLayout()
    {
        Assert.Equal(80, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.OnClosing)));
        Assert.Equal(152, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.CustomSchemeNames)));
        Assert.Equal(280, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.BridgeToken)));
        Assert.Equal(288, Marshal.SizeOf<HermesWindowParams>());
    }
}
#endif
