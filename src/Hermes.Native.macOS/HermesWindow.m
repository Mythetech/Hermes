#import "HermesWindow.h"
#import "HermesWindowDelegate.h"
#import "HermesUiDelegate.h"
#import "HermesUrlSchemeHandler.h"
#import <pthread.h>
#import <QuartzCore/QuartzCore.h>

@implementation HermesWindow

- (instancetype)initWithParams:(const HermesWindowParams*)params {
    self = [super init];
    if (self) {
        _uiThreadId = (int64_t)pthread_mach_thread_np(pthread_self());
        _onClosing = params->OnClosing;
        _onResized = params->OnResized;
        _onMoved = params->OnMoved;
        _onFocusIn = params->OnFocusIn;
        _onFocusOut = params->OnFocusOut;
        _onWebMessage = params->OnWebMessage;
        _onCustomScheme = params->OnCustomScheme;
        _schemeHandlers = [NSMutableArray new];
        _pendingSchemes = [NSMutableArray new];

        NSWindowStyleMask styleMask;
        if (params->Chromeless) {
            styleMask = NSWindowStyleMaskBorderless;
        } else {
            styleMask = NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable;
            if (params->Resizable) {
                styleMask |= NSWindowStyleMaskResizable;
            }
            // CustomTitleBar: extend content under title bar while keeping traffic lights
            if (params->CustomTitleBar) {
                styleMask |= NSWindowStyleMaskFullSizeContentView;
            }
        }

        NSRect frame = NSMakeRect(0, 0, params->Width, params->Height);
        _window = [[NSWindow alloc] initWithContentRect:frame
                                              styleMask:styleMask
                                                backing:NSBackingStoreBuffered
                                                  defer:YES];

        if (params->Title) {
            [_window setTitle:[NSString stringWithUTF8String:params->Title]];
        }

        // CustomTitleBar: make title bar transparent and hide title text
        if (params->CustomTitleBar && !params->Chromeless) {
            [_window setTitlebarAppearsTransparent:YES];
            [_window setTitleVisibility:NSWindowTitleHidden];
            // Remove the title bar separator line
            if (@available(macOS 11.0, *)) {
                [_window setTitlebarSeparatorStyle:NSTitlebarSeparatorStyleNone];
            }

            // Position traffic light buttons for custom title bar
            // Delay to allow macOS to finish setting up the title bar
            __weak typeof(self) weakSelf = self;
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.05 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                [weakSelf repositionTrafficLightButtons];
            });
        }

        if (params->UsePosition) {
            CGFloat y = [self convertYFromTopLeft:params->Y height:params->Height];
            [_window setFrameOrigin:NSMakePoint(params->X, y)];
        } else if (params->CenterOnScreen) {
            [_window center];
        }

        if (params->MinWidth > 0 && params->MinHeight > 0) {
            [_window setMinSize:NSMakeSize(params->MinWidth, params->MinHeight)];
        }
        if (params->MaxWidth > 0 && params->MaxHeight > 0) {
            [_window setMaxSize:NSMakeSize(params->MaxWidth, params->MaxHeight)];
        }

        if (params->TopMost) {
            [_window setLevel:NSFloatingWindowLevel];
        }

        _windowDelegate = [[HermesWindowDelegate alloc] init];
        _windowDelegate.hermesWindow = self;
        [_window setDelegate:_windowDelegate];

        _webViewConfiguration = [[WKWebViewConfiguration alloc] init];
        _devToolsEnabled = params->DevToolsEnabled;
        _customTitleBar = params->CustomTitleBar;

        WKPreferences* prefs = _webViewConfiguration.preferences;
        if (params->DevToolsEnabled) {
            @try {
                [prefs setValue:@YES forKey:@"developerExtrasEnabled"];
            } @catch (NSException *e) {
                NSLog(@"[Hermes] Failed to enable developerExtrasEnabled: %@", [e reason]);
            }
            @try {
                [prefs setValue:@YES forKey:@"javaScriptCanAccessClipboard"];
            } @catch (NSException *e) {
                NSLog(@"[Hermes] Failed to enable javaScriptCanAccessClipboard: %@", [e reason]);
            }
            @try {
                [prefs setValue:@YES forKey:@"domPasteAllowed"];
            } @catch (NSException *e) {
                NSLog(@"[Hermes] Failed to enable domPasteAllowed: %@", [e reason]);
            }
        }

        for (int i = 0; i < 16; i++) {
            if (params->CustomSchemeNames[i] != NULL) {
                NSString* scheme = [NSString stringWithUTF8String:params->CustomSchemeNames[i]];
                [_pendingSchemes addObject:scheme];
            }
        }

        NSString* startUrl = params->StartUrl ? [NSString stringWithUTF8String:params->StartUrl] : nil;
        NSString* startHtml = params->StartHtml ? [NSString stringWithUTF8String:params->StartHtml] : nil;

        [self attachWebView];

        // Window dragging is handled via JavaScript using -webkit-app-region CSS.
        // The JS handler in attachWebView respects drag/no-drag regions,
        // allowing interactive elements (buttons, menus) to receive clicks.

        if (startUrl) {
            [self navigateToUrl:startUrl];
        } else if (startHtml) {
            [self navigateToString:startHtml];
        }

        _premaximizedFrame = frame;
        if (params->Maximized) {
            [_window zoom:nil];
        }
        if (params->Minimized) {
            [_window miniaturize:nil];
        }

        // Set up native drag monitors for custom title bar
        [self setupDragMonitors];
    }
    return self;
}

