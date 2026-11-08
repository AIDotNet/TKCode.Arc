using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// One HTML video element outside the document. Its events drive the player's events; WebAssembly runs on the browser's main
/// thread, so they arrive on the UI thread. Frames: the module copies each presented frame (scaled to the surface's size on
/// the GPU) into a JavaScript buffer while a surface is asking for frames; <see cref="TryAcquireFrame"/> copies that buffer
/// into a reused .NET array, one copy per shown frame. A <c>play()</c> the browser refuses because no user gesture preceded
/// it leaves the player paused instead of failed.
/// </summary>
internal sealed class BrowserVideoPlayer : VideoPlayerBase, IVideoFrameOwner, IVideoNativePresentation
{
    private readonly JSObject _player;
    private int _loadVersion;
    private byte[] _pixels = [];
    private bool _leased;
    private long _frameToken;
    private bool _native;
    private VideoTextTrack? _nativeCaptions;

    public BrowserVideoPlayer(BrowserVideoBackend backend)
        : base(backend)
    {
        _player = BrowserVideoInterop.Create(OnEvent);
        BrowserVideoInterop.SetVolume(_player, Volume);
        BrowserVideoInterop.SetCrossOrigin(_player, backend.CrossOrigin);
    }

    public override TimeSpan Position =>
        IsDisposed || State is VideoPlaybackState.Idle or VideoPlaybackState.Loading
            ? TimeSpan.Zero
            : Clamp(TimeSpan.FromSeconds(BrowserVideoInterop.CurrentTime(_player)));

