// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#include "Exports.h"
#include "HermesWindow.h"
#include <gtk/gtk.h>
#include <string.h>
#include <stdio.h>

typedef struct {
    GtkWidget* window;
    GtkWidget* menu;
    MenuItemCallback callback;
    GHashTable* menuItems; // itemId -> GtkMenuItem
} HermesContextMenu;

typedef struct {
    HermesContextMenu* contextMenu;
    char* itemId;
} ContextMenuItemData;

static void context_menu_item_data_free(gpointer data) {
    ContextMenuItemData* cmid = (ContextMenuItemData*)data;
    g_free(cmid->itemId);
    g_free(cmid);
}

static void on_context_menu_item_activate(GtkMenuItem* menuItem, gpointer user_data) {
    ContextMenuItemData* data = (ContextMenuItemData*)user_data;
    if (data && data->contextMenu && data->contextMenu->callback && data->itemId) {
        data->contextMenu->callback(data->itemId);
    }
}

// ============================================================================
// Context Menu Operations
// ============================================================================

void* Hermes_ContextMenu_Create(void* window, MenuItemCallback callback) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return NULL;

    HermesContextMenu* cm = g_new0(HermesContextMenu, 1);
    cm->window = hw->window;
    cm->callback = callback;
    cm->menu = gtk_menu_new();
    cm->menuItems = g_hash_table_new_full(g_str_hash, g_str_equal, g_free, NULL);
    return cm;
}

void Hermes_ContextMenu_Destroy(void* contextMenu) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm) return;

    g_hash_table_destroy(cm->menuItems);
    if (cm->menu) {
        gtk_widget_destroy(cm->menu);
    }
    g_free(cm);
}

void Hermes_ContextMenu_AddItem(void* contextMenu, const char* itemId, const char* label, const char* accelerator) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !itemId || !label) return;

    GtkWidget* menuItem = gtk_menu_item_new_with_label(label);

    // Create data for callback
    ContextMenuItemData* data = g_new0(ContextMenuItemData, 1);
    data->contextMenu = cm;
    data->itemId = g_strdup(itemId);
    g_signal_connect_data(menuItem, "activate", G_CALLBACK(on_context_menu_item_activate),
                          data, (GClosureNotify)context_menu_item_data_free, 0);

    gtk_menu_shell_append(GTK_MENU_SHELL(cm->menu), menuItem);
    gtk_widget_show(menuItem);

    g_hash_table_insert(cm->menuItems, g_strdup(itemId), menuItem);
}

void Hermes_ContextMenu_AddSeparator(void* contextMenu) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm) return;

    GtkWidget* separator = gtk_separator_menu_item_new();
    gtk_menu_shell_append(GTK_MENU_SHELL(cm->menu), separator);
    gtk_widget_show(separator);
}

void Hermes_ContextMenu_RemoveItem(void* contextMenu, const char* itemId) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(cm->menuItems, itemId);
    if (menuItem) {
        gtk_widget_destroy(menuItem);
        g_hash_table_remove(cm->menuItems, itemId);
    }
}

void Hermes_ContextMenu_Clear(void* contextMenu) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm) return;

    // Remove all children
    GList* children = gtk_container_get_children(GTK_CONTAINER(cm->menu));
    for (GList* iter = children; iter; iter = iter->next) {
        gtk_widget_destroy(GTK_WIDGET(iter->data));
    }
    g_list_free(children);

    g_hash_table_remove_all(cm->menuItems);
}

void Hermes_ContextMenu_SetItemEnabled(void* contextMenu, const char* itemId, bool enabled) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(cm->menuItems, itemId);
    if (menuItem) {
        gtk_widget_set_sensitive(menuItem, enabled);
    }
}

void Hermes_ContextMenu_SetItemChecked(void* contextMenu, const char* itemId, bool checked) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(cm->menuItems, itemId);
    if (menuItem && GTK_IS_CHECK_MENU_ITEM(menuItem)) {
        gtk_check_menu_item_set_active(GTK_CHECK_MENU_ITEM(menuItem), checked);
    }
}

void Hermes_ContextMenu_SetItemLabel(void* contextMenu, const char* itemId, const char* label) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !itemId || !label) return;

    GtkWidget* menuItem = g_hash_table_lookup(cm->menuItems, itemId);
    if (menuItem) {
        gtk_menu_item_set_label(GTK_MENU_ITEM(menuItem), label);
    }
}

void Hermes_ContextMenu_Show(void* contextMenu, int x, int y) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !cm->menu) return;

    gtk_menu_popup_at_pointer(GTK_MENU(cm->menu), NULL);
}

void Hermes_ContextMenu_Hide(void* contextMenu) {
    HermesContextMenu* cm = (HermesContextMenu*)contextMenu;
    if (!cm || !cm->menu) return;

    gtk_menu_popdown(GTK_MENU(cm->menu));
}
