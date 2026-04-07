// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_STATUS_ICON_H
#define HERMES_STATUS_ICON_H

#include <gtk/gtk.h>
#include <libappindicator/app-indicator.h>
#include "HermesTypes.h"

typedef struct _HermesStatusIcon HermesStatusIcon;

struct _HermesStatusIcon {
    AppIndicator* indicator;
    GtkWidget* menu;
    MenuItemCallback menu_callback;
    GHashTable* items_by_id;      // itemId -> GtkMenuItem
    GHashTable* submenus_by_id;   // submenuId -> GtkMenu
    char* temp_icon_path;
};

typedef struct {
    HermesStatusIcon* icon;
    char* item_id;
} MenuItemData;

HermesStatusIcon* hermes_status_icon_new(MenuItemCallback menu_callback);
void hermes_status_icon_destroy(HermesStatusIcon* icon);

#endif // HERMES_STATUS_ICON_H
