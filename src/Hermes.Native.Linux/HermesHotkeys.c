// Copyright (c) Mythetech. Licensed under the MIT License.
#include <gtk/gtk.h>
#include <string.h>
#include "HermesHotkeys.h"

#ifdef GDK_WINDOWING_X11
#include <gdk/gdkx.h>
#include <X11/XKBlib.h>
#include <X11/keysym.h>
#endif

#ifdef GDK_WINDOWING_WAYLAND
#include <gdk/gdkwayland.h>
#endif

#define PORTAL_BUS_NAME "org.freedesktop.portal.Desktop"
#define PORTAL_OBJECT_PATH "/org/freedesktop/portal/desktop"
#define REGISTRY_INTERFACE "org.freedesktop.host.portal.Registry"
#define SHORTCUTS_INTERFACE "org.freedesktop.portal.GlobalShortcuts"
#define REQUEST_INTERFACE "org.freedesktop.portal.Request"
#define SESSION_INTERFACE "org.freedesktop.portal.Session"
#define PORTAL_CALL_TIMEOUT_MS 10000

typedef enum { MODE_UNKNOWN, MODE_X11, MODE_PORTAL, MODE_NONE } HotkeyMode;

typedef struct {
    int number;
    char* id;
    char* description;
    char* keysym;
    guint modifiers;
    char* trigger;
} HotkeyRequest;

static gpointer g_activatedCallback = NULL;
static gpointer g_registeredCallback = NULL;
static HotkeyMode g_mode = MODE_UNKNOWN;

static void report(int number, HotkeyStatus status, const char* bound, const char* reason) {
    HotkeyRegisteredCallback registered = (HotkeyRegisteredCallback)g_atomic_pointer_get(&g_registeredCallback);
    if (registered != NULL) {
        registered(number, status, bound, reason);
    }
}

static void notify_activated(int number) {
    HotkeyActivatedCallback activated = (HotkeyActivatedCallback)g_atomic_pointer_get(&g_activatedCallback);
    if (activated != NULL) {
        activated(number);
    }
}

static void free_request(HotkeyRequest* request) {
    g_free(request->id);
    g_free(request->description);
    g_free(request->keysym);
    g_free(request->trigger);
    g_free(request);
}

// XWayland grabs fire only while an X11 window has focus, so every Wayland session uses the portal.
static gboolean wants_portal(void) {
#ifdef GDK_WINDOWING_WAYLAND
    if (GDK_IS_WAYLAND_DISPLAY(gdk_display_get_default())) {
        return TRUE;
    }
#endif
    const char* wayland = g_getenv("WAYLAND_DISPLAY");
    return wayland != NULL && *wayland != '\0';
}

// ============================================================================
// X11: passive grabs on the root window, delivered through a GDK filter
// ============================================================================

#ifdef GDK_WINDOWING_X11

#define HOTKEY_MODIFIER_MASK (ShiftMask | ControlMask | Mod1Mask | Mod4Mask)

typedef struct {
    int number;
    KeyCode keycode;
    guint modifiers;
    gboolean pressed;
} X11Grab;

static GPtrArray* g_grabs = NULL;
static guint g_numLockMask = 0;

static guint find_num_lock_mask(Display* display) {
    guint mask = 0;
    KeyCode numLock = XKeysymToKeycode(display, XK_Num_Lock);
    XModifierKeymap* map = XGetModifierMapping(display);
    if (map == NULL) {
        return 0;
    }
    for (int modifier = 0; modifier < 8 && numLock != 0; modifier++) {
        for (int k = 0; k < map->max_keypermod; k++) {
            if (map->modifiermap[modifier * map->max_keypermod + k] == numLock) {
                mask = 1u << modifier;
            }
        }
    }
    XFreeModifiermap(map);
    return mask;
}

// The grab must also match with CapsLock or NumLock on, so each combination is grabbed four times.
static guint lock_variant(int index) {
    switch (index) {
        case 1: return LockMask;
        case 2: return g_numLockMask;
        case 3: return g_numLockMask | LockMask;
        default: return 0;
    }
}

