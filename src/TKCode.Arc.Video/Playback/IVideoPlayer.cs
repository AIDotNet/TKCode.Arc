using Avalonia;

namespace TKCode.Arc;

/// <summary>Where a video player is in its life. Modelled on the HTML media element (design/components/video/video.md section 6).</summary>
public enum VideoPlaybackState
{
    /// <summary>Nothing loaded.</summary>
    Idle,

    /// <summary>Loading until the size and length are known.</summary>
    Loading,

    /// <summary>Loaded and paused.</summary>
    Paused,

    /// <summary>Playing.</summary>
    Playing,

    /// <summary>Playback is wanted but waiting for data (the HTML <c>waiting</c> event).</summary>
    Buffering,

    /// <summary>At the end, not looping.</summary>
    Ended,

    /// <summary>Loading or playback failed; see <see cref="IVideoPlayer.Failed"/>.</summary>
    Failed,
}

/// <summary>Why a video player failed; codes 1-4 of the HTML <c>MediaError</c>, plus a missing backend.</summary>
public enum VideoErrorKind
{
    /// <summary>Loading was aborted.</summary>
    Aborted,

    /// <summary>The media could not be downloaded.</summary>
    Network,

    /// <summary>The media could not be decoded.</summary>
    Decode,

    /// <summary>The format or source is not supported by the backend.</summary>
    Unsupported,

    /// <summary>No real playback backend is available (the simulated backend is in use, or a native library failed to load).</summary>
    BackendUnavailable,
}

/// <summary>How precisely <see cref="IVideoPlayer.Seek"/> lands.</summary>
public enum VideoSeekMode
{
    /// <summary>On the requested time.</summary>
    Accurate,

    /// <summary>On a nearby key frame; used while dragging the seek bar.</summary>
    Fast,
}

/// <summary>Whether a backend can play a media type; mirrors <c>HTMLMediaElement.canPlayType</c>.</summary>
public enum VideoSupport
{
    /// <summary>Cannot play it.</summary>
    No,

    /// <summary>Might play it; only trying tells.</summary>
    Maybe,

    /// <summary>Very likely plays it.</summary>
    Probably,
}

/// <summary>The forms in which a backend can hand out frames.</summary>
[Flags]
public enum VideoFrameKinds
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Opaque 8-bit pixels in CPU memory, BGRA or RGBA (<see cref="VideoFrameLease.CpuPixelFormat"/>).</summary>
    CpuBgra = 1,

    /// <summary>A GPU image importable through Avalonia's <c>ICompositionGpuInterop</c>.</summary>
    Gpu = 2,
}

/// <summary>A span of media time, such as a buffered range.</summary>
public readonly record struct VideoTimeRange(TimeSpan Start, TimeSpan End);

/// <summary>
/// One video stream that can be loaded, played, paused and sought, and that hands out its current frame on request.
/// Implementations raise every event on the <see cref="SynchronizationContext"/> current when the player was created (the UI
/// thread for controls), and report the position a few times a second while playing, like an HTML media element's
/// <c>timeupdate</c>. The backend keeps the clock and outputs the audio; the caller never schedules frames.
/// </summary>
public interface IVideoPlayer : IDisposable
{
    /// <summary>The backend that created this player.</summary>
    IVideoBackend Backend { get; }

    /// <summary>The current playback state.</summary>
    VideoPlaybackState State { get; }

    /// <summary>The playback position, clamped to <see cref="Duration"/>.</summary>
    TimeSpan Position { get; }

    /// <summary>The length; <see cref="TimeSpan.Zero"/> until known, and for live streams.</summary>
    TimeSpan Duration { get; }

    /// <summary>The picture size in pixels after the stream's rotation is applied; 0×0 until known.</summary>
    PixelSize NaturalSize { get; }

    /// <summary>The ranges that are downloaded and can play without waiting.</summary>
    IReadOnlyList<VideoTimeRange> Buffered { get; }

    /// <summary>Whether <see cref="Seek"/> works (false for some live streams).</summary>
    bool IsSeekable { get; }

    /// <summary>Output volume from 0 to 1, independent of <see cref="IsMuted"/>.</summary>
    double Volume { get; set; }

    /// <summary>Whether the audio is silenced without changing <see cref="Volume"/>.</summary>
    bool IsMuted { get; set; }

    /// <summary>Whether playback starts again from the beginning at the end instead of stopping.</summary>
    bool IsLooping { get; set; }

    /// <summary>Playback speed, 0.25 to 4; a backend clamps values it cannot play to the nearest it can.</summary>
    double PlaybackRate { get; set; }

