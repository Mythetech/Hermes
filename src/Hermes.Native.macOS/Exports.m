// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#import "Exports.h"
#import "HermesWindow.h"
#import "HermesMenu.h"
#import "HermesContextMenu.h"
#import "HermesDockMenu.h"
#import "HermesStatusIcon.h"
#import "HermesDialogs.h"
#import "HermesAppDelegate.h"
#import <Cocoa/Cocoa.h>

static BOOL g_appRegistered = NO;
static BOOL g_accessoryMode = NO;
static HermesAppDelegate* g_appDelegate = nil;

#pragma mark - Application Lifecycle

void Hermes_App_Register(void) {
    if (g_appRegistered) return;
    g_appRegistered = YES;

    @autoreleasepool {
        [NSApplication sharedApplication];

        g_appDelegate = [[HermesAppDelegate alloc] init];
        [NSApp setDelegate:g_appDelegate];

        if (g_accessoryMode) {
            [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
            g_appDelegate.accessoryMode = YES;
        } else {
            [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];
        }

        NSMenu* mainMenu = [[NSMenu alloc] init];
        NSMenuItem* appMenuItem = [[NSMenuItem alloc] init];
        NSMenu* appMenu = [[NSMenu alloc] init];

        // Standard Edit items - target nil for responder chain
        NSMenuItem* undoItem = [[NSMenuItem alloc] initWithTitle:@"Undo"
                                                          action:@selector(undo:)
                                                   keyEquivalent:@"z"];
        [appMenu addItem:undoItem];

        NSMenuItem* redoItem = [[NSMenuItem alloc] initWithTitle:@"Redo"
                                                          action:@selector(redo:)
                                                   keyEquivalent:@"Z"];
        [appMenu addItem:redoItem];

        [appMenu addItem:[NSMenuItem separatorItem]];

        NSMenuItem* cutItem = [[NSMenuItem alloc] initWithTitle:@"Cut"
                                                         action:@selector(cut:)
                                                  keyEquivalent:@"x"];
        [appMenu addItem:cutItem];

        NSMenuItem* copyItem = [[NSMenuItem alloc] initWithTitle:@"Copy"
                                                          action:@selector(copy:)
                                                   keyEquivalent:@"c"];
        [appMenu addItem:copyItem];

        NSMenuItem* pasteItem = [[NSMenuItem alloc] initWithTitle:@"Paste"
                                                           action:@selector(paste:)
                                                    keyEquivalent:@"v"];
        [appMenu addItem:pasteItem];

        NSMenuItem* selectAllItem = [[NSMenuItem alloc] initWithTitle:@"Select All"
                                                               action:@selector(selectAll:)
                                                        keyEquivalent:@"a"];
        [appMenu addItem:selectAllItem];

        [appMenu addItem:[NSMenuItem separatorItem]];

        NSString* appName = [[NSProcessInfo processInfo] processName];
        NSMenuItem* quitItem = [[NSMenuItem alloc] initWithTitle:[NSString stringWithFormat:@"Quit %@", appName]
                                                          action:@selector(terminate:)
                                                   keyEquivalent:@"q"];
        [appMenu addItem:quitItem];

        [appMenuItem setSubmenu:appMenu];
        [mainMenu addItem:appMenuItem];

        [NSApp setMainMenu:mainMenu];
    }
}

void Hermes_App_SetAccessoryMode(void) {
    g_accessoryMode = YES;
    if (g_appRegistered) {
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
        g_appDelegate.accessoryMode = YES;
    }
}

#pragma mark - Window Lifecycle

void* Hermes_Window_Create(const HermesWindowParams* params) {
    @autoreleasepool {
        // Ensure app is registered
        Hermes_App_Register();

        HermesWindow* window = [[HermesWindow alloc] initWithParams:params];
        return (__bridge_retained void*)window;
    }
}

void Hermes_Window_Show(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow show];
    }
}

void Hermes_Window_Hide(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow hide];
    }
}

void Hermes_Window_Close(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow close];
    }
}

void Hermes_Window_WaitForClose(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow waitForClose];
    }
}

void Hermes_Window_Destroy(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge_transfer HermesWindow*)window;
        (void)hermesWindow; // Release
    }
}

#pragma mark - Window Properties - Getters

