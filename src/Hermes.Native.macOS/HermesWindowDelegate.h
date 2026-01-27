#ifndef HERMES_WINDOW_DELEGATE_H
#define HERMES_WINDOW_DELEGATE_H

#import <Cocoa/Cocoa.h>

@class HermesWindow;

@interface HermesWindowDelegate : NSObject <NSWindowDelegate>

@property (nonatomic, weak) HermesWindow* hermesWindow;

@end

#endif // HERMES_WINDOW_DELEGATE_H
