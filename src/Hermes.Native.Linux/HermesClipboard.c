// Copyright (c) Mythetech. Licensed under the MIT License.
#include <gtk/gtk.h>
#include "HermesClipboard.h"
#include "HermesWindow.h"

typedef struct {
    int count;
    GtkTargetEntry* entries;
    guchar** data;
    int* lengths;
} OwnedContents;

typedef struct {
    GSourceFunc work;
    gpointer data;
    GMutex mutex;
    GCond cond;
    gboolean done;
} UiCall;

static gulong g_ownerChangeHandler = 0;
static gpointer g_changedCallback = NULL;

static GtkClipboard* hermes_clipboard(void) {
    return gtk_clipboard_get(GDK_SELECTION_CLIPBOARD);
}

static gboolean run_ui_call(gpointer user_data) {
    UiCall* call = user_data;
    call->work(call->data);
    g_mutex_lock(&call->mutex);
    call->done = TRUE;
    g_cond_signal(&call->cond);
    g_mutex_unlock(&call->mutex);
    return G_SOURCE_REMOVE;
}

// GTK clipboard calls must run on the GTK thread; wait_for_* calls spin a nested loop there.
static void run_on_ui_sync(GSourceFunc work, gpointer data) {
    if (hermes_is_ui_thread()) {
        work(data);
        return;
    }

    UiCall call = { .work = work, .data = data, .done = FALSE };
    g_mutex_init(&call.mutex);
    g_cond_init(&call.cond);
    g_idle_add(run_ui_call, &call);

    g_mutex_lock(&call.mutex);
    while (!call.done) {
        g_cond_wait(&call.cond, &call.mutex);
    }
    g_mutex_unlock(&call.mutex);
    g_mutex_clear(&call.mutex);
    g_cond_clear(&call.cond);
}

// Never blocks, so monitoring can be stopped after the GTK loop has ended.
static void run_on_ui_async(GSourceFunc work) {
    if (hermes_is_ui_thread()) {
        work(NULL);
    } else {
        g_idle_add(work, NULL);
    }
}

typedef struct { char** targets; int count; } TargetsCall;

static gboolean get_targets_work(gpointer data) {
    TargetsCall* call = data;
    GdkAtom* atoms = NULL;
    gint count = 0;
    if (gtk_clipboard_wait_for_targets(hermes_clipboard(), &atoms, &count) && count > 0) {
        call->targets = g_new0(char*, count);
        for (gint i = 0; i < count; i++) {
            gchar* name = gdk_atom_name(atoms[i]);
            if (name != NULL) {
                call->targets[call->count++] = name;
            }
        }
    }
    g_free(atoms);
    return G_SOURCE_REMOVE;
}

char** HermesClipboard_GetTargets(int* count) {
    TargetsCall call = { 0 };
    run_on_ui_sync(get_targets_work, &call);
    *count = call.count;
    return call.targets;
}

typedef struct { char* text; } TextCall;

static gboolean read_text_work(gpointer data) {
    TextCall* call = data;
    call->text = gtk_clipboard_wait_for_text(hermes_clipboard());
    return G_SOURCE_REMOVE;
}

char* HermesClipboard_ReadText(void) {
    TextCall call = { 0 };
    run_on_ui_sync(read_text_work, &call);
    return call.text;
}

typedef struct { const char* target; uint8_t* bytes; int length; } BytesCall;

static gboolean read_contents_work(gpointer data) {
    BytesCall* call = data;
    GtkSelectionData* selection = gtk_clipboard_wait_for_contents(hermes_clipboard(), gdk_atom_intern(call->target, FALSE));
    if (selection != NULL) {
        gint length = gtk_selection_data_get_length(selection);
        const guchar* raw = gtk_selection_data_get_data(selection);
        if (length > 0 && raw != NULL) {
            call->bytes = g_memdup2(raw, (gsize)length);
            call->length = length;
        }
        gtk_selection_data_free(selection);
    }
    return G_SOURCE_REMOVE;
}

uint8_t* HermesClipboard_ReadContents(const char* target, int* length) {
    BytesCall call = { .target = target };
    run_on_ui_sync(read_contents_work, &call);
    *length = call.length;
    return call.bytes;
}

static gboolean read_image_work(gpointer data) {
    BytesCall* call = data;
    GdkPixbuf* pixbuf = gtk_clipboard_wait_for_image(hermes_clipboard());
    if (pixbuf != NULL) {
        gchar* buffer = NULL;
        gsize size = 0;
        if (gdk_pixbuf_save_to_buffer(pixbuf, &buffer, &size, "png", NULL, NULL) && size > 0) {
            call->bytes = (uint8_t*)buffer;
            call->length = (int)size;
        } else {
            g_free(buffer);
        }
        g_object_unref(pixbuf);
    }
    return G_SOURCE_REMOVE;
}