void Hermes_Window_GetTitle(void* window, char* buffer, int bufferSize) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        NSString* title = [hermesWindow title];
        const char* utf8 = [title UTF8String];
        strncpy(buffer, utf8, bufferSize - 1);
        buffer[bufferSize - 1] = '\0';
    }
}

void Hermes_Window_GetSize(void* window, int* width, int* height) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow getSize:width height:height];
    }
}

void Hermes_Window_GetPosition(void* window, int* x, int* y) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow getPosition:x y:y];
    }
}

bool Hermes_Window_GetIsMaximized(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        return [hermesWindow isMaximized];
    }
}

bool Hermes_Window_GetIsMinimized(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        return [hermesWindow isMinimized];
    }
}

int64_t Hermes_Window_GetUIThreadId(void* window) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        return hermesWindow.uiThreadId;
    }
}

#pragma mark - Window Properties - Setters

void Hermes_Window_SetTitle(void* window, const char* title) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow setTitle:[NSString stringWithUTF8String:title]];
    }
}

void Hermes_Window_SetSize(void* window, int width, int height) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow setWidth:width height:height];
    }
}

void Hermes_Window_SetPosition(void* window, int x, int y) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow setPositionX:x y:y];
    }
}

void Hermes_Window_SetIsMaximized(void* window, bool maximized) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow setIsMaximized:maximized];
    }
}

void Hermes_Window_SetIsMinimized(void* window, bool minimized) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow setIsMinimized:minimized];
    }
}

#pragma mark - WebView Operations

void Hermes_Window_NavigateToUrl(void* window, const char* url) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow navigateToUrl:[NSString stringWithUTF8String:url]];
    }
}

void Hermes_Window_NavigateToString(void* window, const char* html) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow navigateToString:[NSString stringWithUTF8String:html]];
    }
}

void Hermes_Window_SendWebMessage(void* window, const char* message) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow sendWebMessage:[NSString stringWithUTF8String:message]];
    }
}

void Hermes_Window_RegisterCustomScheme(void* window, const char* scheme) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow registerCustomScheme:[NSString stringWithUTF8String:scheme]];
    }
}

#pragma mark - Threading

void Hermes_Window_Invoke(void* window, InvokeCallback callback) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow invoke:callback];
    }
}

void Hermes_Window_BeginInvoke(void* window, InvokeCallback callback) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        [hermesWindow beginInvoke:callback];
    }
}

#pragma mark - Menu Operations

void* Hermes_Menu_Create(void* window, MenuItemCallback callback) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        HermesMenu* menu = [[HermesMenu alloc] initWithWindow:hermesWindow callback:callback];
        return (__bridge_retained void*)menu;
    }
}

void Hermes_Menu_Destroy(void* menu) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge_transfer HermesMenu*)menu;
        (void)hermesMenu; // Release
    }
}

void Hermes_Menu_AddMenu(void* menu, const char* label, int insertIndex) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addMenu:[NSString stringWithUTF8String:label] atIndex:insertIndex];
    }
}

void Hermes_Menu_RemoveMenu(void* menu, const char* label) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu removeMenu:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_Menu_AddItem(void* menu, const char* menuLabel, const char* itemId,
                         const char* itemLabel, const char* accelerator) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addItemToMenu:[NSString stringWithUTF8String:menuLabel]
                           itemId:[NSString stringWithUTF8String:itemId]
                        itemLabel:[NSString stringWithUTF8String:itemLabel]
                      accelerator:accelerator ? [NSString stringWithUTF8String:accelerator] : nil];
    }
}

void Hermes_Menu_InsertItem(void* menu, const char* menuLabel, const char* afterItemId,
                            const char* itemId, const char* itemLabel, const char* accelerator) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu insertItemInMenu:[NSString stringWithUTF8String:menuLabel]
                         afterItemId:[NSString stringWithUTF8String:afterItemId]
                              itemId:[NSString stringWithUTF8String:itemId]
                           itemLabel:[NSString stringWithUTF8String:itemLabel]
                         accelerator:accelerator ? [NSString stringWithUTF8String:accelerator] : nil];
    }
}

void Hermes_Menu_RemoveItem(void* menu, const char* menuLabel, const char* itemId) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu removeItemFromMenu:[NSString stringWithUTF8String:menuLabel]
                                itemId:[NSString stringWithUTF8String:itemId]];
    }
}

void Hermes_Menu_AddSeparator(void* menu, const char* menuLabel) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addSeparatorToMenu:[NSString stringWithUTF8String:menuLabel]];
    }
}