static GdkFilterReturn x11_filter(GdkXEvent* gdkXEvent, GdkEvent* event, gpointer data) {
    (void)event; (void)data;
    XEvent* xevent = (XEvent*)gdkXEvent;
    if (xevent->type != KeyPress && xevent->type != KeyRelease) {
        return GDK_FILTER_CONTINUE;
    }

    gboolean matched = FALSE;
    for (guint i = 0; i < g_grabs->len; i++) {
        X11Grab* grab = g_ptr_array_index(g_grabs, i);
        if (grab->keycode != xevent->xkey.keycode) {
            continue;
        }

        // A release carries whatever modifiers are still held, so it matches on the key alone.
        if (xevent->type == KeyRelease) {
            grab->pressed = FALSE;
            matched = TRUE;
            continue;
        }

        if ((xevent->xkey.state & HOTKEY_MODIFIER_MASK) != grab->modifiers) {
            continue;
        }

        // Detectable auto-repeat sends a held key as presses without releases; only the first one counts.
        if (!grab->pressed) {
            grab->pressed = TRUE;
            notify_activated(grab->number);
        }
        return GDK_FILTER_REMOVE;
    }

    return matched ? GDK_FILTER_REMOVE : GDK_FILTER_CONTINUE;
}

static void x11_ungrab(GdkDisplay* gdkDisplay, KeyCode keycode, guint modifiers) {
    Display* display = gdk_x11_display_get_xdisplay(gdkDisplay);
    Window root = gdk_x11_get_default_root_xwindow();
    gdk_x11_display_error_trap_push(gdkDisplay);
    for (int i = 0; i < 4; i++) {
        XUngrabKey(display, keycode, modifiers | lock_variant(i), root);
    }
    gdk_x11_display_error_trap_pop_ignored(gdkDisplay);
}

static void x11_register(const HotkeyRequest* request) {
    GdkDisplay* gdkDisplay = gdk_display_get_default();
    Display* display = gdk_x11_display_get_xdisplay(gdkDisplay);
    Window root = gdk_x11_get_default_root_xwindow();

    if (g_grabs == NULL) {
        Bool supported = False;
        XkbSetDetectableAutoRepeat(display, True, &supported);
        g_numLockMask = find_num_lock_mask(display);
        g_grabs = g_ptr_array_new_with_free_func(g_free);
        gdk_window_add_filter(gdk_get_default_root_window(), x11_filter, NULL);
    }

    KeySym keysym = XStringToKeysym(request->keysym);
    KeyCode keycode = keysym == NoSymbol ? 0 : XKeysymToKeycode(display, keysym);
    if (keycode == 0) {
        char* reason = g_strdup_printf("The keyboard has no key for %s.", request->keysym);
        report(request->number, HotkeyStatus_Unsupported, NULL, reason);
        g_free(reason);
        return;
    }

    // The trap is GDK's X error handler; popping it calls XSync, so a refused grab is known here.
    gdk_x11_display_error_trap_push(gdkDisplay);
    for (int i = 0; i < 4; i++) {
        XGrabKey(display, keycode, request->modifiers | lock_variant(i), root, False, GrabModeAsync, GrabModeAsync);
    }
    int error = gdk_x11_display_error_trap_pop(gdkDisplay);
    if (error != Success) {
        x11_ungrab(gdkDisplay, keycode, request->modifiers);
        if (error == BadAccess) {
            report(request->number, HotkeyStatus_Conflict, NULL, "Another application already uses this shortcut.");
        } else {
            char* reason = g_strdup_printf("The X server refused the shortcut with error %d.", error);
            report(request->number, HotkeyStatus_Failed, NULL, reason);
            g_free(reason);
        }
        return;
    }

    X11Grab* grab = g_new0(X11Grab, 1);
    grab->number = request->number;
    grab->keycode = keycode;
    grab->modifiers = request->modifiers;
    g_ptr_array_add(g_grabs, grab);
    report(request->number, HotkeyStatus_Registered, NULL, NULL);
}

static void x11_unregister(int number) {
    if (g_grabs == NULL) {
        return;
    }
    for (guint i = 0; i < g_grabs->len; i++) {
        X11Grab* grab = g_ptr_array_index(g_grabs, i);
        if (grab->number == number) {
            x11_ungrab(gdk_display_get_default(), grab->keycode, grab->modifiers);
            g_ptr_array_remove_index(g_grabs, i);
            return;
        }
    }
}

#endif

// ============================================================================
// Wayland: the GlobalShortcuts portal over GDBus
// ============================================================================

typedef struct {
    int number;
    char* id;
    char* description;
    char* trigger;
    gboolean answered;
    gboolean bound;
} PortalShortcut;

