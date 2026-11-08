/* TKCode.Arc.Video.Native for Linux: the C ABI of ../tkcode_video.h over GStreamer 1.x.
   Design: design/components/video/video.md section 5.2 (Linux row); ADR 0036.

   GStreamer is loaded at run time with dlopen, so the library has no link-time dependency on it (only libc, libdl,
   libpthread) and needs no system headers: the few GLib / GObject / GStreamer types it reads are declared below.
   Missing libraries or core elements make tkv_initialize return TKV_E_UNAVAILABLE.

   Pipeline per player: playbin (uri) with
     video-sink = appsink (caps video/x-raw,format=BGRA; max-buffers 2; drop; sync; no signals); playbin plugs the
                  converter and the decoders;
     audio-sink = autoaudiosink, or fakesink sync=true when no audio output can be opened (no sound server, e.g. in a
                  container): probed once at create, and again when the audio sink fails while loading, so the clock
                  still runs and the video plays silently;
     audio-filter = scaletempo when present (keeps the pitch at rates other than 1).
   A bus thread per player pops messages (gst_bus_timed_pop_filtered) and maps them to TKV_EVENT_*; it also sends the
   4 Hz POSITION tick while playing. Frames are pulled on the caller's (UI) thread with gst_app_sink_try_pull_sample(0)
   (newest kept) or gst_app_sink_try_pull_preroll(0) after a load or seek while paused; the sample stays mapped until
   tkv_player_release_frame.

   Locking: one mutex per player guards its fields and serializes GStreamer calls made on its behalf. Events are queued
   while the lock is held and delivered after it is released, so the callback never runs under the lock. */

#define _GNU_SOURCE
#include "../tkcode_video.h"

#include <dlfcn.h>
#include <pthread.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

/* ---------------------------------------------------------------------------------------------------------------
   Minimal GLib / GObject / GStreamer declarations (LP64: x86_64 and aarch64 Linux). Layouts checked against
   GStreamer 1.22 headers (gstminiobject.h, gstmessage.h, gstbuffer.h, gstmemory.h, gstmeta.h, video/gstvideometa.h)
   and GLib's gerror.h; these struct prefixes have been ABI-stable since 1.0. */

typedef int gboolean;
typedef unsigned int guint;
typedef uint32_t GQuark;
typedef size_t GType;
typedef uint64_t GstClockTime;
#define GST_CLOCK_TIME_NONE ((GstClockTime)-1)
#define GST_SECOND ((int64_t)1000000000)

typedef struct GError { GQuark domain; int code; char* message; } GError;

typedef struct GstMiniObject {
    GType type;
    int refcount;
    int lockstate;
    guint flags;
    void* copy;
    void* dispose;
    void* free;
    guint n_qdata;
    void* qdata;
} GstMiniObject; /* 64 bytes */

typedef struct GstMessage {
    GstMiniObject mini_object;
    uint32_t type;      /* GstMessageType */
    uint64_t timestamp;
    void* src;          /* GstObject* */
    uint32_t seqnum;
} GstMessage;

typedef struct GstBuffer {
    GstMiniObject mini_object;
    void* pool;
    GstClockTime pts;
    GstClockTime dts;
    GstClockTime duration;
    uint64_t offset;
    uint64_t offset_end;
} GstBuffer;

typedef struct GstMapInfo {
    void* memory;
    int flags;
    uint8_t* data;
    size_t size;
    size_t maxsize;
    void* user_data[4];
    void* _gst_reserved[4];
} GstMapInfo; /* 104 bytes */

typedef struct GstMeta { guint flags; const void* info; } GstMeta;

typedef struct GstVideoMeta {
    GstMeta meta;
    void* buffer;
    int flags;
    int format;
    int id;
    guint width;
    guint height;
    guint n_planes;
    size_t offset[4];
    int stride[4];
} GstVideoMeta; /* prefix only */

_Static_assert(sizeof(GstMiniObject) == 64, "GstMiniObject layout");
_Static_assert(offsetof(GstMessage, type) == 64 && offsetof(GstMessage, src) == 80 && offsetof(GstMessage, seqnum) == 88,
               "GstMessage layout");
_Static_assert(offsetof(GstBuffer, pts) == 72, "GstBuffer layout");
_Static_assert(sizeof(GstMapInfo) == 104 && offsetof(GstMapInfo, data) == 16 && offsetof(GstMapInfo, size) == 24,
               "GstMapInfo layout");
_Static_assert(offsetof(GstVideoMeta, offset) == 48 && offsetof(GstVideoMeta, stride) == 80, "GstVideoMeta layout");

enum {
    GST_STATE_VOID_PENDING = 0, GST_STATE_NULL = 1, GST_STATE_READY = 2, GST_STATE_PAUSED = 3, GST_STATE_PLAYING = 4
};
enum { GST_STATE_CHANGE_FAILURE = 0, GST_STATE_CHANGE_SUCCESS = 1, GST_STATE_CHANGE_ASYNC = 2 };
enum { GST_FORMAT_BYTES = 2, GST_FORMAT_TIME = 3, GST_FORMAT_PERCENT = 5 };
#define GST_FORMAT_PERCENT_MAX 1000000
enum { GST_SEEK_FLAG_FLUSH = 1, GST_SEEK_FLAG_ACCURATE = 2, GST_SEEK_FLAG_KEY_UNIT = 4, GST_SEEK_FLAG_SNAP_BEFORE = 32 };
enum { GST_SEEK_TYPE_NONE = 0, GST_SEEK_TYPE_SET = 1 };
enum { GST_MAP_READ = 1 };
enum {
    GST_MESSAGE_EOS = 1u << 0,
    GST_MESSAGE_ERROR = 1u << 1,
    GST_MESSAGE_TAG = 1u << 4,
    GST_MESSAGE_BUFFERING = 1u << 5,
    GST_MESSAGE_STATE_CHANGED = 1u << 6,
    GST_MESSAGE_APPLICATION = 1u << 14,
    GST_MESSAGE_DURATION_CHANGED = 1u << 18,
    GST_MESSAGE_ASYNC_DONE = 1u << 21
};
/* GstResourceError / GstStreamError / GstCoreError codes used by the error mapping. */
enum {
    GST_STREAM_ERROR_NOT_IMPLEMENTED = 3, GST_STREAM_ERROR_TYPE_NOT_FOUND = 4, GST_STREAM_ERROR_WRONG_TYPE = 5,
    GST_STREAM_ERROR_CODEC_NOT_FOUND = 6, GST_STREAM_ERROR_DECRYPT = 12, GST_STREAM_ERROR_DECRYPT_NOKEY = 13
};
enum { GST_CORE_ERROR_MISSING_PLUGIN = 12 };
#define GST_PLAY_FLAG_TEXT 0x4

