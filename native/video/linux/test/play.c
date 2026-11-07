/* Harness for the GStreamer shim (native/video/linux/tkcode_video_gst.c), run by tools/video/test-linux.sh in Docker.
   Usage:
     play <clip.mp4>          full run against tests/Video/Media/red-blue-2s.mp4 (0-1 s red, 1-2 s blue, 320x240)
     play --unavailable       expects TKV_E_UNAVAILABLE (container without GStreamer)
   TKV_NOPULL=1 does not pull frames while playing (a consumer that does not draw; EOS must still arrive).
   TKV_SLOW=<factor> scales every timeout and skips the wall-clock checks (valgrind). Exit code 0 = all checks passed. */
#define _GNU_SOURCE
#include "../../tkcode_video.h"

#include <limits.h>
#include <math.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

static int s_failures;
static double s_slow = 1;

#define CHECK(cond, ...) do { \
    if (cond) { printf("  ok   "); } else { printf("  FAIL "); s_failures++; } \
    printf(__VA_ARGS__); printf("\n"); fflush(stdout); \
} while (0)

typedef struct log_state {
    pthread_mutex_t lock;
    int state;
    int states[64];
    int n_states;
    double duration;
    int width;
    int height;
    int seekable;
    int frames;
    int errors;
    int error_kind;
    int ended;
    int positions;
    int buffered;
    double last_position;
    int position_went_back;
} log_state;

static const char* state_name(int s) {
    static const char* names[] = { "IDLE", "LOADING", "PAUSED", "PLAYING", "BUFFERING", "ENDED", "FAILED" };
    return s >= 0 && s <= 6 ? names[s] : "?";
}

static void on_event(void* context, int32_t event, double a, double b) {
    log_state* l = (log_state*)context;
    pthread_mutex_lock(&l->lock);
    switch (event) {
        case TKV_EVENT_STATE:
            l->state = (int)a;
            if (l->n_states < 64) {
                l->states[l->n_states++] = (int)a;
            }
            printf("    event STATE %s\n", state_name((int)a));
            break;
        case TKV_EVENT_POSITION:
            if (a + 0.3 < l->last_position) {
                l->position_went_back = 1;
            }
            l->last_position = a;
            l->positions++;
            break;
        case TKV_EVENT_DURATION:
            l->duration = a;
            printf("    event DURATION %.3f\n", a);
            break;
        case TKV_EVENT_SIZE:
            l->width = (int)a;
            l->height = (int)b;
            printf("    event SIZE %dx%d\n", (int)a, (int)b);
            break;
        case TKV_EVENT_BUFFERED:
            l->buffered++;
            break;
        case TKV_EVENT_FRAME:
            l->frames++;
            break;
        case TKV_EVENT_ERROR:
            l->errors++;
            l->error_kind = (int)a;
            printf("    event ERROR kind %d\n", (int)a);
            break;
        case TKV_EVENT_ENDED:
            l->ended++;
            printf("    event ENDED\n");
            break;
        case TKV_EVENT_SEEKABLE:
            l->seekable = (int)a;
            break;
        default:
            break;
    }
    fflush(stdout);
    pthread_mutex_unlock(&l->lock);
}

static double now(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (double)ts.tv_sec + (double)ts.tv_nsec / 1e9;
}

static void nap_ms(int ms) {
    struct timespec ts = { ms / 1000, (long)(ms % 1000) * 1000000L };
    nanosleep(&ts, NULL);
}

/* Waits until pred(l) holds; while waiting it pulls frames like a UI thread would (when pull != 0). */
typedef int (*pred_fn)(log_state*, void*);
static int s_pulled;

static void pull_once(tkv_player* p) {
    if (getenv("TKV_NOPULL") != NULL) {
        return; /* like a consumer that does not draw while playing (the managed tests) */
    }
    tkv_frame_request req;
    memset(&req, 0, sizeof(req));
    req.accept = TKV_FRAME_CPU_BGRA;
    tkv_frame f;
    if (tkv_player_acquire_frame(p, &req, &f) == TKV_OK) {
        volatile uint8_t sink = f.pixels[(size_t)(f.height / 2) * (size_t)f.row_bytes + (size_t)(f.width / 2) * 4];
        (void)sink;
        s_pulled++;
        tkv_player_release_frame(p, f.token);
    }
}

static int wait_for(log_state* l, tkv_player* p, int pull, double seconds, pred_fn pred, void* arg) {
    double deadline = now() + seconds * s_slow;
    for (;;) {
        pthread_mutex_lock(&l->lock);
        int ok = pred(l, arg);
        pthread_mutex_unlock(&l->lock);
        if (ok) {
            return 1;
        }
        if (now() > deadline) {
            return 0;
        }
        if (pull) {
            pull_once(p);
        }
        nap_ms(16);
    }
}