typedef void (*ResponseHandler)(guint32 response, GVariant* results);

static struct {
    GDBusConnection* bus;
    char* busError;
    char* appId;
    gboolean registering;
    char* registryError;
    GPtrArray* shortcuts;
    gboolean dirty;
    gboolean busy;
    char* session;
    GArray* binding;
    guint activatedSubscription;
    guint responseSubscription;
    char* requestPath;
    guint requestSerial;
    ResponseHandler onResponse;
    guint nextToken;
} g_portal;

static void portal_sync(void);

static void free_portal_shortcut(PortalShortcut* shortcut) {
    g_free(shortcut->id);
    g_free(shortcut->description);
    g_free(shortcut->trigger);
    g_free(shortcut);
}

static gboolean portal_ensure_bus(void) {
    if (g_portal.bus != NULL) {
        return TRUE;
    }
    if (g_portal.busError != NULL) {
        return FALSE;
    }

    GError* error = NULL;
    g_portal.bus = g_bus_get_sync(G_BUS_TYPE_SESSION, NULL, &error);
    if (g_portal.bus == NULL) {
        g_portal.busError = g_strdup_printf("Wayland global hotkeys need a D-Bus session bus: %s",
                                            error != NULL ? error->message : "unknown error");
        g_clear_error(&error);
        return FALSE;
    }

    g_portal.shortcuts = g_ptr_array_new_with_free_func((GDestroyNotify)free_portal_shortcut);
    g_portal.binding = g_array_new(FALSE, FALSE, sizeof(int));
    return TRUE;
}

static PortalShortcut* portal_find(int number, guint* index) {
    for (guint i = 0; i < g_portal.shortcuts->len; i++) {
        PortalShortcut* shortcut = g_ptr_array_index(g_portal.shortcuts, i);
        if (shortcut->number == number) {
            if (index != NULL) {
                *index = i;
            }
            return shortcut;
        }
    }
    return NULL;
}

static void on_app_registered(GObject* source, GAsyncResult* result, gpointer data) {
    (void)data;
    GError* error = NULL;
    GVariant* reply = g_dbus_connection_call_finish(G_DBUS_CONNECTION(source), result, &error);
    if (reply != NULL) {
        g_variant_unref(reply);
    } else {
        g_dbus_error_strip_remote_error(error);
        g_portal.registryError = g_strdup(error->message);
        g_debug("Hermes could not register the AppId with the desktop portal: %s", error->message);
        g_clear_error(&error);
    }

    g_portal.registering = FALSE;
    portal_sync();
}

static gboolean is_remote_error(const char* remote, const char* name) {
    return remote != NULL && strcmp(remote, name) == 0;
}

// Answers every shortcut in the failed request; returns whether any was dropped.
static gboolean portal_refuse_pending(HotkeyStatus status, const char* reason) {
    gboolean dropped = FALSE;
    for (guint i = 0; i < g_portal.binding->len; i++) {
        guint index = 0;
        PortalShortcut* shortcut = portal_find(g_array_index(g_portal.binding, int, i), &index);
        if (shortcut != NULL && !shortcut->answered) {
            report(shortcut->number, status, NULL, reason);
            g_ptr_array_remove_index(g_portal.shortcuts, index);
            dropped = TRUE;
        }
    }
    return dropped;
}

static void portal_close_session(void) {
    if (g_portal.session == NULL) {
        return;
    }
    g_dbus_connection_call(g_portal.bus, PORTAL_BUS_NAME, g_portal.session, SESSION_INTERFACE, "Close",
                           NULL, NULL, G_DBUS_CALL_FLAGS_NONE, -1, NULL, NULL, NULL);
    g_clear_pointer(&g_portal.session, g_free);
    for (guint i = 0; i < g_portal.shortcuts->len; i++) {
        ((PortalShortcut*)g_ptr_array_index(g_portal.shortcuts, i))->bound = FALSE;
    }
}

static void portal_finish(gboolean failed, gboolean dropped) {
    if (failed) {
        portal_close_session();
        // Shortcuts approved earlier lost their session too; a request without the refused ones restores them.
        if (dropped && g_portal.shortcuts->len > 0) {
            g_portal.dirty = TRUE;
        }
    }
    g_array_set_size(g_portal.binding, 0);
    g_portal.busy = FALSE;
    portal_sync();
}

