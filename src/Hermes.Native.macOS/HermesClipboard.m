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

static BOOL HasFileUrls(NSArray<NSPasteboardType>* types) {
    return [types containsObject:NSPasteboardTypeFileURL];
}

// Bitmap types only, so a PDF rendition never counts as an image.
static NSPasteboardType AvailableBitmapType(NSPasteboard* pasteboard) {
    return [pasteboard availableTypeFromArray:NSBitmapImageRep.imageTypes];
}

static char* CopyUtf8(NSString* value) {
    const char* s = value.UTF8String;
    return s != NULL ? strdup(s) : NULL;
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

// stringForType: gives up on HTML written as UTF-16, which some apps prefix with a byte order mark.
static NSString* DecodeUtf16Html(NSData* data) {
    const uint8_t* bytes = data.bytes;
    BOOL hasBom = data.length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
    return hasBom ? [[NSString alloc] initWithData:data encoding:NSUTF16StringEncoding] : nil;
}

// Cocoa apps put rich text on the pasteboard as RTF or flat RTFD rather than HTML.
static NSString* HtmlFromRichText(NSPasteboard* pasteboard) {
    NSPasteboardType type = [pasteboard availableTypeFromArray:@[NSPasteboardTypeRTF, NSPasteboardTypeRTFD]];
    NSData* data = type != nil ? [pasteboard dataForType:type] : nil;
    if (data == nil) {
        return nil;
    }
    NSAttributedStringDocumentType documentType = [type isEqualToString:NSPasteboardTypeRTFD] ? NSRTFDTextDocumentType : NSRTFTextDocumentType;
    NSAttributedString* text = [[NSAttributedString alloc] initWithData:data
                                                                options:@{ NSDocumentTypeDocumentOption: documentType }
                                                     documentAttributes:nil
                                                                  error:nil];
    if (text == nil) {
        return nil;
    }
    NSDictionary* attributes = @{
        NSDocumentTypeDocumentAttribute: NSHTMLTextDocumentType,
        NSCharacterEncodingDocumentAttribute: @(NSUTF8StringEncoding),
    };
    NSData* html = [text dataFromRange:NSMakeRange(0, text.length) documentAttributes:attributes error:nil];
    return html != nil ? [[NSString alloc] initWithData:html encoding:NSUTF8StringEncoding] : nil;
}

int HermesClipboard_GetFormats(void) {
    __block int formats = 0;
    RunOnMainSync(^{
        @autoreleasepool {
            NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
            NSArray<NSPasteboardType>* types = pasteboard.types ?: @[];
            BOOL files = HasFileUrls(types);
            if ([types containsObject:NSPasteboardTypeString]) formats |= FormatText;
            if ([types containsObject:NSPasteboardTypeHTML] || [types containsObject:NSPasteboardTypeRTF] ||
                [types containsObject:NSPasteboardTypeRTFD]) formats |= FormatHtml;
            if (files) formats |= FormatFiles;
            // Finder adds each file's icon as an image, so with files present only real PNG data counts.
            if ([types containsObject:NSPasteboardTypePNG] || (!files && AvailableBitmapType(pasteboard) != nil)) formats |= FormatPng;
        }
    });
    return formats;
}

bool HermesClipboard_HasType(const char* mimeType) {
    __block BOOL found = NO;
    RunOnMainSync(^{
        @autoreleasepool {
            NSArray<NSPasteboardType>* types = [NSPasteboard generalPasteboard].types ?: @[];
            found = [types containsObject:TypeForMime(mimeType)];
        }
    });
    return found;
}

char* HermesClipboard_ReadText(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        @autoreleasepool {
            result = CopyUtf8([[NSPasteboard generalPasteboard] stringForType:NSPasteboardTypeString]);
        }
    });
    return result;
}

char* HermesClipboard_ReadHtml(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        @autoreleasepool {
            NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
            NSString* html = [pasteboard stringForType:NSPasteboardTypeHTML];
            if (html == nil) {
                NSData* data = [pasteboard dataForType:NSPasteboardTypeHTML];
                html = data != nil ? DecodeUtf16Html(data) : nil;
            }
            if (html == nil) {
                html = HtmlFromRichText(pasteboard);
            }
            result = CopyUtf8(html);
        }
    });
    return result;
}

