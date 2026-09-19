// Copyright (c) Mythetech. Licensed under the MIT License.
#include "HermesNotifications.h"
#include "Exports.h"
#include <stdlib.h>
#include <string.h>

#define NOTIFY_BUS_NAME "org.freedesktop.Notifications"
#define NOTIFY_OBJECT_PATH "/org/freedesktop/Notifications"
#define NOTIFY_INTERFACE "org.freedesktop.Notifications"
#define CAPABILITIES_TIMEOUT_MS 2000

typedef struct {
    HermesNotifications* notifications;
    char* id;
    NotificationCompletionCallback callback;
    void* context;
} ShowCall;

// ============================================================================
// Signals (delivered on the GLib main context, which is the GTK UI thread)
// ============================================================================

static void on_notification_signal(GDBusConnection* connection,
                                   const gchar* sender_name,
                                   const gchar* object_path,
                                   const gchar* interface_name,
                                   const gchar* signal_name,
                                   GVariant* parameters,
                                   gpointer user_data) {
    (void)connection; (void)sender_name; (void)object_path; (void)interface_name;
    HermesNotifications* n = (HermesNotifications*)user_data;

    if (g_strcmp0(signal_name, "ActionInvoked") == 0) {
        guint32 daemon_id = 0;
        const gchar* action_key = NULL;
        g_variant_get(parameters, "(u&s)", &daemon_id, &action_key);

        const char* id = g_hash_table_lookup(n->ids_by_daemon_id, GUINT_TO_POINTER(daemon_id));
        if (id && n->click_callback && g_strcmp0(action_key, "default") == 0) {
            n->click_callback(id);
        }
    } else if (g_strcmp0(signal_name, "NotificationClosed") == 0) {
        guint32 daemon_id = 0;
        guint32 reason = 0;
        g_variant_get(parameters, "(uu)", &daemon_id, &reason);
        g_hash_table_remove(n->ids_by_daemon_id, GUINT_TO_POINTER(daemon_id));
    }
}

// ============================================================================
// Lifecycle
// ============================================================================

HermesNotifications* hermes_notifications_new(const char* appName, const char* iconPath, NotificationClickedCallback clickCallback) {
    HermesNotifications* n = calloc(1, sizeof(HermesNotifications));
    if (!n) return NULL;

    n->cancellable = g_cancellable_new();
    n->click_callback = clickCallback;
    n->app_name = g_strdup(appName && *appName ? appName : "Hermes");
    n->icon_path = g_strdup(iconPath ? iconPath : "");
    n->ids_by_daemon_id = g_hash_table_new_full(g_direct_hash, g_direct_equal, NULL, g_free);

    GError* error = NULL;
    n->connection = g_bus_get_sync(G_BUS_TYPE_SESSION, NULL, &error);
    if (!n->connection) {
        n->unsupported_reason = g_strdup_printf("session bus unavailable: %s", error ? error->message : "unknown error");
        g_clear_error(&error);
        return n;
    }

    // GetCapabilities doubles as the liveness probe; it auto-starts an activatable daemon.
    GVariant* reply = g_dbus_connection_call_sync(n->connection,
        NOTIFY_BUS_NAME, NOTIFY_OBJECT_PATH, NOTIFY_INTERFACE, "GetCapabilities",
        NULL, G_VARIANT_TYPE("(as)"), G_DBUS_CALL_FLAGS_NONE, CAPABILITIES_TIMEOUT_MS, NULL, &error);
    if (!reply) {
        n->unsupported_reason = g_strdup_printf("notification daemon unavailable: %s", error ? error->message : "unknown error");
        g_clear_error(&error);
        return n;
    }
    g_variant_unref(reply);

    n->action_subscription = g_dbus_connection_signal_subscribe(n->connection,
        NOTIFY_BUS_NAME, NOTIFY_INTERFACE, "ActionInvoked", NOTIFY_OBJECT_PATH, NULL,
        G_DBUS_SIGNAL_FLAGS_NONE, on_notification_signal, n, NULL);
    n->closed_subscription = g_dbus_connection_signal_subscribe(n->connection,
        NOTIFY_BUS_NAME, NOTIFY_INTERFACE, "NotificationClosed", NOTIFY_OBJECT_PATH, NULL,
        G_DBUS_SIGNAL_FLAGS_NONE, on_notification_signal, n, NULL);

    n->supported = TRUE;
    return n;
}

void hermes_notifications_destroy(HermesNotifications* n) {
    if (!n) return;

    // A Notify reply can arrive after this call frees n, so cancel before tearing anything down:
    // cancelled calls complete through the error branch, which never touches call->notifications.
    g_cancellable_cancel(n->cancellable);

    if (n->connection) {
        if (n->action_subscription) g_dbus_connection_signal_unsubscribe(n->connection, n->action_subscription);
        if (n->closed_subscription) g_dbus_connection_signal_unsubscribe(n->connection, n->closed_subscription);
        g_object_unref(n->connection);
    }

    g_hash_table_destroy(n->ids_by_daemon_id);
    g_free(n->app_name);
    g_free(n->icon_path);
    g_free(n->unsupported_reason);
    g_object_unref(n->cancellable);
    free(n);
}

