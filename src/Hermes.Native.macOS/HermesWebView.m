// Copyright (c) Mythetech. Licensed under the MIT License.
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#import "HermesWebView.h"
#import "HermesClipboard.h"

// The page claims a drop within milliseconds; a capture this old belongs to a drop the page ignored.
static const NSTimeInterval CaptureLifetime = 2.0;

// Source of the drags the app starts. Not the web view, which is already the source of the page's own drags.
@interface HermesDragSource : NSObject <NSDraggingSource>
@property (nonatomic, weak) HermesWebView* webView;
@end

@interface HermesWebView ()
- (void)dragSource:(HermesDragSource*)source endedWithOperation:(NSDragOperation)operation;
@end

// AppKit does not document that a session keeps its source alive, so the active source is held here.
static HermesDragSource* g_activeDragSource = nil;

@implementation HermesDragSource

- (NSDragOperation)draggingSession:(NSDraggingSession*)session sourceOperationMaskForDraggingContext:(NSDraggingContext)context {
    return NSDragOperationCopy;
}

- (void)draggingSession:(NSDraggingSession*)session endedAtPoint:(NSPoint)screenPoint operation:(NSDragOperation)operation {
    if (g_activeDragSource == self) {
        g_activeDragSource = nil;
    }
    [self.webView dragSource:self endedWithOperation:operation];
}

@end

@implementation HermesWebView {
    BOOL _dragDropEnabled;
    NSArray<NSString*>* _dropCustomTypes;
    HermesNativePackage _capture;
    BOOL _hasCapture;
    NSTimeInterval _capturedAt;
    HermesDragSource* _activeDrag;
    DragEndedCallback _dragEnded;
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

- (BOOL)startDragWithPackage:(const HermesNativePackage*)package event:(NSEvent*)event ended:(DragEndedCallback)ended {
    NSPoint point = [self convertPoint:event.locationInWindow fromView:nil];
    NSRect frame = NSMakeRect(point.x - 16, point.y - 16, 32, 32);
    NSWorkspace* workspace = [NSWorkspace sharedWorkspace];
    NSMutableArray<NSDraggingItem*>* items = [NSMutableArray array];

    for (NSURL* url in HermesPasteboard_FileUrls(package)) {
        NSDraggingItem* item = [[NSDraggingItem alloc] initWithPasteboardWriter:url];
        [item setDraggingFrame:frame contents:[workspace iconForFile:url.path]];
        [items addObject:item];
    }

    NSPasteboardItem* data = HermesPasteboard_CreateItem(package);
    if (data != nil) {
        UTType* type = package->Png != NULL ? UTTypeImage : package->Url != NULL ? UTTypeURL : UTTypeText;
        NSDraggingItem* item = [[NSDraggingItem alloc] initWithPasteboardWriter:data];
        [item setDraggingFrame:frame contents:[workspace iconForContentType:type]];
        [items addObject:item];
    }

    if (items.count == 0) {
        return NO;
    }

    HermesDragSource* source = [[HermesDragSource alloc] init];
    source.webView = self;
    g_activeDragSource = source;
    _activeDrag = source;
    _dragEnded = ended;
    [self beginDraggingSessionWithItems:items event:event source:source];
    return YES;
}

- (void)cancelDrag {
    if (_activeDrag != nil) {
        [self finishDrag:NO];
    }
}

- (void)dragSource:(HermesDragSource*)source endedWithOperation:(NSDragOperation)operation {
    if (source != _activeDrag) {
        return;
    }
    [self releasePageMouse];
    [self finishDrag:operation != NSDragOperationNone];
}

- (void)finishDrag:(BOOL)dropped {
    DragEndedCallback ended = _dragEnded;
    _activeDrag = nil;
    _dragEnded = NULL;
    if (ended != NULL) {
        ended(dropped);
    }
}

// AppKit's drag loop swallows the mouse-up, which leaves WebKit's page pressed. Release it at a point
// outside the page, where it cannot click anything.
- (void)releasePageMouse {
    NSEvent* mouseUp = [NSEvent mouseEventWithType:NSEventTypeLeftMouseUp
                                          location:NSMakePoint(-1, -1)
                                     modifierFlags:0
                                         timestamp:[NSProcessInfo processInfo].systemUptime
                                      windowNumber:self.window.windowNumber
                                           context:nil
                                       eventNumber:0
                                        clickCount:1
                                          pressure:0];
    [self mouseUp:mouseUp];
}

- (void)clearDropCapture {
    HermesNativePackage_Free(&_capture);
    _hasCapture = NO;
}

- (void)dealloc {
    HermesNativePackage_Free(&_capture);
}

@end
