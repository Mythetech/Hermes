// Copyright (c) Mythetech. Licensed under the MIT License.
#include <string.h>
#include "HermesDragDrop.h"

// How long a capture waits for the page's claim, and a claim for its capture to finish.
#define HERMES_DROP_WAIT_MS 2000

typedef struct {
    HermesDragDrop* owner;
    GdkDragContext* context;
    int count;
    char** targets;
    GBytes** data;
    gboolean* answered;
    int pending;
    gboolean claimed;
    int64_t claimId;
    guint timeout;
} DropCapture;

struct _HermesDragDrop {
    GtkWidget* webView;
    GtkWidget* receiver;
    char** dropTargets;
    DropClaimedCallback claimed;
    DragEndedCallback ended;
    GQueue captures;
    GdkEvent* press;
    GdkDragContext* drag;
    gboolean dragFailed;
    int payloadCount;
    GBytes** payload;
};

static void capture_free(DropCapture* capture) {
    if (capture->timeout != 0) {
        g_source_remove(capture->timeout);
    }
    for (int i = 0; i < capture->count; i++) {
        g_free(capture->targets[i]);
        if (capture->data[i] != NULL) {
            g_bytes_unref(capture->data[i]);
        }
    }
    g_free(capture->targets);
    g_free(capture->data);
    g_free(capture->answered);
    g_object_unref(capture->context);
    g_free(capture);
}

static void capture_deliver(DropCapture* capture) {
    HermesDragDrop* dd = capture->owner;
    g_queue_remove(&dd->captures, capture);

    const char** targets = g_new0(const char*, capture->count + 1);
    const uint8_t** data = g_new0(const uint8_t*, capture->count + 1);
    int* lengths = g_new0(int, capture->count + 1);
    int count = 0;
    for (int i = 0; i < capture->count; i++) {
        if (capture->data[i] == NULL) {
            continue;
        }
        gsize size = 0;
        data[count] = g_bytes_get_data(capture->data[i], &size);
        targets[count] = capture->targets[i];
        lengths[count] = (int)size;
        count++;
    }

    dd->claimed(capture->claimId, targets, data, lengths, count);

    g_free(targets);
    g_free(data);
    g_free(lengths);
    capture_free(capture);
}

static void clear_captures(HermesDragDrop* dd) {
    DropCapture* capture;
    while ((capture = g_queue_pop_head(&dd->captures)) != NULL) {
        capture_free(capture);
    }
}

// Unclaimed captures are dropped; a claim that waited this long gets whatever arrived.
static gboolean on_capture_timeout(gpointer user_data) {
    DropCapture* capture = user_data;
    capture->timeout = 0;
    if (capture->claimed) {
        capture_deliver(capture);
    } else {
        g_queue_remove(&capture->owner->captures, capture);
        capture_free(capture);
    }
    return G_SOURCE_REMOVE;
}

static gboolean is_drop_target(HermesDragDrop* dd, const char* name) {
    for (char** target = dd->dropTargets; *target != NULL; target++) {
        if (g_ascii_strcasecmp(*target, name) == 0) {
            return TRUE;
        }
    }
    return FALSE;
}

static void drop_unclaimed_captures(HermesDragDrop* dd) {
    GList* link = dd->captures.head;
    while (link != NULL) {
        GList* next = link->next;
        DropCapture* capture = link->data;
        if (!capture->claimed) {
            g_queue_delete_link(&dd->captures, link);
            capture_free(capture);
        }
        link = next;
    }
}

static gboolean on_drag_drop(GtkWidget* widget, GdkDragContext* context, gint x, gint y, guint time, gpointer user_data) {
    (void)widget; (void)x; (void)y;
    HermesDragDrop* dd = user_data;

    // One slot per window: a drop the page ignored must never pair with a later claim.
    drop_unclaimed_captures(dd);

    // Drags that start in this process belong to the page or to Hermes itself, not to another app.
    if (gtk_drag_get_source_widget(context) != NULL) {
        return FALSE;
    }

    GList* offered = gdk_drag_context_list_targets(context);
    guint offeredCount = g_list_length(offered);
    DropCapture* capture = g_new0(DropCapture, 1);
    capture->owner = dd;
    capture->context = g_object_ref(context);
    capture->targets = g_new0(char*, offeredCount + 1);
    capture->data = g_new0(GBytes*, offeredCount + 1);
    capture->answered = g_new0(gboolean, offeredCount + 1);
    for (GList* item = offered; item != NULL; item = item->next) {
        gchar* name = gdk_atom_name(GDK_POINTER_TO_ATOM(item->data));
        if (name != NULL && is_drop_target(dd, name)) {
            capture->targets[capture->count++] = name;
        } else {
            g_free(name);
        }
    }
    capture->pending = capture->count;
    capture->timeout = g_timeout_add(HERMES_DROP_WAIT_MS, on_capture_timeout, capture);
    g_queue_push_tail(&dd->captures, capture);

    // The receiver is not a drop site, so GTK delivers every target to it and WebKit's own drop never sees these.
    for (int i = 0; i < capture->count; i++) {
        gtk_drag_get_data(dd->receiver, context, gdk_atom_intern(capture->targets[i], FALSE), time);
    }
    return FALSE;
}