/* Function pointers. */
#define TKV_GST_FUNCS(X) \
    X(gboolean, gst_init_check, (int*, char***, GError**)) \
    X(void*, gst_element_factory_make, (const char*, const char*)) \
    X(void*, gst_element_factory_find, (const char*)) \
    X(int, gst_element_set_state, (void*, int)) \
    X(int, gst_element_get_state, (void*, int*, int*, GstClockTime)) \
    X(void*, gst_element_get_bus, (void*)) \
    X(void*, gst_element_get_static_pad, (void*, const char*)) \
    X(gboolean, gst_element_seek, (void*, double, int, int, int, int64_t, int, int64_t)) \
    X(gboolean, gst_element_query, (void*, void*)) \
    X(gboolean, gst_element_query_position, (void*, int, int64_t*)) \
    X(gboolean, gst_element_query_duration, (void*, int, int64_t*)) \
    X(void*, gst_object_ref, (void*)) \
    X(void*, gst_object_ref_sink, (void*)) \
    X(void, gst_object_unref, (void*)) \
    X(gboolean, gst_object_has_as_ancestor, (void*, void*)) \
    X(void, gst_mini_object_unref, (void*)) \
    X(void*, gst_mini_object_ref, (void*)) \
    X(void*, gst_bus_timed_pop_filtered, (void*, GstClockTime, uint32_t)) \
    X(gboolean, gst_bus_post, (void*, void*)) \
    X(void, gst_bus_set_flushing, (void*, gboolean)) \
    X(void*, gst_message_new_application, (void*, void*)) \
    X(uint32_t, gst_message_get_seqnum, (void*)) \
    X(void, gst_message_parse_state_changed, (void*, int*, int*, int*)) \
    X(void, gst_message_parse_error, (void*, GError**, char**)) \
    X(void, gst_message_parse_buffering, (void*, int*)) \
    X(void, gst_message_parse_tag, (void*, void**)) \
    X(gboolean, gst_tag_list_get_string, (void*, const char*, char**)) \
    X(void*, gst_structure_new_empty, (const char*)) \
    X(gboolean, gst_structure_get_int, (void*, const char*, int*)) \
    X(void*, gst_caps_from_string, (const char*)) \
    X(void*, gst_caps_get_structure, (void*, guint)) \
    X(void*, gst_pad_get_current_caps, (void*)) \
    X(void*, gst_sample_get_buffer, (void*)) \
    X(void*, gst_sample_get_caps, (void*)) \
    X(void*, gst_sample_get_segment, (void*)) \
    X(uint64_t, gst_segment_to_stream_time, (void*, int, uint64_t)) \
    X(gboolean, gst_buffer_map, (void*, GstMapInfo*, int)) \
    X(void, gst_buffer_unmap, (void*, GstMapInfo*)) \
    X(void*, gst_query_new_seeking, (int)) \
    X(void, gst_query_parse_seeking, (void*, int*, gboolean*, int64_t*, int64_t*)) \
    X(void*, gst_query_new_buffering, (int)) \
    X(guint, gst_query_get_n_buffering_ranges, (void*)) \
    X(gboolean, gst_query_parse_nth_buffering_range, (void*, guint, int64_t*, int64_t*)) \
    X(void, gst_query_parse_buffering_range, (void*, int*, int64_t*, int64_t*, int64_t*)) \
    X(GQuark, gst_resource_error_quark, (void)) \
    X(GQuark, gst_stream_error_quark, (void)) \
    X(GQuark, gst_core_error_quark, (void))

#define TKV_APP_FUNCS(X) \
    X(void*, gst_app_sink_try_pull_sample, (void*, GstClockTime)) \
    X(void*, gst_app_sink_try_pull_preroll, (void*, GstClockTime))

#define TKV_GOBJECT_FUNCS(X) \
    X(void, g_object_set, (void*, const char*, ...)) \
    X(void, g_object_get, (void*, const char*, ...))

#define TKV_GLIB_FUNCS(X) \
    X(void, g_free, (void*)) \
    X(void, g_error_free, (GError*))

#define TKV_DECLARE(ret, name, args) typedef ret (*name##_fn) args; static name##_fn p_##name;
TKV_GST_FUNCS(TKV_DECLARE)
TKV_APP_FUNCS(TKV_DECLARE)
TKV_GOBJECT_FUNCS(TKV_DECLARE)
TKV_GLIB_FUNCS(TKV_DECLARE)

/* Optional (libgstvideo): the per-buffer stride. Without it the stride is width * 4 (the BGRA default). */
typedef GstVideoMeta* (*gst_buffer_get_video_meta_fn)(void*);
static gst_buffer_get_video_meta_fn p_gst_buffer_get_video_meta;

static pthread_once_t s_init_once = PTHREAD_ONCE_INIT;
static int32_t s_init_result = TKV_E_UNAVAILABLE;
static int s_has_scaletempo;

static int load_all(void* lib, const char* const* names, void** slots, size_t count) {
    for (size_t i = 0; i < count; i++) {
        slots[i] = dlsym(lib, names[i]);
        if (slots[i] == NULL) {
            return 0;
        }
    }
    return 1;
}

#define TKV_NAME(ret, name, args) #name,
#define TKV_SLOT(ret, name, args) (void**)&p_##name,

static int factory_exists(const char* name) {
    void* f = p_gst_element_factory_find(name);
    if (f == NULL) {
        return 0;
    }
    p_gst_object_unref(f);
    return 1;
}

static void initialize_once(void) {
    void* glib = dlopen("libglib-2.0.so.0", RTLD_NOW | RTLD_GLOBAL);
    void* gobject = dlopen("libgobject-2.0.so.0", RTLD_NOW | RTLD_GLOBAL);
    void* gst = dlopen("libgstreamer-1.0.so.0", RTLD_NOW | RTLD_GLOBAL);
    void* app = dlopen("libgstapp-1.0.so.0", RTLD_NOW | RTLD_GLOBAL);
    if (glib == NULL || gobject == NULL || gst == NULL || app == NULL) {
        return;
    }
    static const char* const gst_names[] = { TKV_GST_FUNCS(TKV_NAME) };
    void** gst_slots[] = { TKV_GST_FUNCS(TKV_SLOT) };
    static const char* const app_names[] = { TKV_APP_FUNCS(TKV_NAME) };
    void** app_slots[] = { TKV_APP_FUNCS(TKV_SLOT) };
    static const char* const gobject_names[] = { TKV_GOBJECT_FUNCS(TKV_NAME) };
    void** gobject_slots[] = { TKV_GOBJECT_FUNCS(TKV_SLOT) };
    static const char* const glib_names[] = { TKV_GLIB_FUNCS(TKV_NAME) };
    void** glib_slots[] = { TKV_GLIB_FUNCS(TKV_SLOT) };
    void* tmp[64];
    struct { void* lib; const char* const* names; void*** slots; size_t n; } sets[] = {
        { gst, gst_names, gst_slots, sizeof(gst_names) / sizeof(gst_names[0]) },
        { app, app_names, app_slots, sizeof(app_names) / sizeof(app_names[0]) },
        { gobject, gobject_names, gobject_slots, sizeof(gobject_names) / sizeof(gobject_names[0]) },
        { glib, glib_names, glib_slots, sizeof(glib_names) / sizeof(glib_names[0]) },
    };
    for (size_t s = 0; s < sizeof(sets) / sizeof(sets[0]); s++) {
        if (sets[s].n > 64 || !load_all(sets[s].lib, sets[s].names, tmp, sets[s].n)) {
            return;
        }
        for (size_t i = 0; i < sets[s].n; i++) {
            *sets[s].slots[i] = tmp[i];
        }
    }
    void* video = dlopen("libgstvideo-1.0.so.0", RTLD_NOW | RTLD_GLOBAL);
    if (video != NULL) {
        p_gst_buffer_get_video_meta = (gst_buffer_get_video_meta_fn)dlsym(video, "gst_buffer_get_video_meta");
    }
    GError* error = NULL;
    if (!p_gst_init_check(NULL, NULL, &error)) {
        if (error != NULL) {
            p_g_error_free(error);
        }
        return;
    }
    /* playbin, appsink and the converters live in gst-plugins-base; without them nothing can play. */
    if (!factory_exists("playbin") || !factory_exists("appsink") || !factory_exists("videoconvert")) {
        return;
    }
    s_has_scaletempo = factory_exists("scaletempo");
    s_init_result = TKV_OK;
}

