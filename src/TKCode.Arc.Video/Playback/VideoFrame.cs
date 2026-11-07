using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>Where a player's frames are shown: set by <see cref="VideoSurface"/> through <see cref="IVideoPlayer.FrameTarget"/>.</summary>
public sealed record VideoFrameTarget
{
    /// <summary>The GPU image handle types the compositor imports (<c>ICompositionGpuInterop.SupportedImageHandleTypes</c>); empty when only CPU frames can be shown.</summary>
    public IReadOnlyList<string> GpuHandleTypes { get; init; } = [];

    /// <summary>The LUID of the compositor's graphics adapter (<c>ICompositionGpuInterop.DeviceLuid</c>); a Windows backend creates its D3D11 device on it. Null when unknown.</summary>
    public byte[]? AdapterLuid { get; init; }

    /// <summary>The UUID of the compositor's graphics adapter (<c>ICompositionGpuInterop.DeviceUuid</c>); null when unknown.</summary>
    public byte[]? AdapterUuid { get; init; }

    /// <summary>Whether a GPU frame can be shown at all.</summary>
    public bool AcceptsGpu => GpuHandleTypes.Count > 0;
}

/// <summary>Byte order of CPU frame pixels.</summary>
public enum VideoCpuPixelFormat
{
    /// <summary>B, G, R, A per pixel (desktop backends).</summary>
    Bgra8888,

    /// <summary>R, G, B, A per pixel (the browser backend, which gets RGBA from the canvas).</summary>
    Rgba8888,
}

/// <summary>What a surface asks <see cref="IVideoPlayer.TryAcquireFrame"/> for.</summary>
public readonly struct VideoFrameRequest
{
    /// <summary>Creates a request.</summary>
    public VideoFrameRequest(TimeSpan hostTime, PixelSize targetSize, VideoFrameKinds accepted)
    {
        HostTime = hostTime;
        TargetSize = targetSize;
        Accepted = accepted;
    }

    /// <summary>When the frame will be shown, on the compositor's clock (the <c>RequestAnimationFrame</c> time); backends that pick frames by display time use it, others ignore it.</summary>
    public TimeSpan HostTime { get; }

    /// <summary>
    /// The size the frame will be drawn at, in device pixels. Backends that can scale while copying (Media Foundation, a browser
    /// canvas) hand out frames no larger than this; others ignore it. 0×0 means the natural size.
    /// </summary>
    public PixelSize TargetSize { get; }

    /// <summary>The frame forms the surface can show now.</summary>
    public VideoFrameKinds Accepted { get; }
}

/// <summary>Releases frames handed out by a player; implemented by backends.</summary>
public interface IVideoFrameOwner
{
    /// <summary>Returns the frame identified by <paramref name="token"/> to the player.</summary>
    void ReleaseFrame(long token);
}

/// <summary>
/// One frame lent by <see cref="IVideoPlayer.TryAcquireFrame"/>: CPU pixels (<see cref="Kind"/> = <see cref="VideoFrameKinds.CpuBgra"/>)
/// or a GPU image (<see cref="VideoFrameKinds.Gpu"/>). Dispose it before asking for the next frame.
/// </summary>
public ref struct VideoFrameLease
{
    private IVideoFrameOwner? _owner;
    private readonly long _token;

    /// <summary>Lends CPU pixels: rows of <paramref name="rowBytes"/> bytes in <paramref name="pixelFormat"/>, opaque alpha.</summary>
    public VideoFrameLease(IVideoFrameOwner? owner, long token, PixelSize size, TimeSpan presentationTime, ReadOnlySpan<byte> pixels, int rowBytes, int rotationDegrees = 0,
        VideoCpuPixelFormat pixelFormat = VideoCpuPixelFormat.Bgra8888)
    {
        CpuPixelFormat = pixelFormat;
        _owner = owner;
        _token = token;
        Kind = VideoFrameKinds.CpuBgra;
        Size = size;
        PresentationTime = presentationTime;
        Pixels = pixels;
        RowBytes = rowBytes;
        RotationDegrees = rotationDegrees;
        GpuImage = null;
        GpuImageId = 0;
        GpuFormat = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm;
        KeyedMutexAcquireKey = null;
        KeyedMutexReleaseKey = 0;
    }

    /// <summary>
    /// Lends a GPU image. <paramref name="imageId"/> identifies the underlying texture across frames (backends cycle a small pool),
    /// so a surface imports each texture once. With a keyed mutex the surface acquires <paramref name="acquireKey"/> and the
    /// compositor releases <paramref name="releaseKey"/>.
    /// </summary>
    public VideoFrameLease(IVideoFrameOwner? owner, long token, PixelSize size, TimeSpan presentationTime, IPlatformHandle image, long imageId,
        PlatformGraphicsExternalImageFormat format, uint? acquireKey = null, uint releaseKey = 0, int rotationDegrees = 0)
    {
        _owner = owner;
        _token = token;
        Kind = VideoFrameKinds.Gpu;
        Size = size;
        PresentationTime = presentationTime;
        Pixels = default;
        RowBytes = 0;
        CpuPixelFormat = VideoCpuPixelFormat.Bgra8888;
        RotationDegrees = rotationDegrees;
        GpuImage = image;
        GpuImageId = imageId;
        GpuFormat = format;
        KeyedMutexAcquireKey = acquireKey;
        KeyedMutexReleaseKey = releaseKey;
    }

    /// <summary>The frame's form.</summary>
    public VideoFrameKinds Kind { get; }

    /// <summary>The frame size in pixels, before <see cref="RotationDegrees"/>.</summary>
    public PixelSize Size { get; }

    /// <summary>The frame's media time.</summary>
    public TimeSpan PresentationTime { get; }

    /// <summary>Clockwise rotation to apply when drawing: 0, 90, 180 or 270.</summary>
    public int RotationDegrees { get; }

    /// <summary>CPU pixels; empty for a GPU frame.</summary>
    public ReadOnlySpan<byte> Pixels { get; }

    /// <summary>The byte order of <see cref="Pixels"/>.</summary>
    public VideoCpuPixelFormat CpuPixelFormat { get; }

    /// <summary>Bytes per row of <see cref="Pixels"/>.</summary>
    public int RowBytes { get; }

    /// <summary>The GPU image; null for a CPU frame. Its <see cref="IPlatformHandle.HandleDescriptor"/> is a <c>KnownPlatformGraphicsExternalImageHandleTypes</c> value.</summary>
    public IPlatformHandle? GpuImage { get; }

    /// <summary>A stable id of the texture behind <see cref="GpuImage"/>.</summary>
    public long GpuImageId { get; }

    /// <summary>The GPU image's pixel format.</summary>
    public PlatformGraphicsExternalImageFormat GpuFormat { get; }

    /// <summary>The keyed-mutex key to acquire before reading; null when the image needs no keyed mutex.</summary>
    public uint? KeyedMutexAcquireKey { get; }

    /// <summary>The keyed-mutex key to release after reading.</summary>
    public uint KeyedMutexReleaseKey { get; }

    /// <summary>Returns the frame to the player.</summary>
    public void Dispose()
    {
        var owner = _owner;
        _owner = null;
        owner?.ReleaseFrame(_token);
    }
}
