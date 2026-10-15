// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HermesClipboard_h
#define HermesClipboard_h

#import <Cocoa/Cocoa.h>
#include <stdbool.h>
#include <stdint.h>
#include "HermesTypes.h"

typedef void (*HermesClipboardChangedCallback)(void);

int HermesClipboard_GetFormats(void);
bool HermesClipboard_HasType(const char* mimeType);
char* HermesClipboard_ReadText(void);
char* HermesClipboard_ReadHtml(void);
char* HermesClipboard_ReadUrl(void);
uint8_t* HermesClipboard_ReadPng(int* length);
char** HermesClipboard_ReadFiles(int* count);
uint8_t* HermesClipboard_ReadCustom(const char* mimeType, int* length);
bool HermesClipboard_Write(const HermesNativePackage* package);
void HermesClipboard_Clear(void);
void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback);
void HermesClipboard_StopMonitoring(void);

// Pasteboard building blocks, usable with any pasteboard. Main thread only.
NSPasteboardType HermesPasteboard_TypeForMime(NSString* mimeType);
NSPasteboardItem* HermesPasteboard_CreateItem(const HermesNativePackage* package);
NSArray<NSURL*>* HermesPasteboard_FileUrls(const HermesNativePackage* package);

#endif