/* ---------------------------------------------------------------------------------------------------------------
   Player. */

typedef struct tkv_event { int32_t event; double a; double b; } tkv_event;
typedef struct tkv_events { tkv_event items[64]; int count; } tkv_events;

struct tkv_player {
    pthread_mutex_t lock;
    pthread_mutex_t deliver_lock; /* taken before lock is released, so events reach .NET in the order they were made */
    tkv_event_callback callback;
    void* context;

    void* playbin;
    void* appsink;
    void* audiosink;      /* the autoaudiosink we handed to playbin (our ref), NULL once on the fakesink fallback */
    void* bus;
    pthread_t thread;
    int thread_started;
    volatile int closing;

    int opened;
    int is_file;
    char* uri;
    uint32_t marker_seqnum;  /* messages before this application message belong to the previous media */
    int marker_pending;

    int loaded;
    int target_playing;
    int cur_gst;          /* last playbin state from STATE_CHANGED */
    int seeking;
    double seek_target;
    int buffering;
    int ended;
    int failed;
    int looping;
    int reported;         /* last TKV_STATE_* sent, -1 = none */
    int preroll_ready;

    int has_pending_seek;
    double pending_seek;
    int pending_accurate;

    double rate;
    double volume;
    double duration;
    int width;
    int height;
    int rotation;

    int frame_out;
    void* out_sample;
    void* out_buffer;
    GstMapInfo out_map;
    int64_t out_token;
    int64_t next_token;
    void* last_sample;    /* our ref to the last delivered sample, for tkv_player_redeliver_frame */
    int redeliver;

    double last_tick;
    char error[1024];
    char error_out[1024];
};

static double now_seconds(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (double)ts.tv_sec + (double)ts.tv_nsec / 1e9;
}

/* Progress events that a later one of the same kind replaces; the first to go when a batch is full. */
static int is_droppable(int32_t event) {
    return event == TKV_EVENT_POSITION || event == TKV_EVENT_BUFFERED || event == TKV_EVENT_FRAME;
}

static void push(tkv_events* q, int32_t event, double a, double b) {
    const int capacity = (int)(sizeof(q->items) / sizeof(q->items[0]));
    if (q->count == capacity) {
        /* Full: make room by dropping the oldest progress event; state, error and end events are never lost. */
        int victim = -1;
        for (int i = 0; i < q->count; i++) {
            if (is_droppable(q->items[i].event)) {
                victim = i;
                break;
            }
        }
        if (victim < 0) {
            if (is_droppable(event)) {
                return;
            }
            victim = 0;
        }
        memmove(&q->items[victim], &q->items[victim + 1], (size_t)(q->count - victim - 1) * sizeof(q->items[0]));
        q->count--;
    }
    q->items[q->count].event = event;
    q->items[q->count].a = a;
    q->items[q->count].b = b;
    q->count++;
}

/* Delivers queued events; call without the lock. */
static void deliver(tkv_player* p, tkv_events* q);

/*
 * Releases p->lock and delivers q. Control calls and the bus thread both deliver; taking deliver_lock before releasing
 * the state lock (hand over hand) makes them deliver in the order they changed the state, so a caller's PLAYING cannot
 * overtake the bus thread's later BUFFERING. The callback only queues (it never calls back into the shim), so holding
 * deliver_lock while it runs cannot deadlock.
 */
static void unlock_and_deliver(tkv_player* p, tkv_events* q, int send) {
    pthread_mutex_lock(&p->deliver_lock);
    pthread_mutex_unlock(&p->lock);
    if (send) {
        deliver(p, q);
    }
    pthread_mutex_unlock(&p->deliver_lock);
    q->count = 0;
}

static void deliver(tkv_player* p, tkv_events* q) {
    for (int i = 0; i < q->count; i++) {
        if (p->callback != NULL) {
            p->callback(p->context, q->items[i].event, q->items[i].a, q->items[i].b);
        }
    }
    q->count = 0;
}

static void set_error(tkv_player* p, const char* fmt, ...) {
    va_list args;
    va_start(args, fmt);
    vsnprintf(p->error, sizeof(p->error), fmt, args);
    va_end(args);
}

static int compute_state(tkv_player* p) {
    if (p->failed) {
        return TKV_STATE_FAILED;
    }
    if (!p->opened) {
        return TKV_STATE_IDLE;
    }
    if (!p->loaded) {
        return TKV_STATE_LOADING;
    }
    if (p->ended) {
        return TKV_STATE_ENDED;
    }
    if (p->buffering) {
        return TKV_STATE_BUFFERING;
    }
    if (p->seeking) {
        return p->reported;
    }
    if (p->target_playing) {
        return p->cur_gst == GST_STATE_PLAYING ? TKV_STATE_PLAYING : p->reported;
    }
    return p->cur_gst <= GST_STATE_PAUSED ? TKV_STATE_PAUSED : p->reported;
}

static void update_state(tkv_player* p, tkv_events* q) {
    int s = compute_state(p);
    if (s >= 0 && s != p->reported) {
        p->reported = s;
        push(q, TKV_EVENT_STATE, s, 0);
    }
}

static double query_position(tkv_player* p) {
    if (p->seeking) {
        return p->seek_target;
    }
    int64_t pos = 0;
    if (p->loaded && p_gst_element_query_position(p->playbin, GST_FORMAT_TIME, &pos) && pos >= 0) {
        return (double)pos / (double)GST_SECOND;
    }
    return 0;
}

static double query_duration(tkv_player* p) {
    int64_t d = 0;
    if (p_gst_element_query_duration(p->playbin, GST_FORMAT_TIME, &d) && d > 0) {
        return (double)d / (double)GST_SECOND;
    }
    return 0;
}

static void emit_size(tkv_player* p, tkv_events* q) {
    if (p->width <= 0 || p->height <= 0) {
        return;
    }
    if (p->rotation == 90 || p->rotation == 270) {
        push(q, TKV_EVENT_SIZE, p->height, p->width);
    } else {
        push(q, TKV_EVENT_SIZE, p->width, p->height);
    }
}

static void read_sink_size(tkv_player* p) {
    void* pad = p_gst_element_get_static_pad(p->appsink, "sink");
    if (pad == NULL) {
        return;
    }
    void* caps = p_gst_pad_get_current_caps(pad);
    if (caps != NULL) {
        void* st = p_gst_caps_get_structure(caps, 0);
        int w = 0;
        int h = 0;
        if (st != NULL && p_gst_structure_get_int(st, "width", &w) && p_gst_structure_get_int(st, "height", &h)) {
            p->width = w;
            p->height = h;
        }
        p_gst_mini_object_unref(caps);
    }
    p_gst_object_unref(pad);
}

