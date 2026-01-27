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
        IFileOpenDialog* pfd = null;

        try
        {
            var clsid = typeof(FileOpenDialog).GUID;
            var iid = typeof(IFileOpenDialog).GUID;

            int hr = PInvoke.CoCreateInstance(
                in clsid,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                in iid,
                (void**)&pfd);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            fixed (char* titlePtr = title)
            {
                pfd->SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder(pfd, defaultPath);
            }

            if (filters is { Length: > 0 })
            {
                SetFileFilters(pfd, filters);
            }

            FILEOPENDIALOGOPTIONS options;
            pfd->GetOptions(&options);
            options |= FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST | FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR;

            if (multiSelect)
                options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

            pfd->SetOptions(options);

            hr = pfd->Show(_hwnd);
            if (hr < 0)
                return null;

            IShellItemArray* pResults = null;
            pfd->GetResults(&pResults);

            uint count;
            pResults->GetCount(&count);

            var results = new string[count];
            for (uint i = 0; i < count; i++)
            {
                IShellItem* pItem = null;
                pResults->GetItemAt(i, &pItem);

                PWSTR pszPath = default;
                pItem->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &pszPath);
                results[i] = new string(pszPath);

                PInvoke.CoTaskMemFree(pszPath);
                pItem->Release();
            }

            pResults->Release();
            return results;
        }
        finally
        {
            if (pfd != null)
                pfd->Release();
        }
    }

    public unsafe string[]? ShowOpenFolder(string title, string? defaultPath, bool multiSelect)
    {
        IFileOpenDialog* pfd = null;

        try
        {
            var clsid = typeof(FileOpenDialog).GUID;
            var iid = typeof(IFileOpenDialog).GUID;

            int hr = PInvoke.CoCreateInstance(
                in clsid,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                in iid,
                (void**)&pfd);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            fixed (char* titlePtr = title)
            {
                pfd->SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder(pfd, defaultPath);
            }

            FILEOPENDIALOGOPTIONS options;
            pfd->GetOptions(&options);
            options |= FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR;

            if (multiSelect)
                options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;

            pfd->SetOptions(options);

            hr = pfd->Show(_hwnd);
            if (hr < 0)
                return null;

            IShellItemArray* pResults = null;
            pfd->GetResults(&pResults);

            uint count;
            pResults->GetCount(&count);

            var results = new string[count];
            for (uint i = 0; i < count; i++)
            {
                IShellItem* pItem = null;
                pResults->GetItemAt(i, &pItem);

                PWSTR pszPath = default;
                pItem->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &pszPath);
                results[i] = new string(pszPath);

                PInvoke.CoTaskMemFree(pszPath);
                pItem->Release();
            }

            pResults->Release();
            return results;
        }
        finally
        {
            if (pfd != null)
                pfd->Release();
        }
    }

    public unsafe string? ShowSaveFile(string title, string? defaultPath, DialogFilter[]? filters, string? defaultFileName)
    {
        IFileSaveDialog* pfd = null;

        try
        {
            var clsid = typeof(FileSaveDialog).GUID;
            var iid = typeof(IFileSaveDialog).GUID;

            int hr = PInvoke.CoCreateInstance(
                in clsid,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                in iid,
                (void**)&pfd);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            fixed (char* titlePtr = title)
            {
                pfd->SetTitle(titlePtr);
            }

            if (!string.IsNullOrEmpty(defaultFileName))
            {
                fixed (char* fileNamePtr = defaultFileName)
                {
                    pfd->SetFileName(fileNamePtr);
                }
            }

            if (!string.IsNullOrEmpty(defaultPath))
            {
                SetDefaultFolder((IFileDialog*)pfd, defaultPath);
            }

            if (filters is { Length: > 0 })
            {
                SetFileFilters((IFileDialog*)pfd, filters);
            }

            FILEOPENDIALOGOPTIONS options;
            ((IFileDialog*)pfd)->GetOptions(&options);
            options |= FILEOPENDIALOGOPTIONS.FOS_NOCHANGEDIR | FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;
            ((IFileDialog*)pfd)->SetOptions(options);

            hr = pfd->Show(_hwnd);
            if (hr < 0)
                return null;

            IShellItem* pItem = null;
            pfd->GetResult(&pItem);

            PWSTR pszPath = default;
            pItem->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &pszPath);
            var result = new string(pszPath);

            PInvoke.CoTaskMemFree(pszPath);
            pItem->Release();

            return result;
        }
        finally
        {
            if (pfd != null)
                pfd->Release();
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

    private static unsafe void SetDefaultFolder(IFileDialog* pfd, string path)
    {
        IShellItem* psiFolder = null;

        fixed (char* pathPtr = path)
        {
            var iid = typeof(IShellItem).GUID;
            int hr = PInvoke.SHCreateItemFromParsingName(pathPtr, null, in iid, (void**)&psiFolder);

            if (hr >= 0 && psiFolder != null)
            {
                pfd->SetFolder(psiFolder);
                psiFolder->Release();
            }
        }
    }

    private static unsafe void SetFileFilters(IFileDialog* pfd, DialogFilter[] filters)
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
                    pszName = (PCWSTR)nameHandles[i].AddrOfPinnedObject(),
                    pszSpec = (PCWSTR)specHandles[i].AddrOfPinnedObject()
                };
            }

            fixed (COMDLG_FILTERSPEC* specsPtr = specs)
            {
                pfd->SetFileTypes((uint)specs.Length, specsPtr);
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
