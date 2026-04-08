// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_APP_DELEGATE_H
#define HERMES_APP_DELEGATE_H

#import <Cocoa/Cocoa.h>

@class HermesDockMenu;

@interface HermesAppDelegate : NSObject <NSApplicationDelegate>

/// The dock menu shown when right-clicking the app's dock icon.
/// Custom items appear above the default macOS entries.
@property (nonatomic, strong) HermesDockMenu* dockMenu;

/// When YES, the app runs as an accessory (no dock icon, no auto-terminate on last window close).
@property (nonatomic) BOOL accessoryMode;

@end

#endif