static int query_seekable(tkv_player* p) {
    void* query = p_gst_query_new_seeking(GST_FORMAT_TIME);
    int seekable = 0;
    if (query != NULL) {
        if (p_gst_element_query(p->playbin, query)) {
            int fmt = 0;
            gboolean s = 0;
            int64_t start = 0;
            int64_t end = 0;
            p_gst_query_parse_seeking(query, &fmt, &s, &start, &end);
            seekable = s ? 1 : 0;
        }
        p_gst_mini_object_unref(query);
    }
    return seekable;
}

/* Starts a flushing seek at the current rate; the caller holds the lock. */
static int do_seek(tkv_player* p, double seconds, int accurate) {
    int flags = GST_SEEK_FLAG_FLUSH | (accurate ? GST_SEEK_FLAG_ACCURATE : (GST_SEEK_FLAG_KEY_UNIT | GST_SEEK_FLAG_SNAP_BEFORE));
    int64_t ns = (int64_t)(seconds * (double)GST_SECOND);
    if (!p_gst_element_seek(p->playbin, p->rate, GST_FORMAT_TIME, flags, GST_SEEK_TYPE_SET, ns, GST_SEEK_TYPE_NONE, -1)) {
        set_error(p, "seek to %.3f s refused", seconds);
        return 0;
    }
    p->seeking = 1;
    p->seek_target = seconds;
    p->preroll_ready = 0;
    return 1;
}

static void release_frame_locked(tkv_player* p) {
    if (p->frame_out) {
        p_gst_buffer_unmap(p->out_buffer, &p->out_map);
        p_gst_mini_object_unref(p->out_sample);
        p->out_sample = NULL;
        p->out_buffer = NULL;
        p->frame_out = 0;
    }
}

static void* make_fakesink(void) {
    void* fake = p_gst_element_factory_make("fakesink", "tkv-audio-fakesink");
    if (fake != NULL) {
        p_g_object_set(fake, "sync", (gboolean)1, NULL);
    }
    return fake;
}

/* Posts the marker that separates the previous media's messages from the new ones (the bus is FIFO). */
static void post_marker(tkv_player* p) {
    p_gst_bus_set_flushing(p->bus, 1);
    p_gst_bus_set_flushing(p->bus, 0);
    void* msg = p_gst_message_new_application(p->playbin, p_gst_structure_new_empty("tkv-open"));
    if (msg != NULL) {
        p->marker_seqnum = p_gst_message_get_seqnum(msg);
        p->marker_pending = 1;
        p_gst_bus_post(p->bus, msg);
    }
}

static void reset_media_state(tkv_player* p) {
    p->loaded = 0;
    p->seeking = 0;
    p->buffering = 0;
    p->ended = 0;
    p->failed = 0;
    p->preroll_ready = 0;
    p->duration = 0;
    p->width = 0;
    p->height = 0;
    p->rotation = 0;
    p->cur_gst = GST_STATE_READY;
}

/* Restarts loading the current uri (READY, marker, PAUSED or PLAYING); caller holds the lock. */
static int start_loading(tkv_player* p) {
    /* READY keeps the sinks open; after an error playbin is only reliably reusable from NULL. */
    p_gst_element_set_state(p->playbin, p->failed ? GST_STATE_NULL : GST_STATE_READY);
    post_marker(p);
    reset_media_state(p);
    p_g_object_set(p->playbin, "uri", p->uri, NULL);
    int r = p_gst_element_set_state(p->playbin, p->target_playing ? GST_STATE_PLAYING : GST_STATE_PAUSED);
    if (r == GST_STATE_CHANGE_FAILURE) {
        /* The bus carries the reason as an ERROR message. */
        return 0;
    }
    return 1;
}

static int map_error_kind(tkv_player* p, const GError* e) {
    if (e == NULL) {
        return TKV_ERR_DECODE;
    }
    if (e->domain == p_gst_resource_error_quark()) {
        return TKV_ERR_NETWORK;
    }
    if (e->domain == p_gst_core_error_quark()) {
        return e->code == GST_CORE_ERROR_MISSING_PLUGIN ? TKV_ERR_UNSUPPORTED : TKV_ERR_DECODE;
    }
    if (e->domain == p_gst_stream_error_quark()) {
        switch (e->code) {
            case GST_STREAM_ERROR_NOT_IMPLEMENTED:
            case GST_STREAM_ERROR_TYPE_NOT_FOUND:
            case GST_STREAM_ERROR_WRONG_TYPE:
            case GST_STREAM_ERROR_CODEC_NOT_FOUND:
            case GST_STREAM_ERROR_DECRYPT:
            case GST_STREAM_ERROR_DECRYPT_NOKEY:
                return TKV_ERR_UNSUPPORTED;
            default:
                return TKV_ERR_DECODE;
        }
    }
    return TKV_ERR_DECODE;
}

