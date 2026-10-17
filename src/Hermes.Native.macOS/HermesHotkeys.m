// Copyright (c) Mythetech. Licensed under the MIT License.
#import <Cocoa/Cocoa.h>
#import <Carbon/Carbon.h>
#include <os/lock.h>
#include <stdatomic.h>
#import "HermesHotkeys.h"

static const OSType HotkeySignature = 'HrHk';

static _Atomic(HermesHotkeyPressedCallback) g_pressedCallback = NULL;

// Main thread only.
static EventHandlerRef g_pressedHandler = NULL;
static NSMutableDictionary<NSNumber*, NSValue*>* g_hotkeys = nil;

// Unregisters requested on other threads, applied before the main thread's next hotkey call and by the block posted to the main queue.
static os_unfair_lock g_pendingLock = OS_UNFAIR_LOCK_INIT;
static NSMutableArray<NSNumber*>* g_pendingUnregisters = nil;

static void RunOnMainSync(dispatch_block_t work) {
    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }
}

static OSStatus OnHotkeyPressed(EventHandlerCallRef next, EventRef event, void* userData) {
    (void)next;
    (void)userData;
    EventHotKeyID hotkeyId = { 0, 0 };
    OSStatus status = GetEventParameter(event, kEventParamDirectObject, typeEventHotKeyID, NULL,
                                        sizeof(hotkeyId), NULL, &hotkeyId);
    if (status != noErr || hotkeyId.signature != HotkeySignature) {
        return eventNotHandledErr;
    }
    HermesHotkeyPressedCallback pressed = atomic_load(&g_pressedCallback);
    if (pressed != NULL) {
        pressed(hotkeyId.id);
    }
    return noErr;
}

static void UnregisterOnMain(uint32_t hotkeyNumber) {
    NSNumber* key = @(hotkeyNumber);
    NSValue* hotkey = g_hotkeys[key];
    if (hotkey == nil) {
        return;
    }
    [g_hotkeys removeObjectForKey:key];
    UnregisterEventHotKey((EventHotKeyRef)hotkey.pointerValue);
}

static void ApplyPendingUnregisters(void) {
    os_unfair_lock_lock(&g_pendingLock);
    NSArray<NSNumber*>* pending = g_pendingUnregisters;
    g_pendingUnregisters = nil;
    os_unfair_lock_unlock(&g_pendingLock);
    for (NSNumber* hotkeyNumber in pending) {
        UnregisterOnMain(hotkeyNumber.unsignedIntValue);
    }
}

static OSStatus RegisterOnMain(uint32_t hotkeyNumber, uint32_t keyCode, uint32_t modifiers) {
    // A queued unregister may be freeing this same combination.
    ApplyPendingUnregisters();
    if (g_pressedHandler == NULL) {
        EventTypeSpec pressed = { kEventClassKeyboard, kEventHotKeyPressed };
        OSStatus installed = InstallEventHandler(GetApplicationEventTarget(), OnHotkeyPressed, 1, &pressed, NULL, &g_pressedHandler);
        if (installed != noErr) {
            return installed;
        }
    }
    EventHotKeyID hotkeyId = { HotkeySignature, hotkeyNumber };
    EventHotKeyRef hotkey = NULL;
    // Exclusive, so a combination another app holds exclusively, or a system shortcut such as Spotlight's, is refused.
    OSStatus status = RegisterEventHotKey(keyCode, modifiers, hotkeyId, GetApplicationEventTarget(), kEventHotKeyExclusive, &hotkey);
    if (status == noErr) {
        if (g_hotkeys == nil) {
            g_hotkeys = [[NSMutableDictionary alloc] init];
        }
        g_hotkeys[@(hotkeyNumber)] = [NSValue valueWithPointer:hotkey];
    }
    return status;
}

void HermesHotkeys_SetCallback(HermesHotkeyPressedCallback callback) {
    atomic_store(&g_pressedCallback, callback);
}

int32_t HermesHotkeys_Register(uint32_t hotkeyNumber, uint32_t keyCode, uint32_t modifiers) {
    __block OSStatus status = noErr;
    RunOnMainSync(^{
        @autoreleasepool {
            status = RegisterOnMain(hotkeyNumber, keyCode, modifiers);
        }
    });
    return status;
}

// Never blocks, so a hotkey can be unregistered from any thread, even after the run loop has ended.
void HermesHotkeys_Unregister(uint32_t hotkeyNumber) {
    @autoreleasepool {
        if ([NSThread isMainThread]) {
            ApplyPendingUnregisters();
            UnregisterOnMain(hotkeyNumber);
            return;
        }
        os_unfair_lock_lock(&g_pendingLock);
        if (g_pendingUnregisters == nil) {
            g_pendingUnregisters = [[NSMutableArray alloc] init];
        }
        [g_pendingUnregisters addObject:@(hotkeyNumber)];
        os_unfair_lock_unlock(&g_pendingLock);
        dispatch_async(dispatch_get_main_queue(), ^{
            @autoreleasepool {
                ApplyPendingUnregisters();
            }
        });
    }
}
