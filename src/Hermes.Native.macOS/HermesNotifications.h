// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HERMES_NOTIFICATIONS_H
#define HERMES_NOTIFICATIONS_H

#import <Cocoa/Cocoa.h>
#import <UserNotifications/UserNotifications.h>
#import "HermesTypes.h"

/// Wraps UNUserNotificationCenter. Unsupported (and inert) when the process has no bundle identifier,
/// because the framework raises NSInternalInconsistencyException on unbundled processes.
@interface HermesNotifications : NSObject <UNUserNotificationCenterDelegate>

@property (nonatomic, assign) NotificationClickedCallback clickCallback;
@property (nonatomic, assign, readonly) BOOL supported;
@property (nonatomic, copy, readonly) NSString* unsupportedReason;

- (instancetype)initWithClickCallback:(NotificationClickedCallback)clickCallback;

- (void)requestPermission:(NotificationPermissionCallback)callback context:(void*)context;

- (void)showWithIdentifier:(NSString*)identifier
                     title:(NSString*)title
                      body:(NSString*)body
                  iconPath:(NSString*)iconPath
                    silent:(BOOL)silent
                completion:(NotificationCompletionCallback)callback
                   context:(void*)context;

- (void)dismiss:(NSString*)identifier;
- (void)dismissAll;
- (void)shutdown;

@end

#endif // HERMES_NOTIFICATIONS_H