static void handle_message(tkv_player* p, GstMessage* msg, tkv_events* q) {
    if (p->marker_pending) {
        if (msg->type == GST_MESSAGE_APPLICATION && msg->seqnum == p->marker_seqnum) {
            p->marker_pending = 0;
        }
        return;
    }
    switch (msg->type) {
        case GST_MESSAGE_STATE_CHANGED: {
            if (msg->src != p->playbin) {
                break;
            }
            int old_state = 0;
            int new_state = 0;
            int pending = 0;
            p_gst_message_parse_state_changed(msg, &old_state, &new_state, &pending);
            p->cur_gst = new_state;
            if (p->loaded) {
                update_state(p, q);
            }
            break;
        }
        case GST_MESSAGE_ASYNC_DONE: {
            if (msg->src != p->playbin) {
                break;
            }
            if (!p->loaded) {
                p->loaded = 1;
                p->duration = query_duration(p);
                push(q, TKV_EVENT_DURATION, p->duration, 0);
                read_sink_size(p);
                emit_size(p, q);
                push(q, TKV_EVENT_SEEKABLE, query_seekable(p), 0);
                push(q, TKV_EVENT_BUFFERED, 0, 0);
                if (p->has_pending_seek || p->rate != 1.0) {
                    double at = p->has_pending_seek ? p->pending_seek : 0;
                    int accurate = p->has_pending_seek ? p->pending_accurate : 1;
                    p->has_pending_seek = 0;
                    if (do_seek(p, at, accurate)) {
                        /* The seek's ASYNC_DONE brings the frame and the position. */
                        update_state(p, q);
                        break;
                    }
                }
                p->preroll_ready = 1;
                if (!p->target_playing) {
                    push(q, TKV_EVENT_FRAME, 0, 0);
                }
                update_state(p, q);
                break;
            }
            int was_seeking = p->seeking;
            p->seeking = 0;
            p->preroll_ready = 1;
            if (was_seeking) {
                push(q, TKV_EVENT_POSITION, query_position(p), 0);
            }
            if (!p->target_playing) {
                push(q, TKV_EVENT_FRAME, 0, 0);
            }
            update_state(p, q);
            break;
        }
        case GST_MESSAGE_DURATION_CHANGED: {
            if (p->loaded) {
                double d = query_duration(p);
                if (d > 0 && d != p->duration) {
                    p->duration = d;
                    push(q, TKV_EVENT_DURATION, d, 0);
                    push(q, TKV_EVENT_BUFFERED, 0, 0);
                }
            }
            break;
        }
        case GST_MESSAGE_BUFFERING: {
            int percent = 100;
            p_gst_message_parse_buffering(msg, &percent);
            if (!p->is_file) {
                push(q, TKV_EVENT_BUFFERED, 0, 0);
                if (percent < 100 && p->target_playing && p->loaded && !p->buffering) {
                    p->buffering = 1;
                    p_gst_element_set_state(p->playbin, GST_STATE_PAUSED);
                } else if (percent >= 100 && p->buffering) {
                    p->buffering = 0;
                    if (p->target_playing) {
                        p_gst_element_set_state(p->playbin, GST_STATE_PLAYING);
                    }
                }
                update_state(p, q);
            }
            break;
        }
        case GST_MESSAGE_TAG: {
            void* tags = NULL;
            p_gst_message_parse_tag(msg, &tags);
            if (tags != NULL) {
                char* orientation = NULL;
                if (p_gst_tag_list_get_string(tags, "image-orientation", &orientation) && orientation != NULL) {
                    int rotation = 0;
                    if (strcmp(orientation, "rotate-90") == 0) {
                        rotation = 90;
                    } else if (strcmp(orientation, "rotate-180") == 0) {
                        rotation = 180;
                    } else if (strcmp(orientation, "rotate-270") == 0) {
                        rotation = 270;
                    }
                    p_g_free(orientation);
                    if (rotation != p->rotation) {
                        p->rotation = rotation;
                        if (p->loaded) {
                            emit_size(p, q);
                        }
                    }
                }
                p_gst_mini_object_unref(tags);
            }
            break;
        }
        case GST_MESSAGE_EOS: {
            if (p->looping) {
                if (do_seek(p, 0, 1)) {
                    break;
                }
            }
            p->ended = 1;
            p->seeking = 0;
            push(q, TKV_EVENT_POSITION, p->duration > 0 ? p->duration : query_position(p), 0);
            update_state(p, q);
            push(q, TKV_EVENT_ENDED, 0, 0);
            break;
        }
        case GST_MESSAGE_ERROR: {
            if (p->failed) {
                /* Elements report one failure several times (source, demuxer, "internal data stream error"); the
                   first message carries the cause. */
                break;
            }
            GError* error = NULL;
            char* debug = NULL;
            p_gst_message_parse_error(msg, &error, &debug);
            /* No usable audio output (no sound server): play silently on a synced fakesink instead. */
            if (!p->loaded && p->audiosink != NULL && msg->src != NULL &&
                (msg->src == p->audiosink || p_gst_object_has_as_ancestor(msg->src, p->audiosink))) {
                void* fake = make_fakesink();
                if (fake != NULL) {
                    p_gst_element_set_state(p->playbin, GST_STATE_READY);
                    p_g_object_set(p->playbin, "audio-sink", fake, NULL);
                    p_gst_object_unref(p->audiosink);
                    p->audiosink = NULL;
                    start_loading(p);
                    if (error != NULL) {
                        p_g_error_free(error);
                    }
                    p_g_free(debug);
                    break;
                }
            }
            int kind = map_error_kind(p, error);
            set_error(p, "%s", error != NULL && error->message != NULL ? error->message : "playback failed");
            if (error != NULL) {
                p_g_error_free(error);
            }
            p_g_free(debug);
            p->failed = 1;
            push(q, TKV_EVENT_ERROR, kind, 0);
            update_state(p, q);
            break;
        }
        default:
            break;
    }
}

static void* bus_thread(void* arg) {
    tkv_player* p = (tkv_player*)arg;
    const uint32_t types = GST_MESSAGE_EOS | GST_MESSAGE_ERROR | GST_MESSAGE_TAG | GST_MESSAGE_BUFFERING |
                           GST_MESSAGE_STATE_CHANGED | GST_MESSAGE_APPLICATION | GST_MESSAGE_DURATION_CHANGED |
                           GST_MESSAGE_ASYNC_DONE;
    tkv_events q;
    q.count = 0;
    while (!p->closing) {
        GstMessage* msg = (GstMessage*)p_gst_bus_timed_pop_filtered(p->bus, 50 * 1000000ull, types);
        pthread_mutex_lock(&p->lock);
        if (msg != NULL && !p->closing) {
            handle_message(p, msg, &q);
        }
        if (!p->closing && p->reported == TKV_STATE_PLAYING) {
            double now = now_seconds();
            if (now - p->last_tick >= 0.25) {
                p->last_tick = now;
                push(&q, TKV_EVENT_POSITION, query_position(p), 0);
            }
        }
        int closing = p->closing;
        unlock_and_deliver(p, &q, !closing);
        if (msg != NULL) {
            p_gst_mini_object_unref(msg);
        }
    }
    return NULL;
}

/* ---------------------------------------------------------------------------------------------------------------
   Exports. */

TKV_API int32_t tkv_version(void) {
    return TKV_ABI_VERSION;
}

TKV_API int32_t tkv_initialize(void) {
    pthread_once(&s_init_once, initialize_once);
    return s_init_result;
}

TKV_API int32_t tkv_frame_kinds(void) {
    return TKV_FRAME_CPU_BGRA;
}

TKV_API int32_t tkv_gpu_handle_type(void) {
    return TKV_GPU_NONE;
}

static int any_factory(const char* const* names) {
    for (; *names != NULL; names++) {
        if (factory_exists(*names)) {
            return 1;
        }
    }
    return 0;
}

/* 1 when a decoder for the codec is installed, 0 when not, -1 when the codec is unknown. */
static int codec_available(const char* codec) {
    static const char* const h264[] = { "avdec_h264", "openh264dec", "vah264dec", "vaapih264dec", "v4l2h264dec", "nvh264dec", NULL };
    static const char* const h265[] = { "avdec_h265", "vah265dec", "vaapih265dec", "v4l2h265dec", "libde265dec", "nvh265dec", NULL };
    static const char* const vp8[] = { "vp8dec", "avdec_vp8", "vavp8dec", "v4l2vp8dec", NULL };
    static const char* const vp9[] = { "vp9dec", "avdec_vp9", "vavp9dec", "v4l2vp9dec", NULL };
    static const char* const av1[] = { "dav1ddec", "av1dec", "avdec_av1", "vaav1dec", NULL };
    static const char* const aac[] = { "avdec_aac", "faad", "fdkaacdec", NULL };
    static const char* const mp3[] = { "mpg123audiodec", "avdec_mp3", "mpegaudioparse", NULL };
    static const char* const opus[] = { "opusdec", "avdec_opus", NULL };
    static const char* const vorbis[] = { "vorbisdec", "avdec_vorbis", NULL };
    static const char* const flac[] = { "flacdec", "avdec_flac", NULL };
    static const char* const theora[] = { "theoradec", NULL };
    static const struct { const char* prefix; const char* const* decoders; } map[] = {
        { "avc1", h264 }, { "avc3", h264 }, { "hev1", h265 }, { "hvc1", h265 },
        { "vp8", vp8 }, { "vp09", vp9 }, { "vp9", vp9 }, { "av01", av1 },
        { "mp4a.40", aac }, { "mp4a.69", mp3 }, { "mp4a.6b", mp3 }, { "mp4a", aac }, { "mp3", mp3 },
        { "opus", opus }, { "vorbis", vorbis }, { "flac", flac }, { "theora", theora },
    };
    for (size_t i = 0; i < sizeof(map) / sizeof(map[0]); i++) {
        size_t n = strlen(map[i].prefix);
        if (strncmp(codec, map[i].prefix, n) == 0) {
            return any_factory(map[i].decoders);
        }
    }
    return -1;
}

