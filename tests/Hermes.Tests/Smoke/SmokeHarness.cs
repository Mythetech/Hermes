// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Microsoft.Extensions.Time.Testing;

namespace Hermes.Tests.Smoke;

/// <summary>
/// Builds smoke sessions with fake time, captured output and recorded exits, so tests can drive a whole
/// run without ending the test process.
/// </summary>
internal sealed class SmokeHarness
{
    public FakeTimeProvider Time { get; } = new();

    public StringWriter Output { get; } = new();

    public List<int> ExitCodes { get; } = new();

    public List<int> HardExits { get; } = new();

    public Dictionary<string, string> Files { get; } = new();

    public Exception? WriteFailure { get; set; }

    public int CloseRequests { get; private set; }

    public string[] Lines => Output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    public SmokeSession CreateSession(
        TimeSpan? timeout = null,
        string? resultPath = null,
        bool exitWhenDone = true,
        bool isMacOS = false,
        bool attachClose = true)
    {
        var settings = new SmokeTestSettings(true, timeout ?? TimeSpan.FromSeconds(60), resultPath, exitWhenDone);
        var session = new SmokeSession(settings, new SmokeSessionContext
        {
            Time = Time,
            Output = Output,
            App = new SmokeAppInfo("SmokeApp", "1.2.3", "Linux", "x64"),
            IsMacOS = isMacOS,
            SetExitCode = ExitCodes.Add,
            HardExit = HardExits.Add,
            WriteFile = (path, contents) =>
            {
                if (WriteFailure is not null)
                    throw WriteFailure;
                Files[path] = contents;
            },
        });

        if (attachClose)
            session.AttachCloseHandler(() => CloseRequests++);

        session.Start();
        return session;
    }

    public static void Boot(SmokeSession session)
    {
        session.MarkMilestone(SmokeSession.WindowShownMilestone);
        session.MarkMilestone(SmokeSession.FirstRenderMilestone);
    }

    public static SmokeCheck Check(string name, Func<CancellationToken, Task>? run = null, TimeSpan? timeout = null) =>
        new(name, timeout ?? TimeSpan.FromSeconds(10), run ?? (_ => Task.CompletedTask));
}
