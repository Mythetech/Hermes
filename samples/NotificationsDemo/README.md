# NotificationsDemo

Manual verification app for native notifications. One window, six buttons, a log.

## What to check on every platform

1. The status line at the top says "Notifications supported on this host."
2. **Request Permission** logs `permission granted=True` (macOS shows the system prompt the first time).
3. **Show** produces a native banner with the title and body.
4. Clicking the banner logs `CLICKED id=... tag=page/settings`. The tag is the payload an app would use to navigate.
5. **Show Silent** produces a banner with no sound.
6. **Show With Icon** shows the Hermes icon on the banner (Linux depends on the daemon honouring `image-path`).
7. **Dismiss Last** removes the most recent notification from the notification center.

## macOS

UNUserNotificationCenter needs a bundle identifier, so `dotnet run` reports "not supported". Use the bundle script:

    ./bundle-macos.sh            # arm64
    ./bundle-macos.sh osx-x64    # Intel

It publishes, lays out `bin/bundle/NotificationsDemo.app`, ad-hoc signs it, and opens it.

If the app bounces in the Dock and exits, the apphost could not find a shared runtime under `/usr/local/share/dotnet`. Either install the SDK there or change the script's `--self-contained false` to `true`.

## Windows

    dotnet run --project samples/NotificationsDemo

The app registers `HKCU\Software\Classes\AppUserModelId\Mythetech.Hermes.NotificationsDemo` on first run. Toasts appear in the Action Center under "Hermes Notifications Demo".

## Linux

    dotnet run --project samples/NotificationsDemo

Requires a running notification daemon on the session bus (GNOME Shell, KDE Plasma, dunst, mako). Without one the status line reports the D-Bus error.