void Hermes_Menu_SetItemEnabled(void* menu, const char* menuLabel, const char* itemId, bool enabled) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu setItemEnabled:[NSString stringWithUTF8String:menuLabel]
                            itemId:[NSString stringWithUTF8String:itemId]
                           enabled:enabled];
    }
}

void Hermes_Menu_SetItemChecked(void* menu, const char* menuLabel, const char* itemId, bool checked) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu setItemChecked:[NSString stringWithUTF8String:menuLabel]
                            itemId:[NSString stringWithUTF8String:itemId]
                           checked:checked];
    }
}

void Hermes_Menu_SetItemLabel(void* menu, const char* menuLabel, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu setItemLabel:[NSString stringWithUTF8String:menuLabel]
                          itemId:[NSString stringWithUTF8String:itemId]
                           label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_Menu_SetItemAccelerator(void* menu, const char* menuLabel, const char* itemId, const char* accelerator) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu setItemAccelerator:[NSString stringWithUTF8String:menuLabel]
                                itemId:[NSString stringWithUTF8String:itemId]
                           accelerator:[NSString stringWithUTF8String:accelerator]];
    }
}

#pragma mark - Submenu Operations

void Hermes_Menu_AddSubmenu(void* menu, const char* menuPath, const char* submenuLabel) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addSubmenu:[NSString stringWithUTF8String:menuPath]
                  submenuLabel:[NSString stringWithUTF8String:submenuLabel]];
    }
}

void Hermes_Menu_AddSubmenuItem(void* menu, const char* menuPath, const char* itemId,
                                 const char* itemLabel, const char* accelerator) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addItemToSubmenu:[NSString stringWithUTF8String:menuPath]
                              itemId:[NSString stringWithUTF8String:itemId]
                           itemLabel:[NSString stringWithUTF8String:itemLabel]
                         accelerator:accelerator ? [NSString stringWithUTF8String:accelerator] : nil];
    }
}

void Hermes_Menu_AddSubmenuSeparator(void* menu, const char* menuPath) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addSeparatorToSubmenu:[NSString stringWithUTF8String:menuPath]];
    }
}

#pragma mark - App Menu Operations

char* Hermes_Menu_GetAppName(void) {
    @autoreleasepool {
        NSString* appName = [[NSProcessInfo processInfo] processName];
        const char* utf8 = [appName UTF8String];
        char* result = strdup(utf8);
        return result;
    }
}

void Hermes_Menu_AddAppMenuItem(void* menu, const char* itemId, const char* itemLabel,
                                 const char* accelerator, const char* position) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addAppMenuItem:[NSString stringWithUTF8String:itemId]
                         itemLabel:[NSString stringWithUTF8String:itemLabel]
                       accelerator:accelerator ? [NSString stringWithUTF8String:accelerator] : nil
                          position:position ? [NSString stringWithUTF8String:position] : nil];
    }
}

void Hermes_Menu_AddAppMenuSeparator(void* menu, const char* position) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu addAppMenuSeparator:position ? [NSString stringWithUTF8String:position] : nil];
    }
}

void Hermes_Menu_RemoveAppMenuItem(void* menu, const char* itemId) {
    @autoreleasepool {
        HermesMenu* hermesMenu = (__bridge HermesMenu*)menu;
        [hermesMenu removeAppMenuItem:[NSString stringWithUTF8String:itemId]];
    }
}

#pragma mark - Context Menu Operations

void* Hermes_ContextMenu_Create(void* window, MenuItemCallback callback) {
    @autoreleasepool {
        HermesWindow* hermesWindow = (__bridge HermesWindow*)window;
        HermesContextMenu* contextMenu = [[HermesContextMenu alloc] initWithWindow:hermesWindow callback:callback];
        return (__bridge_retained void*)contextMenu;
    }
}

void Hermes_ContextMenu_Destroy(void* contextMenu) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge_transfer HermesContextMenu*)contextMenu;
        (void)menu; // Release
    }
}

void Hermes_ContextMenu_AddItem(void* contextMenu, const char* itemId, const char* label, const char* accelerator) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu addItem:[NSString stringWithUTF8String:itemId]
                label:[NSString stringWithUTF8String:label]
          accelerator:accelerator ? [NSString stringWithUTF8String:accelerator] : nil];
    }
}

void Hermes_ContextMenu_AddSeparator(void* contextMenu) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu addSeparator];
    }
}

