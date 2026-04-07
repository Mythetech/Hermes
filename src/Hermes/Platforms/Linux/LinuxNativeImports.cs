// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Hermes.Platforms.Linux;

/// <summary>
/// P/Invoke declarations for the Hermes native Linux library.
/// Uses LibraryImport with source generators for AOT compatibility.
/// </summary>
[SupportedOSPlatform("linux")]
internal static partial class LinuxNative
{
    private const string LibraryName = "libHermes.Native.Linux";

    #region Application Lifecycle

    [LibraryImport(LibraryName, EntryPoint = "Hermes_App_Init")]
    internal static partial void AppInit(ref int argc, ref IntPtr argv);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_App_Run")]
    internal static partial void AppRun();

    [LibraryImport(LibraryName, EntryPoint = "Hermes_App_Quit")]
    internal static partial void AppQuit();

    #endregion

    #region Window Lifecycle

    // Use DllImport for struct parameter (LibraryImport can't handle non-blittable structs)
    [DllImport(LibraryName, EntryPoint = "Hermes_Window_Create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr WindowCreate(ref HermesWindowParams parameters);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_Show")]
    internal static partial void WindowShow(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_Hide")]
    internal static partial void WindowHide(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_Close")]
    internal static partial void WindowClose(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_WaitForClose")]
    internal static partial void WindowWaitForClose(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_Destroy")]
    internal static partial void WindowDestroy(IntPtr window);

    #endregion

    #region Window Properties - Getters

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetTitle")]
    internal static partial void WindowGetTitle(IntPtr window, IntPtr buffer, int bufferSize);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetSize")]
    internal static partial void WindowGetSize(IntPtr window, out int width, out int height);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetPosition")]
    internal static partial void WindowGetPosition(IntPtr window, out int x, out int y);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetIsMaximized")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool WindowGetIsMaximized(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetIsMinimized")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool WindowGetIsMinimized(IntPtr window);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_GetUIThreadId")]
    internal static partial long WindowGetUIThreadId(IntPtr window);

    #endregion

    #region Window Properties - Setters

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SetTitle", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowSetTitle(IntPtr window, string title);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SetSize")]
    internal static partial void WindowSetSize(IntPtr window, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SetPosition")]
    internal static partial void WindowSetPosition(IntPtr window, int x, int y);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SetIsMaximized")]
    internal static partial void WindowSetIsMaximized(IntPtr window, [MarshalAs(UnmanagedType.U1)] bool maximized);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SetIsMinimized")]
    internal static partial void WindowSetIsMinimized(IntPtr window, [MarshalAs(UnmanagedType.U1)] bool minimized);

    #endregion

    #region WebView Operations

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_NavigateToUrl", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowNavigateToUrl(IntPtr window, string url);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_NavigateToString", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowNavigateToString(IntPtr window, string html);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_SendWebMessage", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowSendWebMessage(IntPtr window, string message);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_RegisterCustomScheme", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowRegisterCustomScheme(IntPtr window, string scheme);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_RunJavascript", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void WindowRunJavascript(IntPtr window, string script);

    #endregion

    #region Threading

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_Invoke")]
    internal static partial void WindowInvoke(IntPtr window, IntPtr callback);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Window_BeginInvoke")]
    internal static partial void WindowBeginInvoke(IntPtr window, IntPtr callback);

    #endregion

    #region Menu Operations

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_Create")]
    internal static partial IntPtr MenuCreate(IntPtr window, IntPtr callback);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_Destroy")]
    internal static partial void MenuDestroy(IntPtr menu);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_Hide")]
    internal static partial void MenuHide(IntPtr menu);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddMenu", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddMenu(IntPtr menu, string label, int insertIndex);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_RemoveMenu", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuRemoveMenu(IntPtr menu, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddItem(IntPtr menu, string menuLabel, string itemId,
                                             string itemLabel, string? accelerator);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_InsertItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuInsertItem(IntPtr menu, string menuLabel, string afterItemId,
                                                string itemId, string itemLabel, string? accelerator);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_RemoveItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuRemoveItem(IntPtr menu, string menuLabel, string itemId);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddSeparator", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddSeparator(IntPtr menu, string menuLabel);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_SetItemEnabled", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuSetItemEnabled(IntPtr menu, string menuLabel, string itemId,
                                                    [MarshalAs(UnmanagedType.U1)] bool enabled);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_SetItemChecked", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuSetItemChecked(IntPtr menu, string menuLabel, string itemId,
                                                    [MarshalAs(UnmanagedType.U1)] bool isChecked);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_SetItemLabel", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuSetItemLabel(IntPtr menu, string menuLabel, string itemId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_SetItemAccelerator", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuSetItemAccelerator(IntPtr menu, string menuLabel, string itemId, string accelerator);

    // Submenu Operations
    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddSubmenu", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddSubmenu(IntPtr menu, string menuPath, string submenuLabel);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddSubmenuItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddSubmenuItem(IntPtr menu, string menuPath, string itemId,
                                                    string itemLabel, string? accelerator);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Menu_AddSubmenuSeparator", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void MenuAddSubmenuSeparator(IntPtr menu, string menuPath);

    #endregion

    #region Context Menu Operations

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_Create")]
    internal static partial IntPtr ContextMenuCreate(IntPtr window, IntPtr callback);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_Destroy")]
    internal static partial void ContextMenuDestroy(IntPtr contextMenu);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_AddItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ContextMenuAddItem(IntPtr contextMenu, string itemId, string label, string? accelerator);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_AddSeparator")]
    internal static partial void ContextMenuAddSeparator(IntPtr contextMenu);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_RemoveItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ContextMenuRemoveItem(IntPtr contextMenu, string itemId);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_Clear")]
    internal static partial void ContextMenuClear(IntPtr contextMenu);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_SetItemEnabled", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ContextMenuSetItemEnabled(IntPtr contextMenu, string itemId,
                                                            [MarshalAs(UnmanagedType.U1)] bool enabled);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_SetItemChecked", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ContextMenuSetItemChecked(IntPtr contextMenu, string itemId,
                                                            [MarshalAs(UnmanagedType.U1)] bool isChecked);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_SetItemLabel", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void ContextMenuSetItemLabel(IntPtr contextMenu, string itemId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_Show")]
    internal static partial void ContextMenuShow(IntPtr contextMenu, int x, int y);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_ContextMenu_Hide")]
    internal static partial void ContextMenuHide(IntPtr contextMenu);

    #endregion

    #region Status Icon Operations

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_Create")]
    internal static partial IntPtr StatusIconCreate(IntPtr menuCallback);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_Destroy")]
    internal static partial void StatusIconDestroy(IntPtr statusIcon);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_Show")]
    internal static partial void StatusIconShow(IntPtr statusIcon);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_Hide")]
    internal static partial void StatusIconHide(IntPtr statusIcon);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetIconFromPath", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconSetIconFromPath(IntPtr statusIcon, string filePath);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetIconFromData")]
    internal static partial void StatusIconSetIconFromData(IntPtr statusIcon, IntPtr data, int length);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetTooltip", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconSetTooltip(IntPtr statusIcon, string tooltip);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_AddItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconAddItem(IntPtr statusIcon, string itemId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_AddSeparator")]
    internal static partial void StatusIconAddSeparator(IntPtr statusIcon);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_RemoveItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconRemoveItem(IntPtr statusIcon, string itemId);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_Clear")]
    internal static partial void StatusIconClear(IntPtr statusIcon);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetItemEnabled", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconSetItemEnabled(IntPtr statusIcon, string itemId,
                                                          [MarshalAs(UnmanagedType.U1)] bool enabled);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetItemChecked", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconSetItemChecked(IntPtr statusIcon, string itemId,
                                                          [MarshalAs(UnmanagedType.U1)] bool isChecked);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_SetItemLabel", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconSetItemLabel(IntPtr statusIcon, string itemId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_AddSubmenu", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconAddSubmenu(IntPtr statusIcon, string submenuId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_AddSubmenuItem", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconAddSubmenuItem(IntPtr statusIcon, string submenuId,
                                                          string itemId, string label);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_AddSubmenuSeparator", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconAddSubmenuSeparator(IntPtr statusIcon, string submenuId);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_StatusIcon_ClearSubmenu", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void StatusIconClearSubmenu(IntPtr statusIcon, string submenuId);

    #endregion

    #region Dialog Operations

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Dialog_ShowOpenFile", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr DialogShowOpenFile(IntPtr parentWindow, string title, string? defaultPath,
                                                      [MarshalAs(UnmanagedType.U1)] bool multiSelect,
                                                      IntPtr filters, int filterCount, out int resultCount);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Dialog_ShowOpenFolder", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr DialogShowOpenFolder(IntPtr parentWindow, string title, string? defaultPath,
                                                        [MarshalAs(UnmanagedType.U1)] bool multiSelect,
                                                        out int resultCount);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Dialog_ShowSaveFile", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr DialogShowSaveFile(IntPtr parentWindow, string title, string? defaultPath,
                                                      IntPtr filters, int filterCount, string? defaultFileName);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Dialog_ShowMessage", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int DialogShowMessage(IntPtr parentWindow, string title, string message, int buttons, int icon);

    #endregion

    #region Memory Management

    [LibraryImport(LibraryName, EntryPoint = "Hermes_Free")]
    internal static partial void Free(IntPtr ptr);

    [LibraryImport(LibraryName, EntryPoint = "Hermes_FreeStringArray")]
    internal static partial void FreeStringArray(IntPtr array, int count);

    #endregion
}
