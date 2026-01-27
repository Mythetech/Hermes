#ifndef HERMES_CONTEXT_MENU_H
#define HERMES_CONTEXT_MENU_H

#import <Cocoa/Cocoa.h>
#import "HermesTypes.h"

@class HermesWindow;
@class HermesContextMenuActionHandler;

@interface HermesContextMenu : NSObject

@property (nonatomic, weak) HermesWindow* hermesWindow;
@property (nonatomic, assign) MenuItemCallback callback;
@property (nonatomic, strong) NSMenu* menu;
@property (nonatomic, strong) NSMutableArray<HermesContextMenuActionHandler*>* actionHandlers;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenuItem*>* itemsById;

- (instancetype)initWithWindow:(HermesWindow*)window callback:(MenuItemCallback)callback;

// Item operations
- (void)addItem:(NSString*)itemId label:(NSString*)label accelerator:(NSString*)accelerator;
- (void)addSeparator;
- (void)removeItem:(NSString*)itemId;
- (void)clear;

// Item state
- (void)setItemEnabled:(NSString*)itemId enabled:(BOOL)enabled;
- (void)setItemChecked:(NSString*)itemId checked:(BOOL)checked;
- (void)setItemLabel:(NSString*)itemId label:(NSString*)label;

// Display
- (void)showAtX:(int)x y:(int)y;
- (void)hide;

@end

// Action handler for context menu items
@interface HermesContextMenuActionHandler : NSObject

@property (nonatomic, weak) HermesContextMenu* contextMenu;
@property (nonatomic, copy) NSString* itemId;

- (void)menuItemClicked:(id)sender;

@end

#endif // HERMES_CONTEXT_MENU_H
