#ifndef HERMES_EXPORTS_H
#define HERMES_EXPORTS_H

#include "HermesTypes.h"

#ifdef __cplusplus
extern "C" {
#endif

// ============================================================================
// Application Lifecycle
// ============================================================================

/// Register the application with NSApplication (call once at startup)
void Hermes_App_Register(void);

// ============================================================================
// Window Lifecycle
// ============================================================================

/// Create a new window with the specified parameters
void* Hermes_Window_Create(const HermesWindowParams* params);

/// Show the window
void Hermes_Window_Show(void* window);

/// Close the window
void Hermes_Window_Close(void* window);

/// Show the window and run the main loop until closed
void Hermes_Window_WaitForClose(void* window);

/// Destroy the window and release all resources
void Hermes_Window_Destroy(void* window);

// ============================================================================
// Window Properties - Getters
// ============================================================================

/// Get the window title (copies to buffer)
void Hermes_Window_GetTitle(void* window, char* buffer, int bufferSize);

/// Get the window size
void Hermes_Window_GetSize(void* window, int* width, int* height);

/// Get the window position (in screen coordinates, top-left origin)
void Hermes_Window_GetPosition(void* window, int* x, int* y);

/// Get whether the window is maximized (zoomed)
bool Hermes_Window_GetIsMaximized(void* window);

/// Get whether the window is minimized
bool Hermes_Window_GetIsMinimized(void* window);

/// Get the UI thread ID
int64_t Hermes_Window_GetUIThreadId(void* window);

// ============================================================================
// Window Properties - Setters
// ============================================================================

/// Set the window title
void Hermes_Window_SetTitle(void* window, const char* title);

/// Set the window size
void Hermes_Window_SetSize(void* window, int width, int height);

/// Set the window position (in screen coordinates, top-left origin)
void Hermes_Window_SetPosition(void* window, int x, int y);

/// Set whether the window is maximized (zoomed)
void Hermes_Window_SetIsMaximized(void* window, bool maximized);

/// Set whether the window is minimized
void Hermes_Window_SetIsMinimized(void* window, bool minimized);

// ============================================================================
// WebView Operations
// ============================================================================

/// Navigate the WebView to a URL
void Hermes_Window_NavigateToUrl(void* window, const char* url);

/// Load HTML content directly into the WebView
void Hermes_Window_NavigateToString(void* window, const char* html);

/// Send a message to JavaScript in the WebView
void Hermes_Window_SendWebMessage(void* window, const char* message);

/// Register a custom URL scheme (must be called before window is shown)
void Hermes_Window_RegisterCustomScheme(void* window, const char* scheme);

// ============================================================================
// Threading
// ============================================================================

/// Execute a callback on the UI thread synchronously (blocks until complete)
void Hermes_Window_Invoke(void* window, InvokeCallback callback);

/// Execute a callback on the UI thread asynchronously (returns immediately)
void Hermes_Window_BeginInvoke(void* window, InvokeCallback callback);

// ============================================================================
// Menu Operations
// ============================================================================

/// Create a menu backend for the window
void* Hermes_Menu_Create(void* window, MenuItemCallback callback);

/// Destroy the menu backend
void Hermes_Menu_Destroy(void* menu);

/// Add a new top-level menu (insertIndex -1 appends)
void Hermes_Menu_AddMenu(void* menu, const char* label, int insertIndex);

/// Remove a top-level menu by label
void Hermes_Menu_RemoveMenu(void* menu, const char* label);

/// Add an item to a menu
void Hermes_Menu_AddItem(void* menu, const char* menuLabel, const char* itemId,
                         const char* itemLabel, const char* accelerator);

/// Insert an item after another item
void Hermes_Menu_InsertItem(void* menu, const char* menuLabel, const char* afterItemId,
                            const char* itemId, const char* itemLabel, const char* accelerator);

/// Remove an item from a menu
void Hermes_Menu_RemoveItem(void* menu, const char* menuLabel, const char* itemId);

/// Add a separator to a menu
void Hermes_Menu_AddSeparator(void* menu, const char* menuLabel);

/// Set whether a menu item is enabled
void Hermes_Menu_SetItemEnabled(void* menu, const char* menuLabel, const char* itemId, bool enabled);

/// Set whether a menu item is checked
void Hermes_Menu_SetItemChecked(void* menu, const char* menuLabel, const char* itemId, bool checked);

/// Set the label of a menu item
void Hermes_Menu_SetItemLabel(void* menu, const char* menuLabel, const char* itemId, const char* label);

/// Set the accelerator of a menu item
void Hermes_Menu_SetItemAccelerator(void* menu, const char* menuLabel, const char* itemId, const char* accelerator);

// ============================================================================
// Context Menu Operations
// ============================================================================

/// Create a context menu for the window
void* Hermes_ContextMenu_Create(void* window, MenuItemCallback callback);

/// Destroy the context menu
void Hermes_ContextMenu_Destroy(void* contextMenu);

/// Add an item to the context menu
void Hermes_ContextMenu_AddItem(void* contextMenu, const char* itemId, const char* label, const char* accelerator);

/// Add a separator to the context menu
void Hermes_ContextMenu_AddSeparator(void* contextMenu);

/// Remove an item from the context menu
void Hermes_ContextMenu_RemoveItem(void* contextMenu, const char* itemId);

/// Clear all items from the context menu
void Hermes_ContextMenu_Clear(void* contextMenu);

/// Set whether a context menu item is enabled
void Hermes_ContextMenu_SetItemEnabled(void* contextMenu, const char* itemId, bool enabled);

/// Set whether a context menu item is checked
void Hermes_ContextMenu_SetItemChecked(void* contextMenu, const char* itemId, bool checked);

/// Set the label of a context menu item
void Hermes_ContextMenu_SetItemLabel(void* contextMenu, const char* itemId, const char* label);

/// Show the context menu at the specified screen coordinates
void Hermes_ContextMenu_Show(void* contextMenu, int x, int y);

/// Hide the context menu
void Hermes_ContextMenu_Hide(void* contextMenu);

// ============================================================================
// Dialog Operations
// ============================================================================

/// Show a file open dialog. Returns array of paths (caller must free with Hermes_FreeStringArray)
char** Hermes_Dialog_ShowOpenFile(const char* title, const char* defaultPath,
                                   bool multiSelect, const char** filters,
                                   int filterCount, int* resultCount);

/// Show a folder open dialog. Returns array of paths (caller must free with Hermes_FreeStringArray)
char** Hermes_Dialog_ShowOpenFolder(const char* title, const char* defaultPath,
                                     bool multiSelect, int* resultCount);

/// Show a file save dialog. Returns path (caller must free with Hermes_Free)
char* Hermes_Dialog_ShowSaveFile(const char* title, const char* defaultPath,
                                  const char** filters, int filterCount,
                                  const char* defaultFileName);

/// Show a message dialog. Returns DialogResult value.
int Hermes_Dialog_ShowMessage(const char* title, const char* message,
                               int buttons, int icon);

// ============================================================================
// Memory Management
// ============================================================================

/// Free a single pointer allocated by Hermes
void Hermes_Free(void* ptr);

/// Free an array of strings allocated by Hermes
void Hermes_FreeStringArray(char** array, int count);

#ifdef __cplusplus
}
#endif

#endif // HERMES_EXPORTS_H