- (void)attachWebView {
    // JavaScript for message passing and drag region detection
    // The drag detection informs native code whether to handle drag/zoom
    NSString* initScript = @"window.__receiveMessageCallbacks = [];"
        "window.__dispatchMessageCallback = function(message) {"
        "    window.__receiveMessageCallbacks.forEach(function(callback) { callback(message); });"
        "};"
        "window.external = {"
        "    sendMessage: function(message) {"
        "        window.webkit.messageHandlers.hermesinterop.postMessage(message);"
        "    },"
        "    receiveMessage: function(callback) {"
        "        window.__receiveMessageCallbacks.push(callback);"
        "    }"
        "};"
        // Helper to check if element is in a no-drag region
        "function __hermesIsNoDragRegion(el) {"
        "    while (el && el !== document.body && el !== document.documentElement) {"
        "        var style = window.getComputedStyle(el);"
        "        var region = style.getPropertyValue('-webkit-app-region') || style.getPropertyValue('app-region');"
        "        if (region === 'no-drag') return true;"
        "        if (region === 'drag') return false;"
        "        el = el.parentElement;"
        "    }"
        "    return false;"
        "}"
        // Track click timing for double-click detection (only for drag regions)
        "window.__hermesDragClick = { time: 0, x: 0, y: 0 };"
        // Listen for mousedown to inform native about drag regions
        "document.addEventListener('mousedown', function(e) {"
        "    if (e.button !== 0) return;"
        "    if (__hermesIsNoDragRegion(e.target)) {"
        "        window.webkit.messageHandlers.hermesDragRegion.postMessage('no-drag');"
        "        window.__hermesDragClick = { time: 0, x: 0, y: 0 };"
        "        return;"
        "    }"
        "    var now = Date.now();"
        "    var dx = e.screenX - window.__hermesDragClick.x;"
        "    var dy = e.screenY - window.__hermesDragClick.y;"
        "    var dist = Math.sqrt(dx*dx + dy*dy);"
        "    if ((now - window.__hermesDragClick.time) < 300 && dist < 10) {"
        "        window.webkit.messageHandlers.hermesDragRegion.postMessage('double-click');"
        "        window.__hermesDragClick = { time: 0, x: 0, y: 0 };"
        "    } else {"
        "        window.webkit.messageHandlers.hermesDragRegion.postMessage('drag');"
        "        window.__hermesDragClick = { time: now, x: e.screenX, y: e.screenY };"
        "    }"
        "}, true);";

    WKUserScript* userScript = [[WKUserScript alloc] initWithSource:initScript
                                                      injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                   forMainFrameOnly:YES];

    WKUserContentController* contentController = [[WKUserContentController alloc] init];
    [contentController addUserScript:userScript];

    _uiDelegate = [[HermesUiDelegate alloc] init];
    _uiDelegate.hermesWindow = self;
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesinterop"];
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesDragRegion"];

    _webViewConfiguration.userContentController = contentController;

    for (NSString* scheme in _pendingSchemes) {
        HermesUrlSchemeHandler* handler = [[HermesUrlSchemeHandler alloc] init];
        handler.callback = _onCustomScheme;
        [_schemeHandlers addObject:handler];
        [_webViewConfiguration setURLSchemeHandler:handler forURLScheme:scheme];
    }
    [_pendingSchemes removeAllObjects];

    _webView = [[WKWebView alloc] initWithFrame:_window.contentView.bounds
                                  configuration:_webViewConfiguration];

    if (_devToolsEnabled) {
        if (@available(macOS 13.3, *)) {
            _webView.inspectable = YES;
        }
    }

    [_webView setAutoresizingMask:NSViewWidthSizable | NSViewHeightSizable];
    _webView.UIDelegate = _uiDelegate;
    [_window.contentView addSubview:_webView];
}

