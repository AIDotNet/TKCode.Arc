/* tkv_play_test.exe: a console harness for the Windows video shim (tkcode_video.dll), run by hand on Windows.
   Usage: tkv_play_test.exe <path to tests/Video/Media/red-blue-2s.mp4>
   The clip is 320x240, 30 fps, H.264 + AAC: 0-1 s solid red, 1-2 s solid blue. The harness loads the DLL next to it,
   plays the clip through the C ABI and checks pictures and events. Each check prints PASS or FAIL; the last line is
   "RESULT: PASS" or "RESULT: FAIL (n)". Exit code 0 on success. */

#define COBJMACROS
#define WIN32_LEAN_AND_MEAN
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0A00
#endif

#include <windows.h>
#include <initguid.h>
#include <objbase.h>
#include <d3d11_1.h>
#include <dxgi1_2.h>

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "../../tkcode_video.h"

typedef int32_t (*fn_version)(void);
typedef int32_t (*fn_initialize)(void);
typedef int32_t (*fn_frame_kinds)(void);
typedef int32_t (*fn_gpu_handle_type)(void);
typedef int32_t (*fn_can_play)(const char*);
typedef int32_t (*fn_create)(tkv_event_callback, void*, const uint8_t*, tkv_player**);
typedef void (*fn_destroy)(tkv_player*);
typedef int32_t (*fn_open)(tkv_player*, const char*);
typedef int32_t (*fn_player)(tkv_player*);
typedef int32_t (*fn_seek)(tkv_player*, double, int32_t);
typedef double (*fn_position)(tkv_player*);
typedef int32_t (*fn_set_double)(tkv_player*, double);
typedef int32_t (*fn_set_loop)(tkv_player*, int32_t);
typedef int32_t (*fn_buffered)(tkv_player*, double*, int32_t);
typedef int32_t (*fn_acquire)(tkv_player*, const tkv_frame_request*, tkv_frame*);
typedef void (*fn_release)(tkv_player*, int64_t);
typedef const char* (*fn_last_error)(tkv_player*);

static struct {
    fn_version version;
    fn_initialize initialize;
    fn_frame_kinds frame_kinds;
    fn_gpu_handle_type gpu_handle_type;
    fn_can_play can_play;
    fn_create create;
    fn_destroy destroy;
    fn_open open;
    fn_player play;
    fn_player pause;
    fn_seek seek;
    fn_position position;
    fn_set_double set_volume;
    fn_set_double set_rate;
    fn_set_loop set_loop;
    fn_buffered buffered;
    fn_acquire acquire;
    fn_release release;
    fn_last_error last_error;
} tkv;

/* ---------------------------------------------------------------------------------------------------------------- */

static LARGE_INTEGER g_start;
static LARGE_INTEGER g_freq;
static int g_failures;

static double now_ms(void)
{
    LARGE_INTEGER t;
    QueryPerformanceCounter(&t);
    return (double)(t.QuadPart - g_start.QuadPart) * 1000.0 / (double)g_freq.QuadPart;
}

static void check(int ok, const char* what, const char* detail)
{
    printf("[%8.1f ms] %s  %s%s%s\n", now_ms(), ok ? "PASS" : "FAIL", what, detail[0] ? "  -- " : "", detail);
    fflush(stdout);
    if (!ok) {
        g_failures++;
    }
}

/* ---------------------------------------------------------------------------------------------------------------- */
/* Events (arrive on Media Foundation threads).                                                                      */

typedef struct events {
    CRITICAL_SECTION lock;
    HANDLE paused;    /* manual reset: STATE(PAUSED) seen */
    HANDLE frame;     /* auto reset: FRAME */
    HANDLE ended;     /* manual reset: ENDED */
    HANDLE error;     /* manual reset: ERROR */
    double duration;
    double width;
    double height;
    double seekable;
    double last_position;
    int32_t last_state;
    int32_t error_kind;
    int count;
    int position_count;
    LONG volatile destroyed;
    LONG volatile late;
} events;

static const char* state_name(int s)
{
    static const char* names[] = { "IDLE", "LOADING", "PAUSED", "PLAYING", "BUFFERING", "ENDED", "FAILED" };
    return s >= 0 && s <= 6 ? names[s] : "?";
}

