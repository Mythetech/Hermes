#include "HermesWindow.h"
#include "Exports.h"
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <pthread.h>
#include <libsoup/soup.h>

// Thread ID for the UI thread
static pthread_t g_uiThreadId = 0;
static gboolean g_gtkInitialized = FALSE;

// ============================================================================
// Application Lifecycle
// ============================================================================

void Hermes_App_Init(int* argc, char*** argv) {
    if (g_gtkInitialized) return;

    gtk_init(argc, argv);
    g_uiThreadId = pthread_self();
    g_gtkInitialized = TRUE;
}

void Hermes_App_Run(void) {
    gtk_main();
}

void Hermes_App_Quit(void) {
    gtk_main_quit();
}

// ============================================================================
// Internal Event Handlers
// ============================================================================

static gboolean on_window_delete(GtkWidget* widget, GdkEvent* event, gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;
    if (hw->onClosing) {
        hw->onClosing();
    }
    return FALSE; // Allow close to proceed
}

static gboolean on_window_configure(GtkWidget* widget, GdkEventConfigure* event, gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;

    // Fire Resized only when size actually changed
    if (event->width != hw->lastWidth || event->height != hw->lastHeight) {
        hw->lastWidth = event->width;
        hw->lastHeight = event->height;
        if (hw->onResized) {
            hw->onResized(event->width, event->height);
        }
    }

    // Fire Moved only when position actually changed
    if (event->x != hw->lastX || event->y != hw->lastY) {
        hw->lastX = event->x;
        hw->lastY = event->y;
        if (hw->onMoved) {
            hw->onMoved(event->x, event->y);
        }
    }

    return FALSE;
}

static gboolean on_window_focus_in(GtkWidget* widget, GdkEventFocus* event, gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;
    if (hw->onFocusIn) {
        hw->onFocusIn();
    }
    return FALSE;
}

static gboolean on_window_focus_out(GtkWidget* widget, GdkEventFocus* event, gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;
    if (hw->onFocusOut) {
        hw->onFocusOut();
    }
    return FALSE;
}

static void on_script_message_received(WebKitUserContentManager* manager,
                                        WebKitJavascriptResult* jsResult,
                                        gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;

    JSCValue* value = webkit_javascript_result_get_js_value(jsResult);
    if (jsc_value_is_string(value)) {
        char* message = jsc_value_to_string(value);
        if (hw->onWebMessage && message) {
            hw->onWebMessage(message);
        }
        g_free(message);
    }
}

static gboolean on_context_menu(WebKitWebView* webView,
                                 WebKitContextMenu* contextMenu,
                                 GdkEvent* event,
                                 WebKitHitTestResult* hitTestResult,
                                 gpointer user_data) {
    // Return TRUE to suppress context menu
    return TRUE;
}

// ============================================================================
// Custom URI Scheme Handler
// ============================================================================

