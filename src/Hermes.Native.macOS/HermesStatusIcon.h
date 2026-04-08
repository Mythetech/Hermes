// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_STATUS_ICON_H
#define HERMES_STATUS_ICON_H

#import <Cocoa/Cocoa.h>
#import "HermesTypes.h"

@class HermesStatusIconActionHandler;

/// Manages a system tray status icon (NSStatusItem) with a context menu.
@interface HermesStatusIcon : NSObject

@property (nonatomic, assign) MenuItemCallback menuCallback;
@property (nonatomic, assign) InvokeCallback clickCallback;
@property (nonatomic, strong) NSStatusItem* statusItem;
@property (nonatomic, strong) NSMenu* menu;
@property (nonatomic, strong) NSMutableArray<HermesStatusIconActionHandler*>* actionHandlers;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenuItem*>* itemsById;
@property (nonatomic, strong) NSMutableDictionary<NSString*, NSMenu*>* submenusById;

- (instancetype)initWithMenuCallback:(MenuItemCallback)menuCallback
                       clickCallback:(InvokeCallback)clickCallback;

#pragma mark - Lifecycle

- (void)show;
- (void)hide;

#pragma mark - Icon

- (void)setIconFromPath:(NSString*)filePath;
- (void)setIconFromData:(const void*)data length:(int)length;
- (void)setTooltip:(NSString*)tooltip;
- (void)getScreenPosition:(int*)x y:(int*)y width:(int*)width height:(int*)height;

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

/// Action handler for status icon menu items
@interface HermesStatusIconActionHandler : NSObject

@property (nonatomic, weak) HermesStatusIcon* statusIcon;
@property (nonatomic, copy) NSString* itemId;

- (void)menuItemClicked:(id)sender;

@end

#endif // HERMES_STATUS_ICON_H
