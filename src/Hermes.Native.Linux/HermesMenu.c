// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#include "HermesMenu.h"
#include "HermesWindow.h"
#include "Exports.h"
#include <string.h>
#include <stdio.h>

// ============================================================================
// Internal Helpers
// ============================================================================

typedef struct {
    HermesMenu* menu;
    char* itemId;
} MenuItemData;

static void menu_item_data_free(gpointer data) {
    MenuItemData* mid = (MenuItemData*)data;
    g_free(mid->itemId);
    g_free(mid);
}

static void on_menu_item_activate(GtkMenuItem* menuItem, gpointer user_data) {
    MenuItemData* data = (MenuItemData*)user_data;
    if (data && data->menu && data->menu->callback && data->itemId) {
        data->menu->callback(data->itemId);
    }
}

static guint parse_accelerator(const char* accelerator, GdkModifierType* mods) {
    if (!accelerator || !accelerator[0]) {
        *mods = 0;
        return 0;
    }

    *mods = 0;
    guint key = 0;

    // Parse modifiers
    const char* p = accelerator;
    while (*p) {
        if (g_str_has_prefix(p, "Ctrl+") || g_str_has_prefix(p, "ctrl+")) {
            *mods |= GDK_CONTROL_MASK;
            p += 5;
        } else if (g_str_has_prefix(p, "Alt+") || g_str_has_prefix(p, "alt+")) {
            *mods |= GDK_MOD1_MASK;
            p += 4;
        } else if (g_str_has_prefix(p, "Shift+") || g_str_has_prefix(p, "shift+")) {
            *mods |= GDK_SHIFT_MASK;
            p += 6;
        } else {
            break;
        }
    }

    // Parse key
    if (strlen(p) == 1) {
        key = gdk_unicode_to_keyval(g_utf8_get_char(p));
    } else if (g_ascii_strcasecmp(p, "Enter") == 0 || g_ascii_strcasecmp(p, "Return") == 0) {
        key = GDK_KEY_Return;
    } else if (g_ascii_strcasecmp(p, "Escape") == 0 || g_ascii_strcasecmp(p, "Esc") == 0) {
        key = GDK_KEY_Escape;
    } else if (g_ascii_strcasecmp(p, "Tab") == 0) {
        key = GDK_KEY_Tab;
    } else if (g_ascii_strcasecmp(p, "Space") == 0) {
        key = GDK_KEY_space;
    } else if (g_ascii_strcasecmp(p, "Delete") == 0 || g_ascii_strcasecmp(p, "Del") == 0) {
        key = GDK_KEY_Delete;
    } else if (g_ascii_strcasecmp(p, "Backspace") == 0) {
        key = GDK_KEY_BackSpace;
    } else if (p[0] == 'F' || p[0] == 'f') {
        int fnum = atoi(p + 1);
        if (fnum >= 1 && fnum <= 12) {
            key = GDK_KEY_F1 + fnum - 1;
        }
    }

    return key;
}

static GtkWidget* find_menu_by_label(HermesMenu* hm, const char* label) {
    return g_hash_table_lookup(hm->topLevelMenus, label);
}

static GtkWidget* find_menu_item_by_id(HermesMenu* hm, const char* itemId) {
    return g_hash_table_lookup(hm->menuItems, itemId);
}

// ============================================================================
// Menu Creation
// ============================================================================

HermesMenu* hermes_menu_new(GtkWidget* window, GtkWidget* container, MenuItemCallback callback) {
    HermesMenu* hm = g_new0(HermesMenu, 1);
    hm->window = window;
    hm->container = container;
    hm->callback = callback;
    hm->menuItems = g_hash_table_new_full(g_str_hash, g_str_equal, g_free, NULL);
    hm->topLevelMenus = g_hash_table_new_full(g_str_hash, g_str_equal, g_free, NULL);

    // Create menu bar
    hm->menuBar = gtk_menu_bar_new();
    gtk_box_pack_start(GTK_BOX(container), hm->menuBar, FALSE, FALSE, 0);
    gtk_box_reorder_child(GTK_BOX(container), hm->menuBar, 0);
    gtk_widget_show(hm->menuBar);

    return hm;
}

void hermes_menu_destroy(HermesMenu* hm) {
    if (!hm) return;

    g_hash_table_destroy(hm->menuItems);
    g_hash_table_destroy(hm->topLevelMenus);

    if (hm->menuBar) {
        gtk_widget_destroy(hm->menuBar);
    }

    g_free(hm);
}

// ============================================================================
// Menu Operations (Exports)
// ============================================================================

