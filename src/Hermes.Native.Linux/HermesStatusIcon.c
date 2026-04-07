// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#include "HermesStatusIcon.h"
#include "Exports.h"
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>

// ============================================================================
// Internal Helpers
// ============================================================================

static void free_menu_item_data(gpointer data) {
    MenuItemData* mid = (MenuItemData*)data;
    g_free(mid->item_id);
    g_free(mid);
}

static void on_menu_item_activated(GtkMenuItem* menuItem, gpointer user_data) {
    MenuItemData* data = (MenuItemData*)user_data;
    if (data && data->icon && data->icon->menu_callback && data->item_id) {
        data->icon->menu_callback(data->item_id);
    }
}

// ============================================================================
// Status Icon Lifecycle
// ============================================================================

HermesStatusIcon* hermes_status_icon_new(MenuItemCallback menu_callback) {
    HermesStatusIcon* icon = calloc(1, sizeof(HermesStatusIcon));
    if (!icon) return NULL;

    icon->menu_callback = menu_callback;
    icon->menu = gtk_menu_new();
    icon->items_by_id = g_hash_table_new_full(g_str_hash, g_str_equal, g_free, NULL);
    icon->submenus_by_id = g_hash_table_new_full(g_str_hash, g_str_equal, g_free, NULL);
    icon->temp_icon_path = NULL;

    // Create AppIndicator with a default ID
    icon->indicator = app_indicator_new("hermes-app", "application-default-icon",
                                         APP_INDICATOR_CATEGORY_APPLICATION_STATUS);

    app_indicator_set_status(icon->indicator, APP_INDICATOR_STATUS_PASSIVE);
    app_indicator_set_menu(icon->indicator, GTK_MENU(icon->menu));

    return icon;
}

void hermes_status_icon_destroy(HermesStatusIcon* icon) {
    if (!icon) return;

    if (icon->indicator) {
        g_object_unref(icon->indicator);
        icon->indicator = NULL;
    }

    if (icon->items_by_id) {
        g_hash_table_destroy(icon->items_by_id);
    }

    if (icon->submenus_by_id) {
        g_hash_table_destroy(icon->submenus_by_id);
    }

    if (icon->temp_icon_path) {
        unlink(icon->temp_icon_path);
        free(icon->temp_icon_path);
        icon->temp_icon_path = NULL;
    }

    if (icon->menu) {
        gtk_widget_destroy(icon->menu);
    }

    free(icon);
}

// ============================================================================
// Status Icon Operations (Exports)
// ============================================================================

void* Hermes_StatusIcon_Create(MenuItemCallback menuCallback) {
    return hermes_status_icon_new(menuCallback);
}

void Hermes_StatusIcon_Destroy(void* statusIcon) {
    hermes_status_icon_destroy((HermesStatusIcon*)statusIcon);
}

void Hermes_StatusIcon_Show(void* statusIcon) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !icon->indicator) return;

    app_indicator_set_status(icon->indicator, APP_INDICATOR_STATUS_ACTIVE);
    gtk_widget_show_all(icon->menu);
}

void Hermes_StatusIcon_Hide(void* statusIcon) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !icon->indicator) return;

    app_indicator_set_status(icon->indicator, APP_INDICATOR_STATUS_PASSIVE);
}

void Hermes_StatusIcon_SetIconFromPath(void* statusIcon, const char* filePath) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !icon->indicator || !filePath) return;

    app_indicator_set_icon_full(icon->indicator, filePath, "application icon");
}

void Hermes_StatusIcon_SetIconFromData(void* statusIcon, const void* data, int length) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !icon->indicator || !data || length <= 0) return;

    // Clean up previous temp file
    if (icon->temp_icon_path) {
        unlink(icon->temp_icon_path);
        free(icon->temp_icon_path);
        icon->temp_icon_path = NULL;
    }

    // Write data to a temporary file
    char template[] = "/tmp/hermes-icon-XXXXXX.png";
    int fd = mkstemps(template, 4); // 4 = length of ".png"
    if (fd < 0) return;

    ssize_t written = write(fd, data, length);
    close(fd);

    if (written != length) {
        unlink(template);
        return;
    }

    icon->temp_icon_path = strdup(template);
    app_indicator_set_icon_full(icon->indicator, icon->temp_icon_path, "application icon");
}

void Hermes_StatusIcon_SetTooltip(void* statusIcon, const char* tooltip) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !icon->indicator || !tooltip) return;

    // AppIndicator doesn't have a tooltip API; use title as the closest equivalent
    app_indicator_set_title(icon->indicator, tooltip);
}

// ============================================================================
// Menu Item Operations
// ============================================================================

void Hermes_StatusIcon_AddItem(void* statusIcon, const char* itemId, const char* label) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !itemId || !label) return;

    GtkWidget* menuItem = gtk_check_menu_item_new_with_label(label);
    gtk_check_menu_item_set_draw_as_radio(GTK_CHECK_MENU_ITEM(menuItem), FALSE);

    MenuItemData* data = g_new0(MenuItemData, 1);
    data->icon = icon;
    data->item_id = g_strdup(itemId);
    g_signal_connect_data(menuItem, "activate", G_CALLBACK(on_menu_item_activated),
                          data, (GClosureNotify)free_menu_item_data, 0);

    gtk_menu_shell_append(GTK_MENU_SHELL(icon->menu), menuItem);
    gtk_widget_show(menuItem);

    g_hash_table_insert(icon->items_by_id, g_strdup(itemId), menuItem);
}