// ============================================================================
// Show
// ============================================================================

static void on_notify_reply(GObject* source, GAsyncResult* result, gpointer user_data) {
    ShowCall* call = (ShowCall*)user_data;
    GError* error = NULL;

    GVariant* reply = g_dbus_connection_call_finish(G_DBUS_CONNECTION(source), result, &error);
    if (!reply) {
        if (call->callback) call->callback(call->context, error ? error->message : "Notify failed");
        g_clear_error(&error);
        g_free(call->id);
        g_free(call);
        return;
    }

    guint32 daemon_id = 0;
    g_variant_get(reply, "(u)", &daemon_id);
    g_variant_unref(reply);

    // The table takes ownership of call->id.
    g_hash_table_insert(call->notifications->ids_by_daemon_id, GUINT_TO_POINTER(daemon_id), call->id);
    if (call->callback) call->callback(call->context, NULL);
    g_free(call);
}

static guint32 find_daemon_id(HermesNotifications* n, const char* id) {
    GHashTableIter iter;
    gpointer key = NULL;
    gpointer value = NULL;
    g_hash_table_iter_init(&iter, n->ids_by_daemon_id);
    while (g_hash_table_iter_next(&iter, &key, &value)) {
        if (g_strcmp0((const char*)value, id) == 0) {
            return GPOINTER_TO_UINT(key);
        }
    }
    return 0;
}

static void close_daemon_id(HermesNotifications* n, guint32 daemon_id) {
    g_dbus_connection_call(n->connection,
        NOTIFY_BUS_NAME, NOTIFY_OBJECT_PATH, NOTIFY_INTERFACE, "CloseNotification",
        g_variant_new("(u)", daemon_id), NULL, G_DBUS_CALL_FLAGS_NONE, -1, NULL, NULL, NULL);
}

// ============================================================================
// Exports
// ============================================================================

void* Hermes_Notifications_Create(const char* appName, const char* iconPath, NotificationClickedCallback clickCallback) {
    return hermes_notifications_new(appName, iconPath, clickCallback);
}

bool Hermes_Notifications_IsSupported(void* center, const char** reason) {
    HermesNotifications* n = (HermesNotifications*)center;
    if (!n) {
        if (reason) *reason = "notification center was not created";
        return false;
    }
    if (reason) *reason = n->supported ? NULL : n->unsupported_reason;
    return n->supported;
}

void Hermes_Notifications_RequestPermission(void* center, NotificationPermissionCallback callback, void* context) {
    HermesNotifications* n = (HermesNotifications*)center;
    if (callback) callback(context, n && n->supported);
}

void Hermes_Notifications_Show(void* center, const char* id, const char* title, const char* body,
                               const char* iconPath, bool silent,
                               NotificationCompletionCallback callback, void* context) {
    HermesNotifications* n = (HermesNotifications*)center;
    if (!n || !n->supported) {
        if (callback) callback(context, "notifications are not supported on this host");
        return;
    }

    GVariantBuilder actions;
    g_variant_builder_init(&actions, G_VARIANT_TYPE("as"));
    g_variant_builder_add(&actions, "s", "default");
    g_variant_builder_add(&actions, "s", "Open");

    GVariantBuilder hints;
    g_variant_builder_init(&hints, G_VARIANT_TYPE("a{sv}"));
    if (iconPath && *iconPath) {
        g_variant_builder_add(&hints, "{sv}", "image-path", g_variant_new_string(iconPath));
    }
    if (silent) {
        g_variant_builder_add(&hints, "{sv}", "suppress-sound", g_variant_new_boolean(TRUE));
    }

    ShowCall* call = g_new0(ShowCall, 1);
    call->notifications = n;
    call->id = g_strdup(id);
    call->callback = callback;
    call->context = context;

    g_dbus_connection_call(n->connection,
        NOTIFY_BUS_NAME, NOTIFY_OBJECT_PATH, NOTIFY_INTERFACE, "Notify",
        g_variant_new("(susssasa{sv}i)",
            n->app_name, (guint32)0, n->icon_path, title, body ? body : "", &actions, &hints, (gint32)-1),
        G_VARIANT_TYPE("(u)"), G_DBUS_CALL_FLAGS_NONE, -1, n->cancellable, on_notify_reply, call);
}

void Hermes_Notifications_Dismiss(void* center, const char* id) {
    HermesNotifications* n = (HermesNotifications*)center;
    if (!n || !n->supported || !id) return;

    guint32 daemon_id = find_daemon_id(n, id);
    if (daemon_id != 0) close_daemon_id(n, daemon_id);
}

void Hermes_Notifications_DismissAll(void* center) {
    HermesNotifications* n = (HermesNotifications*)center;
    if (!n || !n->supported) return;

    GList* keys = g_hash_table_get_keys(n->ids_by_daemon_id);
    for (GList* item = keys; item != NULL; item = item->next) {
        close_daemon_id(n, GPOINTER_TO_UINT(item->data));
    }
    g_list_free(keys);
}

void Hermes_Notifications_Destroy(void* center) {
    hermes_notifications_destroy((HermesNotifications*)center);
}
