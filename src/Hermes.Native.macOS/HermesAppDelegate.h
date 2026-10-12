// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HERMES_APP_DELEGATE_H
#define HERMES_APP_DELEGATE_H

#import <Cocoa/Cocoa.h>

@class HermesDockMenu;

/// NSApplication subclass that turns a quit request into a close of the run loop window, so the app
/// shuts down by returning from waitForClose instead of AppKit calling exit().
@interface HermesApplication : NSApplication
@end

@interface HermesAppDelegate : NSObject <NSApplicationDelegate>

/// The dock menu shown when right-clicking the app's dock icon.
/// Custom items appear above the default macOS entries.
@property (nonatomic, strong) HermesDockMenu* dockMenu;

@end

#endif
