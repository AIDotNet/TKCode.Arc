/* TKCode.Arc.Video.Native for Windows: the C ABI of ../tkcode_video.h over Media Foundation's IMFMediaEngine in
   frame-server mode (no window). Design: design/components/video/video.md section 5.2 (Windows row) and 14; ADR 0036.

   Shape:
   - One D3D11 device per player (BGRA + VIDEO support, multithread-protected) on the adapter whose LUID the consumer
     composites on, shared with the engine through an IMFDXGIDeviceManager.
   - Engine events arrive on Media Foundation work-queue threads through a C IMFMediaEngineNotify (refcounted). The
     notify object counts callbacks in flight; tkv_player_destroy detaches it and waits for them, so no event is
     delivered after destroy returns.
   - Frames are pulled on the UI thread (tkv_player_acquire_frame): OnVideoStreamTick, then TransferVideoFrame into
     either a pool of three shared BGRA textures (NT handle + keyed mutex; producer 0 -> 1, consumer 1 -> 0) or a
     private BGRA texture that is copied to a staging texture and read back into a player-owned buffer.
   - mfplat.dll is loaded at run time, so the library still loads on N editions without the Media Feature Pack and
     tkv_initialize reports TKV_E_UNAVAILABLE there. GUIDs come from the headers through initguid.h (no uuid or mfuuid
     import library is needed). */

#define COBJMACROS
#define WIN32_LEAN_AND_MEAN
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0A00
#endif
#ifndef WINVER
#define WINVER 0x0A00
#endif

#include <windows.h>
#include <initguid.h>
#include <objbase.h>
#include <oleauto.h>
#include <d3d10.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfmediaengine.h>

#include <math.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "../tkcode_video.h"

/* ------------------------------------------------------------------------------------------------------------------ */
/* Media Foundation entry points, resolved from mfplat.dll at run time.                                                */

typedef HRESULT (WINAPI *pfn_MFStartup)(ULONG version, DWORD flags);
typedef HRESULT (WINAPI *pfn_MFCreateAttributes)(IMFAttributes** attributes, UINT32 initial_size);
typedef HRESULT (WINAPI *pfn_MFCreateDXGIDeviceManager)(UINT* reset_token, IMFDXGIDeviceManager** manager);

/* TKV_TRACE=1 writes what the engine does to stderr: the only view of a stalled pipeline on a machine out of reach. */
static int trace_enabled(void)
{
    static LONG volatile enabled = -1;
    if (enabled < 0) {
        InterlockedExchange(&enabled, GetEnvironmentVariableA("TKV_TRACE", NULL, 0) > 0 ? 1 : 0);
    }
    return enabled == 1;
}

#define TKV_TRACE(...)                      \
    do {                                    \
        if (trace_enabled()) {              \
            fprintf(stderr, "[tkv] ");      \
            fprintf(stderr, __VA_ARGS__);   \
            fprintf(stderr, "\n");          \
            fflush(stderr);                 \
        }                                   \
    } while (0)

static INIT_ONCE g_init_once = INIT_ONCE_STATIC_INIT;
static int32_t g_init_result = TKV_E_FAIL;
static HMODULE g_mfplat;
static pfn_MFCreateAttributes g_MFCreateAttributes;
static pfn_MFCreateDXGIDeviceManager g_MFCreateDXGIDeviceManager;

static SRWLOCK g_probe_lock = SRWLOCK_INIT;
static IMFMediaEngine* g_probe_engine; /* for tkv_can_play; kept for the process lifetime */

static LONG volatile g_pool_generation;

