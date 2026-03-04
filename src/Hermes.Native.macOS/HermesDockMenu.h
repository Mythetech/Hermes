// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_DOCK_MENU_H
#define HERMES_DOCK_MENU_H

#import <Cocoa/Cocoa.h>
#import "HermesTypes.h"

@class HermesDockMenuActionHandler;

/// Manages the application dock menu (right-click on dock icon).
/// Items added here appear above the default macOS entries (Options, Show All Windows, Hide, Quit).
@interface HermesDockMenu : NSObject

@property (nonatomic, assign) MenuItemCallback callback;
@property (nonatomic, strong) NSMenu* menu;
@property (nonatomic, strong) NSMutableArray<HermesDockMenuActionHandler*>* actionHandlers;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenuItem*>* itemsById;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenu*>* submenusById;

- (instancetype)initWithCallback:(MenuItemCallback)callback;

/// Build the menu for applicationDockMenu: delegate method
- (NSMenu*)buildMenu;

#pragma mark - Item Operations

- (void)addItem:(NSString*)itemId label:(NSString*)label;
- (void)addSeparator;
- (void)removeItem:(NSString*)itemId;
- (void)clear;

#pragma mark - Item State

- (void)setItemEnabled:(NSString*)itemId enabled:(BOOL)enabled;
- (void)setItemChecked:(NSString*)itemId checked:(BOOL)checked;
- (void)setItemLabel:(NSString*)itemId label:(NSString*)label;

#pragma mark - Submenu Operations

- (void)addSubmenu:(NSString*)submenuId label:(NSString*)label;
- (void)addSubmenuItem:(NSString*)submenuId itemId:(NSString*)itemId label:(NSString*)label;
- (void)addSubmenuSeparator:(NSString*)submenuId;
- (void)clearSubmenu:(NSString*)submenuId;

@end

/// Action handler for dock menu items
@interface HermesDockMenuActionHandler : NSObject

@property (nonatomic, weak) HermesDockMenu* dockMenu;
@property (nonatomic, copy) NSString* itemId;

- (void)menuItemClicked:(id)sender;

@end

#endif // HERMES_DOCK_MENU_H