static void trim_lower(char* s) {
    char* start = s;
    while (*start == ' ' || *start == '\t') {
        start++;
    }
    size_t n = strlen(start);
    while (n > 0 && (start[n - 1] == ' ' || start[n - 1] == '\t')) {
        n--;
    }
    for (size_t i = 0; i < n; i++) {
        char c = start[i];
        s[i] = (c >= 'A' && c <= 'Z') ? (char)(c - 'A' + 'a') : c;
    }
    s[n] = 0;
}

TKV_API int32_t tkv_can_play(const char* media_type) {
    if (media_type == NULL || tkv_initialize() != TKV_OK) {
        return 0;
    }
    char buf[512];
    snprintf(buf, sizeof(buf), "%s", media_type);
    char* params = strchr(buf, ';');
    if (params != NULL) {
        *params++ = 0;
    }
    trim_lower(buf);
    static const char* const qt[] = { "qtdemux", NULL };
    static const char* const mkv[] = { "matroskademux", NULL };
    static const char* const ogg[] = { "oggdemux", NULL };
    static const char* const ts[] = { "tsdemux", NULL };
    static const char* const avi[] = { "avidemux", NULL };
    static const struct { const char* mime; const char* const* demuxers; } containers[] = {
        { "video/mp4", qt }, { "audio/mp4", qt }, { "video/quicktime", qt }, { "video/x-m4v", qt },
        { "video/webm", mkv }, { "audio/webm", mkv }, { "video/x-matroska", mkv }, { "video/matroska", mkv },
        { "video/ogg", ogg }, { "audio/ogg", ogg }, { "video/mp2t", ts }, { "video/x-msvideo", avi },
    };
    const char* const* demuxers = NULL;
    for (size_t i = 0; i < sizeof(containers) / sizeof(containers[0]); i++) {
        if (strcmp(buf, containers[i].mime) == 0) {
            demuxers = containers[i].demuxers;
            break;
        }
    }
    if (demuxers == NULL || !any_factory(demuxers)) {
        return 0;
    }
    /* codecs="a, b" */
    if (params != NULL) {
        for (char* c = params; *c != 0; c++) {
            if (*c >= 'A' && *c <= 'Z') {
                *c = (char)(*c - 'A' + 'a');
            }
        }
    }
    char* codecs = params != NULL ? strstr(params, "codecs") : NULL;
    if (codecs == NULL) {
        return 1;
    }
    codecs = strchr(codecs, '=');
    if (codecs == NULL) {
        return 1;
    }
    codecs++;
    char list[384];
    snprintf(list, sizeof(list), "%s", codecs);
    int any = 0;
    char* save = NULL;
    for (char* tok = strtok_r(list, ",\"'", &save); tok != NULL; tok = strtok_r(NULL, ",\"'", &save)) {
        char* semi = strchr(tok, ';');
        if (semi != NULL) {
            *semi = 0;
        }
        trim_lower(tok);
        if (tok[0] == 0) {
            continue;
        }
        any = 1;
        if (codec_available(tok) != 1) {
            return 0;
        }
    }
    return any ? 2 : 1;
}

TKV_API int32_t tkv_player_create(tkv_event_callback callback, void* context, const uint8_t* adapter_luid, tkv_player** player) {
    (void)adapter_luid;
    if (player == NULL) {
        return TKV_E_INVALID_ARG;
    }
    *player = NULL;
    int32_t init = tkv_initialize();
    if (init != TKV_OK) {
        return init;
    }
    tkv_player* p = (tkv_player*)calloc(1, sizeof(tkv_player));
    if (p == NULL) {
        return TKV_E_FAIL;
    }
    pthread_mutex_init(&p->lock, NULL);
    pthread_mutex_init(&p->deliver_lock, NULL);
    p->callback = callback;
    p->context = context;
    p->rate = 1.0;
    p->volume = 1.0;
    p->reported = TKV_STATE_IDLE;
    p->cur_gst = GST_STATE_NULL;

    p->playbin = p_gst_element_factory_make("playbin", NULL);
    p->appsink = p_gst_element_factory_make("appsink", "tkv-video-sink");
    if (p->playbin == NULL || p->appsink == NULL) {
        if (p->playbin != NULL) {
            p_gst_object_unref(p_gst_object_ref_sink(p->playbin));
        }
        if (p->appsink != NULL) {
            p_gst_object_unref(p_gst_object_ref_sink(p->appsink));
        }
        pthread_mutex_destroy(&p->deliver_lock);
        pthread_mutex_destroy(&p->lock);
        free(p);
        return TKV_E_FAIL;
    }
    p_gst_object_ref_sink(p->playbin);
    p_gst_object_ref_sink(p->appsink);
    void* caps = p_gst_caps_from_string("video/x-raw,format=BGRA");
    p_g_object_set(p->appsink, "caps", caps, "max-buffers", (guint)2, "drop", (gboolean)1, "sync", (gboolean)1,
                   "emit-signals", (gboolean)0, "enable-last-sample", (gboolean)0, NULL);
    /* wait-on-eos (default TRUE) holds EOS back until the queued samples are pulled; a consumer that does not draw
       (hidden surface, tests) would then never see the end, so EOS must not wait for the UI. */
    p_g_object_set(p->appsink, "wait-on-eos", (gboolean)0, NULL);
    p_gst_mini_object_unref(caps);
    p_g_object_set(p->playbin, "video-sink", p->appsink, NULL);

    /* Audio: autoaudiosink when it can open a device, otherwise a synced fakesink so the clock still runs. */
    void* audio = p_gst_element_factory_make("autoaudiosink", "tkv-audio-sink");
    if (audio != NULL) {
        p_gst_object_ref_sink(audio);
        int r = p_gst_element_set_state(audio, GST_STATE_READY);
        p_gst_element_set_state(audio, GST_STATE_NULL);
        if (r == GST_STATE_CHANGE_FAILURE) {
            p_gst_object_unref(audio);
            audio = NULL;
        }
    }
    if (audio != NULL) {
        p->audiosink = audio;
        p_g_object_set(p->playbin, "audio-sink", audio, NULL);
    } else {
        void* fake = make_fakesink();
        if (fake != NULL) {
            p_g_object_set(p->playbin, "audio-sink", fake, NULL);
        }
    }
    if (s_has_scaletempo) {
        void* tempo = p_gst_element_factory_make("scaletempo", "tkv-scaletempo");
        if (tempo != NULL) {
            p_g_object_set(p->playbin, "audio-filter", tempo, NULL);
        }
    }
    /* Captions are drawn by VideoCaptionLayer; keep playbin from rendering embedded subtitles. */
    guint flags = 0;
    p_g_object_get(p->playbin, "flags", &flags, NULL);
    p_g_object_set(p->playbin, "flags", flags & ~(guint)GST_PLAY_FLAG_TEXT, NULL);

    p->bus = p_gst_element_get_bus(p->playbin);
    if (p->bus == NULL || pthread_create(&p->thread, NULL, bus_thread, p) != 0) {
        tkv_player_destroy(p);
        return TKV_E_FAIL;
    }
    p->thread_started = 1;
    *player = p;
    return TKV_OK;
}

