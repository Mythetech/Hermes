// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxDialogBackend : IDialogBackend
{
    private readonly IntPtr _windowHandle;

    internal LinuxDialogBackend(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters)
    {
        var (filtersPtr, filterCount) = MarshalFilters(filters);

        try
        {
            var resultPtr = LinuxNative.DialogShowOpenFile(
                _windowHandle,
                title,
                defaultPath,
                multiSelect,
                filtersPtr,
                filterCount,
                out int resultCount);

            return UnmarshalStringArray(resultPtr, resultCount);
        }
        finally
        {
            FreeFilters(filtersPtr, filterCount);
        }
    }

    public string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        var resultPtr = LinuxNative.DialogShowOpenFolder(
            _windowHandle,
            title,
            defaultPath,
            multiSelect,
            out int resultCount);

        return UnmarshalStringArray(resultPtr, resultCount);
    }

    public string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        var (filtersPtr, filterCount) = MarshalFilters(filters);

        try
        {
            var resultPtr = LinuxNative.DialogShowSaveFile(
                _windowHandle,
                title,
                defaultPath,
                filtersPtr,
                filterCount,
                defaultFileName);

            if (resultPtr == IntPtr.Zero)
                return null;

            var result = Marshal.PtrToStringUTF8(resultPtr);
            LinuxNative.Free(resultPtr);
            return result;
        }
        finally
        {
            FreeFilters(filtersPtr, filterCount);
        }
    }

    public DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon)
    {
        // Map to native enum values
        var nativeButtons = buttons switch
        {
            DialogButtons.Ok => 0,          // DialogButtons_Ok
            DialogButtons.OkCancel => 1,    // DialogButtons_OkCancel
            DialogButtons.YesNo => 2,       // DialogButtons_YesNo
            DialogButtons.YesNoCancel => 3, // DialogButtons_YesNoCancel
            _ => 0
        };

        var nativeIcon = icon switch
        {
            DialogIcon.Info => 0,     // DialogIcon_Info
            DialogIcon.Warning => 1,  // DialogIcon_Warning
            DialogIcon.Error => 2,    // DialogIcon_Error
            DialogIcon.Question => 3, // DialogIcon_Question
            _ => 0
        };

        var nativeResult = LinuxNative.DialogShowMessage(
            _windowHandle,
            title,
            message,
            nativeButtons,
            nativeIcon);

        // Map native result back to DialogResult
        return nativeResult switch
        {
            0 => DialogResult.Ok,     // DialogResult_Ok
            1 => DialogResult.Cancel, // DialogResult_Cancel
            2 => DialogResult.Yes,    // DialogResult_Yes
            3 => DialogResult.No,     // DialogResult_No
            _ => DialogResult.Cancel
        };
    }

    #region Private Helpers

    /// <summary>
    /// Unmarshals a native string array and frees the native memory.
    /// </summary>
    private static string[]? UnmarshalStringArray(IntPtr ptr, int count)
    {
        if (ptr == IntPtr.Zero || count == 0)
            return null;

        var result = new string[count];
        for (int i = 0; i < count; i++)
        {
            var strPtr = Marshal.ReadIntPtr(ptr, i * IntPtr.Size);
            result[i] = Marshal.PtrToStringUTF8(strPtr) ?? "";
        }

        LinuxNative.FreeStringArray(ptr, count);
        return result;
    }

    /// <summary>
    /// Marshals DialogFilter array to native format.
    /// Each filter is encoded as "Name|ext1;ext2;ext3" string.
    /// </summary>
    private static (IntPtr, int) MarshalFilters(DialogFilter[]? filters)
    {
        if (filters == null || filters.Length == 0)
            return (IntPtr.Zero, 0);

        // Allocate array of pointers
        var arrayPtr = Marshal.AllocHGlobal(filters.Length * IntPtr.Size);

        for (int i = 0; i < filters.Length; i++)
        {
            var filter = filters[i];
            // Format: "Name|ext1;ext2;ext3"
            var filterStr = $"{filter.Name}|{string.Join(";", filter.Extensions)}";
            var strPtr = Marshal.StringToHGlobalAnsi(filterStr);
            Marshal.WriteIntPtr(arrayPtr, i * IntPtr.Size, strPtr);
        }

        return (arrayPtr, filters.Length);
    }

    /// <summary>
    /// Frees the native filter array.
    /// </summary>
    private static void FreeFilters(IntPtr arrayPtr, int count)
    {
        if (arrayPtr == IntPtr.Zero)
            return;

        for (int i = 0; i < count; i++)
        {
            var strPtr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
            Marshal.FreeHGlobal(strPtr);
        }

        Marshal.FreeHGlobal(arrayPtr);
    }

    #endregion
}
