#import "HermesAppDelegate.h"
#import "HermesDockMenu.h"

@implementation HermesAppDelegate

- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication*)sender {
    return YES;
}

- (NSMenu*)applicationDockMenu:(NSApplication*)sender {
    if (_dockMenu) {
        return [_dockMenu buildMenu];
    }
    return nil;
}

@end
