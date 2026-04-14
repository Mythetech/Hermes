# Platform Differences

This document describes behavioral differences across macOS, Windows, and Linux when using the Hermes framework. Understanding these differences helps you write cross-platform code that behaves predictably.

---

## App Menu

The "app menu" is the menu associated with your application's name.

| Platform | Behavior |
|----------|----------|
| **macOS** | Native system menu (first menu in menu bar). Automatically includes system items like About, Services, Hide, and Quit. Items you add appear alongside these. |
| **Windows** | Regular top-level menu styled as an app menu. Created with the application name as the label. No system integration. |
| **Linux** | Same as Windows (GTK menu bar item). |

### macOS-Specific Notes

On macOS, the app menu is owned by the system and includes:
- **About [AppName]** - Shows app info (system-provided)
- **Preferences...** - Standard Cmd+, shortcut
- **Services** - macOS Services submenu
- **Hide [AppName]** - Cmd+H
- **Hide Others** - Cmd+Option+H
- **Quit [AppName]** - Cmd+Q

When you call `AddAppMenuItem()`, your items are inserted relative to these system items. Use the `position` parameter to control placement:
- `"top"` - After the About item
- `"after-about"` - Same as top
- `"before-quit"` - Before the Quit item (most common)

```csharp
// Add a Preferences item before Quit
menuBackend.AddAppMenuItem("preferences", "Preferences...", "Cmd+,", "before-quit");
```

### Windows/Linux Notes

On Windows and Linux, the "app menu" is just a regular menu with the app name. You have full control over its contents. System items like Quit must be added manually.

---

## Keyboard Shortcuts (Accelerators)

| Modifier | macOS | Windows | Linux |
|----------|-------|---------|-------|
| Primary | Cmd (Command) | Ctrl | Ctrl |
| Secondary | Ctrl | Alt | Alt |
| Option | Option | - | - |

### Cross-Platform Accelerator Strings

Use platform-agnostic strings in your code. The framework translates them appropriately:

| You Write | macOS Shows | Windows/Linux Shows |
|-----------|-------------|---------------------|
| `"Cmd+S"` | Cmd+S | Ctrl+S |
| `"Ctrl+S"` | Ctrl+S | Ctrl+S |
| `"Alt+F4"` | Option+F4 | Alt+F4 |
| `"Shift+Cmd+N"` | Shift+Cmd+N | Ctrl+Shift+N |

**Best Practice:** Use `"Cmd+..."` for primary shortcuts. They'll appear as Cmd on macOS and Ctrl on Windows/Linux.

### Accelerator Enforcement

| Platform | Behavior |
|----------|----------|
| **macOS** | Accelerators are enforced by the OS. Pressing the shortcut triggers the menu item automatically. |
| **Windows** | Accelerators require proper accelerator table setup. Without it, shortcuts are display-only. |
| **Linux** | Accelerators are handled via GTK AccelGroup. Should work automatically when properly configured. |

---

## CheckMenuItem (Toggleable Items)

| Platform | Dynamic Conversion |
|----------|-------------------|
| **macOS** | Full support. Any item can be toggled. |
| **Windows** | Full support. Uses MF_CHECKED flag. |
| **Linux** | Limited. GTK requires items to be created as CheckMenuItem. Calling `SetItemChecked` on a regular MenuItem may not work. |

**Workaround for Linux:** If you need a checkable item, consider creating it as a CheckMenuItem from the start, or removing and re-adding it as a CheckMenuItem when checked state is first needed.

---

## WebView Message Serialization

Messages sent between C# and JavaScript use JSON serialization on all platforms for consistency:

| Platform | Serialization Method |
|----------|---------------------|
| **macOS** | NSJSONSerialization |
| **Windows** | System.Text.Json with PostWebMessageAsJson |
| **Linux** | System.Text.Json in evaluated JavaScript |

**JavaScript Side:** All platforms receive messages as the deserialized value, not a raw string. Special characters are properly escaped.

```javascript
window.external.receiveMessage(function(message) {
    // message is already the deserialized value
    console.log(message); // Works correctly with special chars: "Hello \"World\""
});
```

---

## File Dialogs

