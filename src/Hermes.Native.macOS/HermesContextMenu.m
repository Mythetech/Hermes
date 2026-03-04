// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#import "HermesContextMenu.h"
#import "HermesWindow.h"
#import "HermesMenu.h" // For parseAccelerator

@implementation HermesContextMenuActionHandler

- (void)menuItemClicked:(id)sender {
    if (_contextMenu && _contextMenu.callback && _itemId) {
        const char* itemIdUtf8 = [_itemId UTF8String];
        _contextMenu.callback(itemIdUtf8);
    }
}

@end

@implementation HermesContextMenu

- (instancetype)initWithWindow:(HermesWindow*)window callback:(MenuItemCallback)callback {
    self = [super init];
    if (self) {
        _hermesWindow = window;
        _callback = callback;
        _menu = [[NSMenu alloc] init];
        [_menu setAutoenablesItems:NO]; // Allow manual enable/disable
        _actionHandlers = [NSMutableArray new];
        _itemsById = [NSMutableDictionary new];
    }
    return self;
}

#pragma mark - Item Operations

- (void)addItem:(NSString*)itemId label:(NSString*)label accelerator:(NSString*)accelerator {
    // Parse accelerator
    NSString* keyEquivalent = @"";
    NSEventModifierFlags modifierMask = 0;
    [HermesMenu parseAccelerator:accelerator keyEquivalent:&keyEquivalent modifierMask:&modifierMask];

    // Create action handler
    HermesContextMenuActionHandler* handler = [[HermesContextMenuActionHandler alloc] init];
    handler.contextMenu = self;
    handler.itemId = itemId;
    [_actionHandlers addObject:handler];

    // Create menu item
    NSMenuItem* item = [[NSMenuItem alloc] initWithTitle:label
                                                  action:@selector(menuItemClicked:)
                                           keyEquivalent:keyEquivalent];
    [item setTarget:handler];
    [item setKeyEquivalentModifierMask:modifierMask];

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
            HermesContextMenuActionHandler* handler = _actionHandlers[i];
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

#pragma mark - Display

- (void)showAtX:(int)x y:(int)y {
    // Convert screen coordinates to window coordinates for positioning
    // macOS uses bottom-left origin, so we need to flip y

    NSScreen* screen = [NSScreen mainScreen];
    CGFloat screenHeight = screen.frame.size.height;

    // Create location in screen coordinates (macOS flipped)
    NSPoint location = NSMakePoint(x, screenHeight - y);

    // Pop up the menu at the specified location
    // nil for event and view means popup at absolute screen coordinates
    [_menu popUpMenuPositioningItem:nil atLocation:location inView:nil];
}

- (void)hide {
    [_menu cancelTracking];
}

@end
