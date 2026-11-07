using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The default backend: players keep time with a clock, make no sound and draw a generated test picture, so a video UI works
/// the same everywhere (and in tests) when no platform backend is installed. <see cref="SimulatedVideoSource"/> sets the clip's
/// size and length; any other source loads as a <see cref="FallbackSize"/>, <see cref="FallbackDuration"/> clip.
/// </summary>
public sealed class SimulatedVideoBackend : IVideoBackend
{
    /// <summary>The shared instance, on the system clock.</summary>
    public static SimulatedVideoBackend Instance { get; } = new(TimeProvider.System);

    /// <summary>Creates a backend on <paramref name="timeProvider"/> (a fake clock in tests).</summary>
    public SimulatedVideoBackend(TimeProvider timeProvider)
    {
        TimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>The clock players run on.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>The length of a non-simulated source.</summary>
    public static TimeSpan FallbackDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>The picture size of a non-simulated source.</summary>
    public static PixelSize FallbackSize { get; set; } = new(1280, 720);

    /// <summary>The generated picture's frame rate.</summary>
    public const int FramesPerSecond = 30;

    /// <inheritdoc />
    public string Name => "simulated";

    /// <inheritdoc />
    public bool IsReal => false;

    /// <inheritdoc />
    public VideoFrameKinds FrameKinds => VideoFrameKinds.CpuBgra;

    /// <inheritdoc />
    public IReadOnlyList<string> GpuHandleTypes => [];

    /// <inheritdoc />
    public VideoSupport CanPlay(string mediaType) => VideoSupport.Maybe;

    /// <inheritdoc />
    public IVideoPlayer CreatePlayer(VideoPlayerOptions? options = null) => new SimulatedVideoPlayer(this);
}

/// <summary>
/// A silent <see cref="IVideoPlayer"/> on a clock that draws a test picture: a gradient that drifts with time, a white bar at
/// the playback fraction, and the frame number as 16 black/white blocks along the top row band (bit 15 first), so tests can
/// read which frame is shown. Reports the position every 250 ms while playing.
/// </summary>
public sealed class SimulatedVideoPlayer : VideoPlayerBase, IVideoFrameOwner
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMilliseconds(250);
    private readonly SimulatedVideoBackend _backend;
    private ITimer? _timer;
    private long _startTimestamp;
    private bool _running;
    private TimeSpan _origin;
    private long _lastFrameIndex = -1;
    private PixelSize _lastFrameSize;
    private byte[] _pixels = [];
    private bool _leased;
    private bool _buffering;

    /// <summary>Creates a player on <paramref name="backend"/>'s clock; events are raised on the current synchronization context.</summary>
    public SimulatedVideoPlayer(SimulatedVideoBackend backend)
        : base(backend)
    {
        _backend = backend;
    }

    /// <summary>How many frames were handed out; for tests.</summary>
    public int FramesDelivered { get; private set; }

    /// <inheritdoc />
    public override TimeSpan Position
    {
        get
        {
            var position = _origin + (_running ? Elapsed() : TimeSpan.Zero);
            if (Duration <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }
            if (IsLooping)
            {
                return TimeSpan.FromTicks(position.Ticks % Duration.Ticks);
            }
            return position < Duration ? position : Duration;
        }
    }

    /// <inheritdoc />
    public override Task LoadAsync(VideoSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        StopClock();
        _origin = TimeSpan.Zero;
        _lastFrameIndex = -1;
        _buffering = false;
        SetState(VideoPlaybackState.Loading);
        var (size, duration) = source is SimulatedVideoSource simulated
            ? (simulated.Size, simulated.Duration)
            : (SimulatedVideoBackend.FallbackSize, SimulatedVideoBackend.FallbackDuration);
        SetNaturalSize(size);
        SetDuration(duration);
        SetSeekable(true);
        SetBuffered([new VideoTimeRange(TimeSpan.Zero, duration)]);
        SetState(VideoPlaybackState.Paused);
        RaisePositionChanged();
        RaiseFrameAvailable();
        return Task.CompletedTask;
    }

