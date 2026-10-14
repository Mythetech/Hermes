// Copyright (c) Mythetech. Licensed under the MIT License.
#import <Cocoa/Cocoa.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>
#import "HermesClipboard.h"

// Must match Hermes.Contracts.DataTransfer.HermesDataFormats.
enum { FormatText = 1, FormatHtml = 2, FormatPng = 4, FormatFiles = 8 };

static NSTimer* g_monitorTimer = nil;
static NSInteger g_lastChangeCount = 0;
static _Atomic(HermesClipboardChangedCallback) g_changedCallback = NULL;

static void RunOnMainSync(dispatch_block_t work) {
    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }
}

// Never blocks, so monitoring can be stopped after the run loop has ended.
static void RunOnMainAsync(dispatch_block_t work) {
    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_async(dispatch_get_main_queue(), work);
    }
}

static NSPasteboardType TypeForMime(const char* mimeType) {
    NSString* mime = [NSString stringWithUTF8String:mimeType];
    UTType* type = [UTType typeWithMIMEType:mime];
    return type != nil ? (NSPasteboardType)type.identifier : (NSPasteboardType)mime;
}

static NSDictionary* FileUrlOptions(void) {
    return @{ NSPasteboardURLReadingFileURLsOnlyKey: @YES };
}

static BOOL HasFiles(NSPasteboard* pasteboard) {
    return [pasteboard canReadObjectForClasses:@[[NSURL class]] options:FileUrlOptions()];
}

static char* CopyUtf8(NSString* value) {
    return value != nil ? strdup(value.UTF8String) : NULL;
}

static uint8_t* CopyBytes(NSData* data, int* length) {
    *length = 0;
    if (data == nil || data.length == 0) {
        return NULL;
    }
    uint8_t* buffer = malloc(data.length);
    memcpy(buffer, data.bytes, data.length);
    *length = (int)data.length;
    return buffer;
}

int HermesClipboard_GetFormats(void) {
    __block int formats = 0;
    RunOnMainSync(^{
        NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
        NSArray<NSPasteboardType>* types = pasteboard.types ?: @[];
        BOOL files = HasFiles(pasteboard);
        if ([types containsObject:NSPasteboardTypeString]) formats |= FormatText;
        if ([types containsObject:NSPasteboardTypeHTML]) formats |= FormatHtml;
        if (files) formats |= FormatFiles;
        // Finder adds each file's icon as an image, so with files present only real PNG data counts.
        if ([types containsObject:NSPasteboardTypePNG] || (!files && [NSImage canInitWithPasteboard:pasteboard])) formats |= FormatPng;
    });
    return formats;
}

bool HermesClipboard_HasType(const char* mimeType) {
    __block BOOL found = NO;
    RunOnMainSync(^{
        NSArray<NSPasteboardType>* types = [NSPasteboard generalPasteboard].types ?: @[];
        found = [types containsObject:TypeForMime(mimeType)];
    });
    return found;
}

char* HermesClipboard_ReadText(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        result = CopyUtf8([[NSPasteboard generalPasteboard] stringForType:NSPasteboardTypeString]);
    });
    return result;
}

char* HermesClipboard_ReadHtml(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        result = CopyUtf8([[NSPasteboard generalPasteboard] stringForType:NSPasteboardTypeHTML]);
    });
    return result;
}

