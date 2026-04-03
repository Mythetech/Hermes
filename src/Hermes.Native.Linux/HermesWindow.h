// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_WINDOW_H
#define HERMES_WINDOW_H

#include <gtk/gtk.h>
#include <webkit2/webkit2.h>
#include "HermesTypes.h"

// Forward declaration
typedef struct _HermesWindow HermesWindow;

// Internal window structure
struct _HermesWindow {
    GtkWidget* window;
    GtkWidget* webView;
    GtkWidget* container;
    WebKitUserContentManager* userContentManager;

    // Callbacks
    ClosingCallback onClosing;
    ResizedCallback onResized;
    MovedCallback onMoved;
    FocusCallback onFocusIn;
    FocusCallback onFocusOut;
    WebMessageCallback onWebMessage;
    CustomSchemeCallback onCustomScheme;
    WebViewCrashCallback onWebViewCrash;

    // State tracking
    int lastWidth;
    int lastHeight;
    int lastX;
    int lastY;
    int64_t uiThreadId;
    gboolean isShown;

    // Custom schemes (up to 16)
    char* customSchemes[16];
    int customSchemeCount;
};

// Internal functions
HermesWindow* hermes_window_new(const HermesWindowParams* params);
void hermes_window_destroy(HermesWindow* hw);

#endif // HERMES_WINDOW_H