static int is_state(log_state* l, void* arg) { return l->state == *(int*)arg; }
static int frames_at_least(log_state* l, void* arg) { return l->frames >= *(int*)arg; }
static int ended_at_least(log_state* l, void* arg) { return l->ended >= *(int*)arg; }
static int errors_at_least(log_state* l, void* arg) { return l->errors >= *(int*)arg; }

static int wait_state(log_state* l, tkv_player* p, int state, double seconds, int pull) {
    return wait_for(l, p, pull, seconds, is_state, &state);
}

static int frames_now(log_state* l) {
    pthread_mutex_lock(&l->lock);
    int n = l->frames;
    pthread_mutex_unlock(&l->lock);
    return n;
}

/* Acquires a frame and returns its centre pixel (B, G, R) in bgr; time in *t. */
static int centre_pixel(tkv_player* p, int bgr[3], double* t, tkv_frame* out) {
    tkv_frame_request req;
    memset(&req, 0, sizeof(req));
    req.accept = TKV_FRAME_CPU_BGRA;
    tkv_frame f;
    int r = tkv_player_acquire_frame(p, &req, &f);
    if (r != TKV_OK) {
        return r;
    }
    const uint8_t* px = f.pixels + (size_t)(f.height / 2) * (size_t)f.row_bytes + (size_t)(f.width / 2) * 4;
    bgr[0] = px[0];
    bgr[1] = px[1];
    bgr[2] = px[2];
    *t = f.time;
    if (out != NULL) {
        *out = f;
    }
    /* A second acquire while one is out must fail without breaking the first. */
    tkv_frame g;
    int second = tkv_player_acquire_frame(p, &req, &g);
    if (second == TKV_OK) {
        tkv_player_release_frame(p, g.token);
        printf("  FAIL second acquire while a frame is out returned OK\n");
        s_failures++;
    }
    tkv_player_release_frame(p, f.token);
    return TKV_OK;
}

static int is_red(const int bgr[3]) { return bgr[2] > 200 && bgr[1] < 50 && bgr[0] < 50; }
static int is_blue(const int bgr[3]) { return bgr[0] > 200 && bgr[1] < 50 && bgr[2] < 50; }

static int unavailable_mode(void) {
    printf("unavailable mode\n");
    int r = tkv_initialize();
    CHECK(r == TKV_E_UNAVAILABLE, "tkv_initialize = %d (want TKV_E_UNAVAILABLE %d)", r, TKV_E_UNAVAILABLE);
    int c = tkv_can_play("video/mp4; codecs=\"avc1.42E01E\"");
    CHECK(c == 0, "tkv_can_play = %d (want 0)", c);
    tkv_player* p = (tkv_player*)0x1;
    r = tkv_player_create(NULL, NULL, NULL, &p);
    CHECK(r == TKV_E_UNAVAILABLE && p == NULL, "tkv_player_create = %d, player %p", r, (void*)p);
    CHECK(tkv_frame_kinds() == TKV_FRAME_CPU_BGRA && tkv_gpu_handle_type() == TKV_GPU_NONE, "frame kinds CPU, gpu NONE");
    printf("%s (%d failures)\n", s_failures == 0 ? "PASS" : "FAIL", s_failures);
    return s_failures == 0 ? 0 : 1;
}