#pragma mark - Lifecycle

- (void)show {
    [_window makeKeyAndOrderFront:nil];
    [NSApp activateIgnoringOtherApps:YES];
}

- (void)close {
    [_window close];
}

- (void)waitForClose {
    _isRunning = YES;
    [self show];
    [NSApp run];
}

#pragma mark - Properties

- (NSString*)title {
    return [_window title];
}

- (void)setTitle:(NSString*)title {
    [_window setTitle:title];
}

- (void)getSize:(int*)width height:(int*)height {
    NSRect frame = [_window frame];
    *width = (int)frame.size.width;
    *height = (int)frame.size.height;
}

- (void)setWidth:(int)width height:(int)height {
    NSRect frame = [_window frame];
    frame.size.width = width;
    frame.size.height = height;
    [_window setFrame:frame display:YES];
}

- (void)getPosition:(int*)x y:(int*)y {
    NSRect frame = [_window frame];
    *x = (int)frame.origin.x;
    *y = (int)[self convertYToTopLeft:frame.origin.y height:frame.size.height];
}

- (void)setPositionX:(int)x y:(int)y {
    NSRect frame = [_window frame];
    CGFloat convertedY = [self convertYFromTopLeft:y height:frame.size.height];
    [_window setFrameOrigin:NSMakePoint(x, convertedY)];
}

- (BOOL)isMaximized {
    return [_window isZoomed];
}

- (void)setIsMaximized:(BOOL)maximized {
    if (maximized != [_window isZoomed]) {
        if (maximized) {
            _premaximizedFrame = [_window frame];
        }
        [_window zoom:nil];
    }
}

- (BOOL)isMinimized {
    return [_window isMiniaturized];
}

- (void)setIsMinimized:(BOOL)minimized {
    if (minimized && ![_window isMiniaturized]) {
        [_window miniaturize:nil];
    } else if (!minimized && [_window isMiniaturized]) {
        [_window deminiaturize:nil];
    }
}

#pragma mark - WebView

- (void)navigateToUrl:(NSString*)url {
    NSURL* nsUrl = [NSURL URLWithString:url];
    if (nsUrl) {
        NSURLRequest* request = [NSURLRequest requestWithURL:nsUrl];
        [_webView loadRequest:request];
    }
}

- (void)navigateToString:(NSString*)html {
    [_webView loadHTMLString:html baseURL:nil];
}

- (void)sendWebMessage:(NSString*)message {
    NSData* data = [NSJSONSerialization dataWithJSONObject:@[message]
                                                   options:0
                                                     error:nil];
    NSString* jsonMessage = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    jsonMessage = [[jsonMessage substringToIndex:([jsonMessage length] - 1)] substringFromIndex:1];

    NSString* script = [NSString stringWithFormat:@"__dispatchMessageCallback(%@)", jsonMessage];
    [_webView evaluateJavaScript:script completionHandler:nil];
}

