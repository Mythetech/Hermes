// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Mobile.Android.WebView;

internal static class BlazorInitScript
{
    public const string Contents = """
        if (!window.__hermesInitialized) {
            window.__hermesInitialized = true;

            window.__receiveMessageCallbacks = [];
            window.__dispatchMessageCallback = function(message) {
                window.__receiveMessageCallbacks.forEach(function(callback) { callback(message); });
            };
            window.external = {
                sendMessage: function(message) {
                    HermesBridge.postMessage(message);
                },
                receiveMessage: function(callback) {
                    window.__receiveMessageCallbacks.push(callback);
                }
            };

            Blazor.start();

            window.onpageshow = function(event) {
                if (event.persisted) { window.location.reload(); }
            };
        }
        """;
}
