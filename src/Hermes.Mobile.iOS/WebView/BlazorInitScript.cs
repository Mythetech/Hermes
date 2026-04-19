// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Mobile.iOS.WebView;

internal static class BlazorInitScript
{
    /// <summary>
    /// Injected at document-end into every page. Wires window.external.sendMessage/receiveMessage
    /// to the WKWebView WebKit bridge. The JS contract matches Hermes desktop and MAUI's BlazorWebView.
    /// </summary>
    public const string Contents = """
        window.__receiveMessageCallbacks = [];
        window.__dispatchMessageCallback = function(message) {
            window.__receiveMessageCallbacks.forEach(function(callback) { callback(message); });
        };
        window.external = {
            sendMessage: function(message) {
                window.webkit.messageHandlers.webwindowinterop.postMessage(message);
            },
            receiveMessage: function(callback) {
                window.__receiveMessageCallbacks.push(callback);
            }
        };

        Blazor.start();

        (function () {
            window.onpageshow = function(event) {
                if (event.persisted) { window.location.reload(); }
            };
        })();
        """;
}
