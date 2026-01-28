#import "Exports.h"
#import "HermesWindow.h"
#import "HermesMenu.h"
#import "HermesContextMenu.h"
#import "HermesDialogs.h"
#import "HermesAppDelegate.h"
#import <Cocoa/Cocoa.h>

static BOOL g_appRegistered = NO;
static HermesAppDelegate* g_appDelegate = nil;

#pragma mark - Application Lifecycle

void Hermes_App_Register(void) {
    if (g_appRegistered) return;
    g_appRegistered = YES;

    @autoreleasepool {
        [NSApplication sharedApplication];

        g_appDelegate = [[HermesAppDelegate alloc] init];
        [NSApp setDelegate:g_appDelegate];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];

        NSMenu* mainMenu = [[NSMenu alloc] init];
        NSMenuItem* appMenuItem = [[NSMenuItem alloc] init];
        NSMenu* appMenu = [[NSMenu alloc] init];

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
