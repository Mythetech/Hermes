// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HERMES_HOTKEYS_H
#define HERMES_HOTKEYS_H

#include "HermesTypes.h"

void HermesHotkeys_SetAppId(const char* appId);
void HermesHotkeys_Start(HotkeyActivatedCallback activated, HotkeyRegisteredCallback registered);
void HermesHotkeys_Register(int number, const char* id, const char* description,
                            const char* keysym, unsigned int modifiers, const char* portalTrigger);
void HermesHotkeys_Unregister(int number);

#endif