/* GetProcAddress returns FARPROC; going through a generic function pointer keeps -Wcast-function-type quiet. */
typedef void (*tkv_generic_fn)(void);
static tkv_generic_fn load_symbol(HMODULE module, const char* name)
{
    return (tkv_generic_fn)(void*)GetProcAddress(module, name);
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Player.                                                                                                              */

#define TKV_POOL_SIZE 3
#define TKV_ERROR_CAPACITY 512

typedef struct tkv_notify tkv_notify;

typedef struct tkv_slot {
    ID3D11Texture2D* texture;
    IDXGIKeyedMutex* mutex;
    HANDLE handle;
    int64_t image_id;
    int outstanding;      /* handed out and not yet released */
    ULONGLONG produced;   /* GetTickCount64 of the last transfer, for picking the oldest slot to reclaim */
} tkv_slot;

struct tkv_player {
    tkv_event_callback callback;
    void* context;

    ID3D11Device* device;
    ID3D11DeviceContext* immediate;
    ID3D10Multithread* multithread;
    IMFDXGIDeviceManager* manager;
    IMFMediaEngine* engine;
    IMFMediaEngineEx* engine_ex; /* NULL when the system has no IMFMediaEngineEx */
    tkv_notify* notify;

    CRITICAL_SECTION lock;   /* guards everything below that MF threads touch */
    int32_t state;           /* last TKV_STATE_* reported */
    int loaded;              /* PAUSED (or PLAYING) reported after the current load */
    ULONGLONG last_position_tick;
    char error[TKV_ERROR_CAPACITY];

    LONG volatile force_frame; /* transfer the current frame on the next acquire even without a new tick */

    /* UI thread only. */
    char error_out[TKV_ERROR_CAPACITY];
    int64_t next_token;
    int64_t out_token;       /* 0 = no frame out */
    int out_slot;            /* pool index of the GPU frame out, or -1 */

    tkv_slot pool[TKV_POOL_SIZE];
    int pool_width;
    int pool_height;
    int pool_next;
    int gpu_failed;          /* creating the shared pool failed once; GPU frames are not offered again */

    ID3D11Texture2D* cpu_texture;
    ID3D11Texture2D* cpu_staging;
    int cpu_width;
    int cpu_height;
    uint8_t* cpu_pixels;
    size_t cpu_capacity;
};

/* ------------------------------------------------------------------------------------------------------------------ */
/* Helpers.                                                                                                             */

static void copy_string(char* dst, size_t capacity, const char* src)
{
    size_t n = strlen(src);
    if (n >= capacity) {
        n = capacity - 1;
    }
    memcpy(dst, src, n);
    dst[n] = 0;
}

static void set_error(tkv_player* p, const char* format, ...)
{
    char text[TKV_ERROR_CAPACITY];
    va_list args;
    va_start(args, format);
    vsnprintf(text, sizeof text, format, args);
    va_end(args);
    EnterCriticalSection(&p->lock);
    copy_string(p->error, sizeof p->error, text);
    LeaveCriticalSection(&p->lock);
}

/* The system's text for an HRESULT, UTF-8, without the trailing line break. */
static void describe_hresult(HRESULT hr, char* out, size_t capacity)
{
    WCHAR* wide = NULL;
    DWORD flags = FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_IGNORE_INSERTS;
    DWORD n = FormatMessageW(flags | FORMAT_MESSAGE_FROM_SYSTEM, NULL, (DWORD)hr, 0, (LPWSTR)&wide, 0, NULL);
    if (n == 0 && g_mfplat != NULL) {
        n = FormatMessageW(flags | FORMAT_MESSAGE_FROM_HMODULE, g_mfplat, (DWORD)hr, 0, (LPWSTR)&wide, 0, NULL);
    }
    if (n == 0) {
        HMODULE mferror = LoadLibraryExW(L"mferror.dll", NULL, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (mferror != NULL) {
            n = FormatMessageW(flags | FORMAT_MESSAGE_FROM_HMODULE, mferror, (DWORD)hr, 0, (LPWSTR)&wide, 0, NULL);
            FreeLibrary(mferror);
        }
    }
    if (n == 0 || wide == NULL) {
        snprintf(out, capacity, "HRESULT 0x%08lX", (unsigned long)hr);
        return;
    }
    while (n > 0 && (wide[n - 1] == L'\r' || wide[n - 1] == L'\n' || wide[n - 1] == L' ' || wide[n - 1] == L'.')) {
        n--;
    }
    char text[TKV_ERROR_CAPACITY];
    int bytes = WideCharToMultiByte(CP_UTF8, 0, wide, (int)n, text, (int)sizeof text - 1, NULL, NULL);
    LocalFree(wide);
    if (bytes <= 0) {
        snprintf(out, capacity, "HRESULT 0x%08lX", (unsigned long)hr);
        return;
    }
    text[bytes] = 0;
    snprintf(out, capacity, "%s (HRESULT 0x%08lX)", text, (unsigned long)hr);
}

static void set_error_hr(tkv_player* p, const char* what, HRESULT hr)
{
    char text[TKV_ERROR_CAPACITY];
    describe_hresult(hr, text, sizeof text);
    set_error(p, "%s: %s", what, text);
}

/* UTF-8 to a newly allocated wide string (free with free). */
static WCHAR* utf8_to_wide(const char* s, int length)
{
    int n = MultiByteToWideChar(CP_UTF8, 0, s, length, NULL, 0);
    if (n <= 0) {
        return NULL;
    }
    WCHAR* w = (WCHAR*)calloc((size_t)n + 1, sizeof(WCHAR));
    if (w == NULL) {
        return NULL;
    }
    MultiByteToWideChar(CP_UTF8, 0, s, length, w, n);
    w[n] = 0;
    return w;
}

static int hex_value(char c)
{
    if (c >= '0' && c <= '9') {
        return c - '0';
    }
    if (c >= 'a' && c <= 'f') {
        return c - 'a' + 10;
    }
    if (c >= 'A' && c <= 'F') {
        return c - 'A' + 10;
    }
    return -1;
}

/* The Media Engine's source resolver opens local files by path; file: URLs (percent-encoded, possibly with a host for
   UNC shares) are turned into paths here. Other URLs (http, https, ms-appx, ...) pass through unchanged. */
static BSTR url_to_source(const char* url)
{
    if (_strnicmp(url, "file:", 5) != 0) {
        WCHAR* w = utf8_to_wide(url, -1);
        if (w == NULL) {
            return NULL;
        }
        BSTR b = SysAllocString(w);
        free(w);
        return b;
    }
    const char* s = url + 5;
    size_t len = strlen(url);
    char* path = (char*)calloc(len + 3, 1);
    if (path == NULL) {
        return NULL;
    }
    size_t o = 0;
    if (s[0] == '/' && s[1] == '/') {
        s += 2;
        if (_strnicmp(s, "localhost/", 10) == 0) {
            s += 9;
        }
        if (s[0] != '/') {
            /* file://server/share/x -> \\server\share\x */
            path[o++] = '\\';
            path[o++] = '\\';
        }
    }
    /* /C:/x or /C|/x -> C:/x */
    if (s[0] == '/' && s[1] != 0 && s[2] != 0 &&
        ((s[1] >= 'A' && s[1] <= 'Z') || (s[1] >= 'a' && s[1] <= 'z')) && (s[2] == ':' || s[2] == '|')) {
        s += 1;
    }
    for (; *s != 0 && *s != '?' && *s != '#'; s++) {
        char c = *s;
        if (c == '%' && hex_value(s[1]) >= 0 && hex_value(s[2]) >= 0) {
            c = (char)(hex_value(s[1]) * 16 + hex_value(s[2]));
            s += 2;
        } else if (c == '/') {
            c = '\\';
        }
        path[o++] = c;
    }
    path[o] = 0;
    if (o >= 2 && path[1] == '|') {
        path[1] = ':';
    }
    WCHAR* w = utf8_to_wide(path, -1);
    free(path);
    if (w == NULL) {
        return NULL;
    }
    BSTR b = SysAllocString(w);
    free(w);
    return b;
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* IMFMediaEngineNotify.                                                                                                */

struct tkv_notify {
    IMFMediaEngineNotify iface;   /* first, so the interface pointer is the object pointer */
    LONG volatile refs;
    CRITICAL_SECTION lock;
    CONDITION_VARIABLE idle;
    LONG inflight;
    int detached;
    tkv_player* player;           /* NULL for the can_play probe engine */
};

static void handle_event(tkv_player* p, DWORD event, DWORD_PTR param1, DWORD param2);

static HRESULT STDMETHODCALLTYPE notify_query_interface(IMFMediaEngineNotify* This, REFIID riid, void** out)
{
    if (out == NULL) {
        return E_POINTER;
    }
    if (IsEqualIID(riid, &IID_IUnknown) || IsEqualIID(riid, &IID_IMFMediaEngineNotify)) {
        *out = This;
        IMFMediaEngineNotify_AddRef(This);
        return S_OK;
    }
    *out = NULL;
    return E_NOINTERFACE;
}

static ULONG STDMETHODCALLTYPE notify_add_ref(IMFMediaEngineNotify* This)
{
    tkv_notify* n = (tkv_notify*)This;
    return (ULONG)InterlockedIncrement(&n->refs);
}

static ULONG STDMETHODCALLTYPE notify_release(IMFMediaEngineNotify* This)
{
    tkv_notify* n = (tkv_notify*)This;
    LONG refs = InterlockedDecrement(&n->refs);
    if (refs == 0) {
        DeleteCriticalSection(&n->lock);
        free(n);
    }
    return (ULONG)refs;
}

static HRESULT STDMETHODCALLTYPE notify_event(IMFMediaEngineNotify* This, DWORD event, DWORD_PTR param1, DWORD param2)
{
    tkv_notify* n = (tkv_notify*)This;
    tkv_player* p;
    EnterCriticalSection(&n->lock);
    if (n->detached || n->player == NULL) {
        LeaveCriticalSection(&n->lock);
        return S_OK;
    }
    n->inflight++;
    p = n->player;
    LeaveCriticalSection(&n->lock);

    handle_event(p, event, param1, param2);

    EnterCriticalSection(&n->lock);
    n->inflight--;
    if (n->inflight == 0) {
        WakeAllConditionVariable(&n->idle);
    }
    LeaveCriticalSection(&n->lock);
    return S_OK;
}

static IMFMediaEngineNotifyVtbl g_notify_vtbl = {
    notify_query_interface,
    notify_add_ref,
    notify_release,
    notify_event,
};

static tkv_notify* notify_create(tkv_player* player)
{
    tkv_notify* n = (tkv_notify*)calloc(1, sizeof *n);
    if (n == NULL) {
        return NULL;
    }
    n->iface.lpVtbl = &g_notify_vtbl;
    n->refs = 1;
    InitializeCriticalSection(&n->lock);
    InitializeConditionVariable(&n->idle);
    n->player = player;
    return n;
}

/* Stops delivery and waits until no callback is running. Must not be called from inside a callback. */
static void notify_detach(tkv_notify* n)
{
    EnterCriticalSection(&n->lock);
    n->detached = 1;
    while (n->inflight > 0) {
        SleepConditionVariableCS(&n->idle, &n->lock, INFINITE);
    }
    n->player = NULL;
    LeaveCriticalSection(&n->lock);
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Events.                                                                                                              */

static void emit(tkv_player* p, int32_t event, double a, double b)
{
    if (p->callback != NULL) {
        p->callback(p->context, event, a, b);
    }
}

/* Reports a state once per change. force reports it even when unchanged (open's LOADING). */
static void emit_state(tkv_player* p, int32_t state, int force)
{
    int changed;
    EnterCriticalSection(&p->lock);
    changed = force || p->state != state;
    p->state = state;
    LeaveCriticalSection(&p->lock);
    if (changed) {
        emit(p, TKV_EVENT_STATE, (double)state, 0);
    }
}

static int32_t current_state(tkv_player* p)
{
    int32_t s;
    EnterCriticalSection(&p->lock);
    s = p->state;
    LeaveCriticalSection(&p->lock);
    return s;
}

static double finite_or_zero(double v)
{
    if (isnan(v) || isinf(v) || v < 0) {
        return 0;
    }
    return v;
}

static void emit_position(tkv_player* p)
{
    double t = finite_or_zero(IMFMediaEngine_GetCurrentTime(p->engine));
    EnterCriticalSection(&p->lock);
    p->last_position_tick = GetTickCount64();
    LeaveCriticalSection(&p->lock);
    emit(p, TKV_EVENT_POSITION, t, 0);
}

static void emit_size(tkv_player* p)
{
    DWORD cx = 0;
    DWORD cy = 0;
    if (SUCCEEDED(IMFMediaEngine_GetNativeVideoSize(p->engine, &cx, &cy)) && cx > 0 && cy > 0) {
        /* Media Foundation applies MF_MT_VIDEO_ROTATION itself when it converts to the output format, so the native
           size is the displayed size and frames carry rotation 0 (unverified on rotated phone clips). */
        emit(p, TKV_EVENT_SIZE, (double)cx, (double)cy);
    }
}

static void emit_seekable(tkv_player* p)
{
    int seekable = 0;
    DWORD flags = 0;
    if (p->engine_ex != NULL && SUCCEEDED(IMFMediaEngineEx_GetResourceCharacteristics(p->engine_ex, &flags))) {
        seekable = (flags & MFMEDIASOURCE_CAN_SEEK) != 0;
    } else {
        double d = IMFMediaEngine_GetDuration(p->engine);
        seekable = !isnan(d) && !isinf(d) && d > 0;
    }
    emit(p, TKV_EVENT_SEEKABLE, seekable ? 1 : 0, 0);
}

static int32_t map_error_kind(DWORD code, HRESULT hr)
{
    switch (code) {
    case MF_MEDIA_ENGINE_ERR_ABORTED:
        return TKV_ERR_ABORTED;
    case MF_MEDIA_ENGINE_ERR_NETWORK:
        return TKV_ERR_NETWORK;
    case MF_MEDIA_ENGINE_ERR_DECODE:
        return TKV_ERR_DECODE;
    case MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED:
        /* The engine reports a missing file or an unreachable host as "source not supported" too. */
        if (hr == HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND) || hr == HRESULT_FROM_WIN32(ERROR_PATH_NOT_FOUND) ||
            HRESULT_FACILITY(hr) == 12 /* FACILITY_INTERNET (WinINet / WinHTTP) */ ||
            ((ULONG)hr & 0xFFFFFF00u) == 0xC00D4200u /* MF_E_NET_* (0xC00D4268...) */) {
            return TKV_ERR_NETWORK;
        }
        return TKV_ERR_UNSUPPORTED;
    case MF_MEDIA_ENGINE_ERR_ENCRYPTED:
        return TKV_ERR_UNSUPPORTED;
    default:
        return TKV_ERR_DECODE;
    }
}

static const char* error_name(DWORD code)
{
    switch (code) {
    case MF_MEDIA_ENGINE_ERR_ABORTED:
        return "aborted";
    case MF_MEDIA_ENGINE_ERR_NETWORK:
        return "network error";
    case MF_MEDIA_ENGINE_ERR_DECODE:
        return "decode error";
    case MF_MEDIA_ENGINE_ERR_SRC_NOT_SUPPORTED:
        return "source not supported";
    case MF_MEDIA_ENGINE_ERR_ENCRYPTED:
        return "encrypted media";
    default:
        return "error";
    }
}

/* After the current load, the first CANPLAY / LOADEDDATA / FIRSTFRAMEREADY settles the state: PAUSED plus a FRAME,
   unless play() was already called (then PLAYING arrives on its own). */
static void announce_loaded(tkv_player* p)
{
    int first;
    EnterCriticalSection(&p->lock);
    first = !p->loaded;
    p->loaded = 1;
    LeaveCriticalSection(&p->lock);
    if (!first) {
        return;
    }
    InterlockedExchange(&p->force_frame, 1);
    if (IMFMediaEngine_IsPaused(p->engine)) {
        emit_state(p, TKV_STATE_PAUSED, 0);
        emit(p, TKV_EVENT_FRAME, 0, 0);
    }
}

static void handle_event(tkv_player* p, DWORD event, DWORD_PTR param1, DWORD param2)
{
    TKV_TRACE("event %lu param1=%llu param2=0x%08lX time=%.3f paused=%d", (unsigned long)event,
              (unsigned long long)param1, (unsigned long)param2, IMFMediaEngine_GetCurrentTime(p->engine),
              (int)IMFMediaEngine_IsPaused(p->engine));
    switch (event) {
    case MF_MEDIA_ENGINE_EVENT_LOADSTART:
        emit_state(p, TKV_STATE_LOADING, 0);
        break;

    case MF_MEDIA_ENGINE_EVENT_LOADEDMETADATA:
        emit(p, TKV_EVENT_DURATION, finite_or_zero(IMFMediaEngine_GetDuration(p->engine)), 0);
        emit_size(p);
        emit_seekable(p);
        break;

    case MF_MEDIA_ENGINE_EVENT_LOADEDDATA:
    case MF_MEDIA_ENGINE_EVENT_CANPLAY:
    case MF_MEDIA_ENGINE_EVENT_CANPLAYTHROUGH:
    case MF_MEDIA_ENGINE_EVENT_FIRSTFRAMEREADY:
        announce_loaded(p);
        if (event == MF_MEDIA_ENGINE_EVENT_FIRSTFRAMEREADY) {
            InterlockedExchange(&p->force_frame, 1);
            if (IMFMediaEngine_IsPaused(p->engine)) {
                emit(p, TKV_EVENT_FRAME, 0, 0);
            }
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_PLAYING:
        EnterCriticalSection(&p->lock);
        p->loaded = 1;
        LeaveCriticalSection(&p->lock);
        emit_state(p, TKV_STATE_PLAYING, 0);
        break;

    case MF_MEDIA_ENGINE_EVENT_PAUSE:
        /* HTML semantics fire "pause" right before "ended"; keep ENDED as the final state. Before the load settles,
           announce_loaded reports PAUSED. */
        if (!IMFMediaEngine_IsEnded(p->engine)) {
            int loaded;
            EnterCriticalSection(&p->lock);
            loaded = p->loaded;
            LeaveCriticalSection(&p->lock);
            if (loaded) {
                emit_state(p, TKV_STATE_PAUSED, 0);
                emit_position(p);
            }
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_WAITING:
    case MF_MEDIA_ENGINE_EVENT_BUFFERINGSTARTED:
        if (!IMFMediaEngine_IsPaused(p->engine)) {
            emit_state(p, TKV_STATE_BUFFERING, 0);
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_BUFFERINGENDED:
        emit(p, TKV_EVENT_BUFFERED, 0, 0);
        if (current_state(p) == TKV_STATE_BUFFERING) {
            emit_state(p, IMFMediaEngine_IsPaused(p->engine) ? TKV_STATE_PAUSED : TKV_STATE_PLAYING, 0);
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_PROGRESS:
    case MF_MEDIA_ENGINE_EVENT_DOWNLOADCOMPLETE:
        emit(p, TKV_EVENT_BUFFERED, 0, 0);
        break;

    case MF_MEDIA_ENGINE_EVENT_TIMEUPDATE: {
        /* The engine fires this more often than the 4 Hz the ABI promises while playing. */
        ULONGLONG now = GetTickCount64();
        ULONGLONG last;
        EnterCriticalSection(&p->lock);
        last = p->last_position_tick;
        LeaveCriticalSection(&p->lock);
        if (now - last >= 200 || IMFMediaEngine_IsPaused(p->engine)) {
            emit_position(p);
        }
        break;
    }

    case MF_MEDIA_ENGINE_EVENT_DURATIONCHANGE:
        emit(p, TKV_EVENT_DURATION, finite_or_zero(IMFMediaEngine_GetDuration(p->engine)), 0);
        break;

    case MF_MEDIA_ENGINE_EVENT_FORMATCHANGE:
        emit_size(p);
        break;

    case MF_MEDIA_ENGINE_EVENT_SEEKED:
        emit_position(p);
        if (IMFMediaEngine_IsPaused(p->engine)) {
            InterlockedExchange(&p->force_frame, 1);
            int32_t s = current_state(p);
            if (s == TKV_STATE_ENDED || s == TKV_STATE_BUFFERING) {
                emit_state(p, TKV_STATE_PAUSED, 0);
            }
            emit(p, TKV_EVENT_FRAME, 0, 0);
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_ENDED:
        if (!IMFMediaEngine_GetLoop(p->engine)) {
            emit_position(p);
            emit_state(p, TKV_STATE_ENDED, 0);
            emit(p, TKV_EVENT_ENDED, 0, 0);
        }
        break;

    case MF_MEDIA_ENGINE_EVENT_ERROR: {
        DWORD code = (DWORD)param1;
        HRESULT hr = (HRESULT)param2;
        if (code == MF_MEDIA_ENGINE_ERR_NOERROR) {
            break;
        }
        char text[TKV_ERROR_CAPACITY];
        if (hr == S_OK) {
            /* Older systems leave param2 empty; ask the engine. */
            IMFMediaError* error = NULL;
            if (SUCCEEDED(IMFMediaEngine_GetError(p->engine, &error)) && error != NULL) {
                hr = IMFMediaError_GetExtendedErrorCode(error);
                IMFMediaError_Release(error);
            }
        }
        if (hr != S_OK) {
            describe_hresult(hr, text, sizeof text);
            set_error(p, "Media Foundation %s: %s", error_name(code), text);
        } else {
            set_error(p, "Media Foundation %s", error_name(code));
        }
        emit(p, TKV_EVENT_ERROR, (double)map_error_kind(code, hr), 0);
        emit_state(p, TKV_STATE_FAILED, 0);
        break;
    }

    default:
        break;
    }
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Initialization.                                                                                                      */

static HRESULT create_engine(IMFMediaEngineNotify* notify, IMFDXGIDeviceManager* manager, IMFMediaEngine** engine)
{
    IMFMediaEngineClassFactory* factory = NULL;
    IMFAttributes* attributes = NULL;
    HRESULT hr = CoCreateInstance(&CLSID_MFMediaEngineClassFactory, NULL, CLSCTX_INPROC_SERVER,
                                  &IID_IMFMediaEngineClassFactory, (void**)&factory);
    if (SUCCEEDED(hr)) {
        hr = g_MFCreateAttributes(&attributes, 3);
    }
    if (SUCCEEDED(hr)) {
        hr = IMFAttributes_SetUnknown(attributes, &MF_MEDIA_ENGINE_CALLBACK, (IUnknown*)notify);
    }
    if (SUCCEEDED(hr)) {
        hr = IMFAttributes_SetUINT32(attributes, &MF_MEDIA_ENGINE_VIDEO_OUTPUT_FORMAT, DXGI_FORMAT_B8G8R8A8_UNORM);
    }
    if (SUCCEEDED(hr) && manager != NULL) {
        hr = IMFAttributes_SetUnknown(attributes, &MF_MEDIA_ENGINE_DXGI_MANAGER, (IUnknown*)manager);
    }
    if (SUCCEEDED(hr)) {
        hr = IMFMediaEngineClassFactory_CreateInstance(factory, 0, attributes, engine);
    }
    if (attributes != NULL) {
        IMFAttributes_Release(attributes);
    }
    if (factory != NULL) {
        IMFMediaEngineClassFactory_Release(factory);
    }
    return hr;
}

static BOOL CALLBACK initialize_once(PINIT_ONCE once, PVOID parameter, PVOID* context)
{
    (void)once;
    (void)parameter;
    (void)context;

    /* The caller may be an STA UI thread (RPC_E_CHANGED_MODE) or already initialized (S_FALSE); both are fine. The
       reference is kept for the process lifetime. */
    HRESULT hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE) {
        g_init_result = TKV_E_FAIL;
        return TRUE;
    }
    /* Keep a process-wide MTA alive, so later tkv_* calls from threads that never initialized COM (thread-pool
       threads, when the first call came from an STA) run in the implicit MTA instead of failing CoCreateInstance. */
    CO_MTA_USAGE_COOKIE mta_cookie;
    CoIncrementMTAUsage(&mta_cookie);

    g_mfplat = LoadLibraryExW(L"mfplat.dll", NULL, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (g_mfplat == NULL) {
        g_init_result = TKV_E_UNAVAILABLE;
        return TRUE;
    }
    pfn_MFStartup startup = (pfn_MFStartup)load_symbol(g_mfplat, "MFStartup");
    g_MFCreateAttributes = (pfn_MFCreateAttributes)load_symbol(g_mfplat, "MFCreateAttributes");
    g_MFCreateDXGIDeviceManager = (pfn_MFCreateDXGIDeviceManager)load_symbol(g_mfplat, "MFCreateDXGIDeviceManager");
    if (startup == NULL || g_MFCreateAttributes == NULL || g_MFCreateDXGIDeviceManager == NULL) {
        g_init_result = TKV_E_UNAVAILABLE;
        return TRUE;
    }
    if (FAILED(startup(MF_VERSION, MFSTARTUP_FULL))) {
        g_init_result = TKV_E_UNAVAILABLE;
        return TRUE;
    }

    /* The engine's class factory is missing on N editions even when mfplat is present in part. */
    IMFMediaEngineClassFactory* factory = NULL;
    hr = CoCreateInstance(&CLSID_MFMediaEngineClassFactory, NULL, CLSCTX_INPROC_SERVER,
                          &IID_IMFMediaEngineClassFactory, (void**)&factory);
    if (FAILED(hr)) {
        g_init_result = TKV_E_UNAVAILABLE;
        return TRUE;
    }
    IMFMediaEngineClassFactory_Release(factory);
    /* MFShutdown is never called: the framework stays up for the process lifetime. */
    g_init_result = TKV_OK;
    return TRUE;
}

TKV_API int32_t tkv_version(void)
{
    return TKV_ABI_VERSION;
}

TKV_API int32_t tkv_initialize(void)
{
    InitOnceExecuteOnce(&g_init_once, initialize_once, NULL, NULL);
    return g_init_result;
}

TKV_API int32_t tkv_frame_kinds(void)
{
    return TKV_FRAME_CPU_BGRA | TKV_FRAME_GPU;
}

TKV_API int32_t tkv_gpu_handle_type(void)
{
    return TKV_GPU_D3D11_NT_HANDLE;
}

TKV_API int32_t tkv_can_play(const char* media_type)
{
    if (media_type == NULL || tkv_initialize() != TKV_OK) {
        return 0;
    }
    AcquireSRWLockExclusive(&g_probe_lock);
    if (g_probe_engine == NULL) {
        /* A software frame-server engine that never loads anything; it only answers CanPlayType. */
        tkv_notify* n = notify_create(NULL);
        if (n != NULL) {
            if (FAILED(create_engine(&n->iface, NULL, &g_probe_engine))) {
                g_probe_engine = NULL;
            }
            IMFMediaEngineNotify_Release(&n->iface);
        }
    }
    int32_t result = 0;
    if (g_probe_engine != NULL) {
        WCHAR* w = utf8_to_wide(media_type, -1);
        BSTR type = w != NULL ? SysAllocString(w) : NULL;
        free(w);
        MF_MEDIA_ENGINE_CANPLAY answer = MF_MEDIA_ENGINE_CANPLAY_NOT_SUPPORTED;
        if (type != NULL && SUCCEEDED(IMFMediaEngine_CanPlayType(g_probe_engine, type, &answer))) {
            result = answer == MF_MEDIA_ENGINE_CANPLAY_PROBABLY ? 2 : answer == MF_MEDIA_ENGINE_CANPLAY_MAYBE ? 1 : 0;
        }
        SysFreeString(type);
    }
    ReleaseSRWLockExclusive(&g_probe_lock);
    return result;
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Player lifetime.                                                                                                     */

static IDXGIAdapter1* find_adapter(const uint8_t* luid)
{
    if (luid == NULL) {
        return NULL;
    }
    LUID wanted;
    memcpy(&wanted, luid, sizeof wanted);
    IDXGIFactory1* factory = NULL;
    if (FAILED(CreateDXGIFactory1(&IID_IDXGIFactory1, (void**)&factory))) {
        return NULL;
    }
    IDXGIAdapter1* found = NULL;
    for (UINT i = 0;; i++) {
        IDXGIAdapter1* adapter = NULL;
        if (IDXGIFactory1_EnumAdapters1(factory, i, &adapter) != S_OK) {
            break;
        }
        DXGI_ADAPTER_DESC1 desc;
        if (SUCCEEDED(IDXGIAdapter1_GetDesc1(adapter, &desc)) && desc.AdapterLuid.LowPart == wanted.LowPart &&
            desc.AdapterLuid.HighPart == wanted.HighPart) {
            found = adapter;
            break;
        }
        IDXGIAdapter1_Release(adapter);
    }
    IDXGIFactory1_Release(factory);
    return found;
}

static HRESULT create_device(IDXGIAdapter1* adapter, ID3D11Device** device, ID3D11DeviceContext** context)
{
    static const D3D_FEATURE_LEVEL levels[] = {
        D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0,
        D3D_FEATURE_LEVEL_9_3,
    };
    const UINT count = (UINT)(sizeof levels / sizeof levels[0]);
    const UINT flags[] = {
        D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
    };
    HRESULT hr = E_FAIL;
    for (int i = 0; i < 2; i++) {
        D3D_DRIVER_TYPE type = adapter != NULL ? D3D_DRIVER_TYPE_UNKNOWN : D3D_DRIVER_TYPE_HARDWARE;
        hr = D3D11CreateDevice((IDXGIAdapter*)adapter, type, NULL, flags[i], levels, count, D3D11_SDK_VERSION, device,
                               NULL, context);
        if (hr == E_INVALIDARG) {
            /* Systems without D3D 11.1 reject D3D_FEATURE_LEVEL_11_1 in the list. */
            hr = D3D11CreateDevice((IDXGIAdapter*)adapter, type, NULL, flags[i], levels + 1, count - 1,
                                   D3D11_SDK_VERSION, device, NULL, context);
        }
        if (SUCCEEDED(hr)) {
            return hr;
        }
    }
    /* No hardware device (a VM or a basic display driver): WARP still lets the CPU path work. */
    hr = D3D11CreateDevice(NULL, D3D_DRIVER_TYPE_WARP, NULL, D3D11_CREATE_DEVICE_BGRA_SUPPORT, levels + 1, count - 1,
                           D3D11_SDK_VERSION, device, NULL, context);
    return hr;
}

static void release_pool(tkv_player* p)
{
    for (int i = 0; i < TKV_POOL_SIZE; i++) {
        tkv_slot* s = &p->pool[i];
        if (s->handle != NULL) {
            CloseHandle(s->handle);
        }
        if (s->mutex != NULL) {
            IDXGIKeyedMutex_Release(s->mutex);
        }
        if (s->texture != NULL) {
            ID3D11Texture2D_Release(s->texture);
        }
        memset(s, 0, sizeof *s);
    }
    p->pool_width = 0;
    p->pool_height = 0;
    p->pool_next = 0;
    if (p->out_slot >= 0) {
        p->out_slot = -1;
        p->out_token = 0;
    }
}

static void release_cpu(tkv_player* p)
{
    if (p->cpu_texture != NULL) {
        ID3D11Texture2D_Release(p->cpu_texture);
        p->cpu_texture = NULL;
    }
    if (p->cpu_staging != NULL) {
        ID3D11Texture2D_Release(p->cpu_staging);
        p->cpu_staging = NULL;
    }
    p->cpu_width = 0;
    p->cpu_height = 0;
}

TKV_API int32_t tkv_player_create(tkv_event_callback callback, void* context, const uint8_t* adapter_luid,
                                  tkv_player** player)
{
    if (player == NULL) {
        return TKV_E_INVALID_ARG;
    }
    *player = NULL;
    int32_t init = tkv_initialize();
    if (init != TKV_OK) {
        return init;
    }
    tkv_player* p = (tkv_player*)calloc(1, sizeof *p);
    if (p == NULL) {
        return TKV_E_FAIL;
    }
    p->callback = callback;
    p->context = context;
    p->state = TKV_STATE_IDLE;
    p->out_slot = -1;
    p->next_token = 1;
    InitializeCriticalSection(&p->lock);

    IDXGIAdapter1* adapter = find_adapter(adapter_luid);
    HRESULT hr = create_device(adapter, &p->device, &p->immediate);
    if (adapter != NULL) {
        IDXGIAdapter1_Release(adapter);
    }
    if (SUCCEEDED(hr) && trace_enabled()) {
        ID3D11VideoDevice* video = NULL;
        IDXGIDevice* dxgi = NULL;
        IDXGIAdapter* dxgi_adapter = NULL;
        DXGI_ADAPTER_DESC desc;
        memset(&desc, 0, sizeof desc);
        HRESULT video_hr = ID3D11Device_QueryInterface(p->device, &IID_ID3D11VideoDevice, (void**)&video);
        if (SUCCEEDED(ID3D11Device_QueryInterface(p->device, &IID_IDXGIDevice, (void**)&dxgi))) {
            if (SUCCEEDED(IDXGIDevice_GetAdapter(dxgi, &dxgi_adapter))) {
                IDXGIAdapter_GetDesc(dxgi_adapter, &desc);
                IDXGIAdapter_Release(dxgi_adapter);
            }
            IDXGIDevice_Release(dxgi);
        }
        TKV_TRACE("device adapter='%ls' feature_level=0x%x video_device=0x%08lX", desc.Description,
                  (unsigned)ID3D11Device_GetFeatureLevel(p->device), (unsigned long)video_hr);
        if (video != NULL) {
            ID3D11VideoDevice_Release(video);
        }
    }
    if (SUCCEEDED(hr)) {
        hr = ID3D11Device_QueryInterface(p->device, &IID_ID3D10Multithread, (void**)&p->multithread);
    }
    if (SUCCEEDED(hr)) {
        /* The engine decodes and converts on its own threads with this device. */
        ID3D10Multithread_SetMultithreadProtected(p->multithread, TRUE);
        UINT token = 0;
        hr = g_MFCreateDXGIDeviceManager(&token, &p->manager);
        if (SUCCEEDED(hr)) {
            hr = IMFDXGIDeviceManager_ResetDevice(p->manager, (IUnknown*)p->device, token);
        }
    }
    if (SUCCEEDED(hr)) {
        p->notify = notify_create(p);
        hr = p->notify != NULL ? S_OK : E_OUTOFMEMORY;
    }
    if (SUCCEEDED(hr)) {
        hr = create_engine(&p->notify->iface, p->manager, &p->engine);
    }
    if (FAILED(hr)) {
        /* No player is left to carry a message; the managed side reports TKV_E_FAIL. */
        tkv_player_destroy(p);
        return TKV_E_FAIL;
    }
    if (FAILED(IMFMediaEngine_QueryInterface(p->engine, &IID_IMFMediaEngineEx, (void**)&p->engine_ex))) {
        p->engine_ex = NULL;
    }
    IMFMediaEngine_SetPreload(p->engine, MF_MEDIA_ENGINE_PRELOAD_AUTOMATIC);
    IMFMediaEngine_SetAutoPlay(p->engine, FALSE);
    *player = p;
    return TKV_OK;
}

TKV_API void tkv_player_destroy(tkv_player* p)
{
    if (p == NULL) {
        return;
    }
    /* 1. No more events: detach the callback and wait for those in flight. */
    if (p->notify != NULL) {
        notify_detach(p->notify);
    }
    /* 2. Stop the engine. It may keep its reference to the (now detached) notify object a little longer. */
    if (p->engine_ex != NULL) {
        IMFMediaEngineEx_Release(p->engine_ex);
        p->engine_ex = NULL;
    }
    if (p->engine != NULL) {
        IMFMediaEngine_Shutdown(p->engine);
        IMFMediaEngine_Release(p->engine);
        p->engine = NULL;
    }
    /* 3. Frames and D3D objects. */
    release_pool(p);
    release_cpu(p);
    free(p->cpu_pixels);
    if (p->manager != NULL) {
        IMFDXGIDeviceManager_Release(p->manager);
    }
    if (p->multithread != NULL) {
        ID3D10Multithread_Release(p->multithread);
    }
    if (p->immediate != NULL) {
        ID3D11DeviceContext_ClearState(p->immediate);
        ID3D11DeviceContext_Flush(p->immediate);
        ID3D11DeviceContext_Release(p->immediate);
    }
    if (p->device != NULL) {
        ID3D11Device_Release(p->device);
    }
    if (p->notify != NULL) {
        IMFMediaEngineNotify_Release(&p->notify->iface);
    }
    DeleteCriticalSection(&p->lock);
    free(p);
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Transport.                                                                                                           */

TKV_API int32_t tkv_player_open(tkv_player* p, const char* url)
{
    if (p == NULL || url == NULL || url[0] == 0) {
        return TKV_E_INVALID_ARG;
    }
    BSTR source = url_to_source(url);
    if (source == NULL) {
        set_error(p, "Could not convert the URL to UTF-16");
        return TKV_E_INVALID_ARG;
    }
    EnterCriticalSection(&p->lock);
    p->loaded = 0;
    p->last_position_tick = 0;
    /* p->error is kept: an ERROR of the previous media may still be queued on the .NET side, which reads the text when it
       handles the event. Every failure sets its own text before it is reported, so a stale one is never shown. */
    LeaveCriticalSection(&p->lock);
    InterlockedExchange(&p->force_frame, 0);
    /* A frame still out stays valid until its release; the pool is rebuilt on the next acquire if the size changes. */

    emit_state(p, TKV_STATE_LOADING, 1);
    /* Setting the source runs the HTML load algorithm; IMFMediaEngine::Load is only for re-reading source elements
       ("calling this method is generally not required", MSDN), and a second load would abort the first. */
    HRESULT hr = IMFMediaEngine_SetSource(p->engine, source);
    SysFreeString(source);
    if (FAILED(hr)) {
        set_error_hr(p, "Opening the source failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_play(tkv_player* p)
{
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    HRESULT hr = IMFMediaEngine_Play(p->engine);
    TKV_TRACE("play hr=0x%08lX", (unsigned long)hr);
    if (FAILED(hr)) {
        set_error_hr(p, "Play failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_pause(tkv_player* p)
{
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    HRESULT hr = IMFMediaEngine_Pause(p->engine);
    if (FAILED(hr)) {
        set_error_hr(p, "Pause failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_seek(tkv_player* p, double seconds, int32_t accurate)
{
    if (p == NULL || isnan(seconds) || isinf(seconds)) {
        return TKV_E_INVALID_ARG;
    }
    if (seconds < 0) {
        seconds = 0;
    }
    HRESULT hr;
    if (!accurate && p->engine_ex != NULL) {
        hr = IMFMediaEngineEx_SetCurrentTimeEx(p->engine_ex, seconds, MF_MEDIA_ENGINE_SEEK_MODE_APPROXIMATE);
    } else {
        hr = IMFMediaEngine_SetCurrentTime(p->engine, seconds);
    }
    if (FAILED(hr)) {
        set_error_hr(p, "Seek failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API double tkv_player_position(tkv_player* p)
{
    if (p == NULL) {
        return 0;
    }
    return finite_or_zero(IMFMediaEngine_GetCurrentTime(p->engine));
}

TKV_API int32_t tkv_player_set_volume(tkv_player* p, double volume)
{
    if (p == NULL || isnan(volume)) {
        return TKV_E_INVALID_ARG;
    }
    volume = volume < 0 ? 0 : volume > 1 ? 1 : volume;
    HRESULT hr = IMFMediaEngine_SetVolume(p->engine, volume);
    if (FAILED(hr)) {
        set_error_hr(p, "Setting the volume failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_set_rate(tkv_player* p, double rate)
{
    if (p == NULL || isnan(rate) || rate <= 0) {
        return TKV_E_INVALID_ARG;
    }
    if (p->engine_ex != NULL && !IMFMediaEngineEx_IsPlaybackRateSupported(p->engine_ex, rate)) {
        set_error(p, "Playback rate %.2f is not supported", rate);
        return TKV_E_UNSUPPORTED;
    }
    /* The default rate is what the engine returns to after a load; the playback rate applies now. */
    HRESULT hr = IMFMediaEngine_SetDefaultPlaybackRate(p->engine, rate);
    if (SUCCEEDED(hr)) {
        hr = IMFMediaEngine_SetPlaybackRate(p->engine, rate);
    }
    if (FAILED(hr)) {
        set_error_hr(p, "Setting the playback rate failed", hr);
        return TKV_E_UNSUPPORTED;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_set_loop(tkv_player* p, int32_t loop)
{
    if (p == NULL) {
        return TKV_E_INVALID_ARG;
    }
    HRESULT hr = IMFMediaEngine_SetLoop(p->engine, loop ? TRUE : FALSE);
    if (FAILED(hr)) {
        set_error_hr(p, "Setting loop failed", hr);
        return TKV_E_FAIL;
    }
    return TKV_OK;
}

TKV_API int32_t tkv_player_buffered(tkv_player* p, double* ranges, int32_t capacity)
{
    if (p == NULL || capacity < 0 || (ranges == NULL && capacity > 0)) {
        return TKV_E_INVALID_ARG;
    }
    IMFMediaTimeRange* buffered = NULL;
    HRESULT hr = IMFMediaEngine_GetBuffered(p->engine, &buffered);
    if (FAILED(hr) || buffered == NULL) {
        return 0;
    }
    DWORD length = IMFMediaTimeRange_GetLength(buffered);
    int32_t count = 0;
    for (DWORD i = 0; i < length && count < capacity; i++) {
        double start = 0;
        double end = 0;
        if (SUCCEEDED(IMFMediaTimeRange_GetStart(buffered, i, &start)) &&
            SUCCEEDED(IMFMediaTimeRange_GetEnd(buffered, i, &end))) {
            ranges[count * 2] = finite_or_zero(start);
            ranges[count * 2 + 1] = finite_or_zero(end);
            count++;
        }
    }
    IMFMediaTimeRange_Release(buffered);
    return count;
}

/* ABI 2. Windows GPU frames use a keyed mutex, never a timeline semaphore. */
TKV_API int32_t tkv_player_timeline_sync(tkv_player* p, int64_t token, void** semaphore, int32_t* semaphore_type,
                                         uint64_t* wait_value, uint64_t* signal_value)
{
    (void)p;
    (void)token;
    if (semaphore) {
        *semaphore = NULL;
    }
    if (semaphore_type) {
        *semaphore_type = TKV_SEM_NONE;
    }
    if (wait_value) {
        *wait_value = 0;
    }
    if (signal_value) {
        *signal_value = 0;
    }
    return TKV_E_UNSUPPORTED;
}

/* ABI 2: the next acquire transfers the current frame even without a new video tick (the same path as after a seek). */
TKV_API int32_t tkv_player_redeliver_frame(tkv_player* p)
{
    if (!p) {
        return TKV_E_INVALID_ARG;
    }
    InterlockedExchange(&p->force_frame, 1);
    return TKV_OK;
}

/* Embedded text tracks: not read yet on Windows (IMFTimedText needs checking on a real machine first), so the player
   reports none and accepts only "no track". */
TKV_API int32_t tkv_player_set_text_callback(tkv_player* p, tkv_text_callback callback, void* context)
{
    (void)callback;
    (void)context;
    return p ? TKV_OK : TKV_E_INVALID_ARG;
}

TKV_API int32_t tkv_player_text_track_count(tkv_player* p)
{
    return p ? 0 : TKV_E_INVALID_ARG;
}

TKV_API int32_t tkv_player_text_track_info(tkv_player* p, int32_t index, char* language, int32_t language_size,
                                           char* label, int32_t label_size, int32_t* kind)
{
    (void)p;
    (void)index;
    (void)language;
    (void)language_size;
    (void)label;
    (void)label_size;
    (void)kind;
    return TKV_E_INVALID_ARG;
}

TKV_API int32_t tkv_player_select_text_track(tkv_player* p, int32_t index)
{
    return p && index == -1 ? TKV_OK : TKV_E_INVALID_ARG;
}

TKV_API const char* tkv_player_last_error(tkv_player* p)
{
    if (p == NULL) {
        return "";
    }
    EnterCriticalSection(&p->lock);
    copy_string(p->error_out, sizeof p->error_out, p->error);
    LeaveCriticalSection(&p->lock);
    return p->error_out;
}

/* ------------------------------------------------------------------------------------------------------------------ */
/* Frames.                                                                                                              */

static HRESULT create_pool(tkv_player* p, int width, int height)
{
    release_pool(p);
    LONG generation = InterlockedIncrement(&g_pool_generation);
    D3D11_TEXTURE2D_DESC desc;
    memset(&desc, 0, sizeof desc);
    desc.Width = (UINT)width;
    desc.Height = (UINT)height;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
    desc.MiscFlags = D3D11_RESOURCE_MISC_SHARED_NTHANDLE | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;
    HRESULT hr = S_OK;
    for (int i = 0; i < TKV_POOL_SIZE && SUCCEEDED(hr); i++) {
        tkv_slot* s = &p->pool[i];
        IDXGIResource1* resource = NULL;
        hr = ID3D11Device_CreateTexture2D(p->device, &desc, NULL, &s->texture);
        if (SUCCEEDED(hr)) {
            hr = ID3D11Texture2D_QueryInterface(s->texture, &IID_IDXGIKeyedMutex, (void**)&s->mutex);
        }
        if (SUCCEEDED(hr)) {
            hr = ID3D11Texture2D_QueryInterface(s->texture, &IID_IDXGIResource1, (void**)&resource);
        }
        if (SUCCEEDED(hr)) {
            hr = IDXGIResource1_CreateSharedHandle(resource, NULL, DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE,
                                                   NULL, &s->handle);
        }
        if (resource != NULL) {
            IDXGIResource1_Release(resource);
        }
        s->image_id = ((int64_t)generation << 2) | i;
    }
    if (FAILED(hr)) {
        release_pool(p);
        return hr;
    }
    p->pool_width = width;
    p->pool_height = height;
    return S_OK;
}

/* A slot whose keyed mutex the producer now holds (key 0, or key 1 reclaimed from a frame nobody consumed), or -1. */
static int take_slot(tkv_player* p)
{
    for (int i = 0; i < TKV_POOL_SIZE; i++) {
        int index = (p->pool_next + i) % TKV_POOL_SIZE;
        tkv_slot* s = &p->pool[index];
        if (s->outstanding) {
            continue;
        }
        /* WAIT_TIMEOUT comes back as a success code, so only S_OK means acquired. */
        if (IDXGIKeyedMutex_AcquireSync(s->mutex, 0, 0) == S_OK) {
            return index;
        }
    }
    /* Every free slot still says key 1: the consumer is behind or dropped frames. Take the oldest one back; we
       release it to key 1 again, so a compositor that is about to acquire it simply shows the newer frame. */
    int oldest = -1;
    for (int i = 0; i < TKV_POOL_SIZE; i++) {
        tkv_slot* s = &p->pool[i];
        if (!s->outstanding && (oldest < 0 || s->produced < p->pool[oldest].produced)) {
            oldest = i;
        }
    }
    if (oldest >= 0 && IDXGIKeyedMutex_AcquireSync(p->pool[oldest].mutex, 1, 0) == S_OK) {
        return oldest;
    }
    return -1;
}

static int32_t acquire_gpu(tkv_player* p, int width, int height, double time, tkv_frame* frame)
{
    if (p->pool_width != width || p->pool_height != height || p->pool[0].texture == NULL) {
        HRESULT hr = create_pool(p, width, height);
        if (FAILED(hr)) {
            p->gpu_failed = 1;
            set_error_hr(p, "Creating shared video textures failed", hr);
            return TKV_E_UNSUPPORTED;
        }
    }
    int index = take_slot(p);
    if (index < 0) {
        return TKV_E_NO_FRAME;
    }
    tkv_slot* s = &p->pool[index];
    RECT dst = { 0, 0, width, height };
    MFARGB border = { 0, 0, 0, 255 };
    HRESULT hr = IMFMediaEngine_TransferVideoFrame(p->engine, (IUnknown*)s->texture, NULL, &dst, &border);
    IDXGIKeyedMutex_ReleaseSync(s->mutex, 1);
    if (FAILED(hr)) {
        set_error_hr(p, "TransferVideoFrame failed", hr);
        return TKV_E_NO_FRAME;
    }
    s->produced = GetTickCount64();
    s->outstanding = 1;
    p->pool_next = (index + 1) % TKV_POOL_SIZE;
    p->out_slot = index;
    p->out_token = p->next_token++;

    memset(frame, 0, sizeof *frame);
    frame->kind = TKV_FRAME_GPU;
    frame->width = width;
    frame->height = height;
    frame->rotation = 0;
    frame->time = time;
    frame->gpu_handle_type = TKV_GPU_D3D11_NT_HANDLE;
    frame->gpu_handle = s->handle;
    frame->gpu_image_id = s->image_id;
    frame->keyed_mutex = 1;
    frame->acquire_key = 1;
    frame->release_key = 0;
    frame->token = p->out_token;
    return TKV_OK;
}

static void fit_size(int width, int height, int target_width, int target_height, int* out_width, int* out_height)
{
    double scale = 1;
    if (target_width > 0 && target_width < width) {
        scale = (double)target_width / width;
    }
    if (target_height > 0 && target_height < height) {
        double s = (double)target_height / height;
        if (s < scale) {
            scale = s;
        }
    }
    int w = (int)(width * scale + 0.5);
    int h = (int)(height * scale + 0.5);
    *out_width = w < 1 ? 1 : w;
    *out_height = h < 1 ? 1 : h;
}

static int32_t acquire_cpu(tkv_player* p, int width, int height, const tkv_frame_request* request, double time,
                           tkv_frame* frame)
{
    int w;
    int h;
    fit_size(width, height, request->target_width, request->target_height, &w, &h);
    if (p->cpu_width != w || p->cpu_height != h || p->cpu_texture == NULL) {
        release_cpu(p);
        D3D11_TEXTURE2D_DESC desc;
        memset(&desc, 0, sizeof desc);
        desc.Width = (UINT)w;
        desc.Height = (UINT)h;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
        HRESULT hr = ID3D11Device_CreateTexture2D(p->device, &desc, NULL, &p->cpu_texture);
        if (SUCCEEDED(hr)) {
            desc.Usage = D3D11_USAGE_STAGING;
            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            hr = ID3D11Device_CreateTexture2D(p->device, &desc, NULL, &p->cpu_staging);
        }
        if (FAILED(hr)) {
            release_cpu(p);
            set_error_hr(p, "Creating the frame textures failed", hr);
            return TKV_E_FAIL;
        }
        p->cpu_width = w;
        p->cpu_height = h;
    }
    size_t row_bytes = (size_t)w * 4;
    size_t needed = row_bytes * (size_t)h;
    if (p->cpu_capacity < needed) {
        uint8_t* pixels = (uint8_t*)realloc(p->cpu_pixels, needed);
        if (pixels == NULL) {
            set_error(p, "Out of memory for a %dx%d frame", w, h);
            return TKV_E_FAIL;
        }
        p->cpu_pixels = pixels;
        p->cpu_capacity = needed;
    }

    /* The engine scales to the destination rectangle and keeps the picture's aspect inside it; fit_size kept the
       aspect, so nothing is letterboxed here. */
    RECT dst = { 0, 0, w, h };
    MFARGB border = { 0, 0, 0, 255 };
    HRESULT hr = IMFMediaEngine_TransferVideoFrame(p->engine, (IUnknown*)p->cpu_texture, NULL, &dst, &border);
    if (FAILED(hr)) {
        set_error_hr(p, "TransferVideoFrame failed", hr);
        return TKV_E_NO_FRAME;
    }
    D3D11_MAPPED_SUBRESOURCE mapped;
    ID3D10Multithread_Enter(p->multithread);
    ID3D11DeviceContext_CopyResource(p->immediate, (ID3D11Resource*)p->cpu_staging, (ID3D11Resource*)p->cpu_texture);
    hr = ID3D11DeviceContext_Map(p->immediate, (ID3D11Resource*)p->cpu_staging, 0, D3D11_MAP_READ, 0, &mapped);
    if (SUCCEEDED(hr)) {
        const uint8_t* src = (const uint8_t*)mapped.pData;
        for (int y = 0; y < h; y++) {
            memcpy(p->cpu_pixels + (size_t)y * row_bytes, src + (size_t)y * mapped.RowPitch, row_bytes);
        }
        ID3D11DeviceContext_Unmap(p->immediate, (ID3D11Resource*)p->cpu_staging, 0);
    }
    ID3D10Multithread_Leave(p->multithread);
    if (FAILED(hr)) {
        set_error_hr(p, "Reading the frame back failed", hr);
        return TKV_E_FAIL;
    }
    /* BGRA from the engine carries alpha 255 for video; the ABI promises opaque pixels either way. */
    p->out_slot = -1;
    p->out_token = p->next_token++;

    memset(frame, 0, sizeof *frame);
    frame->kind = TKV_FRAME_CPU_BGRA;
    frame->width = w;
    frame->height = h;
    frame->rotation = 0;
    frame->time = time;
    frame->pixels = p->cpu_pixels;
    frame->row_bytes = (int32_t)row_bytes;
    frame->gpu_handle_type = TKV_GPU_NONE;
    frame->token = p->out_token;
    return TKV_OK;
}

TKV_API int32_t tkv_player_acquire_frame(tkv_player* p, const tkv_frame_request* request, tkv_frame* frame)
{
    if (p == NULL || request == NULL || frame == NULL) {
        return TKV_E_INVALID_ARG;
    }
    int want_gpu = (request->accept & TKV_FRAME_GPU) != 0 && request->gpu_handle_type == TKV_GPU_D3D11_NT_HANDLE &&
                   !p->gpu_failed;
    int want_cpu = (request->accept & TKV_FRAME_CPU_BGRA) != 0;
    if (!want_gpu && !want_cpu) {
        return (request->accept & TKV_FRAME_GPU) != 0 ? TKV_E_UNSUPPORTED : TKV_E_INVALID_ARG;
    }
    /* At most one frame is out; a caller that did not release the previous one gives it up now. */
    if (p->out_token != 0) {
        tkv_player_release_frame(p, p->out_token);
    }

    LONGLONG pts = -1;
    int force = InterlockedExchange(&p->force_frame, 0) != 0;
    HRESULT hr = IMFMediaEngine_OnVideoStreamTick(p->engine, &pts);
    TKV_TRACE("tick hr=0x%08lX pts=%lld force=%d", (unsigned long)hr, (long long)pts, force);
    if (hr != S_OK) {
        if (!force || FAILED(hr)) {
            if (force) {
                InterlockedExchange(&p->force_frame, 1);
            }
            return TKV_E_NO_FRAME;
        }
        /* S_FALSE while paused: no newer frame, but the one on screen (after load or seek) has not been shown. */
        pts = -1;
    }
    DWORD cx = 0;
    DWORD cy = 0;
    if (FAILED(IMFMediaEngine_GetNativeVideoSize(p->engine, &cx, &cy)) || cx == 0 || cy == 0) {
        if (force) {
            InterlockedExchange(&p->force_frame, 1);
        }
        return TKV_E_NO_FRAME;
    }
    double time = pts >= 0 ? (double)pts / 1e7 : finite_or_zero(IMFMediaEngine_GetCurrentTime(p->engine));

    int32_t result = TKV_E_NO_FRAME;
    if (want_gpu) {
        result = acquire_gpu(p, (int)cx, (int)cy, time, frame);
        if (result == TKV_E_UNSUPPORTED && want_cpu) {
            result = acquire_cpu(p, (int)cx, (int)cy, request, time, frame);
        }
    } else {
        result = acquire_cpu(p, (int)cx, (int)cy, request, time, frame);
    }
    if (result == TKV_E_NO_FRAME) {
        /* The tick was consumed but nothing was handed out: transfer the current frame next time. */
        InterlockedExchange(&p->force_frame, 1);
    }
    return result;
}

TKV_API void tkv_player_release_frame(tkv_player* p, int64_t token)
{
    if (p == NULL || token == 0 || token != p->out_token) {
        return;
    }
    if (p->out_slot >= 0 && p->out_slot < TKV_POOL_SIZE) {
        /* The consumer gives the keyed mutex back (key 0) on its own; take_slot checks that before reuse. */
        p->pool[p->out_slot].outstanding = 0;
    }
    p->out_slot = -1;
    p->out_token = 0;
}
