using Android.Graphics;
using Android.Media;
using Android.Runtime;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One <c>android.media.MediaPlayer</c> (see <see cref="AndroidVideoBackend"/>).
/// </summary>
/// <remarks>
/// <para>
/// The player's callbacks arrive on the looper of the thread that created it (the UI thread for <see cref="VideoPlayer"/>);
/// <see cref="VideoPlayerBase"/> raises the public events on the creating context. Calls into MediaPlayer happen on the
/// caller's thread, as the platform allows.
/// </para>
/// <para>
/// Pictures: once the video size is known an <c>ImageReader</c> (YUV_420_888) becomes the player's surface. Each
/// <see cref="TryAcquireFrame"/> takes the latest image, converts it to BGRA at the requested size into a buffer the player
/// owns (<see cref="VideoYuv"/>) and closes the image, so the decoder never waits for the UI. The last converted frame is
/// kept for a new frame target.
/// </para>
/// <para>
/// Embedded text: MediaPlayer's timed text tracks (MP4 3GPP timed text); each cue is reported from the position it
/// arrives at, without an end (the next cue, or an empty one, ends it).
/// </para>
/// </remarks>
internal sealed class AndroidVideoPlayer : VideoPlayerBase, IVideoFrameOwner, IVideoEmbeddedTextTracks
{
    private const int MaxImages = 3;
    private static readonly TimeSpan s_positionTick = TimeSpan.FromMilliseconds(250);
    private const int MediaInfoBufferingStart = 701;
    private const int MediaInfoBufferingEnd = 702;

    private readonly MediaPlayer _player = new();
    private readonly System.Threading.Timer _ticker;
    private ImageReader? _reader;
    private PixelSize _readerSize;
    private TaskCompletionSource? _prepared;
    private int _loadVersion;
    private bool _isPrepared;
    private bool _wantPlay;
    private bool _buffering;
    private bool _leased;
    private bool _redeliver;
    private long _frameToken;
    private byte[] _pixels = [];
    private PixelSize _pixelsSize;
    private bool _hasPixels;
    private TimeSpan _pixelsTime;
    private int[] _textIndexes = [];
    private int _selectedText = -1;
    private IReadOnlyList<VideoEmbeddedTextTrackInfo> _textTracks = [];

    public AndroidVideoPlayer(AndroidVideoBackend backend)
        : base(backend)
    {
        // While playing, position changes are reported about 4 times a second (MediaPlayer has no such callback).
        _ticker = new System.Threading.Timer(static state => ((AndroidVideoPlayer)state!).RaisePositionChanged(), this, Timeout.Infinite, Timeout.Infinite);
        _player.Prepared += OnPrepared;
        _player.Error += OnError;
        _player.Completion += OnCompletion;
        _player.Info += OnInfo;
        _player.BufferingUpdate += OnBufferingUpdate;
        _player.SeekComplete += OnSeekComplete;
        _player.VideoSizeChanged += OnVideoSizeChanged;
        _player.TimedText += OnTimedText;
    }

    public override TimeSpan Position
    {
        get
        {
            if (IsDisposed || !_isPrepared)
            {
                return TimeSpan.Zero;
            }
            var position = TimeSpan.FromMilliseconds(Math.Max(0, _player.CurrentPosition));
            return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
        }
    }

    public IReadOnlyList<VideoEmbeddedTextTrackInfo> EmbeddedTextTracks => _textTracks;

    public event EventHandler? EmbeddedTextTracksChanged;

    public event EventHandler<VideoEmbeddedCueEventArgs>? EmbeddedCue;

