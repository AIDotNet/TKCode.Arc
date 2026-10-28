/* TKCode.Arc.Audio.Native shim over miniaudio (public domain / MIT-0). See tkcode_audio.h. */
#define MINIAUDIO_IMPLEMENTATION
#include "miniaudio.h"
#include "tkcode_audio.h"

#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
  #include <windows.h>
#endif

#define TKA_VERSION 1

struct tka_engine {
    ma_engine engine;
};

struct tka_sound {
    ma_sound sound;
    ma_decoder decoder;
    int has_decoder;
    void* data;
};

int32_t tka_version(void) { return TKA_VERSION; }

int32_t tka_engine_create(tka_engine** engine)
{
    if (engine == NULL) { return MA_INVALID_ARGS; }
    tka_engine* e = (tka_engine*)calloc(1, sizeof(tka_engine));
    if (e == NULL) { return MA_OUT_OF_MEMORY; }
    ma_result result = ma_engine_init(NULL, &e->engine);
    if (result != MA_SUCCESS) { free(e); return result; }
    *engine = e;
    return MA_SUCCESS;
}

void tka_engine_destroy(tka_engine* engine)
{
    if (engine == NULL) { return; }
    ma_engine_uninit(&engine->engine);
    free(engine);
}

int32_t tka_sound_create_from_memory(tka_engine* engine, const void* data, size_t size, tka_sound** sound)
{
    if (engine == NULL || data == NULL || size == 0 || sound == NULL) { return MA_INVALID_ARGS; }
    tka_sound* s = (tka_sound*)calloc(1, sizeof(tka_sound));
    if (s == NULL) { return MA_OUT_OF_MEMORY; }
    s->data = malloc(size);
    if (s->data == NULL) { free(s); return MA_OUT_OF_MEMORY; }
    memcpy(s->data, data, size);
    ma_decoder_config config = ma_decoder_config_init(ma_format_f32, 0, 0);
    ma_result result = ma_decoder_init_memory(s->data, size, &config, &s->decoder);
    if (result != MA_SUCCESS) { free(s->data); free(s); return result; }
    s->has_decoder = 1;
    result = ma_sound_init_from_data_source(&engine->engine, &s->decoder, MA_SOUND_FLAG_NO_SPATIALIZATION, NULL, &s->sound);
    if (result != MA_SUCCESS) { ma_decoder_uninit(&s->decoder); free(s->data); free(s); return result; }
    *sound = s;
    return MA_SUCCESS;
}

int32_t tka_sound_create_from_file(tka_engine* engine, const char* path, tka_sound** sound)
{
    if (engine == NULL || path == NULL || sound == NULL) { return MA_INVALID_ARGS; }
    tka_sound* s = (tka_sound*)calloc(1, sizeof(tka_sound));
    if (s == NULL) { return MA_OUT_OF_MEMORY; }
    ma_uint32 flags = MA_SOUND_FLAG_NO_SPATIALIZATION | MA_SOUND_FLAG_STREAM;
    ma_result result;
#if defined(_WIN32)
    int count = MultiByteToWideChar(CP_UTF8, 0, path, -1, NULL, 0);
    if (count <= 0) { free(s); return MA_INVALID_ARGS; }
    wchar_t* wide = (wchar_t*)malloc(sizeof(wchar_t) * (size_t)count);
    if (wide == NULL) { free(s); return MA_OUT_OF_MEMORY; }
    MultiByteToWideChar(CP_UTF8, 0, path, -1, wide, count);
    result = ma_sound_init_from_file_w(&engine->engine, wide, flags, NULL, NULL, &s->sound);
    free(wide);
#else
    result = ma_sound_init_from_file(&engine->engine, path, flags, NULL, NULL, &s->sound);
#endif
    if (result != MA_SUCCESS) { free(s); return result; }
    *sound = s;
    return MA_SUCCESS;
}

void tka_sound_destroy(tka_sound* sound)
{
    if (sound == NULL) { return; }
    ma_sound_uninit(&sound->sound);
    if (sound->has_decoder) { ma_decoder_uninit(&sound->decoder); }
    free(sound->data);
    free(sound);
}

int32_t tka_sound_start(tka_sound* sound) { return sound == NULL ? MA_INVALID_ARGS : ma_sound_start(&sound->sound); }
int32_t tka_sound_stop(tka_sound* sound) { return sound == NULL ? MA_INVALID_ARGS : ma_sound_stop(&sound->sound); }
int32_t tka_sound_is_playing(tka_sound* sound) { return sound != NULL && ma_sound_is_playing(&sound->sound) ? 1 : 0; }
int32_t tka_sound_at_end(tka_sound* sound) { return sound != NULL && ma_sound_at_end(&sound->sound) ? 1 : 0; }

int32_t tka_sound_sample_rate(tka_sound* sound, uint32_t* rate)
{
    if (sound == NULL || rate == NULL) { return MA_INVALID_ARGS; }
    return ma_sound_get_data_format(&sound->sound, NULL, NULL, rate, NULL, 0);
}

int32_t tka_sound_length(tka_sound* sound, uint64_t* frames)
{
    if (sound == NULL || frames == NULL) { return MA_INVALID_ARGS; }
    ma_uint64 value = 0;
    ma_result result = ma_sound_get_length_in_pcm_frames(&sound->sound, &value);
    *frames = value;
    return result;
}

int32_t tka_sound_cursor(tka_sound* sound, uint64_t* frame)
{
    if (sound == NULL || frame == NULL) { return MA_INVALID_ARGS; }
    ma_uint64 value = 0;
    ma_result result = ma_sound_get_cursor_in_pcm_frames(&sound->sound, &value);
    *frame = value;
    return result;
}

int32_t tka_sound_seek(tka_sound* sound, uint64_t frame)
{
    return sound == NULL ? MA_INVALID_ARGS : ma_sound_seek_to_pcm_frame(&sound->sound, frame);
}

void tka_sound_set_volume(tka_sound* sound, float volume)
{
    if (sound != NULL) { ma_sound_set_volume(&sound->sound, volume); }
}

void tka_sound_set_looping(tka_sound* sound, int32_t looping)
{
    if (sound != NULL) { ma_sound_set_looping(&sound->sound, looping ? MA_TRUE : MA_FALSE); }
}
