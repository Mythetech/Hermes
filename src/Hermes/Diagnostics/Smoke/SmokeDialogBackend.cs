// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;

namespace Hermes.Diagnostics.Smoke;

/// <summary>
/// Stands in for native dialogs during a smoke run. A real dialog would block a headless run until the
/// CI timeout, so each attempt fails the run and returns the answer that commits to nothing.
/// </summary>
internal sealed class SmokeDialogBackend(SmokeSession session) : IDialogBackend
{
    public string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters)
    {
        Record("open file", title, null);
        return null;
    }

    public string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        Record("open folder", title, null);
        return null;
    }

    public string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        Record("save file", title, null);
        return null;
    }

    public DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon)
    {
        Record("message", title, message);
        return buttons switch
        {
            DialogButtons.Ok => DialogResult.Ok,
            DialogButtons.YesNo => DialogResult.No,
            _ => DialogResult.Cancel,
        };
    }

    private void Record(string kind, string title, string? detail)
    {
        var message = detail is null
            ? $"A native {kind} dialog was opened: {title}"
            : $"A native {kind} dialog was opened: {title}: {detail}";
        session.RecordError("dialog", "NativeDialog", message, null);
    }
}
