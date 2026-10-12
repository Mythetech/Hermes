// Copyright (c) Mythetech. Licensed under the MIT License.
#import "HermesAppDelegate.h"
#import "HermesDockMenu.h"
#import "HermesWindow.h"

@implementation HermesApplication

// AppKit's terminate: ends the process with exit() from inside the run loop, so waitForClose never
// returns and nothing after it in the app runs (hosted service shutdown, dispose, the exit code). While
// a window's waitForClose owns the run loop, a quit request (the Quit menu item, the Dock, a quit Apple
// event, logout) closes that window instead, which ends the loop and lets the process exit on its own.
- (void)terminate:(id)sender {
    HermesWindow* runLoopWindow = [HermesWindow runLoopWindow];
    if (runLoopWindow) {
        [runLoopWindow closeForQuit];
        return;
    }

    [super terminate:sender];
}

@end

@implementation HermesAppDelegate

// The run loop ends when the window that waitForClose is running for closes, and the process then exits
// by returning from the app's Main. Letting AppKit terminate here would call exit() before any of that.
- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication*)sender {
    return NO;
}

- (NSMenu*)applicationDockMenu:(NSApplication*)sender {
    if (_dockMenu) {
        return [_dockMenu buildMenu];
    }
    return nil;
}

@end
