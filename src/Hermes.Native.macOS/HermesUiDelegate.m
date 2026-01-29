#import "HermesUiDelegate.h"
#import "HermesWindow.h"

@implementation HermesUiDelegate

#pragma mark - WKScriptMessageHandler

- (void)userContentController:(WKUserContentController*)userContentController
      didReceiveScriptMessage:(WKScriptMessage*)message {
    if ([message.name isEqualToString:@"hermesinterop"]) {
        if (_hermesWindow && _hermesWindow.onWebMessage) {
            NSString* body = message.body;
            if ([body isKindOfClass:[NSString class]]) {
                const char* messageUtf8 = [body UTF8String];
                _hermesWindow.onWebMessage(messageUtf8);
            }
        }
    } else if ([message.name isEqualToString:@"hermesWindowDrag"]) {
        // Start window drag using the current event
        NSEvent* currentEvent = [NSApp currentEvent];
        if (currentEvent && _hermesWindow) {
            [_hermesWindow.window performWindowDragWithEvent:currentEvent];
        }
    }
}

#pragma mark - WKUIDelegate

// Handle JavaScript alert()
- (void)webView:(WKWebView*)webView
    runJavaScriptAlertPanelWithMessage:(NSString*)message
                      initiatedByFrame:(WKFrameInfo*)frame
                     completionHandler:(void (^)(void))completionHandler {
    NSAlert* alert = [[NSAlert alloc] init];
    [alert setMessageText:message];
    [alert addButtonWithTitle:@"OK"];
    [alert runModal];
    completionHandler();
}

// Handle JavaScript confirm()
- (void)webView:(WKWebView*)webView
    runJavaScriptConfirmPanelWithMessage:(NSString*)message
                        initiatedByFrame:(WKFrameInfo*)frame
                       completionHandler:(void (^)(BOOL result))completionHandler {
    NSAlert* alert = [[NSAlert alloc] init];
    [alert setMessageText:message];
    [alert addButtonWithTitle:@"OK"];
    [alert addButtonWithTitle:@"Cancel"];

    NSModalResponse response = [alert runModal];
    completionHandler(response == NSAlertFirstButtonReturn);
}

// Handle JavaScript prompt()
- (void)webView:(WKWebView*)webView
    runJavaScriptTextInputPanelWithPrompt:(NSString*)prompt
                              defaultText:(NSString*)defaultText
                         initiatedByFrame:(WKFrameInfo*)frame
                        completionHandler:(void (^)(NSString* _Nullable result))completionHandler {
    NSAlert* alert = [[NSAlert alloc] init];
    [alert setMessageText:prompt];
    [alert addButtonWithTitle:@"OK"];
    [alert addButtonWithTitle:@"Cancel"];

    NSTextField* input = [[NSTextField alloc] initWithFrame:NSMakeRect(0, 0, 200, 24)];
    [input setStringValue:defaultText ?: @""];
    [alert setAccessoryView:input];

    NSModalResponse response = [alert runModal];
    if (response == NSAlertFirstButtonReturn) {
        completionHandler([input stringValue]);
    } else {
        completionHandler(nil);
    }
}

@end
