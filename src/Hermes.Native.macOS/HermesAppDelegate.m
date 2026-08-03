// Copyright (c) Mythetech. Licensed under the MIT License.
#import "HermesAppDelegate.h"
#import "HermesDockMenu.h"

@implementation HermesAppDelegate

- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication*)sender {
    return !self.accessoryMode;
}

- (NSMenu*)applicationDockMenu:(NSApplication*)sender {
    if (_dockMenu) {
        return [_dockMenu buildMenu];
    }
    return nil;
}

@end
