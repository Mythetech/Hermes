// Copyright (c) Mythetech. Licensed under the MIT License.
#import "HermesWebView.h"
#import "HermesClipboard.h"

// The page claims a drop within milliseconds; a capture this old belongs to a drop the page ignored.
static const NSTimeInterval CaptureLifetime = 2.0;

@implementation HermesWebView {
    BOOL _dragDropEnabled;
    NSArray<NSString*>* _dropCustomTypes;
    HermesNativePackage _capture;
    BOOL _hasCapture;
    NSTimeInterval _capturedAt;
}

- (void)enableDragDropWithCustomTypes:(NSArray<NSString*>*)mimeTypes {
    _dragDropEnabled = YES;
    _dropCustomTypes = [mimeTypes copy];

    NSMutableArray<NSPasteboardType>* types = [NSMutableArray array];
    for (NSString* mimeType in mimeTypes) {
        [types addObject:HermesPasteboard_TypeForMime(mimeType)];
    }
    // Adds to WebKit's own types; AppKit never offers the view a drag that carries none of them.
    if (types.count > 0) {
        [self registerForDraggedTypes:types];
    }
}

- (BOOL)performDragOperation:(id<NSDraggingInfo>)sender {
    // Drags from this process have a source; the page handles its own drags without Hermes.
    if (_dragDropEnabled && sender.draggingSource == nil) {
        // The page claims each drop before the next can land, so an unclaimed capture is one the page ignored.
        [self clearDropCapture];
        @autoreleasepool {
            HermesPasteboard_ReadAll(sender.draggingPasteboard, _dropCustomTypes, &_capture);
        }
        _hasCapture = YES;
        _capturedAt = [NSProcessInfo processInfo].systemUptime;
    }
    return [super performDragOperation:sender];
}

- (BOOL)takeDropCapture:(HermesNativePackage*)package {
    memset(package, 0, sizeof(*package));
    if (!_hasCapture || [NSProcessInfo processInfo].systemUptime - _capturedAt > CaptureLifetime) {
        [self clearDropCapture];
        return NO;
    }
    *package = _capture;
    memset(&_capture, 0, sizeof(_capture));
    _hasCapture = NO;
    return YES;
}

- (void)clearDropCapture {
    HermesNativePackage_Free(&_capture);
    _hasCapture = NO;
}

- (void)dealloc {
    HermesNativePackage_Free(&_capture);
}

@end
