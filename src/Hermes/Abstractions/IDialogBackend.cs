namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for native file and message dialogs.
/// </summary>
public interface IDialogBackend
{
    /// <summary>
    /// Show a file open dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultPath">Initial directory path, or null.</param>
    /// <param name="multiSelect">Whether multiple files can be selected.</param>
    /// <param name="filters">File type filters, or null for all files.</param>
    /// <returns>Array of selected file paths, or null if cancelled.</returns>
    string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters);

    /// <summary>
    /// Show a folder open dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultPath">Initial directory path, or null.</param>
    /// <param name="multiSelect">Whether multiple folders can be selected.</param>
    /// <returns>Array of selected folder paths, or null if cancelled.</returns>
    string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect);

    /// <summary>
    /// Show a file save dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="defaultPath">Initial directory path, or null.</param>
    /// <param name="filters">File type filters, or null for all files.</param>
    /// <param name="defaultFileName">Default file name, or null.</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName);

    /// <summary>
    /// Show a message dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Message text.</param>
    /// <param name="buttons">Button configuration.</param>
    /// <param name="icon">Icon to display.</param>
    /// <returns>The button that was clicked.</returns>
    DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon);
}

/// <summary>
/// File type filter for file dialogs.
/// </summary>
/// <param name="Name">Display name (e.g., "Text Files").</param>
/// <param name="Extensions">File extensions without dots (e.g., ["txt", "md"]).</param>
public readonly record struct DialogFilter(string Name, string[] Extensions);

/// <summary>
/// Button configuration for message dialogs.
/// </summary>
public enum DialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel
}

/// <summary>
/// Icon to display in message dialogs.
/// </summary>
public enum DialogIcon
{
    Info,
    Warning,
    Error,
    Question
}

/// <summary>
/// Result from a message dialog.
/// </summary>
public enum DialogResult
{
    Ok,
    Cancel,
    Yes,
    No
}
