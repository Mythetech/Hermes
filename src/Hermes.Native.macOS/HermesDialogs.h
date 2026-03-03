// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#ifndef HERMES_DIALOGS_H
#define HERMES_DIALOGS_H

#import <Cocoa/Cocoa.h>
#import "HermesTypes.h"

// File dialog functions
char** Hermes_ShowOpenFileDialog(const char* title,
                                  const char* defaultPath,
                                  bool multiSelect,
                                  const char** filters,
                                  int filterCount,
                                  int* resultCount);

char** Hermes_ShowOpenFolderDialog(const char* title,
                                    const char* defaultPath,
                                    bool multiSelect,
                                    int* resultCount);

char* Hermes_ShowSaveFileDialog(const char* title,
                                 const char* defaultPath,
                                 const char** filters,
                                 int filterCount,
                                 const char* defaultFileName);

// Message dialog function
int Hermes_ShowMessageDialog(const char* title,
                              const char* message,
                              int buttons,
                              int icon);

#endif // HERMES_DIALOGS_H
