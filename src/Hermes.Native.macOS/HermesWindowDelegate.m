// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
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
        if (_hermesWindow.onClosing) {
            _hermesWindow.onClosing();
        }
    }
}

- (BOOL)windowShouldClose:(NSWindow*)sender {
    // Allow close by default
    return YES;
}

@end