static void on_event(void* context, int32_t event, double a, double b)
{
    events* e = (events*)context;
    if (InterlockedCompareExchange(&e->destroyed, 0, 0) != 0) {
        InterlockedIncrement(&e->late);
        return;
    }
    EnterCriticalSection(&e->lock);
    e->count++;
    switch (event) {
    case TKV_EVENT_STATE:
        e->last_state = (int32_t)a;
        printf("[%8.1f ms]   event STATE %s\n", now_ms(), state_name((int)a));
        if ((int)a == TKV_STATE_PAUSED) {
            SetEvent(e->paused);
        }
        break;
    case TKV_EVENT_POSITION:
        e->last_position = a;
        e->position_count++;
        break;
    case TKV_EVENT_DURATION:
        e->duration = a;
        printf("[%8.1f ms]   event DURATION %.3f\n", now_ms(), a);
        break;
    case TKV_EVENT_SIZE:
        e->width = a;
        e->height = b;
        printf("[%8.1f ms]   event SIZE %.0fx%.0f\n", now_ms(), a, b);
        break;
    case TKV_EVENT_BUFFERED:
        break;
    case TKV_EVENT_FRAME:
        printf("[%8.1f ms]   event FRAME\n", now_ms());
        SetEvent(e->frame);
        break;
    case TKV_EVENT_ERROR:
        e->error_kind = (int32_t)a;
        printf("[%8.1f ms]   event ERROR kind %d\n", now_ms(), (int)a);
        SetEvent(e->error);
        break;
    case TKV_EVENT_ENDED:
        printf("[%8.1f ms]   event ENDED\n", now_ms());
        SetEvent(e->ended);
        break;
    case TKV_EVENT_SEEKABLE:
        e->seekable = a;
        printf("[%8.1f ms]   event SEEKABLE %.0f\n", now_ms(), a);
        break;
    default:
        printf("[%8.1f ms]   event %d (%.3f, %.3f)\n", now_ms(), (int)event, a, b);
        break;
    }
    fflush(stdout);
    LeaveCriticalSection(&e->lock);
}