    /// <summary>Simulates waiting for data while playing (<see cref="VideoPlaybackState.Buffering"/>; the clock stops) and its end.</summary>
    public void SetBuffering(bool buffering)
    {
        if (buffering == _buffering)
        {
            return;
        }
        _buffering = buffering;
        if (buffering && State == VideoPlaybackState.Playing)
        {
            _origin = Position;
            StopClock();
            SetState(VideoPlaybackState.Buffering);
        }
        else if (!buffering && State == VideoPlaybackState.Buffering)
        {
            StartClock();
            SetState(VideoPlaybackState.Playing);
        }
    }

    /// <summary>Fails the player as a backend would; for tests.</summary>
    public void SimulateFailure(VideoErrorKind kind, string message = "Simulated failure.")
    {
        StopClock();
        Fail(kind, message);
    }

    /// <inheritdoc />
    public override void Play()
    {
        if (State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed or VideoPlaybackState.Playing or VideoPlaybackState.Buffering)
        {
            return;
        }
        if (State == VideoPlaybackState.Ended)
        {
            _origin = TimeSpan.Zero;
        }
        if (_buffering)
        {
            SetState(VideoPlaybackState.Buffering);
            return;
        }
        StartClock();
        SetState(VideoPlaybackState.Playing);
    }

    /// <inheritdoc />
    public override void Pause()
    {
        if (State is not (VideoPlaybackState.Playing or VideoPlaybackState.Buffering))
        {
            return;
        }
        _origin = Position;
        StopClock();
        SetState(VideoPlaybackState.Paused);
        RaisePositionChanged();
    }

    /// <inheritdoc />
    public override void Seek(TimeSpan position, VideoSeekMode mode = VideoSeekMode.Accurate)
    {
        if (State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed)
        {
            return;
        }
        if (mode == VideoSeekMode.Fast)
        {
            // Snap to whole seconds, standing in for key frames.
            position = TimeSpan.FromSeconds(Math.Floor(position.TotalSeconds));
        }
        _origin = position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;
        if (_running)
        {
            _startTimestamp = _backend.TimeProvider.GetTimestamp();
        }
        else if (State == VideoPlaybackState.Ended && _origin < Duration)
        {
            SetState(VideoPlaybackState.Paused);
        }
        _lastFrameIndex = -1;
        RaisePositionChanged();
        if (!_running)
        {
            RaiseFrameAvailable();
        }
    }

    /// <summary>Advances the clock check by hand (the timer does this every 250 ms); for tests with a fake clock.</summary>
    public void Tick() => OnTick();

    /// <inheritdoc />
    public override bool TryAcquireFrame(in VideoFrameRequest request, out VideoFrameLease lease)
    {
        lease = default;
        if (IsDisposed || _leased || NaturalSize.Width <= 0 || NaturalSize.Height <= 0 || (request.Accepted & VideoFrameKinds.CpuBgra) == 0
            || State is VideoPlaybackState.Idle or VideoPlaybackState.Loading or VideoPlaybackState.Failed)
        {
            return false;
        }
        var size = FitWithin(NaturalSize, request.TargetSize);
        var index = (long)Math.Floor(Position.TotalSeconds * SimulatedVideoBackend.FramesPerSecond);
        if (index == _lastFrameIndex && size == _lastFrameSize)
        {
            return false;
        }
        _lastFrameIndex = index;
        _lastFrameSize = size;
        var rowBytes = size.Width * 4;
        if (_pixels.Length != rowBytes * size.Height)
        {
            _pixels = new byte[rowBytes * size.Height];
        }
        var fraction = Duration > TimeSpan.Zero ? Position.TotalSeconds / Duration.TotalSeconds : 0;
        Draw(_pixels, size, rowBytes, index, fraction);
        _leased = true;
        FramesDelivered++;
        lease = new VideoFrameLease(this, index, size, TimeSpan.FromSeconds(index / (double)SimulatedVideoBackend.FramesPerSecond), _pixels, rowBytes);
        return true;
    }

    /// <inheritdoc />
    void IVideoFrameOwner.ReleaseFrame(long token) => _leased = false;

