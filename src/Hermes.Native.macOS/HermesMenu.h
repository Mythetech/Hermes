// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_MENU_H
#define HERMES_MENU_H

#import <Cocoa/Cocoa.h>
#import "HermesTypes.h"

@class HermesWindow;
@class HermesMenuActionHandler;

@interface HermesMenu : NSObject

@property (nonatomic, weak) HermesWindow* hermesWindow;
@property (nonatomic, assign) MenuItemCallback callback;
@property (nonatomic, strong) NSMutableArray<HermesMenuActionHandler*>* actionHandlers;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenuItem*>* itemsById;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenu*>* menusByLabel;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenu*>* submenusByPath;

- (instancetype)initWithWindow:(HermesWindow*)window callback:(MenuItemCallback)callback;

// Menu operations
- (void)addMenu:(NSString*)label atIndex:(int)insertIndex;
- (void)removeMenu:(NSString*)label;

// Item operations
- (void)addItemToMenu:(NSString*)menuLabel
               itemId:(NSString*)itemId
            itemLabel:(NSString*)itemLabel
          accelerator:(NSString*)accelerator;

- (void)insertItemInMenu:(NSString*)menuLabel
             afterItemId:(NSString*)afterItemId
                  itemId:(NSString*)itemId
               itemLabel:(NSString*)itemLabel
             accelerator:(NSString*)accelerator;

- (void)removeItemFromMenu:(NSString*)menuLabel itemId:(NSString*)itemId;
- (void)addSeparatorToMenu:(NSString*)menuLabel;

// Item state
- (void)setItemEnabled:(NSString*)menuLabel itemId:(NSString*)itemId enabled:(BOOL)enabled;
- (void)setItemChecked:(NSString*)menuLabel itemId:(NSString*)itemId checked:(BOOL)checked;
- (void)setItemLabel:(NSString*)menuLabel itemId:(NSString*)itemId label:(NSString*)label;
- (void)setItemAccelerator:(NSString*)menuLabel itemId:(NSString*)itemId accelerator:(NSString*)accelerator;

// Submenu operations
- (void)addSubmenu:(NSString*)menuPath submenuLabel:(NSString*)submenuLabel;
- (void)addItemToSubmenu:(NSString*)menuPath
                  itemId:(NSString*)itemId
               itemLabel:(NSString*)itemLabel
             accelerator:(NSString*)accelerator;
- (void)addSeparatorToSubmenu:(NSString*)menuPath;

// App menu operations
- (NSMenu*)getAppMenu;
- (void)addAppMenuItem:(NSString*)itemId
             itemLabel:(NSString*)itemLabel
           accelerator:(NSString*)accelerator
              position:(NSString*)position;
- (void)addAppMenuSeparator:(NSString*)position;
- (void)removeAppMenuItem:(NSString*)itemId;

// Internal
- (NSMenu*)findMenuByLabel:(NSString*)label;
- (NSMenu*)findMenuByPath:(NSString*)path;
- (NSMenuItem*)findItemById:(NSString*)itemId inMenu:(NSMenu*)menu;
- (NSInteger)findQuitItemIndex:(NSMenu*)appMenu;
+ (void)parseAccelerator:(NSString*)accelerator
           keyEquivalent:(NSString**)keyEquivalent
            modifierMask:(NSEventModifierFlags*)modifierMask;

@end

// Action handler for menu items
@interface HermesMenuActionHandler : NSObject

@property (nonatomic, weak) HermesMenu* menu;
@property (nonatomic, copy) NSString* itemId;

- (void)menuItemClicked:(id)sender;

@end

#endif // HERMES_MENU_H