void* Hermes_Menu_Create(void* window, MenuItemCallback callback) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return NULL;

    return hermes_menu_new(hw->window, hw->container, callback);
}

void Hermes_Menu_Destroy(void* menu) {
    hermes_menu_destroy((HermesMenu*)menu);
}

void Hermes_Menu_Hide(void* menu) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !hm->menuBar) return;
    gtk_widget_hide(hm->menuBar);
    gtk_widget_set_no_show_all(hm->menuBar, TRUE);
}

void Hermes_Menu_AddMenu(void* menu, const char* label, int insertIndex) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !label) return;

    // Check if menu already exists
    if (find_menu_by_label(hm, label)) return;

    // Create menu item for menu bar
    GtkWidget* menuItem = gtk_menu_item_new_with_label(label);
    GtkWidget* subMenu = gtk_menu_new();
    gtk_menu_item_set_submenu(GTK_MENU_ITEM(menuItem), subMenu);

    // Add to menu bar
    if (insertIndex < 0) {
        gtk_menu_shell_append(GTK_MENU_SHELL(hm->menuBar), menuItem);
    } else {
        gtk_menu_shell_insert(GTK_MENU_SHELL(hm->menuBar), menuItem, insertIndex);
    }

    gtk_widget_show(menuItem);

    // Store reference
    g_hash_table_insert(hm->topLevelMenus, g_strdup(label), subMenu);
}

void Hermes_Menu_RemoveMenu(void* menu, const char* label) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !label) return;

    GtkWidget* subMenu = find_menu_by_label(hm, label);
    if (!subMenu) return;

    GtkWidget* menuItem = gtk_widget_get_parent(subMenu);
    if (menuItem) {
        gtk_widget_destroy(menuItem);
    }

    g_hash_table_remove(hm->topLevelMenus, label);
}

void Hermes_Menu_AddItem(void* menu, const char* menuLabel, const char* itemId,
                         const char* itemLabel, const char* accelerator) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !menuLabel || !itemId || !itemLabel) return;

    GtkWidget* subMenu = find_menu_by_label(hm, menuLabel);
    if (!subMenu) return;

    // Create menu item
    GtkWidget* menuItem = gtk_menu_item_new_with_label(itemLabel);

    // Set accelerator
    if (accelerator && accelerator[0]) {
        GdkModifierType mods;
        guint key = parse_accelerator(accelerator, &mods);
        if (key) {
            GtkAccelGroup* accelGroup = gtk_accel_group_new();
            gtk_window_add_accel_group(GTK_WINDOW(hm->window), accelGroup);
            gtk_widget_add_accelerator(menuItem, "activate", accelGroup,
                                        key, mods, GTK_ACCEL_VISIBLE);
        }
    }

    // Create data for callback
    MenuItemData* data = g_new0(MenuItemData, 1);
    data->menu = hm;
    data->itemId = g_strdup(itemId);
    g_signal_connect_data(menuItem, "activate", G_CALLBACK(on_menu_item_activate),
                          data, (GClosureNotify)menu_item_data_free, 0);

    gtk_menu_shell_append(GTK_MENU_SHELL(subMenu), menuItem);
    gtk_widget_show(menuItem);

    // Store reference
    g_hash_table_insert(hm->menuItems, g_strdup(itemId), menuItem);
}

void Hermes_Menu_InsertItem(void* menu, const char* menuLabel, const char* afterItemId,
                            const char* itemId, const char* itemLabel, const char* accelerator) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !menuLabel || !itemId || !itemLabel) return;

    GtkWidget* subMenu = find_menu_by_label(hm, menuLabel);
    if (!subMenu) return;

    // Find position of afterItemId
    int position = -1;
    if (afterItemId) {
        GtkWidget* afterItem = find_menu_item_by_id(hm, afterItemId);
        if (afterItem) {
            GList* children = gtk_container_get_children(GTK_CONTAINER(subMenu));
            position = g_list_index(children, afterItem) + 1;
            g_list_free(children);
        }
    }

    // Create menu item
    GtkWidget* menuItem = gtk_menu_item_new_with_label(itemLabel);

    // Set accelerator
    if (accelerator && accelerator[0]) {
        GdkModifierType mods;
        guint key = parse_accelerator(accelerator, &mods);
        if (key) {
            GtkAccelGroup* accelGroup = gtk_accel_group_new();
            gtk_window_add_accel_group(GTK_WINDOW(hm->window), accelGroup);
            gtk_widget_add_accelerator(menuItem, "activate", accelGroup,
                                        key, mods, GTK_ACCEL_VISIBLE);
        }
    }

    // Create data for callback
    MenuItemData* data = g_new0(MenuItemData, 1);
    data->menu = hm;
    data->itemId = g_strdup(itemId);
    g_signal_connect_data(menuItem, "activate", G_CALLBACK(on_menu_item_activate),
                          data, (GClosureNotify)menu_item_data_free, 0);

    if (position >= 0) {
        gtk_menu_shell_insert(GTK_MENU_SHELL(subMenu), menuItem, position);
    } else {
        gtk_menu_shell_append(GTK_MENU_SHELL(subMenu), menuItem);
    }

    gtk_widget_show(menuItem);
    g_hash_table_insert(hm->menuItems, g_strdup(itemId), menuItem);
}

