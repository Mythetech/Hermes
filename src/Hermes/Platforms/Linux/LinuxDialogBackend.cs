using System.Runtime.Versioning;
using Hermes.Abstractions;
using Gtk;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxDialogBackend : IDialogBackend
{
    private readonly Gtk.Window _parentWindow;

    internal LinuxDialogBackend(Gtk.Window parentWindow)
    {
        _parentWindow = parentWindow;
    }

    public string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters)
    {
        using var dialog = new FileChooserDialog(
            title,
            _parentWindow,
            FileChooserAction.Open,
            "Cancel", ResponseType.Cancel,
            "Open", ResponseType.Accept);

        dialog.SelectMultiple = multiSelect;

        if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
        {
            dialog.SetCurrentFolder(defaultPath);
        }

        ApplyFilters(dialog, filters);

        var response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept)
        {
            var filenames = dialog.Filenames;
            dialog.Destroy();
            return filenames;
        }

        dialog.Destroy();
        return null;
    }

    public string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        using var dialog = new FileChooserDialog(
            title,
            _parentWindow,
            FileChooserAction.SelectFolder,
            "Cancel", ResponseType.Cancel,
            "Select", ResponseType.Accept);

        dialog.SelectMultiple = multiSelect;

        if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
        {
            dialog.SetCurrentFolder(defaultPath);
        }

        var response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept)
        {
            var filenames = dialog.Filenames;
            dialog.Destroy();
            return filenames;
        }

        dialog.Destroy();
        return null;
    }

    public string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        using var dialog = new FileChooserDialog(
            title,
            _parentWindow,
            FileChooserAction.Save,
            "Cancel", ResponseType.Cancel,
            "Save", ResponseType.Accept);

        dialog.DoOverwriteConfirmation = true;

        if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
        {
            dialog.SetCurrentFolder(defaultPath);
        }

        if (!string.IsNullOrEmpty(defaultFileName))
        {
            dialog.CurrentName = defaultFileName;
        }

        ApplyFilters(dialog, filters);

        var response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept)
        {
            var filename = dialog.Filename;
            dialog.Destroy();
            return filename;
        }

        dialog.Destroy();
        return null;
    }

    public Abstractions.DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon)
    {
        var gtkButtons = buttons switch
        {
            DialogButtons.Ok => ButtonsType.Ok,
            DialogButtons.OkCancel => ButtonsType.OkCancel,
            DialogButtons.YesNo => ButtonsType.YesNo,
            DialogButtons.YesNoCancel => ButtonsType.None, // Need custom buttons
            _ => ButtonsType.Ok
        };

        var gtkMessageType = icon switch
        {
            DialogIcon.Info => MessageType.Info,
            DialogIcon.Warning => MessageType.Warning,
            DialogIcon.Error => MessageType.Error,
            DialogIcon.Question => MessageType.Question,
            _ => MessageType.Info
        };

        using var dialog = new MessageDialog(
            _parentWindow,
            DialogFlags.Modal | DialogFlags.DestroyWithParent,
            gtkMessageType,
            gtkButtons,
            message);

        dialog.Title = title;

        // Add custom buttons for YesNoCancel
        if (buttons == DialogButtons.YesNoCancel)
        {
            dialog.AddButton("Yes", ResponseType.Yes);
            dialog.AddButton("No", ResponseType.No);
            dialog.AddButton("Cancel", ResponseType.Cancel);
        }

        var response = (ResponseType)dialog.Run();
        dialog.Destroy();

        return response switch
        {
            ResponseType.Ok => Abstractions.DialogResult.Ok,
            ResponseType.Cancel => Abstractions.DialogResult.Cancel,
            ResponseType.Yes => Abstractions.DialogResult.Yes,
            ResponseType.No => Abstractions.DialogResult.No,
            ResponseType.DeleteEvent => Abstractions.DialogResult.Cancel, // Window closed
            _ => Abstractions.DialogResult.Cancel
        };
    }

    private static void ApplyFilters(FileChooserDialog dialog, DialogFilter[]? filters)
    {
        if (filters == null || filters.Length == 0)
        {
            var allFilter = new FileFilter { Name = "All Files" };
            allFilter.AddPattern("*");
            dialog.AddFilter(allFilter);
            return;
        }

        foreach (var filter in filters)
        {
            var gtkFilter = new FileFilter { Name = filter.Name };

            foreach (var ext in filter.Extensions)
            {
                gtkFilter.AddPattern($"*.{ext}");
            }

            dialog.AddFilter(gtkFilter);
        }

        // Also add an "All Files" option
        var allFilesFilter = new FileFilter { Name = "All Files" };
        allFilesFilter.AddPattern("*");
        dialog.AddFilter(allFilesFilter);
    }
}
