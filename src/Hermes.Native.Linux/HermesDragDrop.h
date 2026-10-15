// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HermesDragDrop_h
#define HermesDragDrop_h

#include <gtk/gtk.h>
#include <webkit2/webkit2.h>
#include "HermesTypes.h"

typedef struct _HermesDragDrop HermesDragDrop;

HermesDragDrop* HermesDragDrop_Attach(GtkWidget* webView, WebKitUserContentManager* contentManager, const char* script,
                                      const char** dropTargets, int dropTargetCount,
                                      DropClaimedCallback claimed, DragEndedCallback ended);
void HermesDragDrop_Claim(HermesDragDrop* dragDrop, int64_t claimId);
bool HermesDragDrop_BeginDrag(HermesDragDrop* dragDrop, const char** targets, const uint8_t** data, const int* lengths, int count);
void HermesDragDrop_Detach(HermesDragDrop* dragDrop);

#endif