void Hermes_Menu_RemoveItem(void* menu, const char* menuLabel, const char* itemId) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !itemId) return;

    GtkWidget* menuItem = find_menu_item_by_id(hm, itemId);
    if (menuItem) {
        gtk_widget_destroy(menuItem);
        g_hash_table_remove(hm->menuItems, itemId);
    }
}

void Hermes_Menu_AddSeparator(void* menu, const char* menuLabel) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !menuLabel) return;

    GtkWidget* subMenu = find_menu_by_label(hm, menuLabel);
    if (!subMenu) return;

    GtkWidget* separator = gtk_separator_menu_item_new();
    gtk_menu_shell_append(GTK_MENU_SHELL(subMenu), separator);
    gtk_widget_show(separator);
}

void Hermes_Menu_SetItemEnabled(void* menu, const char* menuLabel, const char* itemId, bool enabled) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !itemId) return;

    GtkWidget* menuItem = find_menu_item_by_id(hm, itemId);
    if (menuItem) {
        gtk_widget_set_sensitive(menuItem, enabled);
    }
}

void Hermes_Menu_SetItemChecked(void* menu, const char* menuLabel, const char* itemId, bool checked) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !itemId) return;

    GtkWidget* menuItem = find_menu_item_by_id(hm, itemId);
    if (menuItem && GTK_IS_CHECK_MENU_ITEM(menuItem)) {
        gtk_check_menu_item_set_active(GTK_CHECK_MENU_ITEM(menuItem), checked);
    }
}

void Hermes_Menu_SetItemLabel(void* menu, const char* menuLabel, const char* itemId, const char* label) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !itemId || !label) return;

    GtkWidget* menuItem = find_menu_item_by_id(hm, itemId);
    if (menuItem) {
        gtk_menu_item_set_label(GTK_MENU_ITEM(menuItem), label);
    }
}

void Hermes_Menu_SetItemAccelerator(void* menu, const char* menuLabel, const char* itemId, const char* accelerator) {
    // Note: Changing accelerators at runtime is complex in GTK
    // This would require removing old accel and adding new one
    // For now, this is a no-op - accelerators should be set at creation time
}

// ============================================================================
// Submenu Operations
// ============================================================================

void Hermes_Menu_AddSubmenu(void* menu, const char* menuPath, const char* submenuLabel) {
    HermesMenu* hm = (HermesMenu*)menu;
    if (!hm || !menuPath || !submenuLabel) return;

    // Find parent menu item
    GtkWidget* parentItem = find_menu_item_by_id(hm, menuPath);
    if (!parentItem) {
        // Try to find by label in top-level menus
        parentItem = find_menu_by_label(hm, menuPath);
    }
    if (!parentItem) return;

    // Create submenu
    GtkWidget* menuItem = gtk_menu_item_new_with_label(submenuLabel);
    GtkWidget* subMenu = gtk_menu_new();
    gtk_menu_item_set_submenu(GTK_MENU_ITEM(menuItem), subMenu);

    GtkWidget* parentMenu = GTK_IS_MENU(parentItem) ? parentItem :
                            gtk_menu_item_get_submenu(GTK_MENU_ITEM(parentItem));
    if (parentMenu) {
        gtk_menu_shell_append(GTK_MENU_SHELL(parentMenu), menuItem);
        gtk_widget_show(menuItem);
    }
}

void Hermes_Menu_AddSubmenuItem(void* menu, const char* menuPath, const char* itemId,
                                const char* itemLabel, const char* accelerator) {
    // Similar to AddItem but for nested submenus
    // Implementation would follow the same pattern
}

void Hermes_Menu_AddSubmenuSeparator(void* menu, const char* menuPath) {
    // Similar to AddSeparator but for nested submenus
}
