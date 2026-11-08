/* CPU frame scaling shared by adapters whose platform does not scale while copying (Linux).
   Header-only: static functions, compiled into each adapter that includes it. */
#ifndef TKV_SCALE_H
#define TKV_SCALE_H

#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>

/* The largest size inside target_width × target_height with the picture's aspect, never larger than the picture (the
   Windows and macOS adapters use the same rule); 0 means no limit on that side. */
static inline void tkv_fit_size(int32_t width, int32_t height, int32_t target_width, int32_t target_height,
                                int32_t* out_width, int32_t* out_height)
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
    int32_t w = (int32_t)(width * scale + 0.5);
    int32_t h = (int32_t)(height * scale + 0.5);
    *out_width = w < 1 ? 1 : w;
    *out_height = h < 1 ? 1 : h;
}

/* Memory reused across frames by tkv_scale_bgra. Zero-initialize; free with tkv_scaler_free. */
typedef struct tkv_scaler {
    uint8_t* pixels;   /* the scaled frame, row_bytes = width * 4 */
    size_t pixels_capacity;
    int32_t* columns;  /* byte offsets of the two source columns sampled for each destination column */
    size_t columns_capacity;
} tkv_scaler;

static inline void tkv_scaler_free(tkv_scaler* scaler)
{
    free(scaler->pixels);
    free(scaler->columns);
    scaler->pixels = NULL;
    scaler->columns = NULL;
    scaler->pixels_capacity = 0;
    scaler->columns_capacity = 0;
}

/* The two source positions sampled across a destination cell [first, end): both pixels of a 2-pixel cell, the 1/4 and 3/4
   points of a wider one, the one pixel twice for a 1-pixel cell. */
static inline void tkv_scale_taps(int32_t first, int32_t end, int32_t* a, int32_t* b)
{
    int32_t span = end - first;
    if (span <= 1) {
        *a = first;
        *b = first;
    } else if (span == 2) {
        *a = first;
        *b = first + 1;
    } else {
        *a = first + span / 4;
        *b = first + (3 * span) / 4;
    }
}

/* Scales a BGRA picture down to dst_width × dst_height (each no larger than the source) by averaging 2 × 2 samples spread
   over the source cell each destination pixel covers: exact for downscales up to 2× on each side, a supersampled
   approximation beyond (averaging every covered pixel cost about 2.6 ms per 1080p frame; this reads four per output pixel).
   Returns the scaled pixels (row bytes dst_width * 4), or NULL when memory runs out. */
static inline const uint8_t* tkv_scale_bgra(tkv_scaler* scaler, const uint8_t* src, int32_t src_width, int32_t src_height,
                                            int32_t src_row_bytes, int32_t dst_width, int32_t dst_height)
{
    if (src_width <= 0 || src_height <= 0 || dst_width <= 0 || dst_height <= 0 || dst_width > src_width ||
        dst_height > src_height) {
        return NULL;
    }
    size_t needed = (size_t)dst_width * 4 * (size_t)dst_height;
    if (needed > scaler->pixels_capacity) {
        uint8_t* grown = (uint8_t*)realloc(scaler->pixels, needed);
        if (grown == NULL) {
            return NULL;
        }
        scaler->pixels = grown;
        scaler->pixels_capacity = needed;
    }
    size_t columns = (size_t)dst_width * 2;
    if (columns > scaler->columns_capacity) {
        int32_t* grown = (int32_t*)realloc(scaler->columns, columns * sizeof(int32_t));
        if (grown == NULL) {
            return NULL;
        }
        scaler->columns = grown;
        scaler->columns_capacity = columns;
    }
    /* Byte offsets of the two sampled columns of each destination column. */
    for (int32_t x = 0; x < dst_width; x++) {
        int32_t first = (int32_t)((int64_t)x * src_width / dst_width);
        int32_t end = (int32_t)((int64_t)(x + 1) * src_width / dst_width);
        int32_t ta, tb;
        tkv_scale_taps(first, end, &ta, &tb);
        scaler->columns[2 * x] = ta * 4;
        scaler->columns[2 * x + 1] = tb * 4;
    }
    for (int32_t y = 0; y < dst_height; y++) {
        int32_t first = (int32_t)((int64_t)y * src_height / dst_height);
        int32_t end = (int32_t)((int64_t)(y + 1) * src_height / dst_height);
        int32_t ya, yb;
        tkv_scale_taps(first, end, &ya, &yb);
        const uint8_t* row_a = src + (size_t)ya * (size_t)src_row_bytes;
        const uint8_t* row_b = src + (size_t)yb * (size_t)src_row_bytes;
        uint8_t* out = scaler->pixels + (size_t)y * (size_t)dst_width * 4;
        const int32_t* taps = scaler->columns;
        for (int32_t x = 0; x < dst_width; x++, out += 4, taps += 2) {
            const uint8_t* p0 = row_a + taps[0];
            const uint8_t* p1 = row_a + taps[1];
            const uint8_t* p2 = row_b + taps[0];
            const uint8_t* p3 = row_b + taps[1];
            out[0] = (uint8_t)(((uint32_t)p0[0] + p1[0] + p2[0] + p3[0] + 2) >> 2);
            out[1] = (uint8_t)(((uint32_t)p0[1] + p1[1] + p2[1] + p3[1] + 2) >> 2);
            out[2] = (uint8_t)(((uint32_t)p0[2] + p1[2] + p2[2] + p3[2] + 2) >> 2);
            out[3] = (uint8_t)(((uint32_t)p0[3] + p1[3] + p2[3] + p3[3] + 2) >> 2);
        }
    }
    return scaler->pixels;
}

#endif
