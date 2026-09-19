// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;
using System.Threading.Channels;
using Hermes;
using Hermes.Contracts.Notifications;
using Hermes.Notifications;

namespace NotificationsDemo;

internal static class Program
{
    private static readonly Channel<string> s_commands = Channel.CreateUnbounded<string>();
    private static HermesWindow? s_window;
    private static string? s_lastId;
    private static volatile bool s_closing;

    // Windows requires the main thread to be STA for WebView2; the attribute
    // only works on an explicit synchronous Main, never top-level statements.
    [STAThread]
    private static void Main(string[] args)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.png");

        HermesApplication.ConfigureNotifications(new NativeNotificationOptions
        {
            AppId = "Mythetech.Hermes.NotificationsDemo",
            DisplayName = "Hermes Notifications Demo",
            IconPath = File.Exists(iconPath) ? iconPath : null,
        });

        s_window = new HermesWindow()
            .SetTitle("Hermes Notifications Demo")
            .SetSize(720, 560)
            .Center()
            .SetDevToolsEnabled(true)
            .LoadHtml(ReadEmbeddedHtml())
            .OnClosing(() =>
            {
                s_closing = true;
                s_commands.Writer.TryComplete();
            })
            .OnWebMessage(message => s_commands.Writer.TryWrite(message));

        // First access on the UI thread, before the message loop starts.
        var notifications = HermesApplication.Notifications;
        notifications.Clicked += OnClicked;

        // Web messages arrive on the UI thread, and on Linux ShowAsync completes on that same
        // thread, so awaiting inside the handler would deadlock. Commands are queued and consumed
        // off the UI thread. Once the window starts closing the pump is about to die, so logging
        // becomes a no-op and the join is capped: a blocking Invoke after the loop exits would hang.
        var consumer = Task.Run(ConsumeCommandsAsync);

        s_window.WaitForClose();

        s_commands.Writer.TryComplete();
        if (!consumer.Wait(TimeSpan.FromSeconds(2)))
            Console.Error.WriteLine("Command consumer did not finish within 2 seconds; exiting anyway.");
        notifications.Clicked -= OnClicked;
        HermesApplication.Shutdown();
    }

    private static async Task ConsumeCommandsAsync()
    {
        await foreach (var command in s_commands.Reader.ReadAllAsync())
            await HandleCommandAsync(command);
    }

    private static void OnClicked(NotificationClickedEventArgs args)
    {
        Log($"CLICKED id={args.Id} tag={args.Tag ?? "(null)"}");
    }

    private static async Task HandleCommandAsync(string command)
    {
        var notifications = HermesApplication.Notifications;
        try
        {
            switch (command)
            {
                case "ready":
                    Status(notifications.IsSupported
                        ? "Notifications supported on this host."
                        : $"Notifications NOT supported: {notifications.UnsupportedReason}");
                    break;

                case "permission":
                    Log($"permission granted={await notifications.RequestPermissionAsync()}");
                    break;

                case "show":
                    await ShowAsync(notifications, "Hello from Hermes", "Click me to see the tag round-trip.", silent: false, iconPath: null);
                    break;

                case "show-silent":
                    await ShowAsync(notifications, "Quiet notification", "No sound was played.", silent: true, iconPath: null);
                    break;

                case "show-icon":
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.png");
                    await ShowAsync(notifications, "With icon", "Uses icon.png from the app folder.", silent: false, iconPath: iconPath);
                    break;

                case "dismiss-last":
                    if (s_lastId is not null)
                    {
                        notifications.Dismiss(s_lastId);
                        Log($"dismissed {s_lastId}");
                    }
                    break;

                case "dismiss-all":
                    notifications.DismissAll();
                    Log("dismissed all");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task ShowAsync(NativeNotificationCenter notifications, string title, string body, bool silent, string? iconPath)
    {
        var notification = new NativeNotification
        {
            Title = title,
            Body = body,
            Tag = "page/settings",
            Silent = silent,
            IconPath = iconPath,
        };
        await notifications.ShowAsync(notification);
        s_lastId = notification.Id;
        Log($"shown id={notification.Id} tag={notification.Tag} silent={silent}");
    }

    private static void Log(string line)
    {
        if (s_closing || s_window is null)
            return;
        s_window.Invoke(() => s_window.SendMessage(line));
    }

    private static void Status(string text)
    {
        if (s_closing || s_window is null)
            return;
        s_window.Invoke(() => s_window.SendMessage("status:" + text));
    }

    private static string ReadEmbeddedHtml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html")
            ?? throw new InvalidOperationException("index.html is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
