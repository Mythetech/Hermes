// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

/// <summary>
/// One drag out of a window: a shell data object carrying the clipboard's formats, run by SHDoDragDrop.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsDragSource
{
    [ThreadStatic]
    private static bool t_oleInitialized;

    private readonly HWND _hwnd;
    private readonly HermesDataPackage _package;
    private readonly TaskCompletionSource<HermesDragResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WindowsDragSource(HWND hwnd, HermesDataPackage package)
    {
        _hwnd = hwnd;
        _package = package;
    }

    public Task<HermesDragResult> Completion => _completion.Task;

    /// <summary>
    /// Runs the drag to its end on the UI thread; SHDoDragDrop pumps messages for as long as the button is held.
    /// </summary>
    public void Run()
    {
        if (_completion.Task.IsCompleted)
            return;

        try
        {
            if (!IsPrimaryButtonDown())
            {
                _completion.TrySetResult(HermesDragResult.Canceled);
                return;
            }

            EnsureOleInitialized();
            var dataObject = CreateDataObject(_package);
            try
            {
                var result = PInvoke.SHDoDragDrop(_hwnd, dataObject, null, DROPEFFECT.DROPEFFECT_COPY, out var effect);
                _completion.TrySetResult(OleDragRules.ToResult(result.Value, (uint)effect));
            }
            finally
            {
                Marshal.ReleaseComObject(dataObject);
            }
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
        }
    }

    /// <summary>Completes the drag as Canceled; a drag that already ended keeps its result.</summary>
    public void Cancel() => _completion.TrySetResult(HermesDragResult.Canceled);

    private static bool IsPrimaryButtonDown()
    {
        var swapped = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_SWAPBUTTON) != 0;
        return OleDragRules.IsDown(PInvoke.GetAsyncKeyState(OleDragRules.PrimaryButtonKey(swapped)));
    }

    // The UI thread is already STA, so this adds OLE's drag support without changing its apartment.
    private static void EnsureOleInitialized()
    {
        if (t_oleInitialized)
            return;

        PInvoke.OleInitialize().ThrowOnFailure();
        t_oleInitialized = true;
    }

    private static unsafe IDataObject CreateDataObject(HermesDataPackage package)
    {
        var formats = WindowsClipboardBackend.EncodeFormats(package);
        var iid = typeof(IDataObject).GUID;
        PInvoke.SHCreateDataObject(null, 0, null, null, in iid, out var created).ThrowOnFailure();
        var dataObject = (IDataObject)created;

        try
        {
            foreach (var (format, data) in formats)
                SetData(dataObject, format, data.Span);
            return dataObject;
        }
        catch
        {
            Marshal.ReleaseComObject(dataObject);
            throw;
        }
    }

    private static unsafe void SetData(IDataObject dataObject, uint format, ReadOnlySpan<byte> data)
    {
        var global = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (global.IsNull)
            throw new OutOfMemoryException("GlobalAlloc failed for drag data.");

        var pointer = PInvoke.GlobalLock(global);
        if (pointer == null)
        {
            PInvoke.GlobalFree(global);
            throw new IOException("GlobalLock failed for drag data.");
        }

        data.CopyTo(new Span<byte>(pointer, data.Length));
        PInvoke.GlobalUnlock(global);

        var formatEtc = new FORMATETC
        {
            cfFormat = unchecked((short)format),
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL,
        };
        var medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = (nint)global.Value };

        // With release set, the data object owns the memory once SetData succeeds; on failure it is still ours.
        try
        {
            dataObject.SetData(ref formatEtc, ref medium, true);
        }
        catch
        {
            PInvoke.GlobalFree(global);
            throw;
        }
    }
}
