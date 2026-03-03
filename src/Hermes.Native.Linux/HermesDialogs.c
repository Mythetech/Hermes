// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#include "Exports.h"
#include "HermesWindow.h"
#include <gtk/gtk.h>
#include <string.h>
#include <stdio.h>

// Helper to extract GtkWindow from HermesWindow handle
static GtkWindow* get_gtk_window(void* hermesWindow) {
    if (!hermesWindow) return NULL;
    HermesWindow* hw = (HermesWindow*)hermesWindow;
    return GTK_WINDOW(hw->window);
}

// ============================================================================
// File Dialogs
// ============================================================================

char** Hermes_Dialog_ShowOpenFile(void* parentWindow, const char* title, const char* defaultPath,
                                   bool multiSelect, const char** filters,
                                   int filterCount, int* resultCount) {
    *resultCount = 0;

    GtkWidget* dialog = gtk_file_chooser_dialog_new(
        title ? title : "Open File",
        get_gtk_window(parentWindow),
        GTK_FILE_CHOOSER_ACTION_OPEN,
        "_Cancel", GTK_RESPONSE_CANCEL,
        "_Open", GTK_RESPONSE_ACCEPT,
        NULL);

    gtk_file_chooser_set_select_multiple(GTK_FILE_CHOOSER(dialog), multiSelect);

    if (defaultPath && defaultPath[0]) {
        gtk_file_chooser_set_current_folder(GTK_FILE_CHOOSER(dialog), defaultPath);
    }

    // Add filters
    for (int i = 0; i < filterCount && filters[i]; i += 2) {
        GtkFileFilter* filter = gtk_file_filter_new();
        gtk_file_filter_set_name(filter, filters[i]);
        if (i + 1 < filterCount && filters[i + 1]) {
            // Parse pattern (e.g., "*.txt;*.md" -> multiple patterns)
            char* patterns = g_strdup(filters[i + 1]);
            char* saveptr;
            char* pattern = strtok_r(patterns, ";", &saveptr);
            while (pattern) {
                gtk_file_filter_add_pattern(filter, pattern);
                pattern = strtok_r(NULL, ";", &saveptr);
            }
            g_free(patterns);
        }
        gtk_file_chooser_add_filter(GTK_FILE_CHOOSER(dialog), filter);
    }

    char** results = NULL;
    if (gtk_dialog_run(GTK_DIALOG(dialog)) == GTK_RESPONSE_ACCEPT) {
        GSList* filenames = gtk_file_chooser_get_filenames(GTK_FILE_CHOOSER(dialog));
        int count = g_slist_length(filenames);

        if (count > 0) {
            results = g_new0(char*, count + 1);
            GSList* iter = filenames;
            for (int i = 0; i < count && iter; i++, iter = iter->next) {
                results[i] = g_strdup((char*)iter->data);
            }
            *resultCount = count;
        }

        g_slist_free_full(filenames, g_free);
    }

    gtk_widget_destroy(dialog);
    return results;
}

char** Hermes_Dialog_ShowOpenFolder(void* parentWindow, const char* title, const char* defaultPath,
                                     bool multiSelect, int* resultCount) {
    *resultCount = 0;

    GtkWidget* dialog = gtk_file_chooser_dialog_new(
        title ? title : "Select Folder",
        get_gtk_window(parentWindow),
        GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER,
        "_Cancel", GTK_RESPONSE_CANCEL,
        "_Select", GTK_RESPONSE_ACCEPT,
        NULL);

    gtk_file_chooser_set_select_multiple(GTK_FILE_CHOOSER(dialog), multiSelect);

    if (defaultPath && defaultPath[0]) {
        gtk_file_chooser_set_current_folder(GTK_FILE_CHOOSER(dialog), defaultPath);
    }

    char** results = NULL;
    if (gtk_dialog_run(GTK_DIALOG(dialog)) == GTK_RESPONSE_ACCEPT) {
        GSList* filenames = gtk_file_chooser_get_filenames(GTK_FILE_CHOOSER(dialog));
        int count = g_slist_length(filenames);

        if (count > 0) {
            results = g_new0(char*, count + 1);
            GSList* iter = filenames;
            for (int i = 0; i < count && iter; i++, iter = iter->next) {
                results[i] = g_strdup((char*)iter->data);
            }
            *resultCount = count;
        }

        g_slist_free_full(filenames, g_free);
    }

    gtk_widget_destroy(dialog);
    return results;
}

