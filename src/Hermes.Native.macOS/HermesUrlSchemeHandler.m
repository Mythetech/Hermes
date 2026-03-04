// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#import "HermesUrlSchemeHandler.h"

@implementation HermesUrlSchemeHandler

- (void)webView:(WKWebView*)webView startURLSchemeTask:(id<WKURLSchemeTask>)urlSchemeTask {
    NSURL* url = [[urlSchemeTask request] URL];
    const char* urlStr = [url.absoluteString UTF8String];

    int numBytes = 0;
    char* contentType = NULL;
    void* responseData = NULL;

    // Call the C# callback if set
    if (_callback) {
        responseData = _callback(urlStr, &numBytes, &contentType);
    }

    // Determine status code
    NSInteger statusCode = (responseData == NULL) ? 404 : 200;

    // Build content type string
    NSString* nsContentType = @"application/octet-stream";
    if (contentType) {
        nsContentType = [NSString stringWithUTF8String:contentType];
        free(contentType);
    }

    // Create HTTP headers
    NSDictionary* headers = @{
        @"Content-Type": nsContentType,
        @"Cache-Control": @"no-cache"
    };

    // Create HTTP response
    NSHTTPURLResponse* response = [[NSHTTPURLResponse alloc] initWithURL:url
                                                              statusCode:statusCode
                                                             HTTPVersion:nil
                                                            headerFields:headers];

    // Send response
    [urlSchemeTask didReceiveResponse:response];

    if (responseData && numBytes > 0) {
        NSData* data = [NSData dataWithBytes:responseData length:numBytes];
        [urlSchemeTask didReceiveData:data];
        free(responseData);
    } else if (statusCode == 404) {
        // Send empty 404 body
        NSData* emptyData = [@"Not Found" dataUsingEncoding:NSUTF8StringEncoding];
        [urlSchemeTask didReceiveData:emptyData];
    }

    [urlSchemeTask didFinish];
}

- (void)webView:(WKWebView*)webView stopURLSchemeTask:(id<WKURLSchemeTask>)urlSchemeTask {
    // Handle cancellation if needed
    // Currently a no-op as we complete synchronously
}

@end
