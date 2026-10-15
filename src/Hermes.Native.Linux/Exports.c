// Copyright (c) Mythetech. Licensed under the MIT License.
// Exports.c
// This file ensures all exported symbols are available in the shared library.
// The actual implementations are in:
// - HermesWindow.c (window, webview, threading, memory management)
// - HermesMenu.c (menu bar operations)
// - HermesDialogs.c (file and message dialogs)
// - HermesContextMenu.c (context menu operations)
// - HermesStatusIcon.c (system tray / status icon operations, requires libappindicator3)
// - HermesClipboard.c (clipboard operations, forwarded below)
// - HermesDragDrop.c (drag and drop, forwarded below)

#include "Exports.h"
#include "HermesClipboard.h"
#include "HermesDragDrop.h"
#include "HermesWindow.h"

// All exports are implemented in the respective .c files.
// This file exists to provide a single compilation unit that
// includes the exports header for validation purposes.

// Version information
const char* Hermes_GetVersion(void) {
    return "1.0.0";
}

bool Hermes_Clipboard_IsReady(void) { return hermes_gtk_ready(); }
char** Hermes_Clipboard_GetTargets(int* count) { return HermesClipboard_GetTargets(count); }
char* Hermes_Clipboard_ReadText(void) { return HermesClipboard_ReadText(); }
uint8_t* Hermes_Clipboard_ReadContents(const char* target, int* length) { return HermesClipboard_ReadContents(target, length); }
uint8_t* Hermes_Clipboard_ReadImageAsPng(int* length) { return HermesClipboard_ReadImageAsPng(length); }
bool Hermes_Clipboard_Write(const char** targets, const uint8_t** data, const int* lengths, int count) {
    return HermesClipboard_Write(targets, data, lengths, count);
}
void Hermes_Clipboard_Clear(void) { HermesClipboard_Clear(); }
void Hermes_Clipboard_StartMonitoring(void (*callback)(void)) { HermesClipboard_StartMonitoring(callback); }
void Hermes_Clipboard_StopMonitoring(void) { HermesClipboard_StopMonitoring(); }

void* Hermes_DragDrop_Attach(void* window, const char* script, const char** dropTargets, int dropTargetCount,
                             DropClaimedCallback claimed, DragEndedCallback ended) {
    HermesWindow* hw = (HermesWindow*)window;
    if (hw == NULL || hw->webView == NULL) {
        return NULL;
    }
    return HermesDragDrop_Attach(hw->webView, hw->userContentManager, script, dropTargets, dropTargetCount, claimed, ended);
}
void Hermes_DragDrop_Claim(void* dragDrop, int64_t claimId) { HermesDragDrop_Claim(dragDrop, claimId); }
bool Hermes_DragDrop_BeginDrag(void* dragDrop, const char** targets, const uint8_t** data, const int* lengths, int count) {
    return HermesDragDrop_BeginDrag(dragDrop, targets, data, lengths, count);
}
void Hermes_DragDrop_Detach(void* dragDrop) { HermesDragDrop_Detach(dragDrop); }