static void on_uri_scheme_request(WebKitURISchemeRequest* request, gpointer user_data) {
    HermesWindow* hw = (HermesWindow*)user_data;
    const char* uri = webkit_uri_scheme_request_get_uri(request);

    printf("[Hermes] URI scheme handler called for: %s\n", uri);
    fflush(stdout);

    if (!hw->onCustomScheme) {
        // Return empty response
        GInputStream* stream = g_memory_input_stream_new();
        webkit_uri_scheme_request_finish(request, stream, 0, "text/plain");
        g_object_unref(stream);
        return;
    }

    int numBytes = 0;
    char* contentType = NULL;
    void* data = hw->onCustomScheme(uri, &numBytes, &contentType);

    if (data && numBytes > 0) {
        // Create input stream from data
        GInputStream* stream = g_memory_input_stream_new_from_data(data, numBytes, g_free);

        // Create response with CORS headers
        SoupMessageHeaders* headers = soup_message_headers_new(SOUP_MESSAGE_HEADERS_RESPONSE);
        soup_message_headers_append(headers, "Access-Control-Allow-Origin", "app://localhost");
        soup_message_headers_append(headers, "Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        soup_message_headers_append(headers, "Access-Control-Allow-Headers", "Content-Type, Authorization");
        soup_message_headers_append(headers, "Access-Control-Allow-Credentials", "true");

        WebKitURISchemeResponse* response = webkit_uri_scheme_response_new(stream, numBytes);
        webkit_uri_scheme_response_set_content_type(response, contentType ? contentType : "application/octet-stream");
        webkit_uri_scheme_response_set_http_headers(response, headers);

        webkit_uri_scheme_request_finish_with_response(request, response);

        g_object_unref(response);
        g_object_unref(stream);
        if (contentType) g_free(contentType);
    } else {
        // Return empty response
        GInputStream* stream = g_memory_input_stream_new();
        webkit_uri_scheme_request_finish(request, stream, 0, "text/plain");
        g_object_unref(stream);
    }
}

// ============================================================================
// Window Creation
// ============================================================================

HermesWindow* hermes_window_new(const HermesWindowParams* params) {
    HermesWindow* hw = g_new0(HermesWindow, 1);

    // Store callbacks
    hw->onClosing = params->OnClosing;
    hw->onResized = params->OnResized;
    hw->onMoved = params->OnMoved;
    hw->onFocusIn = params->OnFocusIn;
    hw->onFocusOut = params->OnFocusOut;
    hw->onWebMessage = params->OnWebMessage;
    hw->onCustomScheme = params->OnCustomScheme;
    hw->uiThreadId = (int64_t)pthread_self();

    // Store custom schemes
    hw->customSchemeCount = 0;
    for (int i = 0; i < 16 && params->CustomSchemeNames[i]; i++) {
        hw->customSchemes[i] = g_strdup(params->CustomSchemeNames[i]);
        hw->customSchemeCount++;
    }

    // Create main window
    hw->window = gtk_window_new(GTK_WINDOW_TOPLEVEL);
    if (params->Title) {
        gtk_window_set_title(GTK_WINDOW(hw->window), params->Title);
    }
    gtk_window_set_default_size(GTK_WINDOW(hw->window), params->Width, params->Height);

    // Position
    if (params->CenterOnScreen) {
        gtk_window_set_position(GTK_WINDOW(hw->window), GTK_WIN_POS_CENTER);
    } else if (params->UsePosition) {
        gtk_window_move(GTK_WINDOW(hw->window), params->X, params->Y);
    }

    // Chromeless (no decorations) - also enabled by CustomTitleBar
    if (params->Chromeless || params->CustomTitleBar) {
        gtk_window_set_decorated(GTK_WINDOW(hw->window), FALSE);
    }

    // Resizable
    gtk_window_set_resizable(GTK_WINDOW(hw->window), params->Resizable);

    // TopMost
    if (params->TopMost) {
        gtk_window_set_keep_above(GTK_WINDOW(hw->window), TRUE);
    }

    // Size constraints
    if (params->MinWidth > 0 || params->MinHeight > 0 ||
        params->MaxWidth > 0 || params->MaxHeight > 0) {
        GdkGeometry geometry = {0};
        GdkWindowHints hints = 0;

        if (params->MinWidth > 0 || params->MinHeight > 0) {
            geometry.min_width = params->MinWidth > 0 ? params->MinWidth : 1;
            geometry.min_height = params->MinHeight > 0 ? params->MinHeight : 1;
            hints |= GDK_HINT_MIN_SIZE;
        }
        if (params->MaxWidth > 0 || params->MaxHeight > 0) {
            geometry.max_width = params->MaxWidth > 0 ? params->MaxWidth : G_MAXINT;
            geometry.max_height = params->MaxHeight > 0 ? params->MaxHeight : G_MAXINT;
            hints |= GDK_HINT_MAX_SIZE;
        }

        gtk_window_set_geometry_hints(GTK_WINDOW(hw->window), NULL, &geometry, hints);
    }

    // Icon
    if (params->IconPath && params->IconPath[0]) {
        GError* error = NULL;
        if (!gtk_window_set_icon_from_file(GTK_WINDOW(hw->window), params->IconPath, &error)) {
            fprintf(stderr, "[Hermes] Failed to load icon: %s\n", error->message);
            g_error_free(error);
        }
    }

    // Wire up window events
    g_signal_connect(hw->window, "delete-event", G_CALLBACK(on_window_delete), hw);
    g_signal_connect(hw->window, "configure-event", G_CALLBACK(on_window_configure), hw);
    g_signal_connect(hw->window, "focus-in-event", G_CALLBACK(on_window_focus_in), hw);
    g_signal_connect(hw->window, "focus-out-event", G_CALLBACK(on_window_focus_out), hw);

    // Create container
    hw->container = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);

    // Create UserContentManager for script message handling
    hw->userContentManager = webkit_user_content_manager_new();
    webkit_user_content_manager_register_script_message_handler(hw->userContentManager, "hermesHost");
    g_signal_connect(hw->userContentManager, "script-message-received::hermesHost",
                     G_CALLBACK(on_script_message_received), hw);

    // Create WebView
    hw->webView = webkit_web_view_new_with_user_content_manager(hw->userContentManager);

    // Register custom URI schemes
    WebKitWebContext* context = webkit_web_view_get_context(WEBKIT_WEB_VIEW(hw->webView));
    WebKitSecurityManager* securityManager = webkit_web_context_get_security_manager(context);

    for (int i = 0; i < hw->customSchemeCount; i++) {
        const char* scheme = hw->customSchemes[i];
        webkit_security_manager_register_uri_scheme_as_local(securityManager, scheme);
        webkit_security_manager_register_uri_scheme_as_secure(securityManager, scheme);
        webkit_security_manager_register_uri_scheme_as_cors_enabled(securityManager, scheme);
        webkit_web_context_register_uri_scheme(context, scheme, on_uri_scheme_request, hw, NULL);
        printf("[Hermes] Registered URI scheme: %s\n", scheme);
    }

    // Configure WebView settings
    WebKitSettings* settings = webkit_web_view_get_settings(WEBKIT_WEB_VIEW(hw->webView));
    webkit_settings_set_enable_developer_extras(settings, params->DevToolsEnabled);
    webkit_settings_set_javascript_can_access_clipboard(settings, TRUE);
    webkit_settings_set_enable_javascript(settings, TRUE);
    webkit_settings_set_enable_write_console_messages_to_stdout(settings, TRUE);

    // Disable context menu if requested
    if (!params->ContextMenuEnabled) {
        g_signal_connect(hw->webView, "context-menu", G_CALLBACK(on_context_menu), hw);
    }

    // Inject JavaScript bridge
    const char* bridgeScript =
        "console.log('[Hermes JS] Bridge script injecting...');\n"
        "window.__hermesReceiveCallbacks = [];\n"
        "window.__hermesDispatchMessage = function(message) {\n"
        "    window.__hermesReceiveCallbacks.forEach(function(cb) { cb(message); });\n"
        "};\n"
        "window.external = {\n"
        "    sendMessage: function(message) {\n"
        "        console.log('[Hermes JS] sendMessage called:', message.substring(0, 100));\n"
        "        window.webkit.messageHandlers.hermesHost.postMessage(message);\n"
        "    },\n"
        "    receiveMessage: function(callback) {\n"
        "        console.log('[Hermes JS] receiveMessage callback registered');\n"
        "        window.__hermesReceiveCallbacks.push(callback);\n"
        "    }\n"
        "};\n"
        "console.log('[Hermes JS] Bridge ready');\n";

    WebKitUserScript* userScript = webkit_user_script_new(
        bridgeScript,
        WEBKIT_USER_CONTENT_INJECT_ALL_FRAMES,
        WEBKIT_USER_SCRIPT_INJECT_AT_DOCUMENT_START,
        NULL, NULL);
    webkit_user_content_manager_add_script(hw->userContentManager, userScript);
    webkit_user_script_unref(userScript);

    // Add WebView to container
    gtk_box_pack_start(GTK_BOX(hw->container), hw->webView, TRUE, TRUE, 0);
    gtk_container_add(GTK_CONTAINER(hw->window), hw->container);

    return hw;
}

