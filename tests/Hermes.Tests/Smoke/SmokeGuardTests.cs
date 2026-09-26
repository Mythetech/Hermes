// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Diagnostics.Smoke;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeGuardTests
{
    private readonly SmokeHarness _harness = new();

    [Theory]
    [InlineData(DialogButtons.Ok, DialogResult.Ok)]
    [InlineData(DialogButtons.OkCancel, DialogResult.Cancel)]
    [InlineData(DialogButtons.YesNo, DialogResult.No)]
    [InlineData(DialogButtons.YesNoCancel, DialogResult.Cancel)]
    public void MessageDialogs_ReturnTheSafestAnswer_AndFailTheRun(DialogButtons buttons, DialogResult expected)
    {
        var dialogs = new SmokeDialogBackend(_harness.CreateSession(exitWhenDone: false));

        var result = dialogs.ShowMessage("Fatal error", "Something broke", buttons, DialogIcon.Error);

        Assert.Equal(expected, result);
        Assert.Contains("HERMES_SMOKE_ERROR: dialog: NativeDialog: A native message dialog was opened: Fatal error: Something broke", _harness.Lines);
    }

    [Fact]
    public void FileDialogs_ReturnNoSelection_AndFailTheRun()
    {
        var dialogs = new SmokeDialogBackend(_harness.CreateSession(exitWhenDone: false));

        Assert.Null(dialogs.ShowOpenFile("Open project", null, false, null));
        Assert.Null(dialogs.ShowOpenFolder("Pick a folder", null, false));
        Assert.Null(dialogs.ShowSaveFile("Save as", null, null, "notes.txt"));

        Assert.Equal(3, _harness.Lines.Count(line => line.StartsWith("HERMES_SMOKE_ERROR: dialog:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Window_WithASmokeSession_NeverReachesThePlatformDialogs()
    {
        var window = new HermesWindow(new RecordingWindowBackend());

        window.AttachSmokeSession(_harness.CreateSession(exitWhenDone: false));

        Assert.IsType<SmokeDialogBackend>(window.Dialogs);
    }
}
