using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IDialogBackend using native NSOpenPanel, NSSavePanel, and NSAlert.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacDialogBackend : IDialogBackend
{
    public string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters)
    {
        var filterStrings = CreateFilterStrings(filters);
        var filterPtr = MarshalStringArray(filterStrings, out int filterCount);

        try
        {
            var resultPtr = MacNative.DialogShowOpenFile(title, defaultPath, multiSelect, filterPtr, filterCount, out int resultCount);

            if (resultPtr == IntPtr.Zero || resultCount == 0)
                return null;

            return UnmarshalStringArray(resultPtr, resultCount);
        }
        finally
        {
            FreeStringArray(filterPtr, filterCount);
        }
    }

    public string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        var resultPtr = MacNative.DialogShowOpenFolder(title, defaultPath, multiSelect, out int resultCount);

        if (resultPtr == IntPtr.Zero || resultCount == 0)
            return null;

        return UnmarshalStringArray(resultPtr, resultCount);
    }

    public string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        var filterStrings = CreateFilterStrings(filters);
        var filterPtr = MarshalStringArray(filterStrings, out int filterCount);

        try
        {
            var resultPtr = MacNative.DialogShowSaveFile(title, defaultPath, filterPtr, filterCount, defaultFileName);

            if (resultPtr == IntPtr.Zero)
                return null;

            var result = Marshal.PtrToStringUTF8(resultPtr);
            MacNative.Free(resultPtr);
            return result;
        }
        finally
        {
            FreeStringArray(filterPtr, filterCount);
        }
    }

    public DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon)
    {
        int result = MacNative.DialogShowMessage(title, message, (int)buttons, (int)icon);
        return (DialogResult)result;
    }

    #region Private Helpers

    /// <summary>
    /// Convert DialogFilter array to flat array of extension strings.
    /// </summary>
    private static string[]? CreateFilterStrings(DialogFilter[]? filters)
    {
        if (filters is null || filters.Length == 0)
            return null;

        var extensions = new List<string>();
        foreach (var filter in filters)
        {
            foreach (var ext in filter.Extensions)
            {
                extensions.Add(ext);
            }
        }

        return extensions.Count > 0 ? extensions.ToArray() : null;
    }

    /// <summary>
    /// Marshal a string array to native memory.
    /// </summary>
    private static IntPtr MarshalStringArray(string[]? strings, out int count)
    {
        if (strings is null || strings.Length == 0)
        {
            count = 0;
            return IntPtr.Zero;
        }

        count = strings.Length;
        var ptrSize = IntPtr.Size;
        var arrayPtr = Marshal.AllocHGlobal(ptrSize * count);

        for (int i = 0; i < count; i++)
        {
            var strPtr = Marshal.StringToHGlobalAnsi(strings[i]);
            Marshal.WriteIntPtr(arrayPtr, i * ptrSize, strPtr);
        }

        return arrayPtr;
    }

    /// <summary>
    /// Free a marshaled string array.
    /// </summary>
    private static void FreeStringArray(IntPtr arrayPtr, int count)
    {
        if (arrayPtr == IntPtr.Zero) return;

        var ptrSize = IntPtr.Size;
        for (int i = 0; i < count; i++)
        {
            var strPtr = Marshal.ReadIntPtr(arrayPtr, i * ptrSize);
            if (strPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(strPtr);
        }

        Marshal.FreeHGlobal(arrayPtr);
    }

    /// <summary>
    /// Unmarshal a string array from native memory and free it.
    /// </summary>
    private static string[] UnmarshalStringArray(IntPtr arrayPtr, int count)
    {
        var result = new string[count];
        var ptrSize = IntPtr.Size;

        for (int i = 0; i < count; i++)
        {
            var strPtr = Marshal.ReadIntPtr(arrayPtr, i * ptrSize);
            result[i] = Marshal.PtrToStringUTF8(strPtr) ?? "";
        }

        // Free the native array
        MacNative.FreeStringArray(arrayPtr, count);

        return result;
    }

    #endregion
}
