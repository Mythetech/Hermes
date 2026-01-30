#import "HermesDockMenu.h"

@implementation HermesDockMenuActionHandler

- (void)menuItemClicked:(id)sender {
    if (_dockMenu && _dockMenu.callback && _itemId) {
        const char* itemIdUtf8 = [_itemId UTF8String];
        _dockMenu.callback(itemIdUtf8);
    }
}

@end

@implementation HermesDockMenu

- (instancetype)initWithCallback:(MenuItemCallback)callback {
    self = [super init];
    if (self) {
        _callback = callback;
        _menu = [[NSMenu alloc] init];
        [_menu setAutoenablesItems:NO];
        _actionHandlers = [NSMutableArray new];
        _itemsById = [NSMutableDictionary new];
        _submenusById = [NSMutableDictionary new];
    }
    return self;
}

- (NSMenu*)buildMenu {
    // Return the menu as-is; macOS will append its standard items below
    return _menu;
}

#pragma mark - Item Operations

- (void)addItem:(NSString*)itemId label:(NSString*)label {
    // Create action handler
    HermesDockMenuActionHandler* handler = [[HermesDockMenuActionHandler alloc] init];
    handler.dockMenu = self;
    handler.itemId = itemId;
    [_actionHandlers addObject:handler];

    // Create menu item
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:label
                                                  action:@selector(menuItemClicked:)
                                           keyEquivalent:@""];
    [item setTarget:handler];

    [_menu addItem:item];
    _itemsById[itemId] = item;
}

- (void)addSeparator {
    [_menu addItem:[NSMenuItem separatorItem]];
}

- (void)removeItem:(NSString*)itemId {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [_menu removeItem:item];
        [_itemsById removeObjectForKey:itemId];

        // Remove the associated action handler
        for (NSInteger i = _actionHandlers.count - 1; i >= 0; i--) {
            HermesDockMenuActionHandler* handler = _actionHandlers[i];
            if ([handler.itemId isEqualToString:itemId]) {
                [_actionHandlers removeObjectAtIndex:i];
                break;
            }
        }
    }
}

- (void)clear {
    [_menu removeAllItems];
    [_itemsById removeAllObjects];
    [_submenusById removeAllObjects];
    [_actionHandlers removeAllObjects];
}

#pragma mark - Item State

- (void)setItemEnabled:(NSString*)itemId enabled:(BOOL)enabled {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setEnabled:enabled];
    }
}

- (void)setItemChecked:(NSString*)itemId checked:(BOOL)checked {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setState:checked ? NSControlStateValueOn : NSControlStateValueOff];
    }
}

- (void)setItemLabel:(NSString*)itemId label:(NSString*)label {
    NSMenuItem* item = _itemsById[itemId];
    if (item) {
        [item setTitle:label];
    }
}

#pragma mark - Submenu Operations

- (void)addSubmenu:(NSString*)submenuId label:(NSString*)label {
    // Create the submenu
    NSMenu* submenu = [[NSMenu alloc] initWithTitle:label];
    [submenu setAutoenablesItems:NO];

    // Create a menu item to hold the submenu
    NSMenuItem* submenuItem = [[NSMenuItem alloc] initWithTitle:label
                                                         action:nil
                                                  keyEquivalent:@""];
    [submenuItem setSubmenu:submenu];

    [_menu addItem:submenuItem];
    _submenusById[submenuId] = submenu;
    _itemsById[submenuId] = submenuItem;
}

- (void)addSubmenuItem:(NSString*)submenuId itemId:(NSString*)itemId label:(NSString*)label {
    NSMenu* submenu = _submenusById[submenuId];
    if (!submenu) return;

    // Create action handler
    HermesDockMenuActionHandler* handler = [[HermesDockMenuActionHandler alloc] init];
    handler.dockMenu = self;
    handler.itemId = itemId;
    [_actionHandlers addObject:handler];

    // Create menu item
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:label
                                                  action:@selector(menuItemClicked:)
                                           keyEquivalent:@""];
    [item setTarget:handler];

    [submenu addItem:item];
    _itemsById[itemId] = item;
}

- (void)addSubmenuSeparator:(NSString*)submenuId {
    NSMenu* submenu = _submenusById[submenuId];
    if (submenu) {
        [submenu addItem:[NSMenuItem separatorItem]];
    }
}

- (void)clearSubmenu:(NSString*)submenuId {
    NSMenu* submenu = _submenusById[submenuId];
    if (submenu) {
        // Remove action handlers for items in this submenu
        NSArray* items = [[submenu itemArray] copy];
        for (NSMenuItem* item in items) {
            // Find and remove handler by matching target
            for (NSInteger i = _actionHandlers.count - 1; i >= 0; i--) {
                HermesDockMenuActionHandler* handler = _actionHandlers[i];
                if (item.target == handler) {
                    [_itemsById removeObjectForKey:handler.itemId];
                    [_actionHandlers removeObjectAtIndex:i];
                    break;
                }
            }
        }
        [submenu removeAllItems];
    }
}

@end
