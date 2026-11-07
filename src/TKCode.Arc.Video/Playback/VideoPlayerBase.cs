using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Shared plumbing for <see cref="IVideoPlayer"/> implementations: state, size, volume, rate and looping fields, and raising
/// events on the <see cref="SynchronizationContext"/> captured at construction. Backends call the <c>Set…</c> and
/// <c>Raise…</c> helpers from any thread.
/// </summary>
public abstract class VideoPlayerBase : IVideoPlayer
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private VideoPlaybackState _state = VideoPlaybackState.Idle;
    private TimeSpan _duration;
    private PixelSize _naturalSize;
    private IReadOnlyList<VideoTimeRange> _buffered = [];
    private bool _isSeekable;
    private double _volume = 1;
    private bool _isMuted;
    private bool _isLooping;
    private double _playbackRate = 1;
    private VideoFrameTarget? _frameTarget;

    /// <summary>Creates the player for <paramref name="backend"/>.</summary>
    protected VideoPlayerBase(IVideoBackend backend)
    {
        Backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <inheritdoc />
    public IVideoBackend Backend { get; }

    /// <inheritdoc />
    public VideoPlaybackState State => _state;

    /// <inheritdoc />
    public abstract TimeSpan Position { get; }

    /// <inheritdoc />
    public TimeSpan Duration => _duration;

    /// <inheritdoc />
    public PixelSize NaturalSize => _naturalSize;

    /// <inheritdoc />
    public IReadOnlyList<VideoTimeRange> Buffered => _buffered;

    /// <inheritdoc />
    public bool IsSeekable => _isSeekable;

    /// <summary>True once <see cref="Dispose()"/> has run.</summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>The lowest <see cref="PlaybackRate"/> accepted.</summary>
    public const double MinPlaybackRate = 0.25;

    /// <summary>The highest <see cref="PlaybackRate"/> accepted.</summary>
    public const double MaxPlaybackRate = 4;

    /// <inheritdoc />
    public double Volume
    {
        get => _volume;
        set
        {
            var clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
            if (clamped != _volume)
            {
                _volume = clamped;
                OnOutputGainChanged();
            }
        }
    }

    /// <inheritdoc />
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (value != _isMuted)
            {
                _isMuted = value;
                OnOutputGainChanged();
            }
        }
    }

    /// <inheritdoc />
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (value != _isLooping)
            {
                _isLooping = value;
                OnLoopingChanged();
            }
        }
    }

    /// <inheritdoc />
    public double PlaybackRate
    {
        get => _playbackRate;
        set
        {
            var clamped = double.IsNaN(value) || value <= 0 ? 1 : Math.Clamp(value, MinPlaybackRate, MaxPlaybackRate);
            if (clamped != _playbackRate)
            {
                var old = _playbackRate;
                _playbackRate = clamped;
                OnPlaybackRateChanged(old);
            }
        }
    }

    /// <inheritdoc />
    public VideoFrameTarget? FrameTarget
    {
        get => _frameTarget;
        set
        {
            if (!Equals(value, _frameTarget))
            {
                _frameTarget = value;
                OnFrameTargetChanged();
            }
        }
    }

    /// <summary>The gain the output should use: <see cref="Volume"/>, or 0 when muted.</summary>
    protected double EffectiveGain => _isMuted ? 0 : _volume;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler? PositionChanged;

    /// <inheritdoc />
    public event EventHandler? DurationChanged;

    /// <inheritdoc />
    public event EventHandler? NaturalSizeChanged;

    /// <inheritdoc />
    public event EventHandler? BufferedChanged;

    /// <inheritdoc />
    public event EventHandler? FrameAvailable;

    /// <inheritdoc />
    public event EventHandler<VideoErrorEventArgs>? Failed;

    /// <inheritdoc />
    public abstract Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract void Play();

    /// <inheritdoc />
    public abstract void Pause();

    /// <inheritdoc />
    public abstract void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate);

    /// <inheritdoc />
    public abstract bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease);

    /// <summary>Called when <see cref="Volume"/> or <see cref="IsMuted"/> changes; apply <see cref="EffectiveGain"/> to the output.</summary>
    protected virtual void OnOutputGainChanged()
    {
    }

    /// <summary>Called when <see cref="IsLooping"/> changes.</summary>
    protected virtual void OnLoopingChanged()
    {
    }

    /// <summary>Called when <see cref="PlaybackRate"/> changes; <paramref name="oldRate"/> is the previous value.</summary>
    protected virtual void OnPlaybackRateChanged(double oldRate)
    {
    }

    /// <summary>Called when <see cref="FrameTarget"/> changes.</summary>
    protected virtual void OnFrameTargetChanged()
    {
    }

    /// <summary>Records a new state and raises <see cref="StateChanged"/> when it differs.</summary>
    protected void SetState(VideoPlaybackState state)
    {
        if (_state == state)
        {
            return;
        }
        _state = state;
        Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Records the length and raises <see cref="DurationChanged"/> when it differs.</summary>
    protected void SetDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }
        if (_duration == duration)
        {
            return;
        }
        _duration = duration;
        Post(() => DurationChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Records the picture size and raises <see cref="NaturalSizeChanged"/> when it differs.</summary>
    protected void SetNaturalSize(PixelSize size)
    {
        if (_naturalSize == size)
        {
            return;
        }
        _naturalSize = size;
        Post(() => NaturalSizeChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Records the buffered ranges and raises <see cref="BufferedChanged"/> when they differ.</summary>
    protected void SetBuffered(IReadOnlyList<VideoTimeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (_buffered.SequenceEqual(ranges))
        {
            return;
        }
        _buffered = ranges.ToArray();
        Post(() => BufferedChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Records whether seeking works.</summary>
    protected void SetSeekable(bool isSeekable) => _isSeekable = isSeekable;

    /// <summary>Raises <see cref="PositionChanged"/>.</summary>
    protected void RaisePositionChanged() => Post(() => PositionChanged?.Invoke(this, EventArgs.Empty));

    /// <summary>Raises <see cref="FrameAvailable"/>.</summary>
    protected void RaiseFrameAvailable() => Post(() => FrameAvailable?.Invoke(this, EventArgs.Empty));

    /// <summary>Moves to <see cref="VideoPlaybackState.Failed"/> and raises <see cref="Failed"/>.</summary>
    protected void Fail(VideoErrorKind kind, string message, Exception? exception = null)
    {
        SetState(VideoPlaybackState.Failed);
        var args = new VideoErrorEventArgs(kind, message, exception);
        Post(() => Failed?.Invoke(this, args));
    }

    /// <summary>Runs <paramref name="action"/> on the captured context, or inline when there is none or we are already on it.</summary>
    protected void Post(Action action)
    {
        if (IsDisposed)
        {
            return;
        }
        if (_context is null || SynchronizationContext.Current == _context)
        {
            action();
            return;
        }
        _context.Post(static state => ((Action)state!)(), action);
    }

    /// <summary>Releases native resources; called once.</summary>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }
        Dispose(true);
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
