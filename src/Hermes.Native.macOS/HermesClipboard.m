// Copyright (c) Mythetech. Licensed under the MIT License.
#import <Cocoa/Cocoa.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <stdatomic.h>
#include <stdlib.h>
#include <string.h>
#import "HermesClipboard.h"

// Must match Hermes.Contracts.DataTransfer.HermesDataFormats.
enum { FormatText = 1, FormatHtml = 2, FormatPng = 4, FormatFiles = 8, FormatUrl = 16 };

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

NSPasteboardType HermesPasteboard_TypeForMime(NSString* mimeType) {
    UTType* type = [UTType typeWithMIMEType:mimeType];
    return type != nil ? (NSPasteboardType)type.identifier : (NSPasteboardType)mimeType;
}

static NSPasteboardType TypeForMime(const char* mimeType) {
    return HermesPasteboard_TypeForMime([NSString stringWithUTF8String:mimeType]);
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

static char** CopyUtf8Array(NSArray<NSString*>* values, int* count) {
    *count = 0;
    if (values.count == 0) {
        return NULL;
    }
    char** result = malloc(sizeof(char*) * values.count);
    for (NSString* value in values) {
        result[(*count)++] = CopyUtf8(value);
    }
    return result;
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

static NSString* ReadText(NSPasteboard* pasteboard) {
    return [pasteboard stringForType:NSPasteboardTypeString];
}

static NSString* ReadHtml(NSPasteboard* pasteboard) {
    NSString* html = [pasteboard stringForType:NSPasteboardTypeHTML];
    if (html == nil) {
        NSData* data = [pasteboard dataForType:NSPasteboardTypeHTML];
        html = data != nil ? DecodeUtf16Html(data) : nil;
    }
    return html != nil ? html : HtmlFromRichText(pasteboard);
}

// File URLs are read as Files, so the first link that is not a file is the URL.
static NSString* ReadUrl(NSPasteboard* pasteboard) {
    for (NSURL* url in [pasteboard readObjectsForClasses:@[[NSURL class]] options:@{}]) {
        if (!url.isFileURL) {
            return url.absoluteString;
        }
    }
    return nil;
}

static NSData* ReadPng(NSPasteboard* pasteboard) {
    NSData* png = [pasteboard dataForType:NSPasteboardTypePNG];
    if (png == nil && !HasFileUrls(pasteboard.types)) {
        NSPasteboardType type = AvailableBitmapType(pasteboard);
        NSData* data = type != nil ? [pasteboard dataForType:type] : nil;
        NSBitmapImageRep* rep = data != nil ? [NSBitmapImageRep imageRepWithData:data] : nil;
        png = [rep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
    }
    return png;
}

static NSArray<NSString*>* ReadFiles(NSPasteboard* pasteboard) {
    NSMutableArray<NSString*>* paths = [NSMutableArray array];
    for (NSURL* url in [pasteboard readObjectsForClasses:@[[NSURL class]] options:FileUrlOptions()]) {
        // NSURL paths come back decomposed (NFD); .NET and most apps name files in NFC, and on APFS and HFS+ lookups ignore normalization.
        NSString* path = url.path.precomposedStringWithCanonicalMapping;
        if (path.UTF8String != NULL) {
            [paths addObject:path];
        }
    }
    return paths;
}

// Text, HTML, the URL, the image and custom formats share one item; each file is an item of its own.
NSPasteboardItem* HermesPasteboard_CreateItem(const HermesNativePackage* package) {
    NSPasteboardItem* item = [[NSPasteboardItem alloc] init];
    if (package->Text != NULL) {
        [item setString:[NSString stringWithUTF8String:package->Text] forType:NSPasteboardTypeString];
    }
    if (package->Html != NULL) {
        [item setString:[NSString stringWithUTF8String:package->Html] forType:NSPasteboardTypeHTML];
    }
    if (package->Url != NULL) {
        [item setString:[NSString stringWithUTF8String:package->Url] forType:NSPasteboardTypeURL];
    }
    if (package->Png != NULL && package->PngLength > 0) {
        NSData* pngData = [NSData dataWithBytes:package->Png length:(NSUInteger)package->PngLength];
        [item setData:pngData forType:NSPasteboardTypePNG];
        NSData* tiff = [[NSBitmapImageRep imageRepWithData:pngData] TIFFRepresentation];
        if (tiff != nil) {
            [item setData:tiff forType:NSPasteboardTypeTIFF];
        }
    }
    for (int i = 0; i < package->CustomCount; i++) {
        NSPasteboardType type = TypeForMime(package->CustomTypes[i]);
        // A standard format already written for this type wins over a colliding custom key.
        if ([item.types containsObject:type]) {
            continue;
        }
        NSData* data = [NSData dataWithBytes:package->CustomData[i] length:(NSUInteger)package->CustomLengths[i]];
        [item setData:data forType:type];
    }
    return item.types.count > 0 ? item : nil;
}

NSArray<NSURL*>* HermesPasteboard_FileUrls(const HermesNativePackage* package) {
    NSMutableArray<NSURL*>* urls = [NSMutableArray array];
    for (int i = 0; i < package->FileCount; i++) {
        [urls addObject:[NSURL fileURLWithPath:[NSString stringWithUTF8String:package->Files[i]]]];
    }
    return urls;
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
            // A link is public.url; file URLs are public.file-url and count as Files.
            if ([types containsObject:NSPasteboardTypeURL]) formats |= FormatUrl;
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
            result = CopyUtf8(ReadText([NSPasteboard generalPasteboard]));
        }
    });
    return result;
}

char* HermesClipboard_ReadHtml(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        @autoreleasepool {
            result = CopyUtf8(ReadHtml([NSPasteboard generalPasteboard]));
        }
    });
    return result;
}

char* HermesClipboard_ReadUrl(void) {
    __block char* result = NULL;
    RunOnMainSync(^{
        @autoreleasepool {
            result = CopyUtf8(ReadUrl([NSPasteboard generalPasteboard]));
        }
    });
    return result;
}

uint8_t* HermesClipboard_ReadPng(int* length) {
    __block uint8_t* result = NULL;
    __block int resultLength = 0;
    RunOnMainSync(^{
        @autoreleasepool {
            result = CopyBytes(ReadPng([NSPasteboard generalPasteboard]), &resultLength);
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
            result = CopyUtf8Array(ReadFiles([NSPasteboard generalPasteboard]), &resultCount);
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

bool HermesClipboard_Write(const HermesNativePackage* package) {
    __block BOOL written = NO;
    RunOnMainSync(^{
        @autoreleasepool {
            NSMutableArray<id<NSPasteboardWriting>>* objects = [NSMutableArray array];
            NSPasteboardItem* item = HermesPasteboard_CreateItem(package);
            if (item != nil) {
                [objects addObject:item];
            }
            [objects addObjectsFromArray:HermesPasteboard_FileUrls(package)];

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
