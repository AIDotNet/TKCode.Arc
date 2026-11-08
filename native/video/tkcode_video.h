/* TKCode.Arc.Video.Native: one flat C ABI over each platform's system player.
   Implemented by win/tkcode_video_mf.c (Media Foundation), mac/tkcode_video_av.m (AVFoundation) and
   linux/tkcode_video_gst.c (GStreamer, loaded with dlopen). Each RID ships one library: tkcode_video.
   Design: design/components/video/video.md section 5.2; ADR 0036.

   Rules:
   - Functions returning int32_t return TKV_OK (0) or a negative TKV_E_* code.
   - Strings are UTF-8. Times are seconds as double unless named *_ns.
   - The event callback may run on any thread, including inside a tkv_* call; the managed side marshals to the UI thread.
     It must not call back into tkv_* functions for the same player.
   - A frame handed out by tkv_player_acquire_frame stays valid until tkv_player_release_frame; at most one is out at a time. */
#ifndef TKCODE_VIDEO_H
#define TKCODE_VIDEO_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
  #define TKV_API __declspec(dllexport)
#else
  #define TKV_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* 1: initial. 2: adds tkv_player_timeline_sync and tkv_player_redeliver_frame (optional exports, see below).
   Later optional exports (no version change; a missing export means the feature is absent): the embedded text track
   functions tkv_player_set_text_callback, tkv_player_text_track_count, tkv_player_text_track_info and
   tkv_player_select_text_track, with TKV_EVENT_TEXT_TRACKS. */
#define TKV_ABI_VERSION 2

/* Results. */
#define TKV_OK                    0
#define TKV_E_FAIL               -1  /* anything else; see tkv_player_last_error */
#define TKV_E_UNAVAILABLE        -2  /* the system framework is missing (no GStreamer, no Media Foundation on N editions) */
#define TKV_E_INVALID_ARG        -3
#define TKV_E_NO_FRAME           -4  /* acquire: nothing new to show */
#define TKV_E_UNSUPPORTED        -5  /* format or operation not supported */

/* Error kinds reported with TKV_EVENT_ERROR (match VideoErrorKind). */
#define TKV_ERR_ABORTED           0
#define TKV_ERR_NETWORK           1
#define TKV_ERR_DECODE            2
#define TKV_ERR_UNSUPPORTED       3

/* Events: (context, event, a, b). */
#define TKV_EVENT_STATE           1  /* a = TKV_STATE_* */
#define TKV_EVENT_POSITION        2  /* a = seconds; about 4 Hz while playing, and after a seek completes */
#define TKV_EVENT_DURATION        3  /* a = seconds (0 = unknown or live) */
#define TKV_EVENT_SIZE            4  /* a = width, b = height (pixels after rotation) */
#define TKV_EVENT_BUFFERED        5  /* ranges changed: call tkv_player_buffered */
#define TKV_EVENT_FRAME           6  /* a new frame is ready while paused (after load or seek) */
#define TKV_EVENT_ERROR           7  /* a = TKV_ERR_*; message via tkv_player_last_error */
#define TKV_EVENT_ENDED           8  /* reached the end (not looping) */
#define TKV_EVENT_SEEKABLE        9  /* a = 1 or 0 */
#define TKV_EVENT_TEXT_TRACKS    10  /* the embedded text tracks are known or changed: call tkv_player_text_track_count */

/* States reported with TKV_EVENT_STATE (match VideoPlaybackState). */
#define TKV_STATE_IDLE            0
#define TKV_STATE_LOADING         1
#define TKV_STATE_PAUSED          2
#define TKV_STATE_PLAYING         3
#define TKV_STATE_BUFFERING       4
#define TKV_STATE_ENDED           5
#define TKV_STATE_FAILED          6

/* Frame kinds (flags, match VideoFrameKinds). */
#define TKV_FRAME_CPU_BGRA        1
#define TKV_FRAME_GPU             2

/* GPU handle types (match Avalonia KnownPlatformGraphicsExternalImageHandleTypes). */
#define TKV_GPU_NONE              0
#define TKV_GPU_D3D11_NT_HANDLE   1  /* HANDLE from IDXGIResource1::CreateSharedHandle; keyed mutex */
#define TKV_GPU_D3D11_GLOBAL      2  /* HANDLE from IDXGIResource::GetSharedHandle; keyed mutex */
#define TKV_GPU_IOSURFACE         3  /* IOSurfaceRef */