| Feature | macOS | Windows | Linux |
|---------|-------|---------|-------|
| Open File | NSOpenPanel | IFileOpenDialog (COM) | GtkFileChooserDialog |
| Save File | NSSavePanel | IFileSaveDialog (COM) | GtkFileChooserDialog |
| Open Folder | NSOpenPanel with folder flag | IFileOpenDialog with folder flag | GtkFileChooserDialog |
| Multiple Selection | Supported | Supported | Supported |
| File Filters | UTType (modern) | Extension patterns | Extension patterns |

### File Filter Format

```csharp
// Cross-platform filter format
var filters = new[] {
    ("Text Files", "*.txt"),
    ("All Files", "*.*")
};
```

---

## Window Behavior

| Feature | macOS | Windows | Linux |
|---------|-------|---------|-------|
| Coordinate Origin | Bottom-left | Top-left | Top-left |
| Maximize | Zoom (may not fill screen) | True maximize | True maximize |
| Full Screen | Native full screen mode | Borderless maximized | Borderless maximized |
| TopMost | NSFloatingWindowLevel | WS_EX_TOPMOST | KeepAbove |

### Coordinate System

macOS uses a bottom-left origin for window coordinates, while Windows and Linux use top-left. The framework automatically converts coordinates so that:
- `Position.Y = 0` means top of screen on all platforms
- Positive Y moves down on all platforms

You don't need to handle coordinate conversion manually.

---

## Threading

All platforms require UI operations on the main/UI thread:

| Platform | Main Thread Detection | Invoke Method |
|----------|----------------------|---------------|
| **macOS** | pthread_mach_thread_np | dispatch_sync/async |
| **Windows** | Environment.CurrentManagedThreadId | PostMessage + WM_USER |
| **Linux** | Environment.CurrentManagedThreadId | GLib.Idle.Add |

Use `CheckAccess()` to verify you're on the UI thread, and `Invoke()` or `BeginInvoke()` to marshal calls:

```csharp
if (!window.CheckAccess())
{
    window.Invoke(() => UpdateUI());
}
else
{
    UpdateUI();
}
```

---

## Key-Value Store File Locations

The key-value store persists data as JSON files in the platform's standard user data directory. Each named store is a separate file (`{name}.json`).

| Platform | Directory |
|----------|-----------|
| **macOS** | `~/Library/Application Support/Hermes/KvStore/` |
| **Windows** | `%LOCALAPPDATA%\Hermes\KvStore\` |
| **Linux** | `$XDG_DATA_HOME/Hermes/KvStore/` (defaults to `~/.local/share/Hermes/KvStore/`) |

These paths are resolved by `AppDataDirectories.GetUserDataPath("KvStore")`. The default store is written to `default.json`.

---

## Clipboard

| Feature | macOS | Windows | Linux |
|---------|-------|---------|-------|
| Text get/set | `pbcopy` / `pbpaste` | Win32 Clipboard API | `xclip` |
| External dependency | None (ships with macOS) | None (built-in) | Requires `xclip` (`sudo apt install xclip`) |

### Notes

- On Linux, `xclip` must be installed. If it is not available, clipboard operations throw `PlatformNotSupportedException` with an install hint.
- macOS and Linux implementations shell out to external processes, which is slightly slower than the direct Win32 API used on Windows. For text operations this is negligible.

---

## Known Limitations

### macOS
- DevTools requires macOS 13.3+ for `inspectable` property
- Some WKPreferences are private API and may fail silently on older versions

### Windows
- WebView2 must be installed on the target machine
- First window creation has cold-start latency (use Prewarm to mitigate)

### Linux
- Requires GTK 3.x and WebKit2GTK 4.x runtime libraries
- Some desktop environments may not support all GTK features
- AppIndicator support varies by desktop environment

---

## Testing Cross-Platform Code

When testing your application across platforms:

1. **Test accelerators** - Verify Cmd maps to Ctrl correctly
2. **Test checkbox items** - Especially on Linux
3. **Test file dialogs** - Filter syntax may vary
4. **Test window positioning** - Coordinate conversion should be transparent
5. **Test WebView messages** - Especially with special characters
