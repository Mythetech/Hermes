// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.Webkit;

namespace Hermes.Mobile.Android.WebView;

internal sealed class HermesWebChromeClient : WebChromeClient
{
    public override bool OnConsoleMessage(ConsoleMessage? consoleMessage)
    {
        if (consoleMessage is null)
            return base.OnConsoleMessage(consoleMessage);

        var messageLevel = consoleMessage.InvokeMessageLevel();
        var level = "LOG";
        if (messageLevel == ConsoleMessage.MessageLevel.Error)
            level = "ERROR";
        else if (messageLevel == ConsoleMessage.MessageLevel.Warning)
            level = "WARN";

        Console.WriteLine($"[Hermes.Mobile.Android] [{level}] {consoleMessage.Message()} ({consoleMessage.SourceId()}:{consoleMessage.LineNumber()})");
        return true;
    }
}
