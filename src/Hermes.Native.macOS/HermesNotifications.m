// Copyright (c) Mythetech. Licensed under the MIT License.
#import "HermesNotifications.h"

@interface HermesNotifications ()
@property (nonatomic, strong) UNUserNotificationCenter* center;
@property (nonatomic, assign, readwrite) BOOL supported;
@property (nonatomic, copy, readwrite) NSString* unsupportedReason;
@end

@implementation HermesNotifications

- (instancetype)initWithClickCallback:(NotificationClickedCallback)clickCallback {
    self = [super init];
    if (self) {
        _clickCallback = clickCallback;

        NSString* bundleId = [[NSBundle mainBundle] bundleIdentifier];
        if (bundleId.length == 0) {
            _supported = NO;
            _unsupportedReason = @"process is not running from an app bundle";
            return self;
        }

        @try {
            _center = [UNUserNotificationCenter currentNotificationCenter];
            _center.delegate = self;
            _supported = YES;
        } @catch (NSException* exception) {
            _center = nil;
            _supported = NO;
            _unsupportedReason = [NSString stringWithFormat:@"UNUserNotificationCenter unavailable: %@", exception.reason];
        }
    }
    return self;
}

- (void)requestPermission:(NotificationPermissionCallback)callback context:(void*)context {
    if (!_supported) {
        if (callback) callback(context, false);
        return;
    }

    UNAuthorizationOptions options = UNAuthorizationOptionAlert | UNAuthorizationOptionSound | UNAuthorizationOptionBadge;
    [_center requestAuthorizationWithOptions:options completionHandler:^(BOOL granted, NSError* error) {
        if (callback) callback(context, granted && error == nil);
    }];
}

- (void)showWithIdentifier:(NSString*)identifier
                     title:(NSString*)title
                      body:(NSString*)body
                  iconPath:(NSString*)iconPath
                    silent:(BOOL)silent
                completion:(NotificationCompletionCallback)callback
                   context:(void*)context {
    if (!_supported) {
        if (callback) callback(context, "notifications are not supported on this host");
        return;
    }

    UNMutableNotificationContent* content = [[UNMutableNotificationContent alloc] init];
    content.title = title;
    if (body.length > 0) content.body = body;
    content.sound = silent ? nil : [UNNotificationSound defaultSound];

    if (iconPath.length > 0) {
        UNNotificationAttachment* attachment = [self attachmentForIconAtPath:iconPath];
        if (attachment) content.attachments = @[attachment];
    }

    UNNotificationRequest* request = [UNNotificationRequest requestWithIdentifier:identifier content:content trigger:nil];
    [_center addNotificationRequest:request withCompletionHandler:^(NSError* error) {
        if (!callback) return;
        if (error) {
            callback(context, error.localizedDescription.UTF8String);
        } else {
            callback(context, NULL);
        }
    }];
}

// UNNotificationAttachment moves the file into the notification store, so attach a private copy
// rather than consuming the caller's icon.
- (UNNotificationAttachment*)attachmentForIconAtPath:(NSString*)iconPath {
    NSString* extension = iconPath.pathExtension.length > 0 ? iconPath.pathExtension : @"png";
    NSString* fileName = [NSString stringWithFormat:@"hermes-notification-%@.%@", [[NSUUID UUID] UUIDString], extension];
    NSString* tempPath = [NSTemporaryDirectory() stringByAppendingPathComponent:fileName];

    NSError* copyError = nil;
    if (![[NSFileManager defaultManager] copyItemAtPath:iconPath toPath:tempPath error:&copyError]) {
        return nil;
    }

    NSError* attachError = nil;
    UNNotificationAttachment* attachment = [UNNotificationAttachment attachmentWithIdentifier:@"icon"
                                                                                          URL:[NSURL fileURLWithPath:tempPath]
                                                                                      options:nil
                                                                                        error:&attachError];
    if (!attachment) {
        // Only a successful attachment hands the copy over to the notification store, so clean up after a failure.
        [[NSFileManager defaultManager] removeItemAtPath:tempPath error:nil];
    }
    return attachment;
}

- (void)dismiss:(NSString*)identifier {
    if (!_supported) return;
    [_center removeDeliveredNotificationsWithIdentifiers:@[identifier]];
    [_center removePendingNotificationRequestsWithIdentifiers:@[identifier]];
}

- (void)dismissAll {
    if (!_supported) return;
    [_center removeAllDeliveredNotifications];
    [_center removeAllPendingNotificationRequests];
}

- (void)shutdown {
    if (_center && _center.delegate == self) {
        _center.delegate = nil;
    }
    _clickCallback = NULL;
}

#pragma mark - UNUserNotificationCenterDelegate

// The default behaviour hides notifications while the app is frontmost; desktop apps expect to see them.
- (void)userNotificationCenter:(UNUserNotificationCenter*)center
       willPresentNotification:(UNNotification*)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler {
    completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionList | UNNotificationPresentationOptionSound);
}

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
didReceiveNotificationResponse:(UNNotificationResponse*)response
         withCompletionHandler:(void (^)(void))completionHandler {
    NotificationClickedCallback callback = _clickCallback;
    if (callback && [response.actionIdentifier isEqualToString:UNNotificationDefaultActionIdentifier]) {
        NSString* identifier = response.notification.request.identifier;
        dispatch_async(dispatch_get_main_queue(), ^{
            callback(identifier.UTF8String);
        });
    }
    completionHandler();
}

@end