static void portal_fail_with_error(GError* error) {
    char* remote = g_dbus_error_get_remote_error(error);
    g_dbus_error_strip_remote_error(error);

    HotkeyStatus status = HotkeyStatus_Unsupported;
    char* reason = NULL;
    if (is_remote_error(remote, "org.freedesktop.DBus.Error.ServiceUnknown")
        || is_remote_error(remote, "org.freedesktop.DBus.Error.NameHasNoOwner")) {
        reason = g_strdup("No desktop portal is running, so Wayland global hotkeys are unavailable.");
    } else if (is_remote_error(remote, "org.freedesktop.DBus.Error.UnknownMethod")
               || is_remote_error(remote, "org.freedesktop.DBus.Error.UnknownInterface")
               || is_remote_error(remote, "org.freedesktop.DBus.Error.UnknownObject")) {
        reason = g_strdup("The desktop portal has no GlobalShortcuts; it needs GNOME 48 or newer, or KDE Plasma 5.27 or newer.");
    } else if (is_remote_error(remote, "org.freedesktop.portal.Error.NotAllowed")) {
        if (g_portal.registryError != NULL) {
            reason = g_strdup_printf("The desktop portal could not identify this app; registering HermesApplication.AppId failed: %s",
                                     g_portal.registryError);
        } else if (g_portal.appId == NULL) {
            reason = g_strdup("The desktop portal could not identify this app; set HermesApplication.AppId.");
        } else {
            reason = g_strdup_printf("The desktop portal refused this app: %s", error->message);
        }
    } else {
        status = HotkeyStatus_Failed;
        reason = g_strdup(error->message);
    }

    gboolean dropped = portal_refuse_pending(status, reason);
    g_free(reason);
    g_free(remote);
    portal_finish(TRUE, dropped);
}

static void portal_end_request(void) {
    if (g_portal.responseSubscription != 0) {
        g_dbus_connection_signal_unsubscribe(g_portal.bus, g_portal.responseSubscription);
        g_portal.responseSubscription = 0;
    }
    g_clear_pointer(&g_portal.requestPath, g_free);
}

static void on_response(GDBusConnection* connection, const gchar* sender, const gchar* path,
                        const gchar* interface, const gchar* signal, GVariant* parameters, gpointer data) {
    (void)connection; (void)sender; (void)path; (void)interface; (void)signal; (void)data;
    if (g_portal.onResponse == NULL || !g_variant_is_of_type(parameters, G_VARIANT_TYPE("(ua{sv})"))) {
        return;
    }

    guint32 response = 2;
    GVariant* results = NULL;
    g_variant_get(parameters, "(u@a{sv})", &response, &results);

    ResponseHandler handler = g_portal.onResponse;
    g_portal.onResponse = NULL;
    portal_end_request();
    handler(response, results);
    g_variant_unref(results);
}

static void subscribe_response(const char* path) {
    g_portal.responseSubscription = g_dbus_connection_signal_subscribe(g_portal.bus, PORTAL_BUS_NAME,
        REQUEST_INTERFACE, "Response", path, NULL, G_DBUS_SIGNAL_FLAGS_NONE, on_response, NULL, NULL);
}

static void on_request_started(GObject* source, GAsyncResult* result, gpointer data) {
    GError* error = NULL;
    GVariant* reply = g_dbus_connection_call_finish(G_DBUS_CONNECTION(source), result, &error);
    gboolean current = GPOINTER_TO_UINT(data) == g_portal.requestSerial && g_portal.onResponse != NULL;

    if (reply == NULL) {
        if (current) {
            g_portal.onResponse = NULL;
            portal_end_request();
            portal_fail_with_error(error);
        }
        g_clear_error(&error);
        return;
    }

    const char* handle = NULL;
    g_variant_get(reply, "(&o)", &handle);
    // Portals before 0.9 choose their own request path instead of the predicted one.
    if (current && g_strcmp0(handle, g_portal.requestPath) != 0) {
        g_dbus_connection_signal_unsubscribe(g_portal.bus, g_portal.responseSubscription);
        g_free(g_portal.requestPath);
        g_portal.requestPath = g_strdup(handle);
        subscribe_response(g_portal.requestPath);
    }
    g_variant_unref(reply);
}

