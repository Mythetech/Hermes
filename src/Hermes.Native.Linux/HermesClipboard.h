// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HermesClipboard_h
#define HermesClipboard_h

#include <stdbool.h>
#include <stdint.h>

typedef void (*HermesClipboardChangedCallback)(void);

char** HermesClipboard_GetTargets(int* count);
char* HermesClipboard_ReadText(void);
uint8_t* HermesClipboard_ReadContents(const char* target, int* length);
uint8_t* HermesClipboard_ReadImageAsPng(int* length);
bool HermesClipboard_Write(const char** targets, const uint8_t** data, const int* lengths, int count);
void HermesClipboard_Clear(void);
void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback);
void HermesClipboard_StopMonitoring(void);

#endif