void hermes_window_destroy(HermesWindow* hw) {
    if (!hw) return;

    // Free custom scheme strings
    for (int i = 0; i < hw->customSchemeCount; i++) {
        g_free(hw->customSchemes[i]);
    }

    // Destroy GTK widgets (this will also destroy children)
    if (hw->window) {
        gtk_widget_destroy(hw->window);
    }

    g_free(hw);
}

// ============================================================================
// Window Lifecycle (Exports)
// ============================================================================

void* Hermes_Window_Create(const HermesWindowParams* params) {
    return hermes_window_new(params);
}

void Hermes_Window_Show(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    hw->isShown = TRUE;
    gtk_widget_show_all(hw->window);
}

void Hermes_Window_Close(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    g_idle_add((GSourceFunc)gtk_widget_destroy, hw->window);
    g_idle_add((GSourceFunc)gtk_main_quit, NULL);
}

void Hermes_Window_WaitForClose(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    Hermes_Window_Show(window);
    gtk_main();
}

void Hermes_Window_Destroy(void* window) {
    hermes_window_destroy((HermesWindow*)window);
}

// ============================================================================
// Window Properties - Getters
// ============================================================================

void Hermes_Window_GetTitle(void* window, char* buffer, int bufferSize) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !buffer || bufferSize <= 0) return;

    const char* title = gtk_window_get_title(GTK_WINDOW(hw->window));
    if (title) {
        strncpy(buffer, title, bufferSize - 1);
        buffer[bufferSize - 1] = '\0';
    } else {
        buffer[0] = '\0';
    }
}

