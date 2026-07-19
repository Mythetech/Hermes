// Copyright (c) Mythetech. Licensed under the MIT License.
#import "HermesWindowDelegate.h"
#import "HermesWindow.h"

@implementation HermesWindowDelegate

- (void)windowDidResize:(NSNotification*)notification {
    if (_hermesWindow && _hermesWindow.onResized) {
        int width, height;
        [_hermesWindow getSize:&width height:&height];
        _hermesWindow.onResized(width, height);
    }
}

- (void)windowDidMove:(NSNotification*)notification {
    if (_hermesWindow && _hermesWindow.onMoved) {
        int x, y;
        [_hermesWindow getPosition:&x y:&y];
        _hermesWindow.onMoved(x, y);
    }
}

- (void)windowDidBecomeKey:(NSNotification*)notification {
    if (_hermesWindow && _hermesWindow.onFocusIn) {
        _hermesWindow.onFocusIn();
    }
}

- (void)windowDidResignKey:(NSNotification*)notification {
    if (_hermesWindow && _hermesWindow.onFocusOut) {
        _hermesWindow.onFocusOut();
    }
}

- (void)windowWillClose:(NSNotification*)notification {
    if (_hermesWindow) {
        _hermesWindow.isRunning = NO;
    }
}

- (BOOL)windowShouldClose:(NSWindow*)sender {
    if (_hermesWindow && _hermesWindow.onClosing) {
        bool shouldClose = _hermesWindow.onClosing();
        return shouldClose ? YES : NO;
    }
    return YES;
}

@end