char* Hermes_Dialog_ShowSaveFile(void* parentWindow, const char* title, const char* defaultPath,
                                  const char** filters, int filterCount,
                                  const char* defaultFileName) {
    GtkWidget* dialog = gtk_file_chooser_dialog_new(
        title ? title : "Save File",
        get_gtk_window(parentWindow),
        GTK_FILE_CHOOSER_ACTION_SAVE,
        "_Cancel", GTK_RESPONSE_CANCEL,
        "_Save", GTK_RESPONSE_ACCEPT,
        NULL);

    gtk_file_chooser_set_do_overwrite_confirmation(GTK_FILE_CHOOSER(dialog), TRUE);

    if (defaultPath && defaultPath[0]) {
        gtk_file_chooser_set_current_folder(GTK_FILE_CHOOSER(dialog), defaultPath);
    }

    if (defaultFileName && defaultFileName[0]) {
        gtk_file_chooser_set_current_name(GTK_FILE_CHOOSER(dialog), defaultFileName);
    }

    // Add filters
    for (int i = 0; i < filterCount && filters[i]; i += 2) {
        GtkFileFilter* filter = gtk_file_filter_new();
        gtk_file_filter_set_name(filter, filters[i]);
        if (i + 1 < filterCount && filters[i + 1]) {
            char* patterns = g_strdup(filters[i + 1]);
            char* saveptr;
            char* pattern = strtok_r(patterns, ";", &saveptr);
            while (pattern) {
                gtk_file_filter_add_pattern(filter, pattern);
                pattern = strtok_r(NULL, ";", &saveptr);
            }
            g_free(patterns);
        }
        gtk_file_chooser_add_filter(GTK_FILE_CHOOSER(dialog), filter);
    }

    char* result = NULL;
    if (gtk_dialog_run(GTK_DIALOG(dialog)) == GTK_RESPONSE_ACCEPT) {
        result = gtk_file_chooser_get_filename(GTK_FILE_CHOOSER(dialog));
    }

    gtk_widget_destroy(dialog);
    return result;
}

// ============================================================================
// Message Dialog
// ============================================================================

int Hermes_Dialog_ShowMessage(void* parentWindow, const char* title, const char* message,
                               int buttons, int icon) {
    GtkMessageType messageType;
    switch (icon) {
        case DialogIcon_Warning:  messageType = GTK_MESSAGE_WARNING; break;
        case DialogIcon_Error:    messageType = GTK_MESSAGE_ERROR; break;
        case DialogIcon_Question: messageType = GTK_MESSAGE_QUESTION; break;
        default:                  messageType = GTK_MESSAGE_INFO; break;
    }

    GtkButtonsType buttonType;
    switch (buttons) {
        case DialogButtons_OkCancel:    buttonType = GTK_BUTTONS_OK_CANCEL; break;
        case DialogButtons_YesNo:       buttonType = GTK_BUTTONS_YES_NO; break;
        case DialogButtons_YesNoCancel:
            // GTK doesn't have YES_NO_CANCEL, we'll add Cancel manually
            buttonType = GTK_BUTTONS_YES_NO;
            break;
        default:                        buttonType = GTK_BUTTONS_OK; break;
    }

    GtkWidget* dialog = gtk_message_dialog_new(
        get_gtk_window(parentWindow),
        GTK_DIALOG_MODAL | GTK_DIALOG_DESTROY_WITH_PARENT,
        messageType,
        buttonType,
        "%s", message);

    if (title && title[0]) {
        gtk_window_set_title(GTK_WINDOW(dialog), title);
    }

    // Add Cancel button for YesNoCancel
    if (buttons == DialogButtons_YesNoCancel) {
        gtk_dialog_add_button(GTK_DIALOG(dialog), "_Cancel", GTK_RESPONSE_CANCEL);
    }

    gint response = gtk_dialog_run(GTK_DIALOG(dialog));
    gtk_widget_destroy(dialog);

    switch (response) {
        case GTK_RESPONSE_OK:
        case GTK_RESPONSE_ACCEPT:
            return DialogResult_Ok;
        case GTK_RESPONSE_CANCEL:
        case GTK_RESPONSE_DELETE_EVENT:
            return DialogResult_Cancel;
        case GTK_RESPONSE_YES:
            return DialogResult_Yes;
        case GTK_RESPONSE_NO:
            return DialogResult_No;
        default:
            return DialogResult_Cancel;
    }
}
