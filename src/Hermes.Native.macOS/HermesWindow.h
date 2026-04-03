// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_WINDOW_H
#define HERMES_WINDOW_H

#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import "HermesTypes.h"

@class HermesWindowDelegate;
@class HermesUiDelegate;
@class HermesUrlSchemeHandler;

@interface HermesWindow : NSObject

@property (nonatomic, strong) NSWindow* window;
@property (nonatomic, strong) WKWebView* webView;
@property (nonatomic, strong) WKWebViewConfiguration* webViewConfiguration;
@property (nonatomic, strong) HermesWindowDelegate* windowDelegate;
@property (nonatomic, strong) HermesUiDelegate* uiDelegate;
@property (nonatomic, strong) NSMutableArray<HermesUrlSchemeHandler*>* schemeHandlers;
@property (nonatomic, strong) NSMutableArray<NSString*>* pendingSchemes;

// Callbacks
@property (nonatomic, assign) ClosingCallback onClosing;
@property (nonatomic, assign) ResizedCallback onResized;
@property (nonatomic, assign) MovedCallback onMoved;
@property (nonatomic, assign) FocusCallback onFocusIn;
@property (nonatomic, assign) FocusCallback onFocusOut;
@property (nonatomic, assign) WebMessageCallback onWebMessage;
@property (nonatomic, assign) CustomSchemeCallback onCustomScheme;
@property (nonatomic, assign) WebViewCrashCallback onWebViewCrash;

// State
@property (nonatomic, assign) int64_t uiThreadId;
@property (nonatomic, assign) BOOL isRunning;
@property (nonatomic, assign) NSRect premaximizedFrame;
@property (nonatomic, assign) BOOL devToolsEnabled;
@property (nonatomic, assign) BOOL customTitleBar;

// Drag tracking (hybrid approach - JS informs about no-drag regions)
@property (nonatomic, strong) id mouseDownMonitor;
@property (nonatomic, strong) id mouseDragMonitor;
@property (nonatomic, assign) BOOL isDragging;
@property (nonatomic, assign) BOOL potentialDrag;
@property (nonatomic, assign) NSPoint dragStartWindowOrigin;
@property (nonatomic, assign) NSPoint dragStartMouseLocation;
@property (nonatomic, assign) NSTimeInterval lastClickTime;
@property (nonatomic, assign) NSPoint lastClickLocation;
@property (nonatomic, assign) BOOL clickIsInNoDragRegion;

// Initialization
- (instancetype)initWithParams:(const HermesWindowParams*)params;

// Lifecycle
- (void)show;
- (void)close;
- (void)waitForClose;

// Properties
- (NSString*)title;
- (void)setTitle:(NSString*)title;
- (void)getSize:(int*)width height:(int*)height;
- (void)setWidth:(int)width height:(int)height;
- (void)getPosition:(int*)x y:(int*)y;
- (void)setPositionX:(int)x y:(int)y;
- (BOOL)isMaximized;
- (void)setIsMaximized:(BOOL)maximized;
- (BOOL)isMinimized;
- (void)setIsMinimized:(BOOL)minimized;

// WebView
- (void)navigateToUrl:(NSString*)url;
- (void)navigateToString:(NSString*)html;
- (void)sendWebMessage:(NSString*)message;
- (void)registerCustomScheme:(NSString*)scheme;

// Threading
- (void)invoke:(InvokeCallback)callback;
- (void)beginInvoke:(InvokeCallback)callback;

// Internal
- (void)attachWebView;
- (CGFloat)convertYFromTopLeft:(CGFloat)y height:(CGFloat)height;
- (CGFloat)convertYToTopLeft:(CGFloat)y height:(CGFloat)height;

// Drag support
- (void)setupDragMonitors;
- (void)teardownDragMonitors;

@end

#endif // HERMES_WINDOW_H