    /// <summary>The frame number encoded in a test picture's top band; -1 when the pixels do not hold one. For tests.</summary>
    public static long ReadFrameNumber(ReadOnlySpan<byte> pixels, PixelSize size, int rowBytes)
    {
        if (size.Width < 16 || size.Height < 4)
        {
            return -1;
        }
        var y = Math.Max(1, size.Height / 40);
        long value = 0;
        for (var bit = 0; bit < 16; bit++)
        {
            var x = (int)((bit + 0.5) * size.Width / 16.0);
            var g = pixels[y * rowBytes + x * 4 + 1];
            value = (value << 1) | (g > 127 ? 1L : 0L);
        }
        return value;
    }

    private static PixelSize FitWithin(PixelSize natural, PixelSize target)
    {
        if (target.Width <= 0 || target.Height <= 0 || (natural.Width <= target.Width && natural.Height <= target.Height))
        {
            return natural;
        }
        var scale = Math.Min(target.Width / (double)natural.Width, target.Height / (double)natural.Height);
        return new PixelSize(Math.Max(16, (int)Math.Round(natural.Width * scale)), Math.Max(4, (int)Math.Round(natural.Height * scale)));
    }

    private static void Draw(byte[] pixels, PixelSize size, int rowBytes, long frame, double fraction)
    {
        var band = Math.Max(2, size.Height / 20);
        var barX = (int)(Math.Clamp(fraction, 0, 1) * (size.Width - 1));
        var barHalf = Math.Max(1, size.Width / 200);
        var drift = (int)(frame % 256);
        for (var y = 0; y < size.Height; y++)
        {
            var row = y * rowBytes;
            for (var x = 0; x < size.Width; x++)
            {
                var i = row + x * 4;
                byte b, g, r;
                if (y < band)
                {
                    var bit = 15 - Math.Min(15, x * 16 / size.Width);
                    var on = ((frame >> bit) & 1) != 0;
                    b = g = r = on ? (byte)255 : (byte)0;
                }
                else if (Math.Abs(x - barX) <= barHalf)
                {
                    b = g = r = 255;
                }
                else
                {
                    r = (byte)((x * 255 / Math.Max(1, size.Width - 1) + drift) & 0xFF);
                    g = (byte)(64 + y * 127 / Math.Max(1, size.Height - 1));
                    b = (byte)(255 - ((x + y) * 255 / Math.Max(1, size.Width + size.Height - 2)));
                }
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = 255;
            }
        }
    }

    private TimeSpan Elapsed() => _backend.TimeProvider.GetElapsedTime(_startTimestamp) * PlaybackRate;

    /// <inheritdoc />
    protected override void OnPlaybackRateChanged(double oldRate)
    {
        if (_running)
        {
            // Rebase so the new rate applies from now, not from the start of the run.
            var elapsed = _backend.TimeProvider.GetElapsedTime(_startTimestamp) * oldRate;
            _origin += elapsed;
            _startTimestamp = _backend.TimeProvider.GetTimestamp();
        }
    }

    private void StartClock()
    {
        _startTimestamp = _backend.TimeProvider.GetTimestamp();
        _running = true;
        // The timer fires on the thread pool; the clock fields belong to the creating thread, so the check runs there.
        _timer ??= _backend.TimeProvider.CreateTimer(static state => ((SimulatedVideoPlayer)state!).OnTimer(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(s_tick, s_tick);
    }

    private void StopClock()
    {
        _running = false;
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void OnTimer() => Post(OnTick);

    /// <inheritdoc />
    protected override void OnLoopingChanged()
    {
        if (_running && Duration > TimeSpan.Zero)
        {
            // Rebase onto the position within the clip: a looped run's raw time is past the end, which would end playback
            // at once when looping is turned off.
            var raw = _origin + Elapsed();
            _origin = TimeSpan.FromTicks(raw.Ticks % Duration.Ticks);
            _startTimestamp = _backend.TimeProvider.GetTimestamp();
        }
    }

    /// <inheritdoc />
    protected override void OnFrameTargetChanged()
    {
        // A surface that is attached again (or a new one) has no picture yet: hand out the current frame on the next request.
        _lastFrameIndex = -1;
    }

    private void OnTick()
    {
        if (IsDisposed || !_running)
        {
            return;
        }
        if (!IsLooping && _origin + Elapsed() >= Duration)
        {
            _origin = Duration;
            StopClock();
            SetState(VideoPlaybackState.Ended);
            RaiseFrameAvailable();
        }
        RaisePositionChanged();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _running = false;
        _timer?.Dispose();
    }
}
