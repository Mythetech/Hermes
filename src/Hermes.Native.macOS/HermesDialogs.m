// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#import "HermesDialogs.h"
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

#pragma mark - Helper Functions

static NSArray<UTType*>* CreateAllowedContentTypes(const char** filters, int filterCount) {
    if (!filters || filterCount == 0) {
        return nil;
    }

    NSMutableArray<UTType*>* types = [NSMutableArray new];

    for (int i = 0; i < filterCount; i++) {
        if (filters[i]) {
            NSString* ext = [NSString stringWithUTF8String:filters[i]];
            // Remove leading dot if present
            if ([ext hasPrefix:@"."]) {
                ext = [ext substringFromIndex:1];
            }
            UTType* type = [UTType typeWithFilenameExtension:ext];
            if (type) {
                [types addObject:type];
            }
        }
    }

    return types.count > 0 ? types : nil;
}

static char* DuplicateString(NSString* str) {
    if (!str) return NULL;
    const char* utf8 = [str UTF8String];
    return strdup(utf8);
}

static char** CreateStringArray(NSArray<NSURL*>* urls, int* count) {
    *count = (int)[urls count];
    if (*count == 0) return NULL;

    char** result = (char**)malloc(sizeof(char*) * (*count));
    for (int i = 0; i < *count; i++) {
        result[i] = DuplicateString([urls[i] path]);
    }
    return result;
}

#pragma mark - File Dialogs

char** Hermes_ShowOpenFileDialog(const char* title,
                                  const char* defaultPath,
                                  bool multiSelect,
                                  const char** filters,
                                  int filterCount,
                                  int* resultCount) {
    __block char** result = NULL;
    __block int count = 0;

    void (^work)(void) = ^{
        @autoreleasepool {
            NSOpenPanel* panel = [NSOpenPanel openPanel];
            [panel setCanChooseFiles:YES];
            [panel setCanChooseDirectories:NO];
            [panel setAllowsMultipleSelection:multiSelect];

            if (title) {
                [panel setTitle:[NSString stringWithUTF8String:title]];
            }

            if (defaultPath) {
                NSString* path = [NSString stringWithUTF8String:defaultPath];
                [panel setDirectoryURL:[NSURL fileURLWithPath:path]];
            }

            NSArray<UTType*>* allowedTypes = CreateAllowedContentTypes(filters, filterCount);
            if (allowedTypes) {
                [panel setAllowedContentTypes:allowedTypes];
            }

            NSModalResponse response = [panel runModal];
            if (response == NSModalResponseOK) {
                result = CreateStringArray([panel URLs], &count);
            }
        }
    };

    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }

    *resultCount = count;
    return result;
}

char** Hermes_ShowOpenFolderDialog(const char* title,
                                    const char* defaultPath,
                                    bool multiSelect,
                                    int* resultCount) {
    __block char** result = NULL;
    __block int count = 0;

    void (^work)(void) = ^{
        @autoreleasepool {
            NSOpenPanel* panel = [NSOpenPanel openPanel];
            [panel setCanChooseFiles:NO];
            [panel setCanChooseDirectories:YES];
            [panel setAllowsMultipleSelection:multiSelect];

            if (title) {
                [panel setTitle:[NSString stringWithUTF8String:title]];
            }

            if (defaultPath) {
                NSString* path = [NSString stringWithUTF8String:defaultPath];
                [panel setDirectoryURL:[NSURL fileURLWithPath:path]];
            }

            NSModalResponse response = [panel runModal];
            if (response == NSModalResponseOK) {
                result = CreateStringArray([panel URLs], &count);
            }
        }
    };

    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }

    *resultCount = count;
    return result;
}

char* Hermes_ShowSaveFileDialog(const char* title,
                                 const char* defaultPath,
                                 const char** filters,
                                 int filterCount,
                                 const char* defaultFileName) {
    __block char* result = NULL;

    void (^work)(void) = ^{
        @autoreleasepool {
            NSSavePanel* panel = [NSSavePanel savePanel];

            if (title) {
                [panel setTitle:[NSString stringWithUTF8String:title]];
            }

            if (defaultPath) {
                NSString* path = [NSString stringWithUTF8String:defaultPath];
                [panel setDirectoryURL:[NSURL fileURLWithPath:path]];
            }

            if (defaultFileName) {
                [panel setNameFieldStringValue:[NSString stringWithUTF8String:defaultFileName]];
            }

            NSArray<UTType*>* allowedTypes = CreateAllowedContentTypes(filters, filterCount);
            if (allowedTypes) {
                [panel setAllowedContentTypes:allowedTypes];
            }

            NSModalResponse response = [panel runModal];
            if (response == NSModalResponseOK) {
                result = DuplicateString([[panel URL] path]);
            }
        }
    };

    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }

    return result;
}

#pragma mark - Message Dialog

int Hermes_ShowMessageDialog(const char* title,
                              const char* message,
                              int buttons,
                              int icon) {
    __block int result = DialogResult_Ok;

    void (^work)(void) = ^{
        @autoreleasepool {
            NSAlert* alert = [[NSAlert alloc] init];

            if (message) {
                [alert setMessageText:[NSString stringWithUTF8String:message]];
            }

            if (title) {
                [alert setInformativeText:[NSString stringWithUTF8String:title]];
            }

            // Set icon style
            switch (icon) {
                case DialogIcon_Info:
                    [alert setAlertStyle:NSAlertStyleInformational];
                    break;
                case DialogIcon_Warning:
                    [alert setAlertStyle:NSAlertStyleWarning];
                    break;
                case DialogIcon_Error:
                    [alert setAlertStyle:NSAlertStyleCritical];
                    break;
                case DialogIcon_Question:
                    [alert setAlertStyle:NSAlertStyleInformational];
                    break;
                default:
                    [alert setAlertStyle:NSAlertStyleInformational];
                    break;
            }

            // Add buttons based on configuration
            switch (buttons) {
                case DialogButtons_Ok:
                    [alert addButtonWithTitle:@"OK"];
                    break;

                case DialogButtons_OkCancel:
                    [alert addButtonWithTitle:@"OK"];
                    [alert addButtonWithTitle:@"Cancel"];
                    break;

                case DialogButtons_YesNo:
                    [alert addButtonWithTitle:@"Yes"];
                    [alert addButtonWithTitle:@"No"];
                    break;

                case DialogButtons_YesNoCancel:
                    [alert addButtonWithTitle:@"Yes"];
                    [alert addButtonWithTitle:@"No"];
                    [alert addButtonWithTitle:@"Cancel"];
                    break;

                default:
                    [alert addButtonWithTitle:@"OK"];
                    break;
            }

            NSModalResponse response = [alert runModal];

            // Map response to DialogResult
            switch (buttons) {
                case DialogButtons_Ok:
                    result = DialogResult_Ok;
                    break;

                case DialogButtons_OkCancel:
                    result = (response == NSAlertFirstButtonReturn) ? DialogResult_Ok : DialogResult_Cancel;
                    break;

                case DialogButtons_YesNo:
                    result = (response == NSAlertFirstButtonReturn) ? DialogResult_Yes : DialogResult_No;
                    break;

                case DialogButtons_YesNoCancel:
                    if (response == NSAlertFirstButtonReturn) result = DialogResult_Yes;
                    else if (response == NSAlertSecondButtonReturn) result = DialogResult_No;
                    else result = DialogResult_Cancel;
                    break;

                default:
                    result = DialogResult_Ok;
                    break;
            }
        }
    };

    if ([NSThread isMainThread]) {
        work();
    } else {
        dispatch_sync(dispatch_get_main_queue(), work);
    }

    return result;
}
