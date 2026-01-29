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

        // Set up event monitor for custom title bar dragging and double-click to zoom
        if (_customTitleBar && !params->Chromeless) {
            __weak typeof(self) weakSelf = self;
            _dragEventMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskLeftMouseDown
                                                                      handler:^NSEvent*(NSEvent* event) {
                __strong typeof(weakSelf) strongSelf = weakSelf;
                if (!strongSelf || event.window != strongSelf.window) {
                    return event;
                }

                // Get click location in window coordinates
                NSPoint locationInWindow = event.locationInWindow;
                NSRect contentRect = strongSelf.window.contentView.frame;

                // Title bar is at the top 38 pixels (matching CSS)
                CGFloat titleBarHeight = 38.0;
                CGFloat titleBarTop = contentRect.size.height;
                CGFloat titleBarBottom = titleBarTop - titleBarHeight;

                // Check if click is in title bar region
                if (locationInWindow.y >= titleBarBottom && locationInWindow.y <= titleBarTop) {
                    // Skip traffic light area (first 78 pixels)
                    if (locationInWindow.x >= 78) {
                        if (event.clickCount == 2) {
                            // Double-click: toggle zoom (maximize/restore)
                            [strongSelf.window zoom:nil];
                        } else {
                            // Single click: start window drag
                            [strongSelf.window performWindowDragWithEvent:event];
                        }
                        return nil;  // Consume the event
                    }
                }

                return event;  // Pass through
            }];
        }

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
    }
    return self;
}

- (void)attachWebView {
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
        // Window drag support for -webkit-app-region: drag
        "document.addEventListener('mousedown', function(e) {"
        "    var el = e.target;"
        "    while (el) {"
        "        var style = window.getComputedStyle(el);"
        "        var region = style.getPropertyValue('-webkit-app-region') || style.getPropertyValue('app-region');"
        "        if (region === 'no-drag') return;"
        "        if (region === 'drag') {"
        "            window.webkit.messageHandlers.hermesWindowDrag.postMessage('drag');"
        "            return;"
        "        }"
        "        el = el.parentElement;"
        "    }"
        "});";

    WKUserScript* userScript = [[WKUserScript alloc] initWithSource:initScript
                                                      injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                   forMainFrameOnly:YES];

    WKUserContentController* contentController = [[WKUserContentController alloc] init];
    [contentController addUserScript:userScript];

    _uiDelegate = [[HermesUiDelegate alloc] init];
    _uiDelegate.hermesWindow = self;
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesinterop"];
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesWindowDrag"];

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

#pragma mark - Cleanup

- (void)dealloc {
    [[NSNotificationCenter defaultCenter] removeObserver:self];
    if (_dragEventMonitor) {
        [NSEvent removeMonitor:_dragEventMonitor];
        _dragEventMonitor = nil;
    }
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesinterop"];
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesWindowDrag"];
    [_webView removeFromSuperview];
}

@end