uint8_t* HermesClipboard_ReadPng(int* length) {
    __block uint8_t* result = NULL;
    __block int resultLength = 0;
    RunOnMainSync(^{
        NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
        NSData* png = [pasteboard dataForType:NSPasteboardTypePNG];
        if (png == nil && !HasFiles(pasteboard)) {
            NSImage* image = [[NSImage alloc] initWithPasteboard:pasteboard];
            NSData* tiff = image.TIFFRepresentation;
            NSBitmapImageRep* rep = tiff != nil ? [NSBitmapImageRep imageRepWithData:tiff] : nil;
            png = [rep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
        }
        result = CopyBytes(png, &resultLength);
    });
    *length = resultLength;
    return result;
}

char** HermesClipboard_ReadFiles(int* count) {
    __block char** result = NULL;
    __block int resultCount = 0;
    RunOnMainSync(^{
        NSArray<NSURL*>* urls = [[NSPasteboard generalPasteboard] readObjectsForClasses:@[[NSURL class]]
                                                                               options:FileUrlOptions()];
        if (urls.count == 0) {
            return;
        }
        result = malloc(sizeof(char*) * urls.count);
        for (NSURL* url in urls) {
            result[resultCount++] = strdup(url.fileSystemRepresentation);
        }
    });
    *count = resultCount;
    return result;
}

uint8_t* HermesClipboard_ReadCustom(const char* mimeType, int* length) {
    __block uint8_t* result = NULL;
    __block int resultLength = 0;
    RunOnMainSync(^{
        result = CopyBytes([[NSPasteboard generalPasteboard] dataForType:TypeForMime(mimeType)], &resultLength);
    });
    *length = resultLength;
    return result;
}

bool HermesClipboard_Write(const char* text, const char* html, const uint8_t* png, int pngLength,
                           const char** files, int fileCount,
                           const char** customTypes, const uint8_t** customData, const int* customLengths, int customCount) {
    __block BOOL written = NO;
    RunOnMainSync(^{
        NSPasteboardItem* item = [[NSPasteboardItem alloc] init];
        if (text != NULL) {
            [item setString:[NSString stringWithUTF8String:text] forType:NSPasteboardTypeString];
        }
        if (html != NULL) {
            [item setString:[NSString stringWithUTF8String:html] forType:NSPasteboardTypeHTML];
        }
        if (png != NULL && pngLength > 0) {
            NSData* pngData = [NSData dataWithBytes:png length:(NSUInteger)pngLength];
            [item setData:pngData forType:NSPasteboardTypePNG];
            NSData* tiff = [[NSBitmapImageRep imageRepWithData:pngData] TIFFRepresentation];
            if (tiff != nil) {
                [item setData:tiff forType:NSPasteboardTypeTIFF];
            }
        }
        for (int i = 0; i < customCount; i++) {
            NSPasteboardType type = TypeForMime(customTypes[i]);
            // A standard format already written for this type wins over a colliding custom key.
            if ([item.types containsObject:type]) {
                continue;
            }
            NSData* data = [NSData dataWithBytes:customData[i] length:(NSUInteger)customLengths[i]];
            [item setData:data forType:type];
        }

        NSMutableArray<id<NSPasteboardWriting>>* objects = [NSMutableArray array];
        if (item.types.count > 0) {
            [objects addObject:item];
        }
        for (int i = 0; i < fileCount; i++) {
            [objects addObject:[NSURL fileURLWithPath:[NSString stringWithUTF8String:files[i]]]];
        }

        NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
        [pasteboard clearContents];
        written = [pasteboard writeObjects:objects];
    });
    return written;
}

void HermesClipboard_Clear(void) {
    RunOnMainSync(^{
        [[NSPasteboard generalPasteboard] clearContents];
    });
}

void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback) {
    atomic_store(&g_changedCallback, callback);
    RunOnMainAsync(^{
        [g_monitorTimer invalidate];
        g_lastChangeCount = [NSPasteboard generalPasteboard].changeCount;
        // macOS has no pasteboard change notification; polling the counter reads no content.
        g_monitorTimer = [NSTimer timerWithTimeInterval:0.5 repeats:YES block:^(NSTimer* timer) {
            NSInteger changeCount = [NSPasteboard generalPasteboard].changeCount;
            if (changeCount == g_lastChangeCount) {
                return;
            }
            g_lastChangeCount = changeCount;
            HermesClipboardChangedCallback changed = atomic_load(&g_changedCallback);
            if (changed != NULL) {
                changed();
            }
        }];
        [[NSRunLoop mainRunLoop] addTimer:g_monitorTimer forMode:NSRunLoopCommonModes];
    });
}

void HermesClipboard_StopMonitoring(void) {
    atomic_store(&g_changedCallback, NULL);
    RunOnMainAsync(^{
        [g_monitorTimer invalidate];
        g_monitorTimer = nil;
    });
}
