// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
// Exports.c
// This file ensures all exported symbols are available in the shared library.
// The actual implementations are in:
// - HermesWindow.c (window, webview, threading, memory management)
// - HermesMenu.c (menu bar operations)
// - HermesDialogs.c (file and message dialogs)
// - HermesContextMenu.c (context menu operations)
// - HermesStatusIcon.c (system tray / status icon operations)

#include "Exports.h"

// All exports are implemented in the respective .c files.
// This file exists to provide a single compilation unit that
// includes the exports header for validation purposes.

// Version information
const char* Hermes_GetVersion(void) {
    return "1.0.0";
}
