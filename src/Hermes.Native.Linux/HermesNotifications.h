// Copyright (c) Mythetech. Licensed under the MIT License.
#ifndef HERMES_NOTIFICATIONS_H
#define HERMES_NOTIFICATIONS_H

#include <gio/gio.h>
#include "HermesTypes.h"

typedef struct _HermesNotifications HermesNotifications;

struct _HermesNotifications {
    GDBusConnection* connection;
    guint action_subscription;
    guint closed_subscription;
    GHashTable* ids_by_daemon_id;   // guint32 daemon id -> char* Hermes id (owned)
    GCancellable* cancellable;      // cancels in-flight calls so replies cannot outlive the struct
    char* app_name;
    char* icon_path;
    NotificationClickedCallback click_callback;
    gboolean supported;
    char* unsupported_reason;
};

HermesNotifications* hermes_notifications_new(const char* appName, const char* iconPath, NotificationClickedCallback clickCallback);
void hermes_notifications_destroy(HermesNotifications* notifications);

#endif // HERMES_NOTIFICATIONS_H