/* Waits while pumping messages, as a UI thread would (the main thread is an STA). */
static int wait_for(HANDLE h, DWORD timeout_ms)
{
    ULONGLONG deadline = GetTickCount64() + timeout_ms;
    for (;;) {
        ULONGLONG now = GetTickCount64();
        if (now >= deadline) {
            return WaitForSingleObject(h, 0) == WAIT_OBJECT_0;
        }
        DWORD r = MsgWaitForMultipleObjects(1, &h, FALSE, (DWORD)(deadline - now), QS_ALLINPUT);
        if (r == WAIT_OBJECT_0) {
            return 1;
        }
        MSG msg;
        while (PeekMessageW(&msg, NULL, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
}

static void pump_sleep(DWORD ms)
{
    HANDLE never = CreateEventW(NULL, TRUE, FALSE, NULL);
    wait_for(never, ms);
    CloseHandle(never);
}

/* ---------------------------------------------------------------------------------------------------------------- */
/* Pixels.                                                                                                           */

typedef struct rgb {
    int r;
    int g;
    int b;
} rgb;

static int is_red(rgb c)
{
    return c.r > 180 && c.g < 80 && c.b < 80;
}

static int is_blue(rgb c)
{
    return c.b > 180 && c.r < 80 && c.g < 80;
}

/* Acquires a CPU frame within timeout_ms; returns 1 and the centre pixel. */
static int cpu_centre(tkv_player* p, int target_w, int target_h, DWORD timeout_ms, rgb* out, tkv_frame* info)
{
    tkv_frame_request req;
    memset(&req, 0, sizeof req);
    req.accept = TKV_FRAME_CPU_BGRA;
    req.target_width = target_w;
    req.target_height = target_h;
    ULONGLONG deadline = GetTickCount64() + timeout_ms;
    while (GetTickCount64() < deadline) {
        tkv_frame f;
        int32_t r = tkv.acquire(p, &req, &f);
        if (r == TKV_OK) {
            const uint8_t* px = f.pixels + (size_t)(f.height / 2) * (size_t)f.row_bytes + (size_t)(f.width / 2) * 4;
            out->b = px[0];
            out->g = px[1];
            out->r = px[2];
            *info = f;
            tkv.release(p, f.token);
            return 1;
        }
        if (r != TKV_E_NO_FRAME) {
            printf("  acquire returned %d: %s\n", (int)r, tkv.last_error(p));
            return 0;
        }
        pump_sleep(10);
    }
    return 0;
}

/* Imports a GPU frame the way a consumer would: open the NT handle on a second device, keyed mutex 1 -> 0. */
static int gpu_consume(ID3D11Device1* device, ID3D11DeviceContext* context, const tkv_frame* f, rgb* out)
{
    ID3D11Texture2D* shared = NULL;
    IDXGIKeyedMutex* mutex = NULL;
    ID3D11Texture2D* staging = NULL;
    int ok = 0;
    HRESULT hr = ID3D11Device1_OpenSharedResource1(device, (HANDLE)f->gpu_handle, &IID_ID3D11Texture2D, (void**)&shared);
    if (FAILED(hr)) {
        printf("  OpenSharedResource1 failed 0x%08lX\n", (unsigned long)hr);
        return 0;
    }
    hr = ID3D11Texture2D_QueryInterface(shared, &IID_IDXGIKeyedMutex, (void**)&mutex);
    if (SUCCEEDED(hr)) {
        hr = IDXGIKeyedMutex_AcquireSync(mutex, (UINT64)f->acquire_key, 1000);
        if (hr != S_OK) {
            printf("  AcquireSync(%u) returned 0x%08lX\n", f->acquire_key, (unsigned long)hr);
            hr = E_FAIL;
        }
    }
    if (SUCCEEDED(hr)) {
        D3D11_TEXTURE2D_DESC desc;
        ID3D11Texture2D_GetDesc(shared, &desc);
        desc.Usage = D3D11_USAGE_STAGING;
        desc.BindFlags = 0;
        desc.MiscFlags = 0;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        if (SUCCEEDED(ID3D11Device1_CreateTexture2D(device, &desc, NULL, &staging))) {
            ID3D11DeviceContext_CopyResource(context, (ID3D11Resource*)staging, (ID3D11Resource*)shared);
            D3D11_MAPPED_SUBRESOURCE m;
            if (SUCCEEDED(ID3D11DeviceContext_Map(context, (ID3D11Resource*)staging, 0, D3D11_MAP_READ, 0, &m))) {
                const uint8_t* px = (const uint8_t*)m.pData + (size_t)(desc.Height / 2) * m.RowPitch + (desc.Width / 2) * 4;
                out->b = px[0];
                out->g = px[1];
                out->r = px[2];
                ok = 1;
                ID3D11DeviceContext_Unmap(context, (ID3D11Resource*)staging, 0);
            }
            ID3D11Texture2D_Release(staging);
        }
        IDXGIKeyedMutex_ReleaseSync(mutex, (UINT64)f->release_key);
    }
    if (mutex != NULL) {
        IDXGIKeyedMutex_Release(mutex);
    }
    ID3D11Texture2D_Release(shared);
    return ok;
}

/* file:/// URL for a path, percent-encoding what is not plain ASCII. */
static void path_to_url(const char* arg, char* url, size_t capacity)
{
    WCHAR wide[MAX_PATH * 2];
    WCHAR full[MAX_PATH * 2];
    char utf8[MAX_PATH * 6];
    MultiByteToWideChar(CP_ACP, 0, arg, -1, wide, MAX_PATH * 2);
    GetFullPathNameW(wide, MAX_PATH * 2, full, NULL);
    WideCharToMultiByte(CP_UTF8, 0, full, -1, utf8, (int)sizeof utf8, NULL, NULL);
    size_t o = 0;
    o += (size_t)snprintf(url, capacity, "file:///");
    for (const unsigned char* s = (const unsigned char*)utf8; *s != 0 && o + 4 < capacity; s++) {
        unsigned char c = *s;
        if (c == '\\') {
            url[o++] = '/';
        } else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || strchr("-._~/:", c)) {
            url[o++] = (char)c;
        } else {
            o += (size_t)snprintf(url + o, capacity - o, "%%%02X", c);
        }
    }
    url[o] = 0;
}

#define LOAD(name, sym)                                                                                                \
    do {                                                                                                               \
        tkv.name = (void*)GetProcAddress(dll, sym);                                                                    \
        if (tkv.name == NULL) {                                                                                        \
            printf("FAIL  missing export %s\n", sym);                                                                  \
            return 2;                                                                                                  \
        }                                                                                                              \
    } while (0)

int main(int argc, char** argv)
{
    QueryPerformanceFrequency(&g_freq);
    QueryPerformanceCounter(&g_start);
    if (argc < 2) {
        printf("usage: tkv_play_test.exe <red-blue-2s.mp4>\n");
        return 2;
    }
    char detail[1024];

    /* Like Avalonia's UI thread: an STA before the library initializes COM (exercises RPC_E_CHANGED_MODE). */
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    WCHAR exe[MAX_PATH];
    GetModuleFileNameW(NULL, exe, MAX_PATH);
    WCHAR* slash = wcsrchr(exe, L'\\');
    if (slash != NULL) {
        slash[1] = 0;
    }
    wcscat(exe, L"tkcode_video.dll");
    HMODULE dll = LoadLibraryW(exe);
    if (dll == NULL) {
        printf("FAIL  could not load tkcode_video.dll next to the exe (error %lu)\n", GetLastError());
        return 2;
    }
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wpedantic"
    LOAD(version, "tkv_version");
    LOAD(initialize, "tkv_initialize");
    LOAD(frame_kinds, "tkv_frame_kinds");
    LOAD(gpu_handle_type, "tkv_gpu_handle_type");
    LOAD(can_play, "tkv_can_play");
    LOAD(create, "tkv_player_create");
    LOAD(destroy, "tkv_player_destroy");
    LOAD(open, "tkv_player_open");
    LOAD(play, "tkv_player_play");
    LOAD(pause, "tkv_player_pause");
    LOAD(seek, "tkv_player_seek");
    LOAD(position, "tkv_player_position");
    LOAD(set_volume, "tkv_player_set_volume");
    LOAD(set_rate, "tkv_player_set_rate");
    LOAD(set_loop, "tkv_player_set_loop");
    LOAD(buffered, "tkv_player_buffered");
    LOAD(acquire, "tkv_player_acquire_frame");
    LOAD(release, "tkv_player_release_frame");
    LOAD(last_error, "tkv_player_last_error");
#pragma GCC diagnostic pop
    check(1, "all 19 exports resolved", "");

    double t0 = now_ms();
    int32_t r = tkv.initialize();
    snprintf(detail, sizeof detail, "returned %d in %.1f ms", (int)r, now_ms() - t0);
    check(r == TKV_OK, "tkv_initialize", detail);
    if (r != TKV_OK) {
        printf("RESULT: FAIL (Media Foundation unavailable)\n");
        return 1;
    }
    snprintf(detail, sizeof detail, "version %d, frame kinds %d, gpu handle type %d", (int)tkv.version(),
             (int)tkv.frame_kinds(), (int)tkv.gpu_handle_type());
    check(tkv.version() == TKV_ABI_VERSION && tkv.frame_kinds() == 3 && tkv.gpu_handle_type() == TKV_GPU_D3D11_NT_HANDLE,
          "version and capabilities", detail);

    t0 = now_ms();
    int mp4 = tkv.can_play("video/mp4; codecs=\"avc1.42E01E, mp4a.40.2\"");
    int junk = tkv.can_play("video/x-tkcode-nonsense");
    snprintf(detail, sizeof detail, "mp4/h264 -> %d, nonsense -> %d (%.1f ms)", mp4, junk, now_ms() - t0);
    check(mp4 >= 1 && junk == 0, "tkv_can_play", detail);

    /* The consumer's adapter: adapter 0, as a compositor on the primary GPU would report it. */
    uint8_t luid[8];
    memset(luid, 0, sizeof luid);
    ID3D11Device* consumer = NULL;
    ID3D11Device1* consumer1 = NULL;
    ID3D11DeviceContext* consumer_ctx = NULL;
    IDXGIAdapter1* adapter = NULL;
    {
        IDXGIFactory1* factory = NULL;
        if (SUCCEEDED(CreateDXGIFactory1(&IID_IDXGIFactory1, (void**)&factory))) {
            if (IDXGIFactory1_EnumAdapters1(factory, 0, &adapter) == S_OK) {
                DXGI_ADAPTER_DESC1 desc;
                IDXGIAdapter1_GetDesc1(adapter, &desc);
                memcpy(luid, &desc.AdapterLuid, sizeof luid);
                printf("adapter 0: %ls\n", desc.Description);
            }
            IDXGIFactory1_Release(factory);
        }
        if (adapter != NULL &&
            SUCCEEDED(D3D11CreateDevice((IDXGIAdapter*)adapter, D3D_DRIVER_TYPE_UNKNOWN, NULL,
                                        D3D11_CREATE_DEVICE_BGRA_SUPPORT, NULL, 0, D3D11_SDK_VERSION, &consumer, NULL,
                                        &consumer_ctx))) {
            ID3D11Device_QueryInterface(consumer, &IID_ID3D11Device1, (void**)&consumer1);
        }
    }

    events ev;
    memset(&ev, 0, sizeof ev);
    InitializeCriticalSection(&ev.lock);
    ev.paused = CreateEventW(NULL, TRUE, FALSE, NULL);
    ev.frame = CreateEventW(NULL, FALSE, FALSE, NULL);
    ev.ended = CreateEventW(NULL, TRUE, FALSE, NULL);
    ev.error = CreateEventW(NULL, TRUE, FALSE, NULL);
    ev.last_state = -1;

    tkv_player* p = NULL;
    t0 = now_ms();
    r = tkv.create(on_event, &ev, adapter != NULL ? luid : NULL, &p);
    snprintf(detail, sizeof detail, "returned %d in %.1f ms", (int)r, now_ms() - t0);
    check(r == TKV_OK && p != NULL, "tkv_player_create (adapter 0 LUID)", detail);
    if (p == NULL) {
        printf("RESULT: FAIL (%d)\n", g_failures);
        return 1;
    }

    /* Open and wait for PAUSED. */
    char url[4096];
    path_to_url(argv[1], url, sizeof url);
    printf("url: %s\n", url);
    t0 = now_ms();
    r = tkv.open(p, url);
    check(r == TKV_OK, "tkv_player_open", r == TKV_OK ? "" : tkv.last_error(p));
    int paused = wait_for(ev.paused, 10000);
    snprintf(detail, sizeof detail, "%.1f ms after open%s%s", now_ms() - t0, paused ? "" : "; last error: ",
             paused ? "" : tkv.last_error(p));
    check(paused, "STATE(PAUSED) after open", detail);
    if (!paused) {
        tkv.destroy(p);
        printf("RESULT: FAIL (%d)\n", g_failures);
        return 1;
    }
    snprintf(detail, sizeof detail, "duration %.3f, size %.0fx%.0f, seekable %.0f", ev.duration, ev.width, ev.height,
             ev.seekable);
    check(fabs(ev.duration - 2.0) < 0.1 && ev.width == 320 && ev.height == 240 && ev.seekable == 1,
          "DURATION / SIZE / SEEKABLE", detail);

    /* First CPU frame: red. */
    rgb c;
    tkv_frame f;
    t0 = now_ms();
    int got = cpu_centre(p, 0, 0, 3000, &c, &f);
    snprintf(detail, sizeof detail, "%dx%d row %d t=%.3f centre RGB(%d,%d,%d) in %.1f ms", got ? f.width : 0,
             got ? f.height : 0, got ? f.row_bytes : 0, got ? f.time : 0, c.r, c.g, c.b, now_ms() - t0);
    check(got && f.width == 320 && f.height == 240 && is_red(c), "CPU frame at 0 s is red", got ? detail : "no frame");

    /* Scaled CPU frame (the paused frame is re-transferred only after a new tick, so seek to 0 to get a fresh one). */
    ResetEvent(ev.frame);
    tkv.seek(p, 0.25, 1);
    wait_for(ev.frame, 5000);
    got = cpu_centre(p, 160, 100, 3000, &c, &f);
    snprintf(detail, sizeof detail, "%dx%d (asked 160x100, aspect kept -> 133x100) centre RGB(%d,%d,%d)",
             got ? f.width : 0, got ? f.height : 0, c.r, c.g, c.b);
    check(got && f.width == 133 && f.height == 100 && is_red(c), "scaled CPU frame", got ? detail : "no frame");

    /* GPU frame through the keyed mutex protocol. */
    if (consumer1 != NULL) {
        ResetEvent(ev.frame);
        tkv.seek(p, 0.5, 1);
        wait_for(ev.frame, 5000);
        tkv_frame_request req;
        memset(&req, 0, sizeof req);
        req.accept = TKV_FRAME_GPU | TKV_FRAME_CPU_BGRA;
        req.gpu_handle_type = TKV_GPU_D3D11_NT_HANDLE;
        ULONGLONG deadline = GetTickCount64() + 3000;
        r = TKV_E_NO_FRAME;
        while (GetTickCount64() < deadline && (r = tkv.acquire(p, &req, &f)) == TKV_E_NO_FRAME) {
            pump_sleep(10);
        }
        if (r == TKV_OK && f.kind == TKV_FRAME_GPU) {
            int consumed = gpu_consume(consumer1, consumer_ctx, &f, &c);
            snprintf(detail, sizeof detail, "handle %p id %lld keys %u/%u centre RGB(%d,%d,%d)", f.gpu_handle,
                     (long long)f.gpu_image_id, f.acquire_key, f.release_key, c.r, c.g, c.b);
            tkv.release(p, f.token);
            check(consumed && f.keyed_mutex == 1 && f.acquire_key == 1 && f.release_key == 0 && is_red(c),
                  "GPU frame (NT handle, keyed mutex) is red", detail);
        } else {
            snprintf(detail, sizeof detail, "acquire %d kind %d: %s", (int)r, r == TKV_OK ? f.kind : 0,
                     tkv.last_error(p));
            check(0, "GPU frame (NT handle, keyed mutex) is red", detail);
        }
    } else {
        check(0, "GPU frame", "could not create the consumer D3D11.1 device");
    }

    /* Accurate seek to 1.5 s: blue. */
    ResetEvent(ev.frame);
    t0 = now_ms();
    r = tkv.seek(p, 1.5, 1);
    int framed = wait_for(ev.frame, 5000);
    double seek_ms = now_ms() - t0;
    got = framed && cpu_centre(p, 0, 0, 3000, &c, &f);
    snprintf(detail, sizeof detail, "seek %d, FRAME after %.1f ms, position %.3f, frame t=%.3f centre RGB(%d,%d,%d)",
             (int)r, seek_ms, tkv.position(p), got ? f.time : 0, c.r, c.g, c.b);
    check(got && is_blue(c) && fabs(tkv.position(p) - 1.5) < 0.05, "accurate seek to 1.5 s is blue", detail);

    /* Fast seek back to 0, then play to the end, pulling frames like the UI would at about 60 Hz. */
    ResetEvent(ev.frame);
    tkv.seek(p, 0, 0);
    wait_for(ev.frame, 5000);
    check(tkv.set_volume(p, 0.2) == TKV_OK, "set_volume 0.2", "");
    check(tkv.set_rate(p, 1.0) == TKV_OK, "set_rate 1.0", tkv.last_error(p));
    ResetEvent(ev.ended);
    t0 = now_ms();
    r = tkv.play(p);
    check(r == TKV_OK, "tkv_player_play", "");
    int frames = 0;
    int red_frames = 0;
    int blue_frames = 0;
    int positions_before = ev.position_count;
    tkv_frame_request req;
    memset(&req, 0, sizeof req);
    req.accept = TKV_FRAME_CPU_BGRA;
    while (WaitForSingleObject(ev.ended, 0) != WAIT_OBJECT_0 && now_ms() - t0 < 8000) {
        if (tkv.acquire(p, &req, &f) == TKV_OK) {
            const uint8_t* px = f.pixels + (size_t)(f.height / 2) * (size_t)f.row_bytes + (size_t)(f.width / 2) * 4;
            rgb k = { px[2], px[1], px[0] };
            frames++;
            red_frames += is_red(k);
            blue_frames += is_blue(k);
            tkv.release(p, f.token);
        }
        pump_sleep(15);
    }
    double play_ms = now_ms() - t0;
    int ended = WaitForSingleObject(ev.ended, 0) == WAIT_OBJECT_0;
    snprintf(detail, sizeof detail, "ENDED after %.1f ms; %d frames (%d red, %d blue); %d POSITION events; state %s",
             play_ms, frames, red_frames, blue_frames, ev.position_count - positions_before, state_name(ev.last_state));
    check(ended && frames >= 30 && red_frames > 0 && blue_frames > 0 && play_ms > 1700 && play_ms < 4000 &&
              ev.last_state == TKV_STATE_ENDED,
          "play to ENDED", detail);

    double ranges[16];
    int n = tkv.buffered(p, ranges, 8);
    snprintf(detail, sizeof detail, "%d range(s), first %.3f-%.3f", n, n > 0 ? ranges[0] : 0, n > 0 ? ranges[1] : 0);
    check(n >= 1, "tkv_player_buffered", detail);

    /* GPU frames while playing: consume each one like the compositor (acquire 1, release 0). */
    if (consumer1 != NULL) {
        ResetEvent(ev.frame);
        tkv.seek(p, 0, 1);
        wait_for(ev.frame, 5000);
        tkv.set_rate(p, 2.0);
        tkv.play(p);
        memset(&req, 0, sizeof req);
        req.accept = TKV_FRAME_GPU;
        req.gpu_handle_type = TKV_GPU_D3D11_NT_HANDLE;
        int gpu_frames = 0;
        int consumed = 0;
        int64_t ids[8];
        int id_count = 0;
        t0 = now_ms();
        while (now_ms() - t0 < 600) {
            if (tkv.acquire(p, &req, &f) == TKV_OK) {
                gpu_frames++;
                consumed += gpu_consume(consumer1, consumer_ctx, &f, &c);
                int seen = 0;
                for (int i = 0; i < id_count; i++) {
                    seen |= ids[i] == f.gpu_image_id;
                }
                if (!seen && id_count < 8) {
                    ids[id_count++] = f.gpu_image_id;
                }
                tkv.release(p, f.token);
            }
            pump_sleep(15);
        }
        tkv.pause(p);
        tkv.set_rate(p, 1.0);
        snprintf(detail, sizeof detail, "%d frames, %d consumed, %d distinct image ids (expect 3)", gpu_frames,
                 consumed, id_count);
        check(gpu_frames >= 10 && consumed == gpu_frames && id_count == 3, "GPU frames while playing at 2x", detail);
    }

    /* A missing file reports ERROR with a message. */
    ResetEvent(ev.error);
    t0 = now_ms();
    tkv.open(p, "file:///C:/does/not/exist/tkcode-missing.mp4");
    int errored = wait_for(ev.error, 10000);
    snprintf(detail, sizeof detail, "kind %d after %.1f ms: %s", ev.error_kind, now_ms() - t0, tkv.last_error(p));
    check(errored, "missing file reports ERROR", detail);

    /* Destroy: no events afterwards. */
    t0 = now_ms();
    tkv.destroy(p);
    InterlockedExchange(&ev.destroyed, 1);
    double destroy_ms = now_ms() - t0;
    pump_sleep(500);
    snprintf(detail, sizeof detail, "destroy took %.1f ms; %ld events after it", destroy_ms, ev.late);
    check(ev.late == 0, "no events after destroy", detail);

    if (consumer1 != NULL) {
        ID3D11Device1_Release(consumer1);
    }
    if (consumer_ctx != NULL) {
        ID3D11DeviceContext_Release(consumer_ctx);
    }
    if (consumer != NULL) {
        ID3D11Device_Release(consumer);
    }
    if (adapter != NULL) {
        IDXGIAdapter1_Release(adapter);
    }
    if (g_failures == 0) {
        printf("RESULT: PASS\n");
        return 0;
    }
    printf("RESULT: FAIL (%d)\n", g_failures);
    return 1;
}
