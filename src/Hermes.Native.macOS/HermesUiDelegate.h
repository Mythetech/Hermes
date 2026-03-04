// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_UI_DELEGATE_H
#define HERMES_UI_DELEGATE_H

#import <WebKit/WebKit.h>

@class HermesWindow;

@interface HermesUiDelegate : NSObject <WKUIDelegate, WKScriptMessageHandler>

@property (nonatomic, weak) HermesWindow* hermesWindow;

@end

#endif // HERMES_UI_DELEGATE_H
