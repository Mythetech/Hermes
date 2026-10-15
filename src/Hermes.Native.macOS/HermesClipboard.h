// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HermesClipboard_h
#define HermesClipboard_h

#include <stdbool.h>
#include <stdint.h>

typedef void (*HermesClipboardChangedCallback)(void);

int HermesClipboard_GetFormats(void);
bool HermesClipboard_HasType(const char* mimeType);
char* HermesClipboard_ReadText(void);
char* HermesClipboard_ReadHtml(void);
uint8_t* HermesClipboard_ReadPng(int* length);
char** HermesClipboard_ReadFiles(int* count);
uint8_t* HermesClipboard_ReadCustom(const char* mimeType, int* length);
bool HermesClipboard_Write(const char* text, const char* html, const uint8_t* png, int pngLength,
                           const char** files, int fileCount,
                           const char** customTypes, const uint8_t** customData, const int* customLengths, int customCount);
void HermesClipboard_Clear(void);
void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback);
void HermesClipboard_StopMonitoring(void);

#endif
