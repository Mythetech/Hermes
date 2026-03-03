// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_URL_SCHEME_HANDLER_H
#define HERMES_URL_SCHEME_HANDLER_H

#import <WebKit/WebKit.h>
#import "HermesTypes.h"

@interface HermesUrlSchemeHandler : NSObject <WKURLSchemeHandler>

@property (nonatomic, assign) CustomSchemeCallback callback;

@end

#endif // HERMES_URL_SCHEME_HANDLER_H
