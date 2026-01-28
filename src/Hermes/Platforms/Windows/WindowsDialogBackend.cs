using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDialogBackend : IDialogBackend
{
    private readonly HWND _hwnd;

    internal WindowsDialogBackend(HWND hwnd)
    {
        _hwnd = hwnd;
    }

    public unsafe string[]? ShowOpenFile(string title, string? defaultPath, bool multiSelect, DialogFilter[]? filters)
    {
        var clsid = typeof(FileOpenDialog).GUID;
        var iid = typeof(IFileOpenDialog).GUID;

        int hr = PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, in iid, out var ppv);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        var pfd = (IFileOpenDialog)ppv;

        try
        {
            fixed (char* titlePtr = title)
            {
                pfd.SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder(pfd, defaultPath);
            }

            if (filters is { Length: > 0 })
            {
                SetFileFilters(pfd, filters);
            }

            pfd.GetOptions(out var options);
            options |= FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST | FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR;

            if (multiSelect)
                options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

            pfd.SetOptions(options);

            try
            {
                pfd.Show(_hwnd);
            }
            catch (COMException)
            {
                return null; // User cancelled
            }

            pfd.GetResults(out var pResults);

            pResults.GetCount(out var count);

            var results = new string[count];
            for (uint i = 0; i < count; i++)
            {
                pResults.GetItemAt(i, out var pItem);

                pItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pszPath);
                results[i] = pszPath.ToString();

                PInvoke.CoTaskMemFree(pszPath);
                Marshal.ReleaseComObject(pItem);
            }

            Marshal.ReleaseComObject(pResults);
            return results;
        }
        finally
        {
            Marshal.ReleaseComObject(pfd);
        }
    }

    public unsafe string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        var clsid = typeof(FileOpenDialog).GUID;
        var iid = typeof(IFileOpenDialog).GUID;

        int hr = PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, in iid, out var ppv);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        var pfd = (IFileOpenDialog)ppv;

        try
        {
            fixed (char* titlePtr = title)
            {
                pfd.SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder(pfd, defaultPath);
            }

            pfd.GetOptions(out var options);
            options |= FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR;

            if (multiSelect)
                options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

            pfd.SetOptions(options);

            try
            {
                pfd.Show(_hwnd);
            }
            catch (COMException)
            {
                return null; // User cancelled
            }

            pfd.GetResults(out var pResults);

            pResults.GetCount(out var count);

            var results = new string[count];
            for (uint i = 0; i < count; i++)
            {
                pResults.GetItemAt(i, out var pItem);

                pItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pszPath);
                results[i] = pszPath.ToString();

                PInvoke.CoTaskMemFree(pszPath);
                Marshal.ReleaseComObject(pItem);
            }

            Marshal.ReleaseComObject(pResults);
            return results;
        }
        finally
        {
            Marshal.ReleaseComObject(pfd);
        }
    }

    public unsafe string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        var clsid = typeof(FileSaveDialog).GUID;
        var iid = typeof(IFileSaveDialog).GUID;

        int hr = PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, in iid, out var ppv);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);

        var pfd = (IFileSaveDialog)ppv;

        try
        {
            fixed (char* titlePtr = title)
            {
                pfd.SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultFileName))
            {
                fixed (char* fileNamePtr = defaultFileName)
                {
                    pfd.SetFileName(fileNamePtr);
                }
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder(pfd, defaultPath);
            }

            if (filters is { Length: > 0 })
            {
                SetFileFilters(pfd, filters);
            }

            pfd.GetOptions(out var options);
            options |= FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;
            pfd.SetOptions(options);

            try
            {
                pfd.Show(_hwnd);
            }
            catch (COMException)
            {
                return null; // User cancelled
            }

            pfd.GetResult(out var pItem);

            pItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var pszPath);
            var result = pszPath.ToString();

            PInvoke.CoTaskMemFree(pszPath);
            Marshal.ReleaseComObject(pItem);

            return result;
        }
        finally
        {
            Marshal.ReleaseComObject(pfd);
        }
    }

    public Abstractions.DialogResult ShowMessage(string title, string message, DialogButtons buttons, DialogIcon icon)
    {
        var flags = buttons switch
        {
            DialogButtons.Ok => MESSAGEBOX_STYLE.MB_OK,
            DialogButtons.OkCancel => MESSAGEBOX_STYLE.MB_OKCANCEL,
            DialogButtons.YesNo => MESSAGEBOX_STYLE.MB_YESNO,
            DialogButtons.YesNoCancel => MESSAGEBOX_STYLE.MB_YESNOCANCEL,
            _ => MESSAGEBOX_STYLE.MB_OK
        };

        flags |= icon switch
        {
            DialogIcon.Info => MESSAGEBOX_STYLE.MB_ICONINFORMATION,
            DialogIcon.Warning => MESSAGEBOX_STYLE.MB_ICONWARNING,
            DialogIcon.Error => MESSAGEBOX_STYLE.MB_ICONERROR,
            DialogIcon.Question => MESSAGEBOX_STYLE.MB_ICONQUESTION,
            _ => 0
        };

        var result = PInvoke.MessageBox(_hwnd, message, title, flags);

        return result switch
        {
            MESSAGEBOX_RESULT.IDOK => Abstractions.DialogResult.Ok,
            MESSAGEBOX_RESULT.IDCANCEL => Abstractions.DialogResult.Cancel,
            MESSAGEBOX_RESULT.IDYES => Abstractions.DialogResult.Yes,
            MESSAGEBOX_RESULT.IDNO => Abstractions.DialogResult.No,
            _ => Abstractions.DialogResult.Cancel
        };
    }

    private static unsafe void SetDefaultFolder(IFileDialog pfd, string path)
    {
        var iid = typeof(IShellItem).GUID;
        int hr = PInvoke.SHCreateItemFromParsingName(path, null, in iid, out var ppv);

        if (hr >= 0 && ppv != null)
        {
            var psiFolder = (IShellItem)ppv;
            pfd.SetFolder(psiFolder);
            Marshal.ReleaseComObject(psiFolder);
        }
    }

    private static unsafe void SetFileFilters(IFileDialog pfd, DialogFilter[] filters)
    {
        var specs = new COMDLG_FILTERSPEC[filters.Length];
        var nameHandles = new GCHandle[filters.Length];
        var specHandles = new GCHandle[filters.Length];

        try
        {
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var pattern = string.Join(";", filter.Extensions.Select(e => $"*.{e}"));

                var nameChars = (filter.Name + '\0').ToCharArray();
                var specChars = (pattern + '\0').ToCharArray();

                nameHandles[i] = GCHandle.Alloc(nameChars, GCHandleType.Pinned);
                specHandles[i] = GCHandle.Alloc(specChars, GCHandleType.Pinned);

                specs[i] = new COMDLG_FILTERSPEC
                {
                    pszName = new PCWSTR((char*)nameHandles[i].AddrOfPinnedObject()),
                    pszSpec = new PCWSTR((char*)specHandles[i].AddrOfPinnedObject())
                };
            }

            fixed (COMDLG_FILTERSPEC* specsPtr = specs)
            {
                pfd.SetFileTypes((uint)specs.Length, specsPtr);
            }
        }
        finally
        {
            for (int i = 0; i < filters.Length; i++)
            {
                if (nameHandles[i].IsAllocated)
                    nameHandles[i].Free();
                if (specHandles[i].IsAllocated)
                    specHandles[i].Free();
            }
        }
    }
}