// Subscribes to the predicted Request path before calling, so a fast Response cannot be missed.
static void portal_request(const char* method, GVariant* parameters, const char* token, ResponseHandler onResponse) {
    char* sender = g_strdup(g_dbus_connection_get_unique_name(g_portal.bus) + 1);
    g_strdelimit(sender, ".", '_');
    g_portal.requestPath = g_strdup_printf("%s/request/%s/%s", PORTAL_OBJECT_PATH, sender, token);
    g_free(sender);

    g_portal.onResponse = onResponse;
    g_portal.requestSerial++;
    subscribe_response(g_portal.requestPath);
    g_dbus_connection_call(g_portal.bus, PORTAL_BUS_NAME, PORTAL_OBJECT_PATH, SHORTCUTS_INTERFACE, method,
                           parameters, G_VARIANT_TYPE("(o)"), G_DBUS_CALL_FLAGS_NONE, PORTAL_CALL_TIMEOUT_MS,
                           NULL, on_request_started, GUINT_TO_POINTER(g_portal.requestSerial));
}

static char* lookup_trigger(GVariant* shortcuts, const char* id, gboolean* present) {
    GVariantIter iter;
    const char* shortcutId = NULL;
    GVariant* properties = NULL;
    *present = FALSE;
    g_variant_iter_init(&iter, shortcuts);
    while (g_variant_iter_next(&iter, "(&s@a{sv})", &shortcutId, &properties)) {
        if (strcmp(shortcutId, id) == 0) {
            char* trigger = NULL;
            g_variant_lookup(properties, "trigger_description", "s", &trigger);
            g_variant_unref(properties);
            *present = TRUE;
            return trigger;
        }
        g_variant_unref(properties);
    }
    return NULL;
}

static void on_shortcuts_bound(guint32 response, GVariant* results) {
    GVariant* bound = response == 0
        ? g_variant_lookup_value(results, "shortcuts", G_VARIANT_TYPE("a(sa{sv})"))
        : NULL;
    gboolean dropped = FALSE;

    for (guint i = 0; i < g_portal.binding->len; i++) {
        guint index = 0;
        PortalShortcut* shortcut = portal_find(g_array_index(g_portal.binding, int, i), &index);
        if (shortcut == NULL) {
            continue;
        }

        gboolean present = FALSE;
        char* trigger = bound != NULL ? lookup_trigger(bound, shortcut->id, &present) : NULL;
        shortcut->bound = trigger != NULL && *trigger != '\0';
        if (!shortcut->answered) {
            shortcut->answered = TRUE;
            if (shortcut->bound) {
                report(shortcut->number, HotkeyStatus_Registered, trigger, NULL);
            } else {
                // GNOME answers a closed consent dialog with 2, KDE with 1; both mean no approval.
                const char* reason = response == 1 ? "The user declined the shortcut."
                    : response != 0 ? "The desktop did not bind the shortcut; the user closed the dialog or the desktop refused."
                    : present ? "The desktop bound no keys to the shortcut."
                    : "The user did not approve the shortcut.";
                report(shortcut->number, HotkeyStatus_Denied, NULL, reason);
                g_ptr_array_remove_index(g_portal.shortcuts, index);
                dropped = TRUE;
            }
        }
        g_free(trigger);
    }

    if (bound != NULL) {
        g_variant_unref(bound);
    }
    portal_finish(response != 0, dropped);
}

static void portal_bind(void) {
    GVariantBuilder shortcuts;
    g_variant_builder_init(&shortcuts, G_VARIANT_TYPE("a(sa{sv})"));
    g_array_set_size(g_portal.binding, 0);
    for (guint i = 0; i < g_portal.shortcuts->len; i++) {
        PortalShortcut* shortcut = g_ptr_array_index(g_portal.shortcuts, i);
        GVariantBuilder properties;
        g_variant_builder_init(&properties, G_VARIANT_TYPE_VARDICT);
        g_variant_builder_add(&properties, "{sv}", "description", g_variant_new_string(shortcut->description));
        g_variant_builder_add(&properties, "{sv}", "preferred_trigger", g_variant_new_string(shortcut->trigger));
        g_variant_builder_add(&shortcuts, "(sa{sv})", shortcut->id, &properties);
        g_array_append_val(g_portal.binding, shortcut->number);
    }

    char* token = g_strdup_printf("hermes_%u", ++g_portal.nextToken);
    GVariantBuilder options;
    g_variant_builder_init(&options, G_VARIANT_TYPE_VARDICT);
    g_variant_builder_add(&options, "{sv}", "handle_token", g_variant_new_string(token));
    portal_request("BindShortcuts", g_variant_new("(oa(sa{sv})sa{sv})", g_portal.session, &shortcuts, "", &options),
                   token, on_shortcuts_bound);
    g_free(token);
}

