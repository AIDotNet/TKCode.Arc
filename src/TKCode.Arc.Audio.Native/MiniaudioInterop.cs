using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>P/Invoke surface of native/miniaudio/tkcode_audio.h.</summary>
internal static unsafe partial class MiniaudioInterop
{
    public const string Library = "tkcode_audio";

    /// <summary>The shim ABI this assembly was written against.</summary>
    public const int AbiVersion = 1;

    [LibraryImport(Library, EntryPoint = "tka_version")]
    public static partial int Version();

    [LibraryImport(Library, EntryPoint = "tka_engine_create")]
    public static partial int EngineCreate(out nint engine);

    [LibraryImport(Library, EntryPoint = "tka_engine_destroy")]
    public static partial void EngineDestroy(nint engine);

    [LibraryImport(Library, EntryPoint = "tka_sound_create_from_memory")]
    public static partial int SoundCreateFromMemory(nint engine, byte* data, nuint size, out nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_create_from_file", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SoundCreateFromFile(nint engine, string path, out nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_destroy")]
    public static partial void SoundDestroy(nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_start")]
    public static partial int SoundStart(nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_stop")]
    public static partial int SoundStop(nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_is_playing")]
    public static partial int SoundIsPlaying(nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_at_end")]
    public static partial int SoundAtEnd(nint sound);

    [LibraryImport(Library, EntryPoint = "tka_sound_sample_rate")]
    public static partial int SoundSampleRate(nint sound, out uint rate);

    [LibraryImport(Library, EntryPoint = "tka_sound_length")]
    public static partial int SoundLength(nint sound, out ulong frames);

    [LibraryImport(Library, EntryPoint = "tka_sound_cursor")]
    public static partial int SoundCursor(nint sound, out ulong frame);

    [LibraryImport(Library, EntryPoint = "tka_sound_seek")]
    public static partial int SoundSeek(nint sound, ulong frame);

    [LibraryImport(Library, EntryPoint = "tka_sound_set_volume")]
    public static partial void SoundSetVolume(nint sound, float volume);

    [LibraryImport(Library, EntryPoint = "tka_sound_set_looping")]
    public static partial void SoundSetLooping(nint sound, int looping);
}