/* Semaphore types for tkv_player_timeline_sync (match Avalonia KnownPlatformGraphicsExternalSemaphoreHandleTypes). */
#define TKV_SEM_NONE               0
#define TKV_SEM_METAL_SHARED_EVENT 1  /* id<MTLSharedEvent> */

typedef void (*tkv_event_callback)(void* context, int32_t event, double a, double b);

/* Text track kinds (match VideoTextTrackKind). */
#define TKV_TEXT_SUBTITLES        0
#define TKV_TEXT_CAPTIONS         1

/* Text formats of a cue. */
#define TKV_TEXT_PLAIN            0  /* plain text */
#define TKV_TEXT_MARKUP           1  /* WebVTT cue text: <b>, <i>, <u> and the entities &amp; &lt; &gt; (Pango markup qualifies) */

/* A cue of the selected embedded text track: shown from start to end (seconds of media time). end < 0: until the next
   call for the same track, which also ends it (an empty text then only ends the shown cue). Same threading rules as the
   event callback. */
typedef void (*tkv_text_callback)(void* context, int32_t track, double start, double end, int32_t format, const char* text);

typedef struct tkv_player tkv_player;

typedef struct tkv_frame {
    int32_t kind;            /* TKV_FRAME_CPU_BGRA or TKV_FRAME_GPU */
    int32_t width;           /* before rotation */
    int32_t height;
    int32_t rotation;        /* clockwise degrees to apply when drawing: 0, 90, 180, 270 */
    double  time;            /* presentation time, seconds */
    /* CPU: BGRA8888, opaque. */
    const uint8_t* pixels;
    int32_t row_bytes;
    /* GPU. */
    int32_t gpu_handle_type; /* TKV_GPU_* */
    void*   gpu_handle;      /* HANDLE or IOSurfaceRef; owned by the player, valid until release */
    int64_t gpu_image_id;    /* stable per pooled texture, so the consumer imports each once */
    int32_t keyed_mutex;     /* 1 when acquire_key/release_key apply */
    uint32_t acquire_key;
    uint32_t release_key;
    int64_t token;           /* pass back to tkv_player_release_frame */
} tkv_frame;

typedef struct tkv_frame_request {
    int64_t host_time_ns;    /* when the frame will be shown, on the compositor clock (0 = now) */
    int32_t target_width;    /* scale down to fit when the backend can; 0 = natural size */
    int32_t target_height;
    int32_t accept;          /* TKV_FRAME_* flags */
    int32_t gpu_handle_type; /* the TKV_GPU_* the consumer imports, when accept has TKV_FRAME_GPU */
} tkv_frame_request;

/* The ABI version this library implements (TKV_ABI_VERSION). */
TKV_API int32_t tkv_version(void);

/* Initializes the platform framework once per process (MFStartup, gst_init after dlopen). TKV_E_UNAVAILABLE when missing. */
TKV_API int32_t tkv_initialize(void);

/* Bit set of TKV_FRAME_* the platform can produce, and the GPU handle type it produces (TKV_GPU_NONE if none). */
TKV_API int32_t tkv_frame_kinds(void);
TKV_API int32_t tkv_gpu_handle_type(void);

/* 0 = no, 1 = maybe, 2 = probably; media_type like "video/mp4; codecs=\"avc1.42E01E\"". */
TKV_API int32_t tkv_can_play(const char* media_type);

/* Creates a player. adapter_luid (8 bytes) or NULL: the GPU adapter the consumer composites on (Windows). */
TKV_API int32_t tkv_player_create(tkv_event_callback callback, void* context, const uint8_t* adapter_luid, tkv_player** player);
/* Stops, releases every frame and native object; no events after it returns. */
TKV_API void tkv_player_destroy(tkv_player* player);

/* Opens an http(s) or file URL. Asynchronous: TKV_EVENT_STATE(LOADING) now, then DURATION, SIZE and STATE(PAUSED),
   or TKV_EVENT_ERROR. Replaces what was loaded. */
TKV_API int32_t tkv_player_open(tkv_player* player, const char* url);