static void on_session_created(guint32 response, GVariant* results) {
    GVariant* handle = g_variant_lookup_value(results, "session_handle", NULL);
    gboolean valid = handle != NULL
        && (g_variant_is_of_type(handle, G_VARIANT_TYPE_STRING) || g_variant_is_of_type(handle, G_VARIANT_TYPE_OBJECT_PATH));
    if (response == 0 && valid) {
        g_portal.session = g_strdup(g_variant_get_string(handle, NULL));
    }
    if (handle != NULL) {
        g_variant_unref(handle);
    }

    // Shortcuts unregistered while the session was created leave nothing to bind, and binding none grabs nothing.
    if (g_portal.session == NULL || g_portal.shortcuts->len == 0) {
        gboolean dropped = g_portal.session == NULL
            && portal_refuse_pending(HotkeyStatus_Failed, "The desktop portal did not create a shortcuts session.");
        portal_finish(TRUE, dropped);
        return;
    }

    portal_bind();
}

static void on_portal_activated(GDBusConnection* connection, const gchar* sender, const gchar* path,
                                const gchar* interface, const gchar* signal, GVariant* parameters, gpointer data) {
    (void)connection; (void)sender; (void)path; (void)interface; (void)signal; (void)data;
    if (g_portal.session == NULL || !g_variant_is_of_type(parameters, G_VARIANT_TYPE("(osta{sv})"))) {
        return;
    }

    const char* session = NULL;
    const char* id = NULL;
    g_variant_get(parameters, "(&o&st@a{sv})", &session, &id, NULL, NULL);
    if (strcmp(session, g_portal.session) != 0) {
        return;
    }

    for (guint i = 0; i < g_portal.shortcuts->len; i++) {
        PortalShortcut* shortcut = g_ptr_array_index(g_portal.shortcuts, i);
        if (shortcut->bound && strcmp(shortcut->id, id) == 0) {
            notify_activated(shortcut->number);
            return;
        }
    }
}

// A session binds once, so every change to the set of shortcuts closes the session and binds the whole set anew.
static void portal_create_session(void) {
    if (g_portal.activatedSubscription == 0) {
        g_portal.activatedSubscription = g_dbus_connection_signal_subscribe(g_portal.bus, PORTAL_BUS_NAME,
            SHORTCUTS_INTERFACE, "Activated", PORTAL_OBJECT_PATH, NULL, G_DBUS_SIGNAL_FLAGS_NONE,
            on_portal_activated, NULL, NULL);
    }

    g_portal.busy = TRUE;
    for (guint i = 0; i < g_portal.shortcuts->len; i++) {
        int number = ((PortalShortcut*)g_ptr_array_index(g_portal.shortcuts, i))->number;
        g_array_append_val(g_portal.binding, number);
    }

    guint serial = ++g_portal.nextToken;
    char* token = g_strdup_printf("hermes_%u", serial);
    char* sessionToken = g_strdup_printf("hermes_session_%u", serial);
    GVariantBuilder options;
    g_variant_builder_init(&options, G_VARIANT_TYPE_VARDICT);
    g_variant_builder_add(&options, "{sv}", "handle_token", g_variant_new_string(token));
    g_variant_builder_add(&options, "{sv}", "session_handle_token", g_variant_new_string(sessionToken));
    portal_request("CreateSession", g_variant_new("(a{sv})", &options), token, on_session_created);
    g_free(token);
    g_free(sessionToken);
}

// Waits for the Registry answer, because Register must reach the portal before any other call.
static void portal_sync(void) {
    if (g_portal.busy || g_portal.registering || !g_portal.dirty) {
        return;
    }
    g_portal.dirty = FALSE;
    portal_close_session();
    if (g_portal.shortcuts->len > 0) {
        portal_create_session();
    }
}

static void portal_register(const HotkeyRequest* request) {
    if (!portal_ensure_bus()) {
        report(request->number, HotkeyStatus_Unsupported, NULL, g_portal.busError);
        return;
    }

    PortalShortcut* shortcut = g_new0(PortalShortcut, 1);
    shortcut->number = request->number;
    shortcut->id = g_strdup(request->id);
    shortcut->description = g_strdup(request->description);
    shortcut->trigger = g_strdup(request->trigger);
    g_ptr_array_add(g_portal.shortcuts, shortcut);
    g_portal.dirty = TRUE;
    portal_sync();
}