static void on_capture_data(GtkWidget* widget, GdkDragContext* context, gint x, gint y,
                            GtkSelectionData* selection, guint info, guint time, gpointer user_data) {
    (void)widget; (void)x; (void)y; (void)info; (void)time;
    HermesDragDrop* dd = user_data;

    DropCapture* capture = NULL;
    for (GList* item = dd->captures.head; item != NULL; item = item->next) {
        if (((DropCapture*)item->data)->context == context) {
            capture = item->data;
            break;
        }
    }
    if (capture == NULL) {
        return;
    }

    gchar* target = gdk_atom_name(gtk_selection_data_get_target(selection));
    for (int i = 0; i < capture->count; i++) {
        if (!capture->answered[i] && target != NULL && strcmp(capture->targets[i], target) == 0) {
            capture->answered[i] = TRUE;
            capture->pending--;
            gint length = gtk_selection_data_get_length(selection);
            const guchar* bytes = gtk_selection_data_get_data(selection);
            if (length > 0 && bytes != NULL) {
                capture->data[i] = g_bytes_new(bytes, (gsize)length);
            }
            break;
        }
    }
    g_free(target);

    if (capture->pending == 0 && capture->claimed) {
        capture_deliver(capture);
    }
}

void HermesDragDrop_Claim(HermesDragDrop* dd, int64_t claimId) {
    if (dd == NULL) {
        return;
    }

    // Newest first: text dropped into an editable field is captured but never claimed, and must not pair with the next drop.
    DropCapture* capture = NULL;
    for (GList* item = dd->captures.tail; item != NULL; item = item->prev) {
        if (!((DropCapture*)item->data)->claimed) {
            capture = item->data;
            break;
        }
    }
    if (capture == NULL) {
        dd->claimed(claimId, NULL, NULL, NULL, -1);
        return;
    }

    capture->claimed = TRUE;
    capture->claimId = claimId;
    if (capture->pending == 0) {
        capture_deliver(capture);
        return;
    }

    g_source_remove(capture->timeout);
    capture->timeout = g_timeout_add(HERMES_DROP_WAIT_MS, on_capture_timeout, capture);
}

static gboolean on_button_press(GtkWidget* widget, GdkEventButton* event, gpointer user_data) {
    (void)widget;
    HermesDragDrop* dd = user_data;
    if (event->type == GDK_BUTTON_PRESS && event->button == GDK_BUTTON_PRIMARY) {
        g_clear_pointer(&dd->press, gdk_event_free);
        dd->press = gdk_event_copy((GdkEvent*)event);
    }
    return FALSE;
}

static gboolean on_button_release(GtkWidget* widget, GdkEventButton* event, gpointer user_data) {
    (void)widget;
    HermesDragDrop* dd = user_data;
    if (event->button == GDK_BUTTON_PRIMARY) {
        g_clear_pointer(&dd->press, gdk_event_free);
    }
    return FALSE;
}

static gboolean primary_button_held(HermesDragDrop* dd) {
    if (dd->press == NULL) {
        return FALSE;
    }

    GdkWindow* window = gtk_widget_get_window(dd->webView);
    GdkDevice* device = gdk_event_get_device(dd->press);
    if (window == NULL || device == NULL) {
        return FALSE;
    }

    GdkModifierType mask = 0;
    gdk_window_get_device_position(window, device, NULL, NULL, &mask);
    return (mask & GDK_BUTTON1_MASK) != 0;
}

static void free_payload(HermesDragDrop* dd) {
    for (int i = 0; i < dd->payloadCount; i++) {
        g_bytes_unref(dd->payload[i]);
    }
    g_clear_pointer(&dd->payload, g_free);
    dd->payloadCount = 0;
}

static void finish_drag(HermesDragDrop* dd, bool dropped) {
    if (dd->drag == NULL) {
        return;
    }

    g_clear_object(&dd->drag);
    free_payload(dd);
    g_clear_pointer(&dd->press, gdk_event_free);
    dd->ended(dropped);
}

// Completes the drag first, so the signals GTK emits while cancelling it are ignored.
static void cancel_drag(HermesDragDrop* dd) {
    if (dd->drag == NULL) {
        return;
    }

    GdkDragContext* drag = g_object_ref(dd->drag);
    finish_drag(dd, false);
    gtk_drag_cancel(drag);
    g_object_unref(drag);
}