void Hermes_ContextMenu_RemoveItem(void* contextMenu, const char* itemId) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu removeItem:[NSString stringWithUTF8String:itemId]];
    }
}

void Hermes_ContextMenu_Clear(void* contextMenu) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu clear];
    }
}

void Hermes_ContextMenu_SetItemEnabled(void* contextMenu, const char* itemId, bool enabled) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu setItemEnabled:[NSString stringWithUTF8String:itemId] enabled:enabled];
    }
}

void Hermes_ContextMenu_SetItemChecked(void* contextMenu, const char* itemId, bool checked) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu setItemChecked:[NSString stringWithUTF8String:itemId] checked:checked];
    }
}

void Hermes_ContextMenu_SetItemLabel(void* contextMenu, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu setItemLabel:[NSString stringWithUTF8String:itemId] label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_ContextMenu_Show(void* contextMenu, int x, int y) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu showAtX:x y:y];
    }
}

void Hermes_ContextMenu_Hide(void* contextMenu) {
    @autoreleasepool {
        HermesContextMenu* menu = (__bridge HermesContextMenu*)contextMenu;
        [menu hide];
    }
}

#pragma mark - Dialog Operations

char** Hermes_Dialog_ShowOpenFile(const char* title, const char* defaultPath,
                                   bool multiSelect, const char** filters,
                                   int filterCount, int* resultCount) {
    return Hermes_ShowOpenFileDialog(title, defaultPath, multiSelect, filters, filterCount, resultCount);
}

char** Hermes_Dialog_ShowOpenFolder(const char* title, const char* defaultPath,
                                     bool multiSelect, int* resultCount) {
    return Hermes_ShowOpenFolderDialog(title, defaultPath, multiSelect, resultCount);
}

char* Hermes_Dialog_ShowSaveFile(const char* title, const char* defaultPath,
                                  const char** filters, int filterCount,
                                  const char* defaultFileName) {
    return Hermes_ShowSaveFileDialog(title, defaultPath, filters, filterCount, defaultFileName);
}

int Hermes_Dialog_ShowMessage(const char* title, const char* message,
                               int buttons, int icon) {
    return Hermes_ShowMessageDialog(title, message, buttons, icon);
}

#pragma mark - Dock Menu Operations

void* Hermes_DockMenu_Create(MenuItemCallback callback) {
    @autoreleasepool {
        // Ensure app is registered
        Hermes_App_Register();

        HermesDockMenu* dockMenu = [[HermesDockMenu alloc] initWithCallback:callback];

        // Wire up to the app delegate
        g_appDelegate.dockMenu = dockMenu;

        return (__bridge_retained void*)dockMenu;
    }
}

void Hermes_DockMenu_Destroy(void* dockMenu) {
    @autoreleasepool {
        // Clear from app delegate
        if (g_appDelegate.dockMenu == (__bridge HermesDockMenu*)dockMenu) {
            g_appDelegate.dockMenu = nil;
        }

        HermesDockMenu* menu = (__bridge_transfer HermesDockMenu*)dockMenu;
        (void)menu; // Release
    }
}

void Hermes_DockMenu_AddItem(void* dockMenu, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu addItem:[NSString stringWithUTF8String:itemId]
                label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_DockMenu_AddSeparator(void* dockMenu) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu addSeparator];
    }
}

void Hermes_DockMenu_RemoveItem(void* dockMenu, const char* itemId) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu removeItem:[NSString stringWithUTF8String:itemId]];
    }
}

void Hermes_DockMenu_Clear(void* dockMenu) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu clear];
    }
}

void Hermes_DockMenu_SetItemEnabled(void* dockMenu, const char* itemId, bool enabled) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu setItemEnabled:[NSString stringWithUTF8String:itemId] enabled:enabled];
    }
}

void Hermes_DockMenu_SetItemChecked(void* dockMenu, const char* itemId, bool checked) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu setItemChecked:[NSString stringWithUTF8String:itemId] checked:checked];
    }
}

void Hermes_DockMenu_SetItemLabel(void* dockMenu, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu setItemLabel:[NSString stringWithUTF8String:itemId]
                     label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_DockMenu_AddSubmenu(void* dockMenu, const char* submenuId, const char* label) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu addSubmenu:[NSString stringWithUTF8String:submenuId]
                   label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_DockMenu_AddSubmenuItem(void* dockMenu, const char* submenuId,
                                     const char* itemId, const char* label) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu addSubmenuItem:[NSString stringWithUTF8String:submenuId]
                      itemId:[NSString stringWithUTF8String:itemId]
                       label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_DockMenu_AddSubmenuSeparator(void* dockMenu, const char* submenuId) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu addSubmenuSeparator:[NSString stringWithUTF8String:submenuId]];
    }
}