    public override async Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = ++_loadVersion;
        SetState(VideoPlaybackState.Loading);
        ResetMedia();
        Uri uri;
        try
        {
            uri = await VideoSourceFiles.MaterializeAsync(source, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (version == _loadVersion && !IsDisposed)
            {
                SetState(VideoPlaybackState.Idle);
            }
            throw;
        }
        catch (Exception exception)
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
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _prepared = prepared;
        try
        {
            _player.SetDataSource(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri);
            _player.Looping = IsLooping;
            ApplyGain();
            _player.PrepareAsync();
        }
        catch (Exception exception) when (exception is Java.Lang.Exception or InvalidOperationException or ArgumentException)
        {
            _prepared = null;
            Fail(VideoErrorKind.Network, exception.Message, exception);
            return;
        }
        using (cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), prepared))
        {
            try
            {
                await prepared.Task.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                if (version == _loadVersion && !IsDisposed)
                {
                    ResetMedia();
                    SetState(VideoPlaybackState.Idle);
                }
                throw;
            }
        }
    }

    public override void Play()
    {
        if (IsDisposed || !_isPrepared)
        {
            _wantPlay = true;
            return;
        }
        _wantPlay = true;
        if (State == VideoPlaybackState.Ended)
        {
            _player.SeekTo(0);
        }
        ApplyRate();
        _player.Start();
        _ticker.Change(s_positionTick, s_positionTick);
        SetState(_buffering ? VideoPlaybackState.Buffering : VideoPlaybackState.Playing);
        RaisePositionChanged();
    }

    public override void Pause()
    {
        _wantPlay = false;
        if (IsDisposed || !_isPrepared)
        {
            return;
        }
        if (_player.IsPlaying)
        {
            _player.Pause();
        }
        _ticker.Change(Timeout.Infinite, Timeout.Infinite);
        if (State is VideoPlaybackState.Playing or VideoPlaybackState.Buffering)
        {
            SetState(VideoPlaybackState.Paused);
        }
        RaisePositionChanged();
    }

    public override void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
    {
        if (IsDisposed || !_isPrepared)
        {
            return;
        }
        var ms = (long)Math.Clamp(position.TotalMilliseconds, 0, Math.Max(0, Duration.TotalMilliseconds));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _player.SeekTo(ms, mode == VideoSeekMode.Accurate ? MediaPlayerSeekMode.Closest : MediaPlayerSeekMode.PreviousSync);
        }
        else
        {
            _player.SeekTo((int)ms);
        }
        if (State == VideoPlaybackState.Ended)
        {
            SetState(VideoPlaybackState.Paused);
        }
        RaisePositionChanged();
    }

    public override bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease)
    {
        lease = default;
        if (IsDisposed || _leased || (request.Accepted & VideoFrameKinds.CpuBgra) == 0 || _reader is null)
        {
            return false;
        }
        var converted = ConvertLatest(request.TargetSize);
        if (!converted && !(_redeliver && _hasPixels))
        {
            return false;
        }
        _redeliver = false;
        _leased = true;
        var length = _pixelsSize.Width * _pixelsSize.Height * 4;
        lease = new VideoFrameLease(this, ++_frameToken, _pixelsSize, _pixelsTime, _pixels.AsSpan(0, length), _pixelsSize.Width * 4);
        return true;
    }

    void IVideoFrameOwner.ReleaseFrame(long token) => _leased = false;

    public void SelectEmbeddedTextTrack(int index)
    {
        if (IsDisposed || !_isPrepared || index == _selectedText || index >= _textIndexes.Length || index < -1)
        {
            return;
        }
        try
        {
            if (_selectedText >= 0)
            {
                _player.DeselectTrack(_textIndexes[_selectedText]);
            }
            if (index >= 0)
            {
                _player.SelectTrack(_textIndexes[index]);
            }
            _selectedText = index;
        }
        catch (Exception exception) when (exception is Java.Lang.Exception or InvalidOperationException)
        {
            // A track the device cannot render: leave the selection as it was.
            System.Diagnostics.Trace.TraceWarning($"TKCode.Arc.Video.Native: selecting text track {index} failed: {exception.Message}");
        }
    }

    protected override void OnOutputGainChanged() => ApplyGain();

    protected override void OnLoopingChanged()
    {
        if (!IsDisposed)
        {
            _player.Looping = IsLooping;
        }
    }

    protected override void OnPlaybackRateChanged(double oldRate)
    {
        if (!IsDisposed && _isPrepared && _player.IsPlaying)
        {
            ApplyRate();
        }
    }

    protected override void OnFrameTargetChanged() => _redeliver = true;

    protected override void Dispose(bool disposing)
    {
        _loadVersion++;
        _ticker.Dispose();
        _prepared?.TrySetCanceled();
        _prepared = null;
        _player.Prepared -= OnPrepared;
        _player.Error -= OnError;
        _player.Completion -= OnCompletion;
        _player.Info -= OnInfo;
        _player.BufferingUpdate -= OnBufferingUpdate;
        _player.SeekComplete -= OnSeekComplete;
        _player.VideoSizeChanged -= OnVideoSizeChanged;
        _player.TimedText -= OnTimedText;
        _player.Release();
        _player.Dispose();
        _reader?.Close();
        _reader?.Dispose();
        _reader = null;
        base.Dispose(disposing);
    }

    // ---- MediaPlayer callbacks (on the creating thread's looper).

    private void OnPrepared(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }
        _isPrepared = true;
        var duration = _player.Duration;
        SetDuration(duration > 0 ? TimeSpan.FromMilliseconds(duration) : TimeSpan.Zero);
        SetSeekable(duration > 0);
        if (_player.VideoWidth > 0 && _player.VideoHeight > 0)
        {
            EnsureReader(_player.VideoWidth, _player.VideoHeight);
        }
        ReadTextTracks();
        SetState(VideoPlaybackState.Paused);
        _prepared?.TrySetResult();
        _prepared = null;
        RaiseFrameAvailable();
        if (_wantPlay)
        {
            Play();
        }
    }

    private void OnError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        e.Handled = true;
        if (IsDisposed)
        {
            return;
        }
        var kind = (int)e.Extra switch
        {
            -1004 or -110 => VideoErrorKind.Network,
            -1007 => VideoErrorKind.Decode,
            -1010 => VideoErrorKind.Unsupported,
            _ => (int)e.What == 100 ? VideoErrorKind.Aborted : VideoErrorKind.Decode,
        };
        _isPrepared = false;
        Fail(kind, $"MediaPlayer error {(int)e.What} ({(int)e.Extra}).");
        _prepared?.TrySetResult();
        _prepared = null;
    }

    private void OnCompletion(object? sender, EventArgs e)
    {
        if (IsDisposed || IsLooping)
        {
            return;
        }
        _wantPlay = false;
        _ticker.Change(Timeout.Infinite, Timeout.Infinite);
        SetState(VideoPlaybackState.Ended);
        RaisePositionChanged();
    }

    private void OnInfo(object? sender, MediaPlayer.InfoEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }
        switch ((int)e.What)
        {
            case MediaInfoBufferingStart:
                _buffering = true;
                if (State == VideoPlaybackState.Playing)
                {
                    SetState(VideoPlaybackState.Buffering);
                }
                break;
            case MediaInfoBufferingEnd:
                _buffering = false;
                if (State == VideoPlaybackState.Buffering)
                {
                    SetState(VideoPlaybackState.Playing);
                }
                break;
        }
    }

    private void OnBufferingUpdate(object? sender, MediaPlayer.BufferingUpdateEventArgs e)
    {
        if (!IsDisposed && Duration > TimeSpan.Zero)
        {
            SetBuffered([new VideoTimeRange(TimeSpan.Zero, Duration * Math.Clamp(e.Percent, 0, 100) / 100.0)]);
        }
    }

    private void OnSeekComplete(object? sender, EventArgs e)
    {
        if (!IsDisposed)
        {
            RaisePositionChanged();
            RaiseFrameAvailable();
        }
    }

    private void OnVideoSizeChanged(object? sender, MediaPlayer.VideoSizeChangedEventArgs e)
    {
        if (!IsDisposed && e.Width > 0 && e.Height > 0)
        {
            EnsureReader(e.Width, e.Height);
        }
    }

    private void OnTimedText(object? sender, MediaPlayer.TimedTextEventArgs e)
    {
        if (IsDisposed || _selectedText < 0)
        {
            return;
        }
        var text = e.Text?.Text ?? "";
        var markup = text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
        EmbeddedCue?.Invoke(this, new VideoEmbeddedCueEventArgs(_selectedText, Position, null, markup));
    }

    // ---- Helpers.

    private void EnsureReader(int width, int height)
    {
        var size = new PixelSize(width, height);
        if (_reader is not null && _readerSize == size)
        {
            return;
        }
        var old = _reader;
        _reader = ImageReader.NewInstance(width, height, ImageFormatType.Yuv420888, MaxImages);
        _readerSize = size;
        _player.SetSurface(_reader.Surface);
        old?.Close();
        old?.Dispose();
        SetNaturalSize(size);
    }

    /// <summary>Takes the newest image, converts it at the fitted target size into <see cref="_pixels"/>. False when no new image waits.</summary>
    private unsafe bool ConvertLatest(PixelSize target)
    {
        using var image = _reader!.AcquireLatestImage();
        if (image is null)
        {
            return false;
        }
        try
        {
            var planes = image.GetPlanes();
            if (planes is not { Length: >= 3 })
            {
                return false;
            }
            var width = image.Width;
            var height = image.Height;
            var size = Fit(width, height, target);
            var length = size.Width * size.Height * 4;
            if (_pixels.Length < length)
            {
                _pixels = new byte[length];
            }
            VideoYuv.ToBgra(PlaneSpan(planes[0]), planes[0].RowStride, PlaneSpan(planes[1]), PlaneSpan(planes[2]),
                planes[1].RowStride, planes[1].PixelStride, width, height, _pixels, size.Width, size.Height);
            _pixelsSize = size;
            _pixelsTime = Position;
            _hasPixels = true;
            return true;
        }
        finally
        {
            image.Close();
        }
    }

    private static unsafe ReadOnlySpan<byte> PlaneSpan(Image.Plane plane)
    {
        var buffer = plane.Buffer!;
        var address = JNIEnv.GetDirectBufferAddress(buffer.Handle);
        return address == 0 ? [] : new ReadOnlySpan<byte>((void*)address, buffer.Capacity());
    }

    private static PixelSize Fit(int width, int height, PixelSize target)
    {
        var scale = 1.0;
        if (target.Width > 0 && target.Width < width)
        {
            scale = (double)target.Width / width;
        }
        if (target.Height > 0 && target.Height < height)
        {
            scale = Math.Min(scale, (double)target.Height / height);
        }
        return new PixelSize(Math.Max(1, (int)(width * scale + 0.5)), Math.Max(1, (int)(height * scale + 0.5)));
    }

    private void ReadTextTracks()
    {
        var indexes = new List<int>();
        var infos = new List<VideoEmbeddedTextTrackInfo>();
        var tracks = _player.GetTrackInfo() ?? [];
        for (var i = 0; i < tracks.Length; i++)
        {
            if (tracks[i].TrackType != MediaTrackType.Timedtext)
            {
                continue;
            }
            var language = tracks[i].Language;
            infos.Add(new VideoEmbeddedTextTrackInfo(infos.Count, VideoTextTrackKind.Subtitles,
                string.IsNullOrEmpty(language) || language == "und" ? null : language, null));
            indexes.Add(i);
        }
        _textIndexes = [.. indexes];
        _selectedText = -1;
        if (infos.Count > 0 || _textTracks.Count > 0)
        {
            _textTracks = infos;
            EmbeddedTextTracksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ResetMedia()
    {
        _prepared?.TrySetCanceled();
        _prepared = null;
        _isPrepared = false;
        _wantPlay = false;
        _buffering = false;
        _hasPixels = false;
        _ticker.Change(Timeout.Infinite, Timeout.Infinite);
        _player.Reset();
        if (_reader is not null)
        {
            _player.SetSurface(_reader.Surface);
        }
        SetDuration(TimeSpan.Zero);
        SetBuffered([]);
        SetSeekable(false);
        _textIndexes = [];
        _selectedText = -1;
        if (_textTracks.Count > 0)
        {
            _textTracks = [];
            EmbeddedTextTracksChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyGain()
    {
        if (!IsDisposed)
        {
            var gain = (float)EffectiveGain;
            _player.SetVolume(gain, gain);
        }
    }

    private void ApplyRate()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            return;
        }
        try
        {
            // Setting the speed starts playback, so it is only applied when playing or about to play.
            _player.PlaybackParams = (_player.PlaybackParams ?? new PlaybackParams()).SetSpeed((float)PlaybackRate)!;
        }
        catch (Java.Lang.IllegalStateException)
        {
        }
    }
}
