using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The desktop backend over the tkcode_video shim: <c>"avfoundation"</c> on macOS, <c>"mediafoundation"</c> on Windows,
/// <c>"gstreamer"</c> on Linux. Created by <see cref="ArcVideoNative.Use"/>.
/// </summary>
public sealed class NativeVideoBackend : IVideoBackend
{
    private readonly int _gpuHandleType;

    internal NativeVideoBackend(string name, int frameKinds, int gpuHandleType)
    {
        Name = name;
        FrameKinds = (VideoFrameKinds)(frameKinds & (VideoNativeInterop.FrameCpuBgra | VideoNativeInterop.FrameGpu));
        var descriptor = HandleDescriptor(gpuHandleType);
        if (descriptor is null || (FrameKinds & VideoFrameKinds.Gpu) == 0)
        {
            FrameKinds &= ~VideoFrameKinds.Gpu;
            _gpuHandleType = VideoNativeInterop.GpuNone;
            GpuHandleTypes = [];
        }
        else
        {
            _gpuHandleType = gpuHandleType;
            GpuHandleTypes = [descriptor];
        }
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsReal => true;

    /// <inheritdoc />
    public VideoFrameKinds FrameKinds { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> GpuHandleTypes { get; }

    /// <summary>The TKV_GPU_* value the shim produces; TKV_GPU_NONE when it has no GPU frames.</summary>
    internal int NativeGpuHandleType => _gpuHandleType;

    /// <summary>The Avalonia handle type string (<see cref="KnownPlatformGraphicsExternalImageHandleTypes"/>) of the GPU frames; null when none.</summary>
    internal string? GpuHandleDescriptor => GpuHandleTypes.Count > 0 ? GpuHandleTypes[0] : null;

    /// <inheritdoc />
    public VideoSupport CanPlay(string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return VideoSupport.No;
        }
        return VideoNativeInterop.CanPlay(mediaType) switch
        {
            2 => VideoSupport.Probably,
            1 => VideoSupport.Maybe,
            _ => VideoSupport.No,
        };
    }

    /// <inheritdoc />
    public IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null) => new NativeVideoPlayer(this, options ?? VideoPlayerOptions.Default);

    /// <summary>Maps a TKV_GPU_* value to Avalonia's handle type string.</summary>
    internal static string? HandleDescriptor(int gpuHandleType) => gpuHandleType switch
    {
        VideoNativeInterop.GpuD3D11NtHandle => KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle,
        VideoNativeInterop.GpuD3D11Global => KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle,
        VideoNativeInterop.GpuIOSurface => KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef,
        _ => null,
    };
}

/// <summary>A timeline semaphore of the native player (an <c>id&lt;MTLSharedEvent&gt;</c> on macOS), owned by the player.</summary>
internal sealed class NativeSemaphoreHandle(nint handle, string descriptor) : IPlatformHandle
{
    /// <inheritdoc />
    public nint Handle { get; } = handle;

    /// <inheritdoc />
    public string HandleDescriptor { get; } = descriptor;
}

/// <summary>A GPU image lent by the native player: an <c>IOSurfaceRef</c> or a D3D11 shared handle, owned by the player.</summary>
internal sealed class NativeGpuImageHandle(nint handle, string descriptor) : IPlatformHandle
{
    /// <inheritdoc />
    public nint Handle { get; } = handle;

    /// <inheritdoc />
    public string HandleDescriptor { get; } = descriptor;
}
