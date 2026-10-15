// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HERMES_WEB_VIEW_H
#define HERMES_WEB_VIEW_H

#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import "HermesTypes.h"

/// The web view of every Hermes window. It behaves exactly like WKWebView unless drag and drop is enabled.
@interface HermesWebView : WKWebView

/// Captures what other apps drop on the view, including the given custom MIME types. Call once, after creation.
- (void)enableDragDropWithCustomTypes:(NSArray<NSString*>*)mimeTypes;

/// Moves the latest unexpired capture into package, which then owns its memory. Returns NO when there is none.
- (BOOL)takeDropCapture:(HermesNativePackage*)package;

/// Starts a drag of the package offering Copy, from a left mouse event of the press in progress.
/// ended runs once on the main thread, when the drag ends or when cancelDrag runs first.
- (BOOL)startDragWithPackage:(const HermesNativePackage*)package event:(NSEvent*)event ended:(DragEndedCallback)ended;

/// Ends the active drag as canceled without waiting for AppKit, which a closing window cannot do.
- (void)cancelDrag;

@end

#endif // HERMES_WEB_VIEW_H