uint8_t* HermesClipboard_ReadImageAsPng(int* length) {
    BytesCall call = { 0 };
    run_on_ui_sync(read_image_work, &call);
    *length = call.length;
    return call.bytes;
}

static void owned_get(GtkClipboard* clipboard, GtkSelectionData* selection, guint info, gpointer user_data) {
    (void)clipboard;
    OwnedContents* owned = user_data;
    if (info < (guint)owned->count) {
        gtk_selection_data_set(selection, gtk_selection_data_get_target(selection), 8, owned->data[info], owned->lengths[info]);
    }
}

static void owned_clear(GtkClipboard* clipboard, gpointer user_data) {
    (void)clipboard;
    OwnedContents* owned = user_data;
    for (int i = 0; i < owned->count; i++) {
        g_free(owned->entries[i].target);
        g_free(owned->data[i]);
    }
    g_free(owned->entries);
    g_free(owned->data);
    g_free(owned->lengths);
    g_free(owned);
}

typedef struct {
    const char** targets;
    const uint8_t** data;
    const int* lengths;
    int count;
    gboolean written;
} WriteCall;

static gboolean write_work(gpointer data) {
    WriteCall* call = data;
    OwnedContents* owned = g_new0(OwnedContents, 1);
    owned->count = call->count;
    owned->entries = g_new0(GtkTargetEntry, call->count);
    owned->data = g_new0(guchar*, call->count);
    owned->lengths = g_new0(int, call->count);
    for (int i = 0; i < call->count; i++) {
        owned->entries[i].target = g_strdup(call->targets[i]);
        owned->entries[i].info = (guint)i;
        owned->data[i] = g_memdup2(call->data[i], (gsize)call->lengths[i]);
        owned->lengths[i] = call->lengths[i];
    }

    GtkClipboard* clipboard = hermes_clipboard();
    call->written = gtk_clipboard_set_with_data(clipboard, owned->entries, (guint)call->count, owned_get, owned_clear, owned);
    if (call->written) {
        // Lets a running clipboard manager keep the contents after the app exits.
        gtk_clipboard_set_can_store(clipboard, NULL, 0);
    } else {
        owned_clear(clipboard, owned);
    }
    return G_SOURCE_REMOVE;
}

bool HermesClipboard_Write(const char** targets, const uint8_t** data, const int* lengths, int count) {
    WriteCall call = { .targets = targets, .data = data, .lengths = lengths, .count = count };
    run_on_ui_sync(write_work, &call);
    return call.written;
}

static gboolean clear_work(gpointer data) {
    (void)data;
    gtk_clipboard_clear(hermes_clipboard());
    return G_SOURCE_REMOVE;
}

void HermesClipboard_Clear(void) {
    run_on_ui_sync(clear_work, NULL);
}

static void on_owner_change(GtkClipboard* clipboard, GdkEvent* event, gpointer user_data) {
    (void)clipboard; (void)event; (void)user_data;
    HermesClipboardChangedCallback changed = (HermesClipboardChangedCallback)g_atomic_pointer_get(&g_changedCallback);
    if (changed != NULL) {
        changed();
    }
}

// Reconciles the signal connection with the latest request, so a stale queued call cannot undo a newer one.
static gboolean apply_monitoring_state(gpointer data) {
    (void)data;
    gboolean wanted = g_atomic_pointer_get(&g_changedCallback) != NULL;
    if (wanted && g_ownerChangeHandler == 0) {
        g_ownerChangeHandler = g_signal_connect(hermes_clipboard(), "owner-change", G_CALLBACK(on_owner_change), NULL);
    } else if (!wanted && g_ownerChangeHandler != 0) {
        g_signal_handler_disconnect(hermes_clipboard(), g_ownerChangeHandler);
        g_ownerChangeHandler = 0;
    }
    return G_SOURCE_REMOVE;
}

void HermesClipboard_StartMonitoring(HermesClipboardChangedCallback callback) {
    g_atomic_pointer_set(&g_changedCallback, (gpointer)callback);
    run_on_ui_async(apply_monitoring_state);
}

void HermesClipboard_StopMonitoring(void) {
    g_atomic_pointer_set(&g_changedCallback, NULL);
    run_on_ui_async(apply_monitoring_state);
}
