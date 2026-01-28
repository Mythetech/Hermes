#import "HermesWindow.h"
#import "HermesWindowDelegate.h"
#import "HermesUiDelegate.h"
#import "HermesUrlSchemeHandler.h"
#import <pthread.h>

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
        }

        NSRect frame = NSMakeRect(0, 0, params->Width, params->Height);
        _window = [[NSWindow alloc] initWithContentRect:frame
                                              styleMask:styleMask
                                                backing:NSBackingStoreBuffered
                                                  defer:YES];

        if (params->Title) {
            [_window setTitle:[NSString stringWithUTF8String:params->Title]];
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
        "};";

    WKUserScript* userScript = [[WKUserScript alloc] initWithSource:initScript
                                                      injectionTime:WKUserScriptInjectionTimeAtDocumentStart
                                                   forMainFrameOnly:YES];

    WKUserContentController* contentController = [[WKUserContentController alloc] init];
    [contentController addUserScript:userScript];

    _uiDelegate = [[HermesUiDelegate alloc] init];
    _uiDelegate.hermesWindow = self;
    [contentController addScriptMessageHandler:_uiDelegate name:@"hermesinterop"];

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

#pragma mark - Cleanup

- (void)dealloc {
    [_webViewConfiguration.userContentController removeScriptMessageHandlerForName:@"hermesinterop"];
    [_webView removeFromSuperview];
}

@end
