/* TKCode.Arc.Audio.Native: a flat C API over miniaudio's engine, one sound per player.
   Functions that return int32_t return 0 on success or a negative miniaudio result code. */
#ifndef TKCODE_AUDIO_H
#define TKCODE_AUDIO_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
  #define TKA_API __declspec(dllexport)
#else
  #define TKA_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct tka_engine tka_engine;
typedef struct tka_sound tka_sound;

/* The shim's ABI version; bump when a signature changes. */
TKA_API int32_t tka_version(void);

TKA_API int32_t tka_engine_create(tka_engine** engine);
TKA_API void tka_engine_destroy(tka_engine* engine);

/* Decodes from a private copy of data (WAV, MP3, FLAC). */
TKA_API int32_t tka_sound_create_from_memory(tka_engine* engine, const void* data, size_t size, tka_sound** sound);
/* Streams from a file; path is UTF-8. */
TKA_API int32_t tka_sound_create_from_file(tka_engine* engine, const char* path, tka_sound** sound);
TKA_API void tka_sound_destroy(tka_sound* sound);

TKA_API int32_t tka_sound_start(tka_sound* sound);
TKA_API int32_t tka_sound_stop(tka_sound* sound);
/* 1 or 0. */
TKA_API int32_t tka_sound_is_playing(tka_sound* sound);
TKA_API int32_t tka_sound_at_end(tka_sound* sound);

TKA_API int32_t tka_sound_sample_rate(tka_sound* sound, uint32_t* rate);
TKA_API int32_t tka_sound_length(tka_sound* sound, uint64_t* frames);
TKA_API int32_t tka_sound_cursor(tka_sound* sound, uint64_t* frame);
TKA_API int32_t tka_sound_seek(tka_sound* sound, uint64_t frame);

TKA_API void tka_sound_set_volume(tka_sound* sound, float volume);
TKA_API void tka_sound_set_looping(tka_sound* sound, int32_t looping);

#ifdef __cplusplus
}
#endif

#endif
