// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Foundation;
using Hermes.Mobile;
using UIKit;

namespace Shared.Mobile;

[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    private HermesMobileHost? _host;

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var builder = HermesMobileAppBuilder.CreateDefault();
        builder.RootComponents.Add<Shared.App.App>("#app");
        // IClipboard is auto-registered to IOSClipboard in CreateDefault.

        _host = builder.Build();

        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = _host.RootViewController
        };
        Window.MakeKeyAndVisible();

        _host.Start();
        return true;
    }
}
