// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Hermes.SingleInstance;

/// <summary>
/// Names the scope the single-instance lock covers: the user on macOS and Linux, the login session on Windows.
/// </summary>
internal static partial class UserScope
{
    public static string Current
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return geteuid().ToString(CultureInfo.InvariantCulture);

            using var process = Process.GetCurrentProcess();
            return process.SessionId.ToString(CultureInfo.InvariantCulture);
        }
    }

    [LibraryImport("libc")]
    private static partial uint geteuid();
}