- (void)registerCustomScheme:(NSString*)scheme {
    if (_webView)
        return;
    [_pendingSchemes addObject:scheme];
}

#pragma mark - Threading

- (void)invoke:(InvokeCallback)callback {
    if ([NSThread isMainThread]) {
        callback();
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            callback();
        });
    }
}

- (void)beginInvoke:(InvokeCallback)callback {
    dispatch_async(dispatch_get_main_queue(), ^{
        callback();
    });
}

#pragma mark - Coordinate Conversion

- (CGFloat)convertYFromTopLeft:(CGFloat)y height:(CGFloat)height {
    NSScreen* screen = [NSScreen mainScreen];
    CGFloat screenHeight = screen.frame.size.height;
    return screenHeight - y - height;
}

- (CGFloat)convertYToTopLeft:(CGFloat)y height:(CGFloat)height {
    NSScreen* screen = [NSScreen mainScreen];
    CGFloat screenHeight = screen.frame.size.height;
    return screenHeight - y - height;
}

#pragma mark - Traffic Light Buttons

- (void)repositionTrafficLightButtons {
    if (!_customTitleBar) return;

    NSButton* closeButton = [_window standardWindowButton:NSWindowCloseButton];
    if (!closeButton) return;

    // Get the buttons' container view
    NSView* containerView = closeButton.superview;
    if (!containerView) return;

    NSView* titleBarView = containerView.superview;
    if (!titleBarView) return;

    CGFloat leftInset = 7.0;   // Extra left margin
    CGFloat topInset = 3.0;    // Move down to center in 38px title bar

    // Remove ALL existing constraints on the container
    [containerView removeConstraints:containerView.constraints];

    // Remove constraints from parent that reference the container
    NSMutableArray* constraintsToRemove = [NSMutableArray array];
    for (NSLayoutConstraint* constraint in titleBarView.constraints) {
        if (constraint.firstItem == containerView || constraint.secondItem == containerView) {
            [constraintsToRemove addObject:constraint];
        }
    }
    [titleBarView removeConstraints:constraintsToRemove];

    // Store current size before changing to Auto Layout
    CGSize containerSize = containerView.frame.size;

    // Use Auto Layout with our own constraints
    containerView.translatesAutoresizingMaskIntoConstraints = NO;

    // Pin to left edge with offset
    NSLayoutConstraint* leftConstraint = [NSLayoutConstraint constraintWithItem:containerView
                                                                      attribute:NSLayoutAttributeLeading
                                                                      relatedBy:NSLayoutRelationEqual
                                                                         toItem:titleBarView
                                                                      attribute:NSLayoutAttributeLeading
                                                                     multiplier:1.0
                                                                       constant:leftInset];
    leftConstraint.priority = NSLayoutPriorityRequired;

    // Pin to top edge with offset
    NSLayoutConstraint* topConstraint = [NSLayoutConstraint constraintWithItem:containerView
                                                                     attribute:NSLayoutAttributeTop
                                                                     relatedBy:NSLayoutRelationEqual
                                                                        toItem:titleBarView
                                                                     attribute:NSLayoutAttributeTop
                                                                    multiplier:1.0
                                                                      constant:topInset];
    topConstraint.priority = NSLayoutPriorityRequired;

    // Preserve width
    NSLayoutConstraint* widthConstraint = [NSLayoutConstraint constraintWithItem:containerView
                                                                       attribute:NSLayoutAttributeWidth
                                                                       relatedBy:NSLayoutRelationEqual
                                                                          toItem:nil
                                                                       attribute:NSLayoutAttributeNotAnAttribute
                                                                      multiplier:1.0
                                                                        constant:containerSize.width];
    widthConstraint.priority = NSLayoutPriorityRequired;

    // Preserve height
    NSLayoutConstraint* heightConstraint = [NSLayoutConstraint constraintWithItem:containerView
                                                                        attribute:NSLayoutAttributeHeight
                                                                        relatedBy:NSLayoutRelationEqual
                                                                           toItem:nil
                                                                        attribute:NSLayoutAttributeNotAnAttribute
                                                                       multiplier:1.0
                                                                         constant:containerSize.height];
    heightConstraint.priority = NSLayoutPriorityRequired;

    [containerView addConstraints:@[widthConstraint, heightConstraint]];
    [titleBarView addConstraints:@[leftConstraint, topConstraint]];
}