void Hermes_Window_GetSize(void* window, int* width, int* height) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    gtk_window_get_size(GTK_WINDOW(hw->window), width, height);
}

void Hermes_Window_GetPosition(void* window, int* x, int* y) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    gtk_window_get_position(GTK_WINDOW(hw->window), x, y);
}

bool Hermes_Window_GetIsMaximized(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return false;

    return gtk_window_is_maximized(GTK_WINDOW(hw->window));
}

bool Hermes_Window_GetIsMinimized(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return false;

    GdkWindow* gdkWindow = gtk_widget_get_window(hw->window);
    if (!gdkWindow) return false;

    GdkWindowState state = gdk_window_get_state(gdkWindow);
    return (state & GDK_WINDOW_STATE_ICONIFIED) != 0;
}

int64_t Hermes_Window_GetUIThreadId(void* window) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return 0;
    return hw->uiThreadId;
}

// ============================================================================
// Window Properties - Setters
// ============================================================================

void Hermes_Window_SetTitle(void* window, const char* title) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    gtk_window_set_title(GTK_WINDOW(hw->window), title);
}

void Hermes_Window_SetSize(void* window, int width, int height) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    gtk_window_resize(GTK_WINDOW(hw->window), width, height);
}

void Hermes_Window_SetPosition(void* window, int x, int y) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    gtk_window_move(GTK_WINDOW(hw->window), x, y);
}

void Hermes_Window_SetIsMaximized(void* window, bool maximized) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    if (maximized) {
        gtk_window_maximize(GTK_WINDOW(hw->window));
    } else {
        gtk_window_unmaximize(GTK_WINDOW(hw->window));
    }
}

void Hermes_Window_SetIsMinimized(void* window, bool minimized) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw) return;

    if (minimized) {
        gtk_window_iconify(GTK_WINDOW(hw->window));
    } else {
        gtk_window_deiconify(GTK_WINDOW(hw->window));
    }
}

// ============================================================================
// WebView Operations
// ============================================================================

void Hermes_Window_NavigateToUrl(void* window, const char* url) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !url) return;

    printf("[Hermes] NavigateToUrl: %s\n", url);
    fflush(stdout);
    webkit_web_view_load_uri(WEBKIT_WEB_VIEW(hw->webView), url);
}

void Hermes_Window_NavigateToString(void* window, const char* html) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !html) return;

    webkit_web_view_load_html(WEBKIT_WEB_VIEW(hw->webView), html, "about:blank");
}

