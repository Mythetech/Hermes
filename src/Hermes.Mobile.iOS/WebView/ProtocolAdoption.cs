// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;

namespace Hermes.Mobile.iOS.WebView;

/// <summary>
/// Registers Obj-C protocol conformance on a managed NSObject subclass by calling
/// class_addProtocol directly on the native Class at type-initialisation time.
/// </summary>
/// <remarks>
/// Works around a .NET iOS 26.2 regression (dotnet/macios PR #23002, which reworked
/// the conformsToProtocol: dispatch path to resolve the managed peer via a GCHandle
/// stored on the native instance). When WebKit probes a handler during
/// AddScriptMessageHandler / SetUrlSchemeHandler the managed override can return NO
/// before the peer's GCHandle is attached, so WKWebView silently drops the handler
/// and the page loads blank with no logs.
///
/// Advertising the protocol on the native Class makes class_conformsToProtocol
/// answer YES purely in Obj-C, bypassing the managed override entirely. The call is
/// idempotent and works with the dynamic, static, and managed-static registrars.
///
/// The [Adopts(...)] attribute alone is insufficient on this runtime because it
/// only stores managed metadata consulted by the NSObject conformsToProtocol:
/// override, which is exactly the path that regressed.
/// </remarks>
internal static class ProtocolAdoption
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";

    [DllImport(LibObjC, EntryPoint = "class_addProtocol")]
    private static extern byte class_addProtocol(IntPtr cls, IntPtr protocol);

    public static void Ensure<T>(params string[] protocolNames) where T : NSObject
    {
        var cls = Class.GetHandle(typeof(T));
        if (cls == IntPtr.Zero)
        {
            Console.WriteLine($"[Hermes.Mobile] ProtocolAdoption: no class handle for {typeof(T).FullName}, skipping");
            return;
        }

        foreach (var name in protocolNames)
        {
            var protocol = Protocol.GetHandle(name);
            if (protocol == IntPtr.Zero)
            {
                Console.WriteLine($"[Hermes.Mobile] ProtocolAdoption: protocol '{name}' not found, skipping");
                continue;
            }

            var added = class_addProtocol(cls, protocol) != 0;
            Console.WriteLine($"[Hermes.Mobile] ProtocolAdoption: {typeof(T).Name} adopts '{name}' ({(added ? "added" : "already present")})");
        }
    }
}
