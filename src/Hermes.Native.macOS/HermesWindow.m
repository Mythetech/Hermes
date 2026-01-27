#import "HermesWindow.h"
#import "HermesWindowDelegate.h"
#import "HermesUiDelegate.h"
#import "HermesUrlSchemeHandler.h"
#import <pthread.h>

@implementation HermesWindow

- (instancetype)initWithParams:(const HermesWindowParams*)params {
    self = [super init];
    if (self) {
        // Store UI thread ID
        _uiThreadId = (int64_t)pthread_mach_thread_np(pthread_self());

        // Store callbacks
        _onClosing = params->OnClosing;
        _onResized = params->OnResized;
        _onMoved = params->OnMoved;
        _onFocusIn = params->OnFocusIn;
        _onFocusOut = params->OnFocusOut;
        _onWebMessage = params->OnWebMessage;
        _onCustomScheme = params->OnCustomScheme;

        // Initialize collections
        _schemeHandlers = [NSMutableArray new];
        _pendingSchemes = [NSMutableArray new];

        // Determine window style
        NSWindowStyleMask styleMask;
        if (params->Chromeless) {
            styleMask = NSWindowStyleMaskBorderless;
        } else {
            styleMask = NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable;
            if (params->Resizable) {
                styleMask |= NSWindowStyleMaskResizable;
            }
        }

        // Calculate frame
        NSRect frame = NSMakeRect(0, 0, params->Width, params->Height);

        // Create window
        _window = [[NSWindow alloc] initWithContentRect:frame
                                              styleMask:styleMask
                                                backing:NSBackingStoreBuffered
                                                  defer:YES];

        // Set window properties
        if (params->Title) {
            [_window setTitle:[NSString stringWithUTF8String:params->Title]];
        }

        // Position window
        if (params->UsePosition) {
            CGFloat y = [self convertYFromTopLeft:params->Y height:params->Height];
            [_window setFrameOrigin:NSMakePoint(params->X, y)];
        } else if (params->CenterOnScreen) {
            [_window center];
        }

        // Size constraints
        if (params->MinWidth > 0 && params->MinHeight > 0) {
            [_window setMinSize:NSMakeSize(params->MinWidth, params->MinHeight)];
        }
        if (params->MaxWidth > 0 && params->MaxHeight > 0) {
            [_window setMaxSize:NSMakeSize(params->MaxWidth, params->MaxHeight)];
        }

        // TopMost
        if (params->TopMost) {
            [_window setLevel:NSFloatingWindowLevel];
        }

        // Set up window delegate
        _windowDelegate = [[HermesWindowDelegate alloc] init];
        _windowDelegate.hermesWindow = self;
        [_window setDelegate:_windowDelegate];

        // Create WebView configuration
        _webViewConfiguration = [[WKWebViewConfiguration alloc] init];

        // Set WebView preferences
        WKPreferences* prefs = _webViewConfiguration.preferences;
        if (params->DevToolsEnabled) {
            // Enable developer extras
            [prefs setValue:@YES forKey:@"developerExtrasEnabled"];
        }

        // Store initial content for later
        NSString* startUrl = params->StartUrl ? [NSString stringWithUTF8String:params->StartUrl] : nil;
        NSString* startHtml = params->StartHtml ? [NSString stringWithUTF8String:params->StartHtml] : nil;

        // Attach WebView
        [self attachWebView];

        // Load initial content
        if (startUrl) {
            [self navigateToUrl:startUrl];
        } else if (startHtml) {
            [self navigateToString:startHtml];
        }

        // Handle initial state
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
    // Inject JavaScript bridge
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
        "};";

    WKUserScript* userScript = [[WKUserScript alloc] initWithSource:initScript
                                                      injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                   forMainFrameOnly:YES];

    WKUserContentController* contentController = [[WKUserContentController alloc] init];
    [contentController addUserScript:userScript];

    // Set up UI delegate for message handling
    _uiDelegate = [[HermesUiDelegate alloc] init];
    _uiDelegate.hermesWindow = self;
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesinterop"];

    _webViewConfiguration.userContentController = contentController;

    // Register any pending custom schemes
    for (NSString* scheme in _pendingSchemes) {
        HermesUrlSchemeHandler* handler = [[HermesUrlSchemeHandler alloc] init];
        handler.callback = _onCustomScheme;
        [_schemeHandlers addObject:handler];
        [_webViewConfiguration setURLSchemeHandler:handler forURLScheme:scheme];
    }
    [_pendingSchemes removeAllObjects];

    // Create WebView
    _webView = [[WKWebView alloc] initWithFrame:_window.contentView.bounds
                                  configuration:_webViewConfiguration];

    [_webView setAutoresizingMask:NSViewWidthSizable | NSViewHeightSizable];
    _webView.UIDelegate = _uiDelegate;

    // Add to window
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

    // Run the main event loop
    while (_isRunning && [_window isVisible]) {
        @autoreleasepool {
            NSEvent* event = [NSApp nextEventMatchingMask:NSEventMaskAny
                                                untilDate:[NSDate distantFuture]
                                                   inMode:NSDefaultRunLoopMode
                                                  dequeue:YES];
            if (event) {
                [NSApp sendEvent:event];
            }
        }
    }
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
    // JSON-encode the message in an array to safely escape special characters
    NSData* data = [NSJSONSerialization dataWithJSONObject:@[message]
                                                   options:0
                                                     error:nil];
    NSString* jsonMessage = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];

    // Remove the array brackets to get just the escaped string
    jsonMessage = [[jsonMessage substringToIndex:([jsonMessage length] - 1)] substringFromIndex:1];

    NSString* script = [NSString stringWithFormat:@"__dispatchMessageCallback(%@)", jsonMessage];
    [_webView evaluateJavaScript:script completionHandler:nil];
}

- (void)registerCustomScheme:(NSString*)scheme {
    // If WebView is already created, we can't add more schemes
    if (_webView) {
        NSLog(@"Hermes: Cannot register custom scheme '%@' after WebView is created", scheme);
        return;
    }
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

// macOS uses bottom-left origin, but our API uses top-left
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

#pragma mark - Cleanup

- (void)dealloc {
    // Remove script message handler to break retain cycle
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesinterop"];

    [_webView removeFromSuperview];
}

@end