bool HermesDragDrop_BeginDrag(HermesDragDrop* dd, const char** targets, const uint8_t** data, const int* lengths, int count) {
    if (dd == NULL || dd->webView == NULL || dd->drag != NULL || count <= 0 || !primary_button_held(dd)) {
        return false;
    }

    dd->payloadCount = count;
    dd->payload = g_new0(GBytes*, count);
    GtkTargetList* list = gtk_target_list_new(NULL, 0);
    for (int i = 0; i < count; i++) {
        dd->payload[i] = g_bytes_new(data[i], (gsize)lengths[i]);
        gtk_target_list_add(list, gdk_atom_intern(targets[i], FALSE), 0, (guint)i);
    }

    GdkDragContext* drag = gtk_drag_begin_with_coordinates(dd->webView, list, GDK_ACTION_COPY, GDK_BUTTON_PRIMARY, dd->press, -1, -1);
    gtk_target_list_unref(list);
    if (drag == NULL) {
        free_payload(dd);
        return false;
    }

    dd->drag = g_object_ref(drag);
    dd->dragFailed = FALSE;
    return true;
}

static void on_drag_data_get(GtkWidget* widget, GdkDragContext* context, GtkSelectionData* selection,
                             guint info, guint time, gpointer user_data) {
    (void)time;
    HermesDragDrop* dd = user_data;
    if (context != dd->drag) {
        return;
    }

    if (info < (guint)dd->payloadCount) {
        gsize size = 0;
        const guchar* bytes = g_bytes_get_data(dd->payload[info], &size);
        gtk_selection_data_set(selection, gtk_selection_data_get_target(selection), 8,
                               bytes != NULL ? bytes : (const guchar*)"", (gint)size);
    }
    g_signal_stop_emission_by_name(widget, "drag-data-get");
}

static gboolean on_drag_failed(GtkWidget* widget, GdkDragContext* context, GtkDragResult result, gpointer user_data) {
    (void)widget; (void)result;
    HermesDragDrop* dd = user_data;
    if (context == dd->drag) {
        dd->dragFailed = TRUE;
    }
    return FALSE;
}

static void on_drag_end(GtkWidget* widget, GdkDragContext* context, gpointer user_data) {
    (void)widget;
    HermesDragDrop* dd = user_data;
    if (context == dd->drag) {
        finish_drag(dd, !dd->dragFailed && gdk_drag_drop_succeeded(context));
    }
}

static void on_web_view_destroyed(GtkWidget* widget, gpointer user_data) {
    HermesDragDrop* dd = user_data;
    cancel_drag(dd);
    clear_captures(dd);
    g_signal_handlers_disconnect_by_data(widget, dd);
    dd->webView = NULL;
}

HermesDragDrop* HermesDragDrop_Attach(GtkWidget* webView, WebKitUserContentManager* contentManager, const char* script,
                                      const char** dropTargets, int dropTargetCount,
                                      DropClaimedCallback claimed, DragEndedCallback ended) {
    HermesDragDrop* dd = g_new0(HermesDragDrop, 1);
    dd->webView = webView;
    dd->claimed = claimed;
    dd->ended = ended;
    dd->dropTargets = g_new0(char*, dropTargetCount + 1);
    for (int i = 0; i < dropTargetCount; i++) {
        dd->dropTargets[i] = g_strdup(dropTargets[i]);
    }
    g_queue_init(&dd->captures);

    dd->receiver = gtk_invisible_new();
    g_signal_connect(dd->receiver, "drag-data-received", G_CALLBACK(on_capture_data), dd);

    g_signal_connect(webView, "drag-drop", G_CALLBACK(on_drag_drop), dd);
    g_signal_connect(webView, "button-press-event", G_CALLBACK(on_button_press), dd);
    g_signal_connect(webView, "button-release-event", G_CALLBACK(on_button_release), dd);
    g_signal_connect(webView, "drag-data-get", G_CALLBACK(on_drag_data_get), dd);
    g_signal_connect(webView, "drag-failed", G_CALLBACK(on_drag_failed), dd);
    g_signal_connect(webView, "drag-end", G_CALLBACK(on_drag_end), dd);
    g_signal_connect(webView, "destroy", G_CALLBACK(on_web_view_destroyed), dd);

    // Top frame only: a reset from an iframe's document would release the window's live drops.
    WebKitUserScript* userScript = webkit_user_script_new(script, WEBKIT_USER_CONTENT_INJECT_TOP_FRAME,
                                                          WEBKIT_USER_SCRIPT_INJECT_AT_DOCUMENT_START, NULL, NULL);
    webkit_user_content_manager_add_script(contentManager, userScript);
    webkit_user_script_unref(userScript);

    return dd;
}

void HermesDragDrop_Detach(HermesDragDrop* dd) {
    if (dd == NULL) {
        return;
    }

    if (dd->webView != NULL) {
        cancel_drag(dd);
        g_signal_handlers_disconnect_by_data(dd->webView, dd);
    }
    clear_captures(dd);
    g_signal_handlers_disconnect_by_data(dd->receiver, dd);
    gtk_widget_destroy(dd->receiver);
    g_clear_pointer(&dd->press, gdk_event_free);
    g_strfreev(dd->dropTargets);
    g_free(dd);
}
