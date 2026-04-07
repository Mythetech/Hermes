// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#import "HermesStatusIcon.h"

@implementation HermesStatusIconActionHandler

- (void)menuItemClicked:(id)sender {
    if (_statusIcon && _statusIcon.menuCallback && _itemId) {
        const char* itemIdUtf8 = [_itemId UTF8String];
        _statusIcon.menuCallback(itemIdUtf8);
    }
}

@end

@implementation HermesStatusIcon

- (instancetype)initWithMenuCallback:(MenuItemCallback)menuCallback
                       clickCallback:(InvokeCallback)clickCallback {
    self = [super init];
    if (self) {
        _menuCallback = menuCallback;
        _clickCallback = clickCallback;
        _menu = [[NSMenu alloc] init];
        [_menu setAutoenablesItems:NO];
        _actionHandlers = [NSMutableArray new];
        _itemsById = [NSMutableDictionary new];
        _submenusById = [NSMutableDictionary new];

        _statusItem = [[NSStatusBar systemStatusBar] statusItemWithLength:NSVariableStatusItemLength];
        _statusItem.visible = NO;

        // Do NOT assign _statusItem.menu directly, as that prevents the button
        // action from firing. Instead, handle clicks manually and show the menu
        // programmatically on right-click.
        _statusItem.button.action = @selector(statusItemClicked:);
        _statusItem.button.target = self;
        [_statusItem.button sendActionOn:NSEventMaskLeftMouseUp | NSEventMaskRightMouseUp];
    }
    return self;
}

- (void)statusItemClicked:(id)sender {
    NSEvent* event = [NSApp currentEvent];

    if (event.type == NSEventTypeRightMouseUp) {
        // Right-click: show the context menu
        [_statusItem popUpStatusItemMenu:_menu];
    } else {
        // Left-click: fire the click callback, then show the menu
        if (_clickCallback) {
            _clickCallback();
        }
        // Also show the menu on left-click (standard macOS behavior)
        if (_menu.numberOfItems > 0) {
            [_statusItem popUpStatusItemMenu:_menu];
        }
    }
}

- (void)dealloc {
    [[NSStatusBar systemStatusBar] removeStatusItem:_statusItem];
}

#pragma mark - Lifecycle

- (void)show {
    _statusItem.visible = YES;
}

- (void)hide {
    _statusItem.visible = NO;
}

#pragma mark - Icon

- (void)setIconFromPath:(NSString*)filePath {
    NSImage* image = [[NSImage alloc] initWithContentsOfFile:filePath];
    if (image) {
        // Check if filename contains "Template" to enable template rendering
        if ([filePath rangeOfString:@"Template"].location != NSNotFound) {
            [image setTemplate:YES];
        }
        [image setSize:NSMakeSize(18, 18)];
        _statusItem.button.image = image;
        _statusItem.button.title = @""; // Clear text title when icon is set
    }
}

- (void)setIconFromData:(const void*)data length:(int)length {
    NSData* nsData = [NSData dataWithBytes:data length:length];
    NSImage* image = [[NSImage alloc] initWithData:nsData];
    if (image) {
        [image setSize:NSMakeSize(18, 18)];
        _statusItem.button.image = image;
        _statusItem.button.title = @""; // Clear text title when icon is set
    }
}

- (void)setTooltip:(NSString*)tooltip {
    _statusItem.button.toolTip = tooltip;
    // If no icon is set, show the tooltip text as the button title so the item is visible
    if (!_statusItem.button.image) {
        _statusItem.button.title = tooltip;
    }
}

#pragma mark - Item Operations

- (void)addItem:(NSString*)itemId label:(NSString*)label {
    // Create action handler
    HermesStatusIconActionHandler* handler = [[HermesStatusIconActionHandler alloc] init];
    handler.statusIcon = self;
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
            HermesStatusIconActionHandler* handler = _actionHandlers[i];
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
    HermesStatusIconActionHandler* handler = [[HermesStatusIconActionHandler alloc] init];
    handler.statusIcon = self;
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
                HermesStatusIconActionHandler* handler = _actionHandlers[i];
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
