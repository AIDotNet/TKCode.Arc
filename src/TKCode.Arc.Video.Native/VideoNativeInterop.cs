using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>P/Invoke surface of native/video/tkcode_video.h (one C ABI for the Media Foundation, AVFoundation and GStreamer shims).</summary>
internal static unsafe partial class VideoNativeInterop
{
    public const string Library = "tkcode_video";

    /// <summary>The shim ABI this assembly was written against (TKV_ABI_VERSION).</summary>
    public const int AbiVersion = 2;

    public const int Ok = 0;
    public const int ErrorFail = -1;
    public const int ErrorUnavailable = -2;
    public const int ErrorInvalidArgument = -3;
    public const int ErrorNoFrame = -4;
    public const int ErrorUnsupported = -5;

    public const int EventState = 1;
    public const int EventPosition = 2;
    public const int EventDuration = 3;
    public const int EventSize = 4;
    public const int EventBuffered = 5;
    public const int EventFrame = 6;
    public const int EventError = 7;
    public const int EventEnded = 8;
    public const int EventSeekable = 9;

    public const int FrameCpuBgra = 1;
    public const int FrameGpu = 2;

    public const int GpuNone = 0;
    public const int GpuD3D11NtHandle = 1;
    public const int GpuD3D11Global = 2;
    public const int GpuIOSurface = 3;

    public const int SemaphoreNone = 0;
    public const int SemaphoreMetalSharedEvent = 1;

    [LibraryImport(Library, EntryPoint = "tkv_version")]
    public static partial int Version();

    [LibraryImport(Library, EntryPoint = "tkv_initialize")]
    public static partial int Initialize();

    [LibraryImport(Library, EntryPoint = "tkv_frame_kinds")]
    public static partial int FrameKinds();

    [LibraryImport(Library, EntryPoint = "tkv_gpu_handle_type")]
    public static partial int GpuHandleType();

    [LibraryImport(Library, EntryPoint = "tkv_can_play", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int CanPlay(string mediaType);

    [LibraryImport(Library, EntryPoint = "tkv_player_create")]
    public static partial int PlayerCreate(delegate* unmanaged[Cdecl]<nint, int, double, double, void> callback, nint context, byte* adapterLuid, out nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_destroy")]
    public static partial void PlayerDestroy(nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_open", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int PlayerOpen(nint player, string url);

    [LibraryImport(Library, EntryPoint = "tkv_player_play")]
    public static partial int PlayerPlay(nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_pause")]
    public static partial int PlayerPause(nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_seek")]
    public static partial int PlayerSeek(nint player, double seconds, int accurate);

    [LibraryImport(Library, EntryPoint = "tkv_player_position")]
    public static partial double PlayerPosition(nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_set_volume")]
    public static partial int PlayerSetVolume(nint player, double volume);

    [LibraryImport(Library, EntryPoint = "tkv_player_set_rate")]
    public static partial int PlayerSetRate(nint player, double rate);

    [LibraryImport(Library, EntryPoint = "tkv_player_set_loop")]
    public static partial int PlayerSetLoop(nint player, int loop);

    [LibraryImport(Library, EntryPoint = "tkv_player_buffered")]
    public static partial int PlayerBuffered(nint player, double* ranges, int capacity);

    [LibraryImport(Library, EntryPoint = "tkv_player_acquire_frame")]
    public static partial int PlayerAcquireFrame(nint player, TkvFrameRequest* request, TkvFrame* frame);

    [LibraryImport(Library, EntryPoint = "tkv_player_release_frame")]
    public static partial void PlayerReleaseFrame(nint player, long token);

    /// <summary>ABI 2, optional export (EntryPointNotFoundException when the shim leaves it out).</summary>
    [LibraryImport(Library, EntryPoint = "tkv_player_timeline_sync")]
    public static partial int PlayerTimelineSync(nint player, long token, out nint semaphore, out int semaphoreType, out ulong waitValue, out ulong signalValue);

    /// <summary>ABI 2, optional export (EntryPointNotFoundException when the shim leaves it out).</summary>
    [LibraryImport(Library, EntryPoint = "tkv_player_redeliver_frame")]
    public static partial int PlayerRedeliverFrame(nint player);

    [LibraryImport(Library, EntryPoint = "tkv_player_last_error")]
    public static partial nint PlayerLastError(nint player);

    /// <summary>tkv_player_last_error as a string; empty when there is none.</summary>
    public static string LastError(nint player) => player == 0 ? "" : Marshal.PtrToStringUTF8(PlayerLastError(player)) ?? "";
}

/// <summary>Mirrors <c>tkv_frame</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TkvFrame
{
    public int Kind;
    public int Width;
    public int Height;
    public int Rotation;
    public double Time;
    public nint Pixels;
    public int RowBytes;
    public int GpuHandleType;
    public nint GpuHandle;
    public long GpuImageId;
    public int KeyedMutex;
    public uint AcquireKey;
    public uint ReleaseKey;
    public long Token;
}

/// <summary>Mirrors <c>tkv_frame_request</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TkvFrameRequest
{
    public long HostTimeNs;
    public int TargetWidth;
    public int TargetHeight;
    public int Accept;
    public int GpuHandleType;
}
