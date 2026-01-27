#ifndef HERMES_TYPES_H
#define HERMES_TYPES_H

#include <stdbool.h>
#include <stdint.h>

// Callback function pointer types
typedef void (*ClosingCallback)(void);
typedef void (*ResizedCallback)(int width, int height);
typedef void (*MovedCallback)(int x, int y);
typedef void (*FocusCallback)(void);
typedef void (*WebMessageCallback)(const char* message);
typedef void* (*CustomSchemeCallback)(const char* url, int* numBytes, char** contentType);
typedef void (*MenuItemCallback)(const char* itemId);
typedef void (*InvokeCallback)(void);

// Window initialization parameters
typedef struct {
    // String properties (UTF-8)
    const char* Title;
    const char* StartUrl;
    const char* StartHtml;
    const char* IconPath;

    // Position and size
    int X;
    int Y;
    int Width;
    int Height;
    int MinWidth;
    int MinHeight;
    int MaxWidth;
    int MaxHeight;

    // Boolean flags
    bool UsePosition;
    bool CenterOnScreen;
    bool Chromeless;
    bool Resizable;
    bool TopMost;
    bool Maximized;
    bool Minimized;
    bool DevToolsEnabled;
    bool ContextMenuEnabled;

    // Event callbacks
    ClosingCallback OnClosing;
    ResizedCallback OnResized;
    MovedCallback OnMoved;
    FocusCallback OnFocusIn;
    FocusCallback OnFocusOut;
    WebMessageCallback OnWebMessage;
    CustomSchemeCallback OnCustomScheme;

} HermesWindowParams;

// Dialog button configurations
typedef enum {
    DialogButtons_Ok = 0,
    DialogButtons_OkCancel = 1,
    DialogButtons_YesNo = 2,
    DialogButtons_YesNoCancel = 3
} DialogButtons;

// Dialog icon types
typedef enum {
    DialogIcon_Info = 0,
    DialogIcon_Warning = 1,
    DialogIcon_Error = 2,
    DialogIcon_Question = 3
} DialogIcon;

// Dialog result values
typedef enum {
    DialogResult_Ok = 0,
    DialogResult_Cancel = 1,
    DialogResult_Yes = 2,
    DialogResult_No = 3
} DialogResult;

#endif // HERMES_TYPES_H