TKV_API int32_t tkv_player_play(tkv_player* player);
TKV_API int32_t tkv_player_pause(tkv_player* player);
/* accurate: 1 = exact time, 0 = nearest key frame. */
TKV_API int32_t tkv_player_seek(tkv_player* player, double seconds, int32_t accurate);
TKV_API double  tkv_player_position(tkv_player* player);
TKV_API int32_t tkv_player_set_volume(tkv_player* player, double volume);   /* 0..1, already muted-aware */
TKV_API int32_t tkv_player_set_rate(tkv_player* player, double rate);       /* 0.25..4; TKV_E_UNSUPPORTED if refused */
TKV_API int32_t tkv_player_set_loop(tkv_player* player, int32_t loop);

/* Copies up to capacity buffered ranges as start/end pairs into ranges (2 * capacity doubles); returns the count. */
TKV_API int32_t tkv_player_buffered(tkv_player* player, double* ranges, int32_t capacity);

/* TKV_OK and a frame, TKV_E_NO_FRAME when nothing new, or an error. Called on the UI thread. */
TKV_API int32_t tkv_player_acquire_frame(tkv_player* player, const tkv_frame_request* request, tkv_frame* frame);
TKV_API void    tkv_player_release_frame(tkv_player* player, int64_t token);

/* ---- ABI 2. Optional exports: a shim that has no use for them may leave them out; the managed side looks them up and
   treats a missing one as TKV_E_UNSUPPORTED (the macOS shim exports both). */

/* Timeline-semaphore sync for the GPU frame `token` (currently acquired, not yet released). Needed where the compositor
   imports the image only with timeline semaphores (Avalonia's Metal backend for IOSurfaceRef).
   On TKV_OK: *semaphore is owned by the player and stable per pooled image (one per gpu_image_id, created on the adapter
   given to tkv_player_create); the player has already signalled *wait_value (the image is complete); the consumer's GPU
   waits for *wait_value before reading and signals *signal_value when done. After tkv_player_release_frame the player
   keeps the image out of its pool until the semaphore reaches *signal_value (or about one second passes, for a frame the
   consumer dropped). Calling it twice for the same token returns the same values.
   TKV_E_UNSUPPORTED when the frame needs no semaphore; TKV_E_INVALID_ARG when token is not the frame currently out. */
TKV_API int32_t tkv_player_timeline_sync(tkv_player* player, int64_t token, void** semaphore, int32_t* semaphore_type,
                                         uint64_t* wait_value, uint64_t* signal_value);

/* Makes the next tkv_player_acquire_frame hand out the current frame again even when no new one was decoded (the consumer
   lost its copy: surface re-attached, moved to full screen, render path changed). One-shot; cleared by any delivered frame
   and by tkv_player_open. */
TKV_API int32_t tkv_player_redeliver_frame(tkv_player* player);

/* Embedded text tracks (optional exports): subtitles and captions stored in the media (MP4 tx3g, Matroska text).
   Nothing is delivered until a track is selected; the player then calls the text callback with that track's cues as the
   media reaches them (a backend may send them ahead of time). The tracks are known after TKV_EVENT_TEXT_TRACKS; opening
   other media clears them and the selection. */
TKV_API int32_t tkv_player_set_text_callback(tkv_player* player, tkv_text_callback callback, void* context);
/* The number of embedded text tracks (0 before TKV_EVENT_TEXT_TRACKS), or a negative TKV_E_*. */
TKV_API int32_t tkv_player_text_track_count(tkv_player* player);
/* Copies the track's language (BCP 47 or ISO 639, may be empty) and label (may be empty), NUL-terminated and cut to the
   buffer sizes, and its kind (TKV_TEXT_SUBTITLES or TKV_TEXT_CAPTIONS). */
TKV_API int32_t tkv_player_text_track_info(tkv_player* player, int32_t index, char* language, int32_t language_size,
                                           char* label, int32_t label_size, int32_t* kind);
/* Selects the track whose cues are delivered; -1 selects none. */
TKV_API int32_t tkv_player_select_text_track(tkv_player* player, int32_t index);

/* The last error message (UTF-8, owned by the player, valid until the next tkv_* call on it). */
TKV_API const char* tkv_player_last_error(tkv_player* player);

#ifdef __cplusplus
}
#endif

#endif