TKV_API void tkv_player_destroy(tkv_player* p) {
    if (p == NULL) {
        return;
    }
    pthread_mutex_lock(&p->lock);
    p->closing = 1;
    pthread_mutex_unlock(&p->lock);
    if (p->thread_started) {
        pthread_join(p->thread, NULL);
    }
    release_frame_locked(p);
    if (p->last_sample != NULL) {
        p_gst_mini_object_unref(p->last_sample);
        p->last_sample = NULL;
    }
    if (p->playbin != NULL) {
        p_gst_element_set_state(p->playbin, GST_STATE_NULL);
    }
    if (p->bus != NULL) {
        p_gst_object_unref(p->bus);
    }
    if (p->audiosink != NULL) {
        p_gst_object_unref(p->audiosink);
    }
    if (p->appsink != NULL) {
        p_gst_object_unref(p->appsink);
    }
    if (p->playbin != NULL) {
        p_gst_object_unref(p->playbin);
    }
    free(p->uri);
    pthread_mutex_destroy(&p->deliver_lock);
    pthread_mutex_destroy(&p->lock);
    free(p);
}

TKV_API int32_t tkv_player_open(tkv_player* p, const char* url) {
    if (p == NULL || url == NULL || url[0] == 0) {
        return TKV_E_INVALID_ARG;
    }
    tkv_events q;
    q.count = 0;
    pthread_mutex_lock(&p->lock);
    char* copy = strdup(url);
    if (copy == NULL) {
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    free(p->uri);
    p->uri = copy;
    p->is_file = strncmp(url, "file:", 5) == 0;
    p->opened = 1;
    p->target_playing = 0;
    p->has_pending_seek = 0;
    p->error[0] = 0;
    p->redeliver = 0;
    if (p->last_sample != NULL) {
        p_gst_mini_object_unref(p->last_sample);
        p->last_sample = NULL;
    }
    int ok = start_loading(p);
    p->reported = TKV_STATE_LOADING;
    push(&q, TKV_EVENT_STATE, TKV_STATE_LOADING, 0);
    unlock_and_deliver(p, &q, 1);
    (void)ok;
    return TKV_OK;
}

TKV_API int32_t tkv_player_play(tkv_player* p) {
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    tkv_events q;
    q.count = 0;
    pthread_mutex_lock(&p->lock);
    if (!p->opened || p->failed) {
        set_error(p, "nothing loaded");
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    p->target_playing = 1;
    if (p->ended) {
        p->ended = 0;
        do_seek(p, 0, 1);
    }
    if (!p->buffering) {
        p_gst_element_set_state(p->playbin, GST_STATE_PLAYING);
    }
    update_state(p, &q);
    unlock_and_deliver(p, &q, 1);
    return TKV_OK;
}

TKV_API int32_t tkv_player_pause(tkv_player* p) {
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    tkv_events q;
    q.count = 0;
    pthread_mutex_lock(&p->lock);
    if (!p->opened || p->failed) {
        pthread_mutex_unlock(&p->lock);
        return TKV_OK;
    }
    p->target_playing = 0;
    p_gst_element_set_state(p->playbin, GST_STATE_PAUSED);
    update_state(p, &q);
    unlock_and_deliver(p, &q, 1);
    return TKV_OK;
}

TKV_API int32_t tkv_player_seek(tkv_player* p, double seconds, int32_t accurate) {
    if (p == NULL || !(seconds >= 0)) {
        return TKV_E_INVALID_ARG;
    }
    tkv_events q;
    q.count = 0;
    int32_t result = TKV_OK;
    pthread_mutex_lock(&p->lock);
    if (!p->opened || p->failed) {
        set_error(p, "nothing loaded");
        result = TKV_E_FAIL;
    } else if (!p->loaded) {
        p->has_pending_seek = 1;
        p->pending_seek = seconds;
        p->pending_accurate = accurate;
    } else {
        if (p->duration > 0 && seconds > p->duration) {
            seconds = p->duration;
        }
        int was_ended = p->ended;
        p->ended = 0;
        if (!do_seek(p, seconds, accurate)) {
            p->ended = was_ended;
            result = TKV_E_FAIL;
        } else if (was_ended) {
            /* Leaving the end: report the user's target once the seek lands. */
            p->reported = p->target_playing ? TKV_STATE_PLAYING : TKV_STATE_PAUSED;
            push(&q, TKV_EVENT_STATE, p->reported, 0);
        }
    }
    unlock_and_deliver(p, &q, 1);
    return result;
}

TKV_API double tkv_player_position(tkv_player* p) {
    if (p == NULL) {
        return 0;
    }
    pthread_mutex_lock(&p->lock);
    double pos = p->has_pending_seek ? p->pending_seek : query_position(p);
    if (p->ended && p->duration > 0) {
        pos = p->duration;
    }
    pthread_mutex_unlock(&p->lock);
    return pos;
}

TKV_API int32_t tkv_player_set_volume(tkv_player* p, double volume) {
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    if (!(volume >= 0)) {
        volume = 0;
    }
    if (volume > 1) {
        volume = 1;
    }
    pthread_mutex_lock(&p->lock);
    p->volume = volume;
    p_g_object_set(p->playbin, "volume", volume, NULL);
    pthread_mutex_unlock(&p->lock);
    return TKV_OK;
}

TKV_API int32_t tkv_player_set_rate(tkv_player* p, double rate) {
    if (p == NULL || !(rate > 0)) {
        return TKV_E_INVALID_ARG;
    }
    if (rate < 0.25 || rate > 4) {
        return TKV_E_UNSUPPORTED;
    }
    int32_t result = TKV_OK;
    pthread_mutex_lock(&p->lock);
    double old_rate = p->rate;
    p->rate = rate;
    if (p->loaded && !p->ended && rate != old_rate) {
        double at = query_position(p);
        if (!do_seek(p, at, 1)) {
            p->rate = old_rate;
            set_error(p, "rate %.2f refused", rate);
            result = TKV_E_UNSUPPORTED;
        }
    }
    pthread_mutex_unlock(&p->lock);
    return result;
}

TKV_API int32_t tkv_player_set_loop(tkv_player* p, int32_t loop) {
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    pthread_mutex_lock(&p->lock);
    p->looping = loop != 0;
    pthread_mutex_unlock(&p->lock);
    return TKV_OK;
}

TKV_API int32_t tkv_player_buffered(tkv_player* p, double* ranges, int32_t capacity) {
    if (p == NULL || (ranges == NULL && capacity > 0) || capacity < 0) {
        return 0;
    }
    int count = 0;
    pthread_mutex_lock(&p->lock);
    if (!p->loaded || capacity == 0) {
        pthread_mutex_unlock(&p->lock);
        return 0;
    }
    if (p->is_file) {
        if (p->duration > 0) {
            ranges[0] = 0;
            ranges[1] = p->duration;
            count = 1;
        }
        pthread_mutex_unlock(&p->lock);
        return count;
    }
    void* query = p_gst_query_new_buffering(GST_FORMAT_TIME);
    if (query != NULL) {
        if (p_gst_element_query(p->playbin, query)) {
            int fmt = 0;
            int64_t start = 0;
            int64_t stop = 0;
            int64_t total = 0;
            p_gst_query_parse_buffering_range(query, &fmt, &start, &stop, &total);
            guint n = p_gst_query_get_n_buffering_ranges(query);
            for (guint i = 0; i < n && count < capacity; i++) {
                int64_t s = 0;
                int64_t e = 0;
                if (!p_gst_query_parse_nth_buffering_range(query, i, &s, &e)) {
                    continue;
                }
                double a;
                double b;
                if (fmt == GST_FORMAT_TIME) {
                    a = (double)s / (double)GST_SECOND;
                    b = (double)e / (double)GST_SECOND;
                } else if (fmt == GST_FORMAT_PERCENT && p->duration > 0) {
                    a = (double)s / GST_FORMAT_PERCENT_MAX * p->duration;
                    b = (double)e / GST_FORMAT_PERCENT_MAX * p->duration;
                } else if (fmt == GST_FORMAT_BYTES && total > 0 && p->duration > 0) {
                    a = (double)s / (double)total * p->duration;
                    b = (double)e / (double)total * p->duration;
                } else {
                    continue;
                }
                if (b > a) {
                    ranges[2 * count] = a;
                    ranges[2 * count + 1] = b;
                    count++;
                }
            }
        }
        p_gst_mini_object_unref(query);
    }
    pthread_mutex_unlock(&p->lock);
    return count;
}

TKV_API int32_t tkv_player_acquire_frame(tkv_player* p, const tkv_frame_request* request, tkv_frame* frame) {
    (void)request; /* No scaling: the appsink delivers the natural size. */
    if (p == NULL || frame == NULL) {
        return TKV_E_INVALID_ARG;
    }
    pthread_mutex_lock(&p->lock);
    if (p->frame_out) {
        set_error(p, "the previous frame was not released");
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    if (!p->opened || p->failed) {
        pthread_mutex_unlock(&p->lock);
        return TKV_E_NO_FRAME;
    }
    /* After a load or seek the preroll sample is pulled first: pulling the queue first can drop it (seen with
       GStreamer 1.22). A queued sample, when there is one, is newer and wins. */
    void* sample = NULL;
    if (p->preroll_ready) {
        p->preroll_ready = 0;
        sample = p_gst_app_sink_try_pull_preroll(p->appsink, 0);
    }
    for (;;) {
        void* s = p_gst_app_sink_try_pull_sample(p->appsink, 0);
        if (s == NULL) {
            break;
        }
        if (sample != NULL) {
            p_gst_mini_object_unref(sample);
        }
        sample = s;
    }
    if (sample == NULL && p->redeliver && p->last_sample != NULL) {
        sample = p_gst_mini_object_ref(p->last_sample);
    }
    if (sample == NULL) {
        pthread_mutex_unlock(&p->lock);
        return TKV_E_NO_FRAME;
    }
    void* buffer = p_gst_sample_get_buffer(sample);
    void* caps = p_gst_sample_get_caps(sample);
    int width = 0;
    int height = 0;
    void* st = caps != NULL ? p_gst_caps_get_structure(caps, 0) : NULL;
    if (buffer == NULL || st == NULL || !p_gst_structure_get_int(st, "width", &width) ||
        !p_gst_structure_get_int(st, "height", &height) || width <= 0 || height <= 0) {
        p_gst_mini_object_unref(sample);
        set_error(p, "sample without video caps");
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    if (!p_gst_buffer_map(buffer, &p->out_map, GST_MAP_READ)) {
        p_gst_mini_object_unref(sample);
        set_error(p, "cannot map the video buffer");
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    size_t offset = 0;
    int stride = width * 4;
    GstVideoMeta* meta = p_gst_buffer_get_video_meta != NULL ? p_gst_buffer_get_video_meta(buffer) : NULL;
    if (meta != NULL && meta->stride[0] >= width * 4) {
        offset = meta->offset[0];
        stride = meta->stride[0];
    }
    if (offset + (size_t)stride * (size_t)(height - 1) + (size_t)width * 4 > p->out_map.size) {
        p_gst_buffer_unmap(buffer, &p->out_map);
        p_gst_mini_object_unref(sample);
        set_error(p, "video buffer smaller than its caps");
        pthread_mutex_unlock(&p->lock);
        return TKV_E_FAIL;
    }
    double time = 0;
    GstClockTime pts = ((GstBuffer*)buffer)->pts;
    if (pts != GST_CLOCK_TIME_NONE) {
        void* segment = p_gst_sample_get_segment(sample);
        uint64_t stream_time = segment != NULL ? p_gst_segment_to_stream_time(segment, GST_FORMAT_TIME, pts) : pts;
        if (stream_time != GST_CLOCK_TIME_NONE) {
            time = (double)stream_time / (double)GST_SECOND;
        }
    }
    p->redeliver = 0;
    if (p->last_sample != sample) {
        if (p->last_sample != NULL) {
            p_gst_mini_object_unref(p->last_sample);
        }
        p->last_sample = p_gst_mini_object_ref(sample);
    }
    p->frame_out = 1;
    p->out_sample = sample;
    p->out_buffer = buffer;
    p->out_token = ++p->next_token;
    memset(frame, 0, sizeof(*frame));
    frame->kind = TKV_FRAME_CPU_BGRA;
    frame->width = width;
    frame->height = height;
    frame->rotation = p->rotation;
    frame->time = time;
    frame->pixels = p->out_map.data + offset;
    frame->row_bytes = stride;
    frame->gpu_handle_type = TKV_GPU_NONE;
    frame->token = p->out_token;
    pthread_mutex_unlock(&p->lock);
    return TKV_OK;
}

TKV_API void tkv_player_release_frame(tkv_player* p, int64_t token) {
    if (p == NULL) {
        return;
    }
    pthread_mutex_lock(&p->lock);
    if (p->frame_out && p->out_token == token) {
        release_frame_locked(p);
    }
    pthread_mutex_unlock(&p->lock);
}

TKV_API const char* tkv_player_last_error(tkv_player* p) {
    if (p == NULL) {
        return "";
    }
    pthread_mutex_lock(&p->lock);
    memcpy(p->error_out, p->error, sizeof(p->error_out));
    pthread_mutex_unlock(&p->lock);
    return p->error_out;
}

/* ABI 2 (optional): hand out the last delivered frame again on the next acquire when nothing newer arrived.
   tkv_player_timeline_sync is not exported: Linux frames are CPU only. */
TKV_API int32_t tkv_player_redeliver_frame(tkv_player* p) {
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    pthread_mutex_lock(&p->lock);
    p->redeliver = 1;
    pthread_mutex_unlock(&p->lock);
    return TKV_OK;
}