uint8_t* HermesClipboard_ReadPng(int* length) {
    __block uint8_t* result = NULL;
    __block int resultLength = 0;
    RunOnMainSync(^{
        @autoreleasepool {
            NSPasteboard* pasteboard = [NSPasteboard generalPasteboard];
            NSData* png = [pasteboard dataForType:NSPasteboardTypePNG];
            if (png == nil && !HasFileUrls(pasteboard.types)) {
                NSPasteboardType type = AvailableBitmapType(pasteboard);
                NSData* data = type != nil ? [pasteboard dataForType:type] : nil;
                NSBitmapImageRep* rep = data != nil ? [NSBitmapImageRep imageRepWithData:data] : nil;
                png = [rep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
            }
            result = CopyBytes(png, &resultLength);
        }
    });
    *length = resultLength;
    return result;
}

char** HermesClipboard_ReadFiles(int* count) {
    __block char** result = NULL;
    __block int resultCount = 0;
    RunOnMainSync(^{
        @autoreleasepool {
            NSArray<NSURL*>* urls = [[NSPasteboard generalPasteboard] readObjectsForClasses:@[[NSURL class]]
                                                                                   options:FileUrlOptions()];
            if (urls.count == 0) {
                return;
            }
            result = malloc(sizeof(char*) * urls.count);
            for (NSURL* url in urls) {
                // NSURL paths come back decomposed (NFD); .NET and most apps name files in NFC, and on APFS and HFS+ lookups ignore normalization.
                NSString* path = url.path.precomposedStringWithCanonicalMapping;
                const char* utf8 = path.UTF8String;
                if (utf8 != NULL) {
                    result[resultCount++] = strdup(utf8);
                }
            }
            if (resultCount == 0) {
                free(result);
                result = NULL;
            }
        }
    });
    *count = resultCount;
    return result;
}

uint8_t* HermesClipboard_ReadCustom(const char* mimeType, int* length) {
    __block uint8_t* result = NULL;
    __block int resultLength = 0;
    RunOnMainSync(^{
        @autoreleasepool {
            result = CopyBytes([[NSPasteboard generalPasteboard] dataForType:TypeForMime(mimeType)], &resultLength);
        }
    });
    *length = resultLength;
    return result;
}

bool HermesClipboard_Write(const char* text, const char* html, const uint8_t* png, int pngLength,
                           const char** files, int fileCount,
                           const char** customTypes, const uint8_t** customData, const int* customLengths, int customCount) {
    __block BOOL written = NO;
    RunOnMainSync(^{
        @autoreleasepool {
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
        }
    });
    return written;
}

void HermesClipboard_Clear(void) {
    RunOnMainSync(^{
        @autoreleasepool {
            [[NSPasteboard generalPasteboard] clearContents];
        }
    });
}

// Reconciles the timer with the latest request, so a stale queued call can never undo a newer one.
static void ApplyMonitoringState(void) {
    BOOL wanted = atomic_load(&g_changedCallback) != NULL;
    if (wanted && g_monitorTimer == nil) {
        g_lastChangeCount = [NSPasteboard generalPasteboard].changeCount;
        // macOS has no pasteboard change notification; polling the counter reads no content.
        g_monitorTimer = [NSTimer timerWithTimeInterval:0.5 repeats:YES block:^(NSTimer* timer) {
            @autoreleasepool {
                NSInteger changeCount = [NSPasteboard generalPasteboard].changeCount;
                if (changeCount == g_lastChangeCount) {
                    return;
                }
                g_lastChangeCount = changeCount;
                HermesClipboardChangedCallback changed = atomic_load(&g_changedCallback);
                if (changed != NULL) {
                    changed();
                }
            }
        }];
        g_monitorTimer.tolerance = 0.1;
        [[NSRunLoop mainRunLoop] addTimer:g_monitorTimer forMode:NSRunLoopCommonModes];
    } else if (!wanted && g_monitorTimer != nil) {
        [g_monitorTimer invalidate];
        g_monitorTimer = nil;
    }
}

void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback) {
    atomic_store(&g_changedCallback, callback);
    RunOnMainAsync(^{
        @autoreleasepool {
            ApplyMonitoringState();
        }
    });
}

void HermesClipboard_StopMonitoring(void) {
    atomic_store(&g_changedCallback, NULL);
    RunOnMainAsync(^{
        @autoreleasepool {
            ApplyMonitoringState();
        }
    });
}
