// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Diagnostics;

namespace Hermes.Platforms.Windows;

/// <summary>
/// Minimal WinRT plumbing for toast notifications: HSTRING helpers, activation, raw vtable calls,
/// and a process-lifetime COM object implementing ITypedEventHandler&lt;ToastNotification, IInspectable&gt;.
/// Everything is function pointers and <see cref="UnmanagedCallersOnlyAttribute"/> so it stays Native AOT clean.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinRtInterop
{
    private const string Combase = "combase.dll";
    private const int E_NOINTERFACE = unchecked((int)0x80004002);

    // IIDs: copied from the Windows 10 SDK headers. Verify against the header, never from memory.
    internal static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");
    internal static readonly Guid IID_IInspectable = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");
    internal static readonly Guid IID_IAgileObject = new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");
    internal static readonly Guid IID_IToastNotificationManagerStatics = new("50AC103F-D235-4598-BBEF-98FE4D1A3AD4");
    internal static readonly Guid IID_IToastNotificationManagerStatics2 = new("7AB93C52-0E48-4750-BA9D-1A4113981847");
    internal static readonly Guid IID_IToastNotificationHistory = new("5CADDC63-01D3-4C97-986F-0533483FEE14");
    internal static readonly Guid IID_IToastNotifier = new("75927B93-03F3-41EC-91D3-6E5BAC1B38E7");
    internal static readonly Guid IID_IToastNotificationFactory = new("04124B20-82C6-4229-B109-FD9ED4662B53");
    internal static readonly Guid IID_IToastNotification = new("997E2675-059E-4E60-8B06-1760917C8B80");
    internal static readonly Guid IID_IToastNotification2 = new("9DFB9FD1-143A-490E-90BF-B9FBA7132DE7");
    internal static readonly Guid IID_IToastActivatedEventArgs = new("E3BF92F3-C197-436F-8265-0625824F8DAC");
    internal static readonly Guid IID_IXmlDocument = new("F7F3A506-1E87-42D6-BCFB-B8C809FA5494");
    internal static readonly Guid IID_IXmlDocumentIO = new("6CD0E74E-EE65-4489-9EBF-CA43E87BA637");
    // __FITypedEventHandler_2_Windows__CUI__CNotifications__CToastNotification_IInspectable
    internal static readonly Guid IID_ToastActivatedHandler = new("AB54DE2D-97D9-5528-B6AD-105AFE156530");

    internal const string ToastNotificationManagerClass = "Windows.UI.Notifications.ToastNotificationManager";
    internal const string ToastNotificationClass = "Windows.UI.Notifications.ToastNotification";
    internal const string XmlDocumentClass = "Windows.Data.Xml.Dom.XmlDocument";

    [LibraryImport(Combase)]
    private static partial int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

    [LibraryImport(Combase)]
    private static partial int RoActivateInstance(IntPtr activatableClassId, out IntPtr instance);

    [LibraryImport(Combase)]
    private static partial int WindowsCreateString(char* sourceString, uint length, out IntPtr hstring);

    [LibraryImport(Combase)]
    private static partial int WindowsDeleteString(IntPtr hstring);

    [LibraryImport(Combase)]
    private static partial char* WindowsGetStringRawBuffer(IntPtr hstring, uint* length);

    #region HSTRING

    internal readonly struct HString : IDisposable
    {
        public IntPtr Handle { get; }

        public HString(string value)
        {
            fixed (char* chars = value)
            {
                ThrowIfFailed(WindowsCreateString(chars, (uint)value.Length, out var handle));
                Handle = handle;
            }
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                WindowsDeleteString(Handle);
        }

        public static string? ToManaged(IntPtr hstring)
        {
            if (hstring == IntPtr.Zero)
                return null;

            uint length;
            var buffer = WindowsGetStringRawBuffer(hstring, &length);
            return new string(buffer, 0, (int)length);
        }

        public static void Delete(IntPtr hstring)
        {
            if (hstring != IntPtr.Zero)
                WindowsDeleteString(hstring);
        }
    }

    #endregion

    #region Activation and IUnknown

    internal static IntPtr GetActivationFactory(string className, in Guid iid)
    {
        using var name = new HString(className);
        ThrowIfFailed(RoGetActivationFactory(name.Handle, in iid, out var factory));
        return factory;
    }

    internal static IntPtr ActivateInstance(string className)
    {
        using var name = new HString(className);
        ThrowIfFailed(RoActivateInstance(name.Handle, out var instance));
        return instance;
    }

    internal static IntPtr QueryInterface(IntPtr obj, in Guid iid)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)VTableSlot(obj, 0);
        IntPtr result;
        fixed (Guid* pIid = &iid)
        {
            ThrowIfFailed(fn(obj, pIid, &result));
        }
        return result;
    }

    internal static void Release(IntPtr obj)
    {
        if (obj == IntPtr.Zero)
            return;

        var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint>)VTableSlot(obj, 2);
        fn(obj);
    }

    private static IntPtr VTableSlot(IntPtr obj, int index) => (*(IntPtr**)obj)[index];

    internal static void ThrowIfFailed(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }

    #endregion

    #region Interface calls (slot indexes are IInspectable's 6 methods plus the interface's own order in the header)

    // IToastNotificationManagerStatics::CreateToastNotifierWithId
    internal static IntPtr CreateToastNotifierWithId(IntPtr statics, IntPtr appIdHString)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)VTableSlot(statics, 7);
        IntPtr notifier;
        ThrowIfFailed(fn(statics, appIdHString, &notifier));
        return notifier;
    }

    // IToastNotificationManagerStatics2::get_History
    internal static IntPtr GetHistory(IntPtr statics2)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)VTableSlot(statics2, 6);
        IntPtr history;
        ThrowIfFailed(fn(statics2, &history));
        return history;
    }

    // IToastNotificationHistory::RemoveGroupedTagWithId
    internal static void RemoveGroupedTagWithId(IntPtr history, IntPtr tag, IntPtr group, IntPtr appId)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, IntPtr, int>)VTableSlot(history, 8);
        ThrowIfFailed(fn(history, tag, group, appId));
    }

    // IToastNotificationHistory::ClearWithId
    internal static void ClearWithId(IntPtr history, IntPtr appId)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTableSlot(history, 12);
        ThrowIfFailed(fn(history, appId));
    }

    // IToastNotifier::Show
    internal static void ShowToast(IntPtr notifier, IntPtr toast)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTableSlot(notifier, 6);
        ThrowIfFailed(fn(notifier, toast));
    }

    // IToastNotifier::get_Setting (0 = Enabled)
    internal static int GetNotifierSetting(IntPtr notifier)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, int*, int>)VTableSlot(notifier, 8);
        int setting;
        ThrowIfFailed(fn(notifier, &setting));
        return setting;
    }

    // IToastNotificationFactory::CreateToastNotification
    internal static IntPtr CreateToastNotification(IntPtr factory, IntPtr xmlDocument)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)VTableSlot(factory, 6);
        IntPtr toast;
        ThrowIfFailed(fn(factory, xmlDocument, &toast));
        return toast;
    }

    // IToastNotification::add_Activated
    internal static long AddActivatedHandler(IntPtr toast, IntPtr handler)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, long*, int>)VTableSlot(toast, 11);
        long token;
        ThrowIfFailed(fn(toast, handler, &token));
        return token;
    }

    // IToastNotification::remove_Activated
    internal static void RemoveActivatedHandler(IntPtr toast, long token)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, long, int>)VTableSlot(toast, 12);
        fn(toast, token);
    }

    // IToastNotification2::put_Tag
    internal static void SetTag(IntPtr toast2, IntPtr tag)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTableSlot(toast2, 6);
        ThrowIfFailed(fn(toast2, tag));
    }

    // IToastNotification2::put_Group
    internal static void SetGroup(IntPtr toast2, IntPtr group)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTableSlot(toast2, 8);
        ThrowIfFailed(fn(toast2, group));
    }

    // IXmlDocumentIO::LoadXml
    internal static void LoadXml(IntPtr xmlDocumentIo, IntPtr xml)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)VTableSlot(xmlDocumentIo, 6);
        ThrowIfFailed(fn(xmlDocumentIo, xml));
    }

    // IToastActivatedEventArgs::get_Arguments (caller owns the returned HSTRING)
    internal static IntPtr GetActivatedArguments(IntPtr args)
    {
        var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)VTableSlot(args, 6);
        IntPtr arguments;
        ThrowIfFailed(fn(args, &arguments));
        return arguments;
    }

    #endregion

    #region Activated handler COM object

    /// <summary>
    /// A single process-lifetime COM object implementing ITypedEventHandler&lt;ToastNotification, IInspectable&gt;.
    /// The launch arguments carry the notification id, so one handler serves every toast.
    /// </summary>
    internal static class ToastActivatedHandler
    {
        private static readonly IntPtr s_vtable;
        private static readonly IntPtr s_instance;
        private static int s_refCount = 1;

        /// <summary>Set by the backend; receives the toast's launch arguments (the notification id) on a WinRT thread.</summary>
        public static Action<string>? Activated { get; set; }

        static ToastActivatedHandler()
        {
            s_vtable = (IntPtr)NativeMemory.Alloc((nuint)(sizeof(IntPtr) * 4));
            var slots = (IntPtr*)s_vtable;
            slots[0] = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)&QueryInterface;
            slots[1] = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint>)&AddRef;
            slots[2] = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, uint>)&Release;
            slots[3] = (IntPtr)(delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int>)&Invoke;

            s_instance = (IntPtr)NativeMemory.Alloc((nuint)sizeof(IntPtr));
            *(IntPtr*)s_instance = s_vtable;
        }

        public static IntPtr Instance => s_instance;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int QueryInterface(IntPtr self, Guid* iid, IntPtr* ppv)
        {
            if (*iid == IID_IUnknown || *iid == IID_IAgileObject || *iid == IID_ToastActivatedHandler)
            {
                Interlocked.Increment(ref s_refCount);
                *ppv = self;
                return 0;
            }

            *ppv = IntPtr.Zero;
            return E_NOINTERFACE;
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static uint AddRef(IntPtr self) => (uint)Interlocked.Increment(ref s_refCount);

        // The object lives for the whole process; the count is tracked only so COM sees sane values.
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static uint Release(IntPtr self) => (uint)Interlocked.Decrement(ref s_refCount);

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static int Invoke(IntPtr self, IntPtr sender, IntPtr args)
        {
            IntPtr activatedArgs = IntPtr.Zero;
            IntPtr arguments = IntPtr.Zero;
            try
            {
                activatedArgs = WinRtInterop.QueryInterface(args, in IID_IToastActivatedEventArgs);
                arguments = GetActivatedArguments(activatedArgs);
                var id = HString.ToManaged(arguments);
                if (!string.IsNullOrEmpty(id))
                    Activated?.Invoke(id);
            }
            catch (Exception ex)
            {
                HermesLogger.Error("Toast activation handler failed.", ex);
            }
            finally
            {
                HString.Delete(arguments);
                WinRtInterop.Release(activatedArgs);
            }

            return 0;
        }
    }

    #endregion
}
#endif