static void portal_unregister(int number) {
    guint index = 0;
    if (g_portal.shortcuts == NULL || portal_find(number, &index) == NULL) {
        return;
    }

    PortalShortcut* shortcut = g_ptr_array_index(g_portal.shortcuts, index);
    if (!shortcut->answered) {
        report(number, HotkeyStatus_Failed, NULL, "The hotkey was unregistered before the desktop answered.");
    }
    g_ptr_array_remove_index(g_portal.shortcuts, index);

    // The portal cannot unbind one shortcut; the next bind leaves it out, and the last one closes the session.
    if (g_portal.shortcuts->len == 0) {
        g_portal.dirty = TRUE;
        portal_sync();
    }
}

// ============================================================================
// Dispatch on the GTK thread
// ============================================================================

static HotkeyMode current_mode(void) {
    // Not cached without a display, so work that runs before gtk_init cannot fix the mode for good.
    if (g_mode == MODE_UNKNOWN && gdk_display_get_default() != NULL) {
        if (wants_portal()) {
            g_mode = MODE_PORTAL;
        }
#ifdef GDK_WINDOWING_X11
        else if (GDK_IS_X11_DISPLAY(gdk_display_get_default())) {
            g_mode = MODE_X11;
        }
#endif
        else {
            g_mode = MODE_NONE;
        }
    }
    return g_mode == MODE_UNKNOWN ? MODE_NONE : g_mode;
}

static gboolean register_work(gpointer data) {
    HotkeyRequest* request = data;
    switch (current_mode()) {
#ifdef GDK_WINDOWING_X11
        case MODE_X11:
            x11_register(request);
            break;
#endif
        case MODE_PORTAL:
            portal_register(request);
            break;
        default:
            report(request->number, HotkeyStatus_Unsupported, NULL, "Global hotkeys need an X11 or Wayland display.");
            break;
    }
    free_request(request);
    return G_SOURCE_REMOVE;
}

static gboolean unregister_work(gpointer data) {
    int number = GPOINTER_TO_INT(data);
    switch (current_mode()) {
#ifdef GDK_WINDOWING_X11
        case MODE_X11:
            x11_unregister(number);
            break;
#endif
        case MODE_PORTAL:
            portal_unregister(number);
            break;
        default:
            break;
    }
    return G_SOURCE_REMOVE;
}

// ============================================================================
// Entry points
// ============================================================================

void HermesHotkeys_SetAppId(const char* appId) {
    if (appId == NULL || *appId == '\0' || g_portal.appId != NULL || !wants_portal() || !portal_ensure_bus()) {
        return;
    }

    g_portal.appId = g_strdup(appId);
    g_portal.registering = TRUE;
    g_dbus_connection_call(g_portal.bus, PORTAL_BUS_NAME, PORTAL_OBJECT_PATH, REGISTRY_INTERFACE, "Register",
                           g_variant_new("(s@a{sv})", appId, g_variant_new_array(G_VARIANT_TYPE("{sv}"), NULL, 0)),
                           NULL, G_DBUS_CALL_FLAGS_NONE, PORTAL_CALL_TIMEOUT_MS, NULL, on_app_registered, NULL);
}

void HermesHotkeys_Start(HotkeyActivatedCallback activated, HotkeyRegisteredCallback registered) {
    g_atomic_pointer_set(&g_activatedCallback, (gpointer)activated);
    g_atomic_pointer_set(&g_registeredCallback, (gpointer)registered);
}

// Always queued, so a callback that unregisters never changes a table being walked, and work queued before
// gtk_init waits for the main loop, which starts after the AppId reaches the portal.
void HermesHotkeys_Register(int number, const char* id, const char* description,
                            const char* keysym, unsigned int modifiers, const char* portalTrigger) {
    HotkeyRequest* request = g_new0(HotkeyRequest, 1);
    request->number = number;
    request->id = g_strdup(id);
    request->description = g_strdup(description);
    request->keysym = g_strdup(keysym);
    request->modifiers = modifiers;
    request->trigger = g_strdup(portalTrigger);
    g_idle_add(register_work, request);
}

void HermesHotkeys_Unregister(int number) {
    g_idle_add(unregister_work, GINT_TO_POINTER(number));
}