    /// <summary>
    /// Where frames go, set by the surface that shows them (its GPU adapter and the handle types its compositor imports);
    /// null until a surface is attached. Backends read it when choosing the frame form and the device to decode on. Setting it
    /// (a surface attached, re-attached or moved to full screen) makes the next <see cref="TryAcquireFrame"/> return the current
    /// frame even when no new frame was decoded, so a paused video shows at once.
    /// </summary>
    VideoFrameTarget? FrameTarget { get; set; }

    /// <summary>Loads <paramref name="source"/>, replacing what was loaded; when it completes the player is paused at zero and <see cref="NaturalSize"/> is known.</summary>
    Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default);

    /// <summary>Starts or resumes playback; from the start when the player is at the end.</summary>
    void Play();

    /// <summary>Pauses playback and keeps the position.</summary>
    void Pause();

    /// <summary>Moves the playback position, clamped to the length.</summary>
    void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate);

    /// <summary>
    /// Hands out the frame to show at <see cref="VideoFrameRequest.HostTime"/>; false when there is no frame or it has not
    /// changed since the last call. Called on the UI thread from the compositor's frame callback. The lease must be disposed
    /// before the next call; its pixels or GPU image stay valid until then.
    /// </summary>
    bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease);

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Raised while playing (about 4 times a second) and after a seek.</summary>
    event EventHandler? PositionChanged;

    /// <summary>Raised when <see cref="Duration"/> becomes known or changes.</summary>
    event EventHandler? DurationChanged;

    /// <summary>Raised when <see cref="NaturalSize"/> becomes known or changes.</summary>
    event EventHandler? NaturalSizeChanged;

    /// <summary>Raised when <see cref="Buffered"/> changes.</summary>
    event EventHandler? BufferedChanged;

    /// <summary>Raised when a new frame is ready while not playing (after a load or a seek); while playing, surfaces poll every frame instead.</summary>
    event EventHandler? FrameAvailable;

    /// <summary>Raised when loading or playback fails; <see cref="State"/> is then <see cref="VideoPlaybackState.Failed"/>.</summary>
    event EventHandler<VideoErrorEventArgs>? Failed;
}

/// <summary>Carries the reason a video player failed.</summary>
public sealed class VideoErrorEventArgs(VideoErrorKind kind, string message, Exception? exception = null) : EventArgs
{
    /// <summary>The kind of failure.</summary>
    public VideoErrorKind Kind { get; } = kind;

    /// <summary>A short, untranslated description for logs.</summary>
    public string Message { get; } = message;

    /// <summary>The underlying exception, when there is one.</summary>
    public Exception? Exception { get; } = exception;
}

/// <summary>Options for <see cref="IVideoBackend.CreatePlayer"/>.</summary>
public sealed class VideoPlayerOptions
{
    /// <summary>The default options.</summary>
    public static VideoPlayerOptions Default { get; } = new();

    /// <summary>Whether frames should be GPU images when the target accepts them; false forces CPU pixels.</summary>
    public bool PreferGpuFrames { get; init; } = true;
}

/// <summary>Creates video players for one platform; set <see cref="ArcVideo.Backend"/> to choose it.</summary>
public interface IVideoBackend
{
    /// <summary>A short name for diagnostics: <c>"mediafoundation"</c>, <c>"avfoundation"</c>, <c>"gstreamer"</c>, <c>"browser"</c> or <c>"simulated"</c>.</summary>
    string Name { get; }

    /// <summary>Whether the backend really decodes media; false for the simulated backend.</summary>
    bool IsReal { get; }

    /// <summary>The frame forms its players can hand out.</summary>
    VideoFrameKinds FrameKinds { get; }

    /// <summary>
    /// The GPU image handle types its players can produce (values of Avalonia's
    /// <c>KnownPlatformGraphicsExternalImageHandleTypes</c>); empty when <see cref="FrameKinds"/> has no <see cref="VideoFrameKinds.Gpu"/>.
    /// </summary>
    IReadOnlyList<string> GpuHandleTypes { get; }

    /// <summary>Whether a media type such as <c>video/mp4</c> or <c>video/mp4; codecs="avc1.42E01E"</c> can play.</summary>
    VideoSupport CanPlay(string mediaType);

    /// <summary>Creates a new player. The caller disposes it.</summary>
    IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null);
}

/// <summary>The process-wide video backend. Platform packages replace the default simulated backend at startup.</summary>
public static class ArcVideo
{
    private static IVideoBackend s_backend = SimulatedVideoBackend.Instance;

    /// <summary>The backend new players come from; defaults to <see cref="SimulatedVideoBackend.Instance"/>.</summary>
    public static IVideoBackend Backend
    {
        get => s_backend;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(value, s_backend))
            {
                return;
            }
            s_backend = value;
            BackendChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Raised on the setting thread when <see cref="Backend"/> changes.</summary>
    public static event EventHandler? BackendChanged;

    /// <summary>Creates a player from <see cref="Backend"/>.</summary>
    public static IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null) => s_backend.CreatePlayer(options);
}