    public override async Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = ++_loadVersion;
        SetState(VideoPlaybackState.Loading);
        SetBuffered([]);
        double result;
        try
        {
            var (url, owned) = await UrlOf(source, cancellationToken);
            if (version != _loadVersion || IsDisposed)
            {
                return;
            }
            result = await BrowserVideoInterop.LoadUrl(_player, url, owned).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (version == _loadVersion && !IsDisposed)
            {
                SetState(VideoPlaybackState.Idle);
            }
            throw;
        }
        catch (Exception exception) when (exception is JSException or NotSupportedException or IOException)
        {
            if (version == _loadVersion && !IsDisposed)
            {
                Fail(exception is NotSupportedException ? VideoErrorKind.Unsupported : VideoErrorKind.Network, exception.Message, exception);
            }
            return;
        }
        if (version != _loadVersion || IsDisposed)
        {
            return;
        }
        if (result < 0)
        {
            var code = (int)-result;
            Fail(KindOf(code), $"Media error {code}.");
            return;
        }
        SetDuration(TimeSpan.FromSeconds(result));
        SetSeekable(BrowserVideoInterop.IsSeekable(_player));
        UpdateBuffered();
        SetState(VideoPlaybackState.Paused);
        RaisePositionChanged();
        RaiseFrameAvailable();
    }

    public override async void Play()
    {
        if (IsDisposed || State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed or VideoPlaybackState.Playing
            or VideoPlaybackState.Buffering)
        {
            return;
        }
        var version = _loadVersion;
        if (State == VideoPlaybackState.Ended)
        {
            BrowserVideoInterop.Seek(_player, 0, false);
        }
        // Optimistic, like the element's own "play" event: the UI flips at once and falls back if the browser refuses.
        SetState(VideoPlaybackState.Playing);
        bool started;
        try
        {
            started = await BrowserVideoInterop.Play(_player);
        }
        catch (Exception exception)
        {
            // async void: nothing may escape, or it would crash the app.
            if (version == _loadVersion && !IsDisposed)
            {
                Fail(VideoErrorKind.Decode, exception.Message, exception);
            }
            return;
        }
        if (!started && version == _loadVersion && !IsDisposed && State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering)
        {
            SetState(VideoPlaybackState.Paused);
        }
    }

    public override void Pause()
    {
        if (IsDisposed || State is not (VideoPlaybackState.Playing or VideoPlaybackState.Buffering))
        {
            return;
        }
        BrowserVideoInterop.Pause(_player);
        SetState(VideoPlaybackState.Paused);
        RaisePositionChanged();
    }

    public override void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
    {
        if (IsDisposed || State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed)
        {
            return;
        }
        var clamped = Clamp(position);
        BrowserVideoInterop.Seek(_player, clamped.TotalSeconds, mode == VideoSeekMode.Fast);
        if (State == VideoPlaybackState.Ended && clamped < Duration)
        {
            SetState(VideoPlaybackState.Paused);
        }
        RaisePositionChanged();
    }

    public override bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease)
    {
        lease = default;
        if (IsDisposed || _leased || _native || (request.Accepted & VideoFrameKinds.CpuBgra) == 0
            || State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed)
        {
            return false;
        }
        var packed = BrowserVideoInterop.PeekFrame(_player, Math.Max(0, request.TargetSize.Width), Math.Max(0, request.TargetSize.Height));
        if (packed <= 0)
        {
            return false;
        }
        var width = (int)(packed / 65536);
        var height = (int)(packed % 65536);
        var length = width * height * 4;
        if (_pixels.Length < length)
        {
            _pixels = new byte[length];
        }
        var pixels = _pixels.AsSpan(0, length);
        var seconds = BrowserVideoInterop.TakeFrame(_player, pixels);
        _leased = true;
        lease = new VideoFrameLease(this, ++_frameToken, new PixelSize(width, height), TimeSpan.FromSeconds(seconds), pixels, width * 4,
            pixelFormat: VideoCpuPixelFormat.Rgba8888);
        return true;
    }

    void IVideoFrameOwner.ReleaseFrame(long token) => _leased = false;

    protected override void OnOutputGainChanged()
    {
        if (IsDisposed)
        {
            return;
        }
        BrowserVideoInterop.SetVolume(_player, Volume);
        BrowserVideoInterop.SetMuted(_player, IsMuted);
    }

    protected override void OnLoopingChanged()
    {
        if (!IsDisposed)
        {
            BrowserVideoInterop.SetLoop(_player, IsLooping);
        }
    }

    /// <summary>A new surface shows this player: the next <see cref="TryAcquireFrame"/> returns the current frame (IVideoPlayer.FrameTarget).</summary>
    protected override void OnFrameTargetChanged()
    {
        if (!IsDisposed)
        {
            BrowserVideoInterop.ResendFrame(_player);
        }
    }

    protected override void OnPlaybackRateChanged(double oldRate)
    {
        if (!IsDisposed)
        {
            BrowserVideoInterop.SetRate(_player, PlaybackRate);
        }
    }

    private void OnEvent(string kind, double value)
    {
        if (IsDisposed)
        {
            return;
        }
        switch (kind)
        {
            case "time":
                RaisePositionChanged();
                break;
            case "seeked":
                RaisePositionChanged();
                break;
            case "duration":
                SetDuration(TimeSpan.FromSeconds(value));
                SetSeekable(BrowserVideoInterop.IsSeekable(_player));
                break;
            case "size":
            {
                var packed = (long)value;
                SetNaturalSize(new PixelSize((int)(packed / 65536), (int)(packed % 65536)));
                break;
            }
            case "buffered":
                UpdateBuffered();
                break;
            case "waiting":
                if (State == VideoPlaybackState.Playing)
                {
                    SetState(VideoPlaybackState.Buffering);
                }
                break;
            case "playing":
                // Also after the browser resumed on its own; a "playing" queued before a Pause() call finds the element paused.
                if (State is VideoPlaybackState.Buffering or VideoPlaybackState.Paused && !BrowserVideoInterop.IsPaused(_player))
                {
                    SetState(VideoPlaybackState.Playing);
                }
                break;
            case "pause":
                // Paused by the browser (another tab took the media focus, the element left the document).
                if (State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering)
                {
                    SetState(VideoPlaybackState.Paused);
                }
                break;
            case "ended":
                if (!IsLooping)
                {
                    SetState(VideoPlaybackState.Ended);
                    RaisePositionChanged();
                }
                break;
            case "error":
                if (State is not (VideoPlaybackState.Idle or VideoPlaybackState.Loading))
                {
                    Fail(KindOf((int)value), $"Media error {value}.");
                }
                break;
            case "tainted":
                // A cross-origin video served without CORS headers: the browser plays it but its frames cannot be read.
                if (State != VideoPlaybackState.Failed)
                {
                    BrowserVideoInterop.Pause(_player);
                    Fail(VideoErrorKind.Network,
                        "The video's frames cannot be read: it is cross-origin and its server sends no CORS headers (Access-Control-Allow-Origin).");
                }
                break;
            case "frame":
                if (State != VideoPlaybackState.Playing)
                {
                    RaiseFrameAvailable();
                }
                break;
        }
    }

    private void UpdateBuffered()
    {
        var values = BrowserVideoInterop.Buffered(_player);
        var ranges = new VideoTimeRange[values.Length / 2];
        for (var i = 0; i < ranges.Length; i++)
        {
            ranges[i] = new VideoTimeRange(TimeSpan.FromSeconds(values[2 * i]), TimeSpan.FromSeconds(values[2 * i + 1]));
        }
        SetBuffered(ranges);
    }

    // MediaError codes: 1 aborted, 2 network, 3 decode, 4 source not supported (also what browsers report for a missing file).
    private static VideoErrorKind KindOf(int code) => code switch
    {
        1 => VideoErrorKind.Aborted,
        2 => VideoErrorKind.Network,
        3 => VideoErrorKind.Decode,
        _ => VideoErrorKind.Unsupported,
    };

    private static async Task<(string Url, bool Owned)> UrlOf(VideoSource source, CancellationToken cancellationToken)
    {
        switch (source)
        {
            case UriVideoSource { Uri.Scheme: "http" or "https" or "blob" or "data" } uri:
                return (uri.Uri.AbsoluteUri, false);
            case UriVideoSource { Uri.Scheme: "avares" } asset:
            {
                await using var stream = AssetLoader.Open(asset.Uri);
                var data = await ReadAllAsync(stream, cancellationToken);
                return (BlobUrl(data, MediaTypeOf(asset.Uri.AbsolutePath)), true);
            }
            case BytesVideoSource bytes:
                return (BlobUrl(bytes.Data, bytes.MediaType), true);
            case StreamVideoSource streamSource:
            {
                await using var stream = streamSource.Open();
                var data = await ReadAllAsync(stream, cancellationToken);
                return (BlobUrl(data, streamSource.MediaType), true);
            }
            case UriVideoSource uri:
                throw new NotSupportedException($"A browser cannot open {uri.Uri.Scheme}: addresses; use http(s), avares, bytes or a stream.");
            default:
                throw new NotSupportedException($"The browser backend cannot play a {source.GetType().Name}.");
        }
    }

    private static async Task<ReadOnlyMemory<byte>> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = stream.CanSeek ? new MemoryStream(checked((int)Math.Max(0, stream.Length - stream.Position))) : new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
    }

    private static string BlobUrl(ReadOnlyMemory<byte> data, string mediaType)
    {
        // A MemoryView needs a writable Span; use the backing array when there is one (JavaScript copies it), else copy.
        Span<byte> span = MemoryMarshal.TryGetArray(data, out var segment) ? segment.AsSpan() : data.ToArray();
        return BrowserVideoInterop.BlobUrl(span, mediaType);
    }

    private static string MediaTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".webm" => "video/webm",
        ".ogv" => "video/ogg",
        ".mov" => "video/quicktime",
        _ => "video/mp4",
    };

    private TimeSpan Clamp(TimeSpan position) =>
        position < TimeSpan.Zero ? TimeSpan.Zero : Duration > TimeSpan.Zero && position > Duration ? Duration : position;

    // ---- Native presentation ----

    public bool CanPresentNatively =>