void Hermes_StatusIcon_AddSeparator(void* statusIcon) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon) return;

    GtkWidget* separator = gtk_separator_menu_item_new();
    gtk_menu_shell_append(GTK_MENU_SHELL(icon->menu), separator);
    gtk_widget_show(separator);
}

void Hermes_StatusIcon_RemoveItem(void* statusIcon, const char* itemId) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(icon->items_by_id, itemId);
    if (menuItem) {
        gtk_widget_destroy(menuItem);
        g_hash_table_remove(icon->items_by_id, itemId);
    }
}

void Hermes_StatusIcon_Clear(void* statusIcon) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon) return;

    GList* children = gtk_container_get_children(GTK_CONTAINER(icon->menu));
    for (GList* iter = children; iter; iter = iter->next) {
        gtk_widget_destroy(GTK_WIDGET(iter->data));
    }
    g_list_free(children);

    g_hash_table_remove_all(icon->items_by_id);
    g_hash_table_remove_all(icon->submenus_by_id);
}

// ============================================================================
// Menu Item State
// ============================================================================

void Hermes_StatusIcon_SetItemEnabled(void* statusIcon, const char* itemId, bool enabled) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(icon->items_by_id, itemId);
    if (menuItem) {
        gtk_widget_set_sensitive(menuItem, enabled);
    }
}

void Hermes_StatusIcon_SetItemChecked(void* statusIcon, const char* itemId, bool checked) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !itemId) return;

    GtkWidget* menuItem = g_hash_table_lookup(icon->items_by_id, itemId);
    if (menuItem && GTK_IS_CHECK_MENU_ITEM(menuItem)) {
        gtk_check_menu_item_set_active(GTK_CHECK_MENU_ITEM(menuItem), checked);
    }
}

void Hermes_StatusIcon_SetItemLabel(void* statusIcon, const char* itemId, const char* label) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !itemId || !label) return;

    GtkWidget* menuItem = g_hash_table_lookup(icon->items_by_id, itemId);
    if (menuItem) {
        gtk_menu_item_set_label(GTK_MENU_ITEM(menuItem), label);
    }
}

// ============================================================================
// Submenu Operations
// ============================================================================

void Hermes_StatusIcon_AddSubmenu(void* statusIcon, const char* submenuId, const char* label) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !submenuId || !label) return;

    GtkWidget* menuItem = gtk_menu_item_new_with_label(label);
    GtkWidget* subMenu = gtk_menu_new();
    gtk_menu_item_set_submenu(GTK_MENU_ITEM(menuItem), subMenu);

    gtk_menu_shell_append(GTK_MENU_SHELL(icon->menu), menuItem);
    gtk_widget_show(menuItem);

    g_hash_table_insert(icon->submenus_by_id, g_strdup(submenuId), subMenu);
}

void Hermes_StatusIcon_AddSubmenuItem(void* statusIcon, const char* submenuId,
                                       const char* itemId, const char* label) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !submenuId || !itemId || !label) return;

    GtkWidget* subMenu = g_hash_table_lookup(icon->submenus_by_id, submenuId);
    if (!subMenu) return;

    GtkWidget* menuItem = gtk_check_menu_item_new_with_label(label);
    gtk_check_menu_item_set_draw_as_radio(GTK_CHECK_MENU_ITEM(menuItem), FALSE);

    MenuItemData* data = g_new0(MenuItemData, 1);
    data->icon = icon;
    data->item_id = g_strdup(itemId);
    g_signal_connect_data(menuItem, "activate", G_CALLBACK(on_menu_item_activated),
                          data, (GClosureNotify)free_menu_item_data, 0);

    gtk_menu_shell_append(GTK_MENU_SHELL(subMenu), menuItem);
    gtk_widget_show(menuItem);

    g_hash_table_insert(icon->items_by_id, g_strdup(itemId), menuItem);
}

void Hermes_StatusIcon_AddSubmenuSeparator(void* statusIcon, const char* submenuId) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !submenuId) return;

    GtkWidget* subMenu = g_hash_table_lookup(icon->submenus_by_id, submenuId);
    if (!subMenu) return;

    GtkWidget* separator = gtk_separator_menu_item_new();
    gtk_menu_shell_append(GTK_MENU_SHELL(subMenu), separator);
    gtk_widget_show(separator);
}

void Hermes_StatusIcon_ClearSubmenu(void* statusIcon, const char* submenuId) {
    HermesStatusIcon* icon = (HermesStatusIcon*)statusIcon;
    if (!icon || !submenuId) return;

    GtkWidget* subMenu = g_hash_table_lookup(icon->submenus_by_id, submenuId);
    if (!subMenu) return;

    GList* children = gtk_container_get_children(GTK_CONTAINER(subMenu));
    for (GList* iter = children; iter; iter = iter->next) {
        gtk_widget_destroy(GTK_WIDGET(iter->data));
    }
    g_list_free(children);
}