#pragma mark - Drag Support

- (void)setupDragMonitors {
    if (!_customTitleBar) return;

    __weak typeof(self) weakSelf = self;
    CGFloat titleBarHeight = 38.0;
    CGFloat dragThreshold = 3.0;

    // Monitor mouseDown to set up potential drag
    // JavaScript will inform us if click was on a no-drag region (and cancel drag)
    // JavaScript handles double-click detection for zoom (so buttons don't trigger zoom)
    _mouseDownMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskLeftMouseDown
                                                              handler:^NSEvent*(NSEvent* event) {
        __strong typeof(weakSelf) strongSelf = weakSelf;
        if (!strongSelf || event.window != strongSelf.window) return event;

        // Check if click is in title bar area (top of window)
        NSPoint windowPoint = event.locationInWindow;
        CGFloat windowHeight = strongSelf.window.frame.size.height;
        CGFloat distanceFromTop = windowHeight - windowPoint.y;

        if (distanceFromTop > titleBarHeight) {
            strongSelf.clickIsInNoDragRegion = NO;
            return event;  // Not in title bar
        }

        // Reset flag - JS will set it if click is on no-drag region
        strongSelf.clickIsInNoDragRegion = NO;

        // Set up potential drag (JS will cancel if needed)
        strongSelf.potentialDrag = YES;
        strongSelf.dragStartWindowOrigin = strongSelf.window.frame.origin;
        strongSelf.dragStartMouseLocation = [NSEvent mouseLocation];

        return event;  // Let event through
    }];

    // Monitor mouseDragged to actually move the window
    _mouseDragMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:(NSEventMaskLeftMouseDragged | NSEventMaskLeftMouseUp)
                                                              handler:^NSEvent*(NSEvent* event) {
        __strong typeof(weakSelf) strongSelf = weakSelf;
        if (!strongSelf) return event;

        if (event.type == NSEventTypeLeftMouseUp) {
            strongSelf.potentialDrag = NO;
            strongSelf.isDragging = NO;
            strongSelf.clickIsInNoDragRegion = NO;
            return event;
        }

        // MouseDragged
        if (!strongSelf.potentialDrag && !strongSelf.isDragging) {
            return event;
        }

        NSPoint currentMouse = [NSEvent mouseLocation];
        CGFloat dx = currentMouse.x - strongSelf.dragStartMouseLocation.x;
        CGFloat dy = currentMouse.y - strongSelf.dragStartMouseLocation.y;
        CGFloat dist = sqrt(dx*dx + dy*dy);

        // Start dragging if we've moved beyond threshold
        if (strongSelf.potentialDrag && dist >= dragThreshold) {
            strongSelf.potentialDrag = NO;
            strongSelf.isDragging = YES;
        }

        if (strongSelf.isDragging) {
            NSPoint newOrigin = NSMakePoint(strongSelf.dragStartWindowOrigin.x + dx,
                                            strongSelf.dragStartWindowOrigin.y + dy);
            [strongSelf.window setFrameOrigin:newOrigin];
        }

        return event;
    }];
}

- (void)teardownDragMonitors {
    if (_mouseDownMonitor) {
        [NSEvent removeMonitor:_mouseDownMonitor];
        _mouseDownMonitor = nil;
    }
    if (_mouseDragMonitor) {
        [NSEvent removeMonitor:_mouseDragMonitor];
        _mouseDragMonitor = nil;
    }
    _isDragging = NO;
    _potentialDrag = NO;
    _clickIsInNoDragRegion = NO;
}

#pragma mark - Cleanup

- (void)dealloc {
    [self teardownDragMonitors];
    [[NSNotificationCenter defaultCenter] removeObserver:self];
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesinterop"];
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesDragRegion"];
    [_webView removeFromSuperview];
}

@end
