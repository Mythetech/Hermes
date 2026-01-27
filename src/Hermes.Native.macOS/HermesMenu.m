#import "HermesMenu.h"
#import "HermesWindow.h"

@implementation HermesMenuActionHandler

- (void)menuItemClicked:(id)sender {
    if (_menu && _menu.callback && _itemId) {
        const char* itemIdUtf8 = [_itemId UTF8String];
        _menu.callback(itemIdUtf8);
    }
}

@end

@implementation HermesMenu

- (instancetype)initWithWindow:(HermesWindow*)window callback:(MenuItemCallback)callback {
    self = [super init];
    if (self) {
        _hermesWindow = window;
        _callback = callback;
        _actionHandlers = [NSMutableArray new];
        _itemsById = [NSMutableDictionary new];
        _menusByLabel = [NSMutableDictionary new];

        // Ensure we have a menu bar
        if (![NSApp mainMenu]) {
            NSMenu* mainMenu = [[NSMenu alloc] init];
            [NSApp setMainMenu:mainMenu];
        }
    }
    return self;
}

#pragma mark - Menu Operations

- (void)addMenu:(NSString*)label atIndex:(int)insertIndex {
    NSMenu* mainMenu = [NSApp mainMenu];

    // Create menu item for the menu bar
    NSMenuItem* menuItem = [[NSMenuItem alloc] init];
    [menuItem setTitle:label];

    // Create the submenu
    NSMenu* submenu = [[NSMenu alloc] initWithTitle:label];
    [menuItem setSubmenu:submenu];

    // Insert at position or append
    if (insertIndex >= 0 && insertIndex < [mainMenu numberOfItems]) {
        [mainMenu insertItem:menuItem atIndex:insertIndex];
    } else {
        [mainMenu addItem:menuItem];
    }

    // Track the menu
    _menusByLabel[label] = submenu;
}

- (void)removeMenu:(NSString*)label {
    NSMenu* mainMenu = [NSApp mainMenu];

    for (NSInteger i = 0; i < [mainMenu numberOfItems]; i++) {
        NSMenuItem* item = [mainMenu itemAtIndex:i];
        if ([[item title] isEqualToString:label]) {
            [mainMenu removeItemAtIndex:i];
            [_menusByLabel removeObjectForKey:label];
            break;
        }
    }
}

#pragma mark - Item Operations

- (void)addItemToMenu:(NSString*)menuLabel
               itemId:(NSString*)itemId
            itemLabel:(NSString*)itemLabel
          accelerator:(NSString*)accelerator {

    NSMenu* menu = [self findMenuByLabel:menuLabel];
    if (!menu) return;

    // Parse accelerator
    NSString* keyEquivalent = @"";
    NSEventModifierFlags modifierMask = 0;
    [HermesMenu parseAccelerator:accelerator keyEquivalent:&keyEquivalent modifierMask:&modifierMask];

    // Create action handler
    HermesMenuActionHandler* handler = [[HermesMenuActionHandler alloc] init];
    handler.menu = self;
    handler.itemId = itemId;
    [_actionHandlers addObject:handler];

    // Create menu item
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:itemLabel
                                                  action:@selector(menuItemClicked:)
                                           keyEquivalent:keyEquivalent];
    [item setTarget:handler];
    [item setKeyEquivalentModifierMask:modifierMask];

    [menu addItem:item];
    _itemsById[itemId] = item;
}

- (void)insertItemInMenu:(NSString*)menuLabel
             afterItemId:(NSString*)afterItemId
                  itemId:(NSString*)itemId
               itemLabel:(NSString*)itemLabel
             accelerator:(NSString*)accelerator {

    NSMenu* menu = [self findMenuByLabel:menuLabel];
    if (!menu) return;

    // Find the item to insert after
    NSInteger insertIndex = [menu numberOfItems]; // Default to end
    NSMenuItem* afterItem = _itemsById[afterItemId];
    if (afterItem) {
        NSInteger afterIndex = [menu indexOfItem:afterItem];
        if (afterIndex != -1) {
            insertIndex = afterIndex + 1;
        }
    }

    // Parse accelerator
    NSString* keyEquivalent = @"";
    NSEventModifierFlags modifierMask = 0;
    [HermesMenu parseAccelerator:accelerator keyEquivalent:&keyEquivalent modifierMask:&modifierMask];

    // Create action handler
    HermesMenuActionHandler* handler = [[HermesMenuActionHandler alloc] init];
    handler.menu = self;
    handler.itemId = itemId;
    [_actionHandlers addObject:handler];

    // Create menu item
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:itemLabel
                                                  action:@selector(menuItemClicked:)
                                           keyEquivalent:keyEquivalent];
    [item setTarget:handler];
    [item setKeyEquivalentModifierMask:modifierMask];

    [menu insertItem:item atIndex:insertIndex];
    _itemsById[itemId] = item;
}

