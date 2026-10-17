// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HermesHotkeys_h
#define HermesHotkeys_h

#include <stdint.h>

typedef void (*HermesHotkeyPressedCallback)(uint32_t hotkeyNumber);

void HermesHotkeys_SetCallback(HermesHotkeyPressedCallback callback);
int32_t HermesHotkeys_Register(uint32_t hotkeyNumber, uint32_t keyCode, uint32_t modifiers);
void HermesHotkeys_Unregister(uint32_t hotkeyNumber);

#endif