int main(int argc, char** argv) {
    setvbuf(stdout, NULL, _IOLBF, 0);
    const char* slow = getenv("TKV_SLOW");
    if (slow != NULL && atof(slow) > 1) {
        s_slow = atof(slow);
    }
    int timing = s_slow <= 1;
    CHECK(tkv_version() == TKV_ABI_VERSION, "tkv_version = %d", tkv_version());
    if (argc > 1 && strcmp(argv[1], "--unavailable") == 0) {
        return unavailable_mode();
    }
    if (argc < 2) {
        fprintf(stderr, "usage: play <clip.mp4> | --unavailable\n");
        return 2;
    }
    char path[PATH_MAX];
    if (realpath(argv[1], path) == NULL) {
        fprintf(stderr, "no such file: %s\n", argv[1]);
        return 2;
    }
    char uri[PATH_MAX + 16];
    snprintf(uri, sizeof(uri), "file://%s", path);

    printf("initialize\n");
    double t0 = now();
    int r = tkv_initialize();
    CHECK(r == TKV_OK, "tkv_initialize = %d (%.0f ms)", r, (now() - t0) * 1000);
    if (r != TKV_OK) {
        return 1;
    }
    CHECK(tkv_initialize() == TKV_OK, "tkv_initialize again is OK");
    CHECK(tkv_frame_kinds() == TKV_FRAME_CPU_BGRA, "frame kinds = CPU_BGRA");
    CHECK(tkv_gpu_handle_type() == TKV_GPU_NONE, "gpu handle type = NONE");

    printf("can_play\n");
    struct { const char* type; int want; } cases[] = {
        { "video/mp4; codecs=\"avc1.42E01E, mp4a.40.2\"", 2 },
        { "video/mp4", 1 },
        { "VIDEO/MP4; CODECS=\"avc1.640028\"", 2 },
        { "video/webm; codecs=\"vp9, opus\"", 2 },
        { "video/webm; codecs=\"vp8, vorbis\"", 2 },
        { "video/mp4; codecs=\"av01.0.05M.08\"", 2 },
        { "video/mp4; codecs=\"hvc1.1.6.L93.B0\"", 2 },
        { "video/mp4; codecs=\"xyz1\"", 0 },
        { "video/x-unknown", 0 },
        { "", 0 },
    };
    for (size_t i = 0; i < sizeof(cases) / sizeof(cases[0]); i++) {
        int got = tkv_can_play(cases[i].type);
        CHECK(got == cases[i].want, "can_play(%s) = %d (want %d)", cases[i].type, got, cases[i].want);
    }
    CHECK(tkv_can_play(NULL) == 0, "can_play(NULL) = 0");

    log_state l;
    memset(&l, 0, sizeof(l));
    pthread_mutex_init(&l.lock, NULL);
    l.state = -1;
    tkv_player* p = NULL;
    r = tkv_player_create(on_event, &l, NULL, &p);
    CHECK(r == TKV_OK && p != NULL, "tkv_player_create = %d", r);
    if (p == NULL) {
        return 1;
    }

    printf("frame before open\n");
    {
        tkv_frame_request req = { 0, 0, 0, TKV_FRAME_CPU_BGRA, 0 };
        tkv_frame f;
        CHECK(tkv_player_acquire_frame(p, &req, &f) == TKV_E_NO_FRAME, "acquire before open = NO_FRAME");
        CHECK(tkv_player_play(p) != TKV_OK, "play before open fails");
    }

    printf("open %s\n", uri);
    t0 = now();
    r = tkv_player_open(p, uri);
    CHECK(r == TKV_OK, "tkv_player_open = %d", r);
    CHECK(l.n_states > 0 && l.states[0] == TKV_STATE_LOADING, "LOADING reported during open");
    CHECK(wait_state(&l, p, TKV_STATE_PAUSED, 5, 0), "PAUSED after %.0f ms", (now() - t0) * 1000);
    CHECK(fabs(l.duration - 2.0) < 0.1, "duration %.3f ~ 2", l.duration);
    CHECK(l.width == 320 && l.height == 240, "size %dx%d", l.width, l.height);
    CHECK(l.seekable == 1, "seekable %d", l.seekable);
    int one = 1;
    CHECK(wait_for(&l, p, 0, 2, frames_at_least, &one), "FRAME event after load (%d)", l.frames);
    double ranges[8] = { 0 };
    int nr = tkv_player_buffered(p, ranges, 4);
    CHECK(nr == 1 && ranges[0] == 0 && fabs(ranges[1] - 2.0) < 0.1, "buffered %d range(s) %.2f..%.2f", nr, ranges[0], ranges[1]);

    printf("paused frame\n");
    int bgr[3];
    double ft = -1;
    tkv_frame f0;
    r = centre_pixel(p, bgr, &ft, &f0);
    CHECK(r == TKV_OK, "acquire paused frame = %d", r);
    if (r == TKV_OK) {
        CHECK(f0.width == 320 && f0.height == 240 && f0.row_bytes >= 320 * 4 && f0.rotation == 0 && f0.kind == TKV_FRAME_CPU_BGRA,
              "frame %dx%d row_bytes %d rotation %d kind %d", f0.width, f0.height, f0.row_bytes, f0.rotation, f0.kind);
        CHECK(is_red(bgr), "centre BGR %d,%d,%d is red", bgr[0], bgr[1], bgr[2]);
        CHECK(ft < 0.1, "frame time %.3f ~ 0", ft);
    }
    {
        tkv_frame_request req = { 0, 0, 0, TKV_FRAME_CPU_BGRA, 0 };
        tkv_frame f;
        CHECK(tkv_player_acquire_frame(p, &req, &f) == TKV_E_NO_FRAME, "second acquire while paused = NO_FRAME");
        CHECK(tkv_player_redeliver_frame(p) == TKV_OK, "redeliver_frame = OK");
        r = centre_pixel(p, bgr, &ft, NULL);
        CHECK(r == TKV_OK && is_red(bgr), "redelivered frame is red (acquire %d)", r);
        CHECK(tkv_player_acquire_frame(p, &req, &f) == TKV_E_NO_FRAME, "redelivery is one-shot");
    }

    printf("seek 1.5 accurate\n");
    int frames = frames_now(&l) + 1;
    r = tkv_player_seek(p, 1.5, 1);
    CHECK(r == TKV_OK, "seek = %d", r);
    CHECK(wait_for(&l, p, 0, 3, frames_at_least, &frames), "FRAME event after seek");
    r = centre_pixel(p, bgr, &ft, NULL);
    CHECK(r == TKV_OK && is_blue(bgr), "centre BGR %d,%d,%d is blue (acquire %d)", bgr[0], bgr[1], bgr[2], r);
    CHECK(fabs(ft - 1.5) < 0.05, "frame time %.3f ~ 1.5", ft);
    double pos = tkv_player_position(p);
    CHECK(fabs(pos - 1.5) < 0.05, "position %.3f ~ 1.5", pos);
    CHECK(l.state == TKV_STATE_PAUSED, "still PAUSED (%s)", state_name(l.state));

    printf("seek 0.5 key frame\n");
    frames = frames_now(&l) + 1;
    r = tkv_player_seek(p, 0.5, 0);
    CHECK(r == TKV_OK && wait_for(&l, p, 0, 3, frames_at_least, &frames), "key-frame seek lands");
    r = centre_pixel(p, bgr, &ft, NULL);
    CHECK(r == TKV_OK && is_red(bgr), "centre is red at %.3f (key frame at or before 0.5)", ft);

    printf("play to the end\n");
    frames = frames_now(&l) + 1;
    tkv_player_seek(p, 0, 1);
    wait_for(&l, p, 0, 3, frames_at_least, &frames);
    tkv_player_set_volume(p, 0.5);
    s_pulled = 0;
    int positions0 = l.positions;
    t0 = now();
    r = tkv_player_play(p);
    CHECK(r == TKV_OK, "play = %d", r);
    CHECK(wait_state(&l, p, TKV_STATE_PLAYING, 3, 1), "PLAYING after %.0f ms", (now() - t0) * 1000);
    double mid_pos = 0;
    nap_ms(500);
    mid_pos = tkv_player_position(p);
    CHECK(mid_pos > 0.2 && mid_pos < 1.2, "position while playing %.3f", mid_pos);
    CHECK(wait_for(&l, p, 1, 4, ended_at_least, &one), "ENDED after %.3f s", now() - t0);
    double played = now() - t0;
    if (timing) {
        CHECK(played > 1.6 && played < 3.0, "2 s clip played in %.3f s", played);
    }
    nap_ms(100);
    CHECK(l.state == TKV_STATE_ENDED, "state ENDED (%s)", state_name(l.state));
    CHECK(l.positions - positions0 >= 4, "%d POSITION events while playing", l.positions - positions0);
    if (getenv("TKV_NOPULL") == NULL) {
        CHECK(s_pulled >= 20, "pulled %d frames while playing", s_pulled);
    }
    pos = tkv_player_position(p);
    CHECK(fabs(pos - 2.0) < 0.1, "position at end %.3f", pos);

    printf("loop\n");
    tkv_player_set_loop(p, 1);
    int ended_before = l.ended;
    l.position_went_back = 0;
    t0 = now();
    r = tkv_player_play(p); /* from ENDED: restarts at 0 */
    CHECK(r == TKV_OK, "play from ENDED = %d", r);
    CHECK(wait_state(&l, p, TKV_STATE_PLAYING, 3, 1), "PLAYING again");
    double loop_until = now() + 4.5 * s_slow;
    while (now() < loop_until) {
        pull_once(p);
        nap_ms(16);
    }
    CHECK(l.ended == ended_before, "no ENDED while looping (%d)", l.ended - ended_before);
    CHECK(l.state == TKV_STATE_PLAYING, "still PLAYING after 4.5 s (%s)", state_name(l.state));
    CHECK(l.position_went_back, "position wrapped to the start");

    printf("rate 2\n");
    tkv_player_set_loop(p, 0);
    tkv_player_pause(p);
    CHECK(wait_state(&l, p, TKV_STATE_PAUSED, 3, 0), "PAUSED");
    frames = frames_now(&l) + 1;
    tkv_player_seek(p, 0, 1);
    wait_for(&l, p, 0, 3, frames_at_least, &frames);
    r = tkv_player_set_rate(p, 2.0);
    CHECK(r == TKV_OK, "set_rate(2) = %d", r);
    CHECK(tkv_player_set_rate(p, 8.0) == TKV_E_UNSUPPORTED, "set_rate(8) = UNSUPPORTED");
    nap_ms(200);
    ended_before = l.ended;
    t0 = now();
    tkv_player_play(p);
    int want_ended = ended_before + 1;
    CHECK(wait_for(&l, p, 1, 4, ended_at_least, &want_ended), "ENDED at rate 2 after %.3f s", now() - t0);
    played = now() - t0;
    if (timing) {
        CHECK(played > 0.7 && played < 1.6, "2 s clip at rate 2 played in %.3f s", played);
    }
    tkv_player_set_rate(p, 1.0);

    printf("open a missing file\n");
    int errors = l.errors + 1;
    r = tkv_player_open(p, "file:///nonexistent/clip.mp4");
    CHECK(r == TKV_OK, "open = %d", r);
    CHECK(wait_for(&l, p, 0, 3, errors_at_least, &errors), "ERROR event");
    CHECK(l.error_kind == TKV_ERR_NETWORK, "error kind %d (want NETWORK)", l.error_kind);
    CHECK(wait_state(&l, p, TKV_STATE_FAILED, 1, 0), "state FAILED");
    const char* message = tkv_player_last_error(p);
    CHECK(message != NULL && message[0] != 0, "last error: %s", message != NULL ? message : "(null)");

    printf("open a file that is not media\n");
    errors = l.errors + 1;
    r = tkv_player_open(p, "file:///etc/hostname");
    CHECK(wait_for(&l, p, 0, 3, errors_at_least, &errors), "ERROR event");
    CHECK(l.error_kind == TKV_ERR_UNSUPPORTED, "error kind %d (want UNSUPPORTED): %s", l.error_kind, tkv_player_last_error(p));

    printf("reopen\n");
    r = tkv_player_open(p, uri);
    CHECK(r == TKV_OK && wait_state(&l, p, TKV_STATE_PAUSED, 5, 0), "PAUSED after reopen");
    frames = 1;
    r = centre_pixel(p, bgr, &ft, NULL);
    CHECK(r == TKV_OK && is_red(bgr), "reopened frame is red (acquire %d)", r);

    printf("seek before load\n");
    tkv_player_open(p, uri);
    tkv_player_seek(p, 1.5, 1);
    CHECK(wait_state(&l, p, TKV_STATE_PAUSED, 5, 0), "PAUSED");
    frames = frames_now(&l);
    nap_ms(300);
    r = centre_pixel(p, bgr, &ft, NULL);
    CHECK(r == TKV_OK && is_blue(bgr), "pending seek honoured: blue at %.3f (acquire %d)", ft, r);

    printf("destroy while playing with a frame out\n");
    tkv_player_play(p);
    wait_state(&l, p, TKV_STATE_PLAYING, 3, 1);
    {
        tkv_frame_request req = { 0, 0, 0, TKV_FRAME_CPU_BGRA, 0 };
        tkv_frame f;
        for (int i = 0; i < 50 && tkv_player_acquire_frame(p, &req, &f) != TKV_OK; i++) {
            nap_ms(10);
        }
    }
    tkv_player_destroy(p);
    int states_after = l.n_states;
    nap_ms(300);
    CHECK(l.n_states == states_after, "no events after destroy");

    int cycles = getenv("TKV_CYCLES") != NULL ? atoi(getenv("TKV_CYCLES")) : 3;
    printf("create/open/destroy x%d\n", cycles);
    for (int i = 0; i < cycles; i++) {
        log_state l2;
        memset(&l2, 0, sizeof(l2));
        pthread_mutex_init(&l2.lock, NULL);
        tkv_player* q = NULL;
        tkv_player_create(on_event, &l2, NULL, &q);
        tkv_player_open(q, uri);
        if (i > 0) {
            wait_state(&l2, q, TKV_STATE_PAUSED, 5, 0);
        }
        tkv_player_destroy(q);
        pthread_mutex_destroy(&l2.lock);
    }
    CHECK(1, "done");

    pthread_mutex_destroy(&l.lock);
    printf("%s (%d failures)\n", s_failures == 0 ? "PASS" : "FAIL", s_failures);
    return s_failures == 0 ? 0 : 1;
}
