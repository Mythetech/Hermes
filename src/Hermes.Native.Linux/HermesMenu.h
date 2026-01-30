#ifndef HERMES_MENU_H
#define HERMES_MENU_H

#include <gtk/gtk.h>
#include "HermesTypes.h"

typedef struct _HermesMenu HermesMenu;

struct _HermesMenu {
    GtkWidget* window;
    GtkWidget* menuBar;
    GtkWidget* container;
    MenuItemCallback callback;
    GHashTable* menuItems;     // itemId -> GtkMenuItem
    GHashTable* topLevelMenus; // label -> GtkMenu
};

HermesMenu* hermes_menu_new(GtkWidget* window, GtkWidget* container, MenuItemCallback callback);
void hermes_menu_destroy(HermesMenu* menu);

#endif // HERMES_MENU_H