void Hermes_Window_SendWebMessage(void* window, const char* message) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !message) return;

    // Escape message for JavaScript
    GString* escaped = g_string_new(NULL);
    for (const char* p = message; *p; p++) {
        switch (*p) {
            case '\\': g_string_append(escaped, "\\\\"); break;
            case '"':  g_string_append(escaped, "\\\""); break;
            case '\n': g_string_append(escaped, "\\n"); break;
            case '\r': g_string_append(escaped, "\\r"); break;
            case '\t': g_string_append(escaped, "\\t"); break;
            default:   g_string_append_c(escaped, *p); break;
        }
    }

    char* script = g_strdup_printf(
        "if(window.__hermesDispatchMessage) window.__hermesDispatchMessage(\"%s\");",
        escaped->str);
    g_string_free(escaped, TRUE);

    webkit_web_view_run_javascript(WEBKIT_WEB_VIEW(hw->webView), script, NULL, NULL, NULL);
    g_free(script);
}

void Hermes_Window_RegisterCustomScheme(void* window, const char* scheme) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !scheme || hw->customSchemeCount >= 16) return;

    // Check if already registered
    for (int i = 0; i < hw->customSchemeCount; i++) {
        if (strcmp(hw->customSchemes[i], scheme) == 0) {
            return; // Already registered
        }
    }

    hw->customSchemes[hw->customSchemeCount++] = g_strdup(scheme);

    // Register with WebKit context
    WebKitWebContext* context = webkit_web_view_get_context(WEBKIT_WEB_VIEW(hw->webView));
    WebKitSecurityManager* securityManager = webkit_web_context_get_security_manager(context);

    webkit_security_manager_register_uri_scheme_as_local(securityManager, scheme);
    webkit_security_manager_register_uri_scheme_as_secure(securityManager, scheme);
    webkit_security_manager_register_uri_scheme_as_cors_enabled(securityManager, scheme);
    webkit_web_context_register_uri_scheme(context, scheme, on_uri_scheme_request, hw, NULL);

    printf("[Hermes] Registered URI scheme: %s\n", scheme);
}

void Hermes_Window_RunJavascript(void* window, const char* script) {
    HermesWindow* hw = (HermesWindow*)window;
    if (!hw || !script) return;

    webkit_web_view_run_javascript(WEBKIT_WEB_VIEW(hw->webView), script, NULL, NULL, NULL);
}

// ============================================================================
// Threading
// ============================================================================

typedef struct {
    InvokeCallback callback;
    GMutex mutex;
    GCond cond;
    gboolean done;
} InvokeContext;

static gboolean invoke_on_main(gpointer user_data) {
    InvokeContext* ctx = (InvokeContext*)user_data;

    if (ctx->callback) {
        ctx->callback();
    }

    g_mutex_lock(&ctx->mutex);
    ctx->done = TRUE;
    g_cond_signal(&ctx->cond);
    g_mutex_unlock(&ctx->mutex);

    return G_SOURCE_REMOVE;
}

void Hermes_Window_Invoke(void* window, InvokeCallback callback) {
    if (!callback) return;

    // Check if we're already on the main thread
    if (pthread_equal(pthread_self(), g_uiThreadId)) {
        callback();
        return;
    }

    InvokeContext ctx = { .callback = callback, .done = FALSE };
    g_mutex_init(&ctx.mutex);
    g_cond_init(&ctx.cond);

    g_idle_add(invoke_on_main, &ctx);

    // Wait for completion
    g_mutex_lock(&ctx.mutex);
    while (!ctx.done) {
        g_cond_wait(&ctx.cond, &ctx.mutex);
    }
    g_mutex_unlock(&ctx.mutex);

    g_mutex_clear(&ctx.mutex);
    g_cond_clear(&ctx.cond);
}

static gboolean begin_invoke_callback(gpointer user_data) {
    InvokeCallback callback = (InvokeCallback)user_data;
    if (callback) {
        callback();
    }
    return G_SOURCE_REMOVE;
}

void Hermes_Window_BeginInvoke(void* window, InvokeCallback callback) {
    if (!callback) return;

    // Check if we're already on the main thread
    if (pthread_equal(pthread_self(), g_uiThreadId)) {
        callback();
        return;
    }

    g_idle_add(begin_invoke_callback, (gpointer)callback);
}

// ============================================================================
// Memory Management
// ============================================================================

void Hermes_Free(void* ptr) {
    g_free(ptr);
}

void Hermes_FreeStringArray(char** array, int count) {
    if (!array) return;
    for (int i = 0; i < count; i++) {
        g_free(array[i]);
    }
    g_free(array);
}
