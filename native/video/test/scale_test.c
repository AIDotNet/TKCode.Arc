/* Unit test of tkv_scale.h (CPU frame scaling): cc -std=c11 -Wall -Wextra -O2 native/video/test/scale_test.c -o scale_test && ./scale_test */
#include "../tkv_scale.h"
#include <stdio.h>
#include <assert.h>
int main(void) {
    int32_t w, h;
    tkv_fit_size(1920, 1080, 640, 640, &w, &h); assert(w == 640 && h == 360);
    tkv_fit_size(320, 240, 1920, 1080, &w, &h); assert(w == 320 && h == 240);
    tkv_fit_size(320, 240, 0, 0, &w, &h); assert(w == 320 && h == 240);
    tkv_fit_size(1000, 10, 3, 0, &w, &h); assert(w == 3 && h == 1);
    /* 4x2 with padded rows -> 2x1: each output averages a 2x2 block. */
    uint8_t src[2 * 20] = {0};
    for (int y = 0; y < 2; y++) for (int x = 0; x < 4; x++) { uint8_t* p = src + y * 20 + x * 4; p[0] = (uint8_t)(x < 2 ? 10 * (x + y) : 200); p[1] = 7; p[2] = (uint8_t)(x * 50); p[3] = 255; }
    tkv_scaler s = {0};
    const uint8_t* out = tkv_scale_bgra(&s, src, 4, 2, 20, 2, 1);
    assert(out != NULL);
    printf("%d %d %d %d | %d %d %d %d\n", out[0], out[1], out[2], out[3], out[4], out[5], out[6], out[7]);
    assert(out[0] == 10 && out[1] == 7 && out[2] == 25 && out[3] == 255);  /* b: (0+10+10+20)/4 ; r: (0+50+0+50)/4 */
    assert(out[4] == 200 && out[6] == 125);
    /* Non-integer ratio, 1920x1080 solid -> 641x361 stays solid. */
    static uint8_t big[1920 * 1080 * 4];
    for (size_t i = 0; i < sizeof big; i += 4) { big[i] = 1; big[i + 1] = 2; big[i + 2] = 3; big[i + 3] = 255; }
    out = tkv_scale_bgra(&s, big, 1920, 1080, 1920 * 4, 641, 361);
    for (size_t i = 0; i < 641 * 361 * 4; i += 4) { assert(out[i] == 1 && out[i + 1] == 2 && out[i + 2] == 3 && out[i + 3] == 255); }
    assert(tkv_scale_bgra(&s, big, 10, 10, 40, 11, 5) == NULL); /* never scales up */
    tkv_scaler_free(&s);
    puts("ok");
    return 0;
}