- (void)removeItemFromMenu:(NSString*)menuLabel itemId:(NSString*)itemId {
    NSMenu* menu = [self findMenuByLabel:menuLabel];
    if (!menu) return;

    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [menu removeItem:item];
        [_itemsById removeObjectForKey:itemId];
    }
}

- (void)addSeparatorToMenu:(NSString*)menuLabel {
    NSMenu* menu = [self findMenuByLabel:menuLabel];
    if (!menu) return;

    [menu addItem:[NSMenuItem separatorItem]];
}

#pragma mark - Item State

- (void)setItemEnabled:(NSString*)menuLabel itemId:(NSString*)itemId enabled:(BOOL)enabled {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setEnabled:enabled];
    }
}

- (void)setItemChecked:(NSString*)menuLabel itemId:(NSString*)itemId checked:(BOOL)checked {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setState:checked ? NSControlStateValueOn : NSControlStateValueOff];
    }
}

- (void)setItemLabel:(NSString*)menuLabel itemId:(NSString*)itemId label:(NSString*)label {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setTitle:label];
    }
}

- (void)setItemAccelerator:(NSString*)menuLabel itemId:(NSString*)itemId accelerator:(NSString*)accelerator {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        NSString* keyEquivalent = @"";
        NSEventModifierFlags modifierMask = 0;
        [HermesMenu parseAccelerator:accelerator keyEquivalent:&keyEquivalent modifierMask:&modifierMask];
        [item setKeyEquivalent:keyEquivalent];
        [item setKeyEquivalentModifierMask:modifierMask];
    }
}

#pragma mark - Internal

- (NSMenu*)findMenuByLabel:(NSString*)label {
    // First check our cache
    NSMenu* cachedMenu = _menusByLabel[label];
    if (cachedMenu) return cachedMenu;

    // Search the main menu
    NSMenu* mainMenu = [NSApp mainMenu];
    for (NSMenuItem* item in [mainMenu itemArray]) {
        if ([[item title] isEqualToString:label]) {
            NSMenu* submenu = [item submenu];
            if (submenu) {
                _menusByLabel[label] = submenu;
                return submenu;
            }
        }
    }
    return nil;
}

- (NSMenuItem*)findItemById:(NSString*)itemId inMenu:(NSMenu*)menu {
    return _itemsById[itemId];
}

+ (void)parseAccelerator:(NSString*)accelerator
           keyEquivalent:(NSString**)keyEquivalent
            modifierMask:(NSEventModifierFlags*)modifierMask {

    *keyEquivalent = @"";
    *modifierMask = 0;

    if (!accelerator || [accelerator length] == 0) return;

    NSString* accel = [accelerator copy];

    // Parse Cmd/Command modifier
    if ([accel containsString:@"Cmd+"] || [accel containsString:@"Command+"]) {
        *modifierMask |= NSEventModifierFlagCommand;
        accel = [accel stringByReplacingOccurrencesOfString:@"Cmd+" withString:@""];
        accel = [accel stringByReplacingOccurrencesOfString:@"Command+" withString:@""];
    }

    // Parse Ctrl/Control modifier
    if ([accel containsString:@"Ctrl+"] || [accel containsString:@"Control+"]) {
        *modifierMask |= NSEventModifierFlagControl;
        accel = [accel stringByReplacingOccurrencesOfString:@"Ctrl+" withString:@""];
        accel = [accel stringByReplacingOccurrencesOfString:@"Control+" withString:@""];
    }

    // Parse Alt/Option modifier
    if ([accel containsString:@"Alt+"] || [accel containsString:@"Option+"]) {
        *modifierMask |= NSEventModifierFlagOption;
        accel = [accel stringByReplacingOccurrencesOfString:@"Alt+" withString:@""];
        accel = [accel stringByReplacingOccurrencesOfString:@"Option+" withString:@""];
    }

    // Parse Shift modifier
    if ([accel containsString:@"Shift+"]) {
        *modifierMask |= NSEventModifierFlagShift;
        accel = [accel stringByReplacingOccurrencesOfString:@"Shift+" withString:@""];
    }

    // Remaining text is the key (convert to lowercase for keyEquivalent)
    if ([accel length] > 0) {
        *keyEquivalent = [accel lowercaseString];
    }
}

@end