void Hermes_DockMenu_ClearSubmenu(void* dockMenu, const char* submenuId) {
    @autoreleasepool {
        HermesDockMenu* menu = (__bridge HermesDockMenu*)dockMenu;
        [menu clearSubmenu:[NSString stringWithUTF8String:submenuId]];
    }
}

#pragma mark - Status Icon Operations

void* Hermes_StatusIcon_Create(MenuItemCallback menuCallback, InvokeCallback clickCallback) {
    @autoreleasepool {
        HermesStatusIcon* statusIcon = [[HermesStatusIcon alloc] initWithMenuCallback:menuCallback
                                                                        clickCallback:clickCallback];
        return (__bridge_retained void*)statusIcon;
    }
}

void Hermes_StatusIcon_Destroy(void* statusIcon) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge_transfer HermesStatusIcon*)statusIcon;
        (void)icon; // Release
    }
}

void Hermes_StatusIcon_Show(void* statusIcon) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon show];
    }
}

void Hermes_StatusIcon_Hide(void* statusIcon) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon hide];
    }
}

void Hermes_StatusIcon_SetIconFromPath(void* statusIcon, const char* filePath) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setIconFromPath:[NSString stringWithUTF8String:filePath]];
    }
}

void Hermes_StatusIcon_SetIconFromData(void* statusIcon, const void* data, int length) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setIconFromData:data length:length];
    }
}

void Hermes_StatusIcon_SetTooltip(void* statusIcon, const char* tooltip) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setTooltip:[NSString stringWithUTF8String:tooltip]];
    }
}

void Hermes_StatusIcon_AddItem(void* statusIcon, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon addItem:[NSString stringWithUTF8String:itemId]
                label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_StatusIcon_AddSeparator(void* statusIcon) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon addSeparator];
    }
}

void Hermes_StatusIcon_RemoveItem(void* statusIcon, const char* itemId) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon removeItem:[NSString stringWithUTF8String:itemId]];
    }
}

void Hermes_StatusIcon_Clear(void* statusIcon) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon clear];
    }
}

void Hermes_StatusIcon_SetItemEnabled(void* statusIcon, const char* itemId, bool enabled) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setItemEnabled:[NSString stringWithUTF8String:itemId] enabled:enabled];
    }
}

void Hermes_StatusIcon_SetItemChecked(void* statusIcon, const char* itemId, bool checked) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setItemChecked:[NSString stringWithUTF8String:itemId] checked:checked];
    }
}

void Hermes_StatusIcon_SetItemLabel(void* statusIcon, const char* itemId, const char* label) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon setItemLabel:[NSString stringWithUTF8String:itemId]
                     label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_StatusIcon_AddSubmenu(void* statusIcon, const char* submenuId, const char* label) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon addSubmenu:[NSString stringWithUTF8String:submenuId]
                   label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_StatusIcon_AddSubmenuItem(void* statusIcon, const char* submenuId,
                                      const char* itemId, const char* label) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon addSubmenuItem:[NSString stringWithUTF8String:submenuId]
                      itemId:[NSString stringWithUTF8String:itemId]
                       label:[NSString stringWithUTF8String:label]];
    }
}

void Hermes_StatusIcon_AddSubmenuSeparator(void* statusIcon, const char* submenuId) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon addSubmenuSeparator:[NSString stringWithUTF8String:submenuId]];
    }
}

void Hermes_StatusIcon_ClearSubmenu(void* statusIcon, const char* submenuId) {
    @autoreleasepool {
        HermesStatusIcon* icon = (__bridge HermesStatusIcon*)statusIcon;
        [icon clearSubmenu:[NSString stringWithUTF8String:submenuId]];
    }
}

#pragma mark - Memory Management

void Hermes_Free(void* ptr) {
    if (ptr) {
        free(ptr);
    }
}

void Hermes_FreeStringArray(char** array, int count) {
    if (array) {
        for (int i = 0; i < count; i++) {
            if (array[i]) {
                free(array[i]);
            }
        }
        free(array);
    }
}