#if ARC_VIDEO_NATIVE
        !IsDisposed;
#else
        false;
#endif

    public Control CreateNativeView()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
#if ARC_VIDEO_NATIVE
        return new BrowserVideoNativeHost(this);
#else
        throw new NotSupportedException("Native video presentation needs the net10.0 build of TKCode.Arc.Video.Browser (Avalonia.Browser).");
#endif
    }

    public void SetNativeCaptions(VideoTextTrack? track)
    {
        if (ReferenceEquals(track, _nativeCaptions))
        {
            PushNativeCaptions();
            return;
        }
        if (_nativeCaptions is not null)
        {
            _nativeCaptions.CuesChanged -= OnNativeCaptionCuesChanged;
        }
        _nativeCaptions = track;
        if (track is not null)
        {
            track.CuesChanged += OnNativeCaptionCuesChanged;
        }
        PushNativeCaptions();
    }

    private void OnNativeCaptionCuesChanged(object? sender, EventArgs e) => PushNativeCaptions();

    private void PushNativeCaptions()
    {
        if (IsDisposed)
        {
            return;
        }
        var cues = _nativeCaptions?.Cues ?? [];
        var starts = new double[cues.Count];
        var ends = new double[cues.Count];
        var texts = new string[cues.Count];
        for (var i = 0; i < cues.Count; i++)
        {
            starts[i] = cues[i].Start.TotalSeconds;
            ends[i] = cues[i].End.TotalSeconds;
            texts[i] = NativeCaptionText(cues[i]);
        }
        BrowserVideoInterop.SetCaptions(_player, starts, ends, texts);
    }

    /// <summary>The cue as WebVTT cue text: its bold, italic and underlined runs as tags, everything else escaped.</summary>
    internal static string NativeCaptionText(VideoCue cue)
    {
        if (cue.Runs.Count == 0)
        {
            return EscapeCueText(cue.Text);
        }
        var builder = new System.Text.StringBuilder();
        foreach (var run in cue.Runs)
        {
            var text = EscapeCueText(run.Text);
            if (run.IsUnderline)
            {
                text = "<u>" + text + "</u>";
            }
            if (run.IsItalic)
            {
                text = "<i>" + text + "</i>";
            }
            if (run.IsBold)
            {
                text = "<b>" + text + "</b>";
            }
            builder.Append(text);
        }
        return builder.ToString();
    }

    private static string EscapeCueText(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Hands the element to a native host; frames stop being copied until <see cref="DetachNative"/>.</summary>
    internal JSObject AttachNative()
    {
        _native = true;
        return BrowserVideoInterop.AttachNative(_player);
    }

    internal void DetachNative()
    {
        _native = false;
        if (!IsDisposed)
        {
            BrowserVideoInterop.DetachNative(_player);
            RaiseFrameAvailable();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_nativeCaptions is not null)
        {
            _nativeCaptions.CuesChanged -= OnNativeCaptionCuesChanged;
            _nativeCaptions = null;
        }
        _loadVersion++;
        BrowserVideoInterop.Dispose(_player);
        _player.Dispose();
        _pixels = [];
    }
}
