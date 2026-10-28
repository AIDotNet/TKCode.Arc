namespace TKCode.Arc;

/// <summary>
/// Shared plumbing for <see cref="IAudioPlayer"/> implementations: state, volume and looping fields, and raising events on the
/// <see cref="SynchronizationContext"/> captured at construction. Backends call the <c>Set…</c> and <c>Raise…</c> helpers from any thread.
/// </summary>
public abstract class AudioPlayerBase : IAudioPlayer
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private AudioPlaybackState _state = AudioPlaybackState.Idle;
    private TimeSpan _duration;
    private double _volume = 1;
    private bool _isMuted;
    private bool _isLooping;

    /// <inheritdoc />
    public AudioPlaybackState State => _state;

    /// <inheritdoc />
    public abstract TimeSpan Position { get; }

    /// <inheritdoc />
    public TimeSpan Duration => _duration;

    /// <summary>True once <see cref="Dispose()"/> has run.</summary>
    protected bool IsDisposed { get; private set; }

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

    /// <summary>The gain the output should use: <see cref="Volume"/>, or 0 when muted.</summary>
    protected double EffectiveGain => _isMuted ? 0 : _volume;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler? PositionChanged;

    /// <inheritdoc />
    public event EventHandler? DurationChanged;

    /// <inheritdoc />
    public event EventHandler<AudioErrorEventArgs>? Failed;

    /// <inheritdoc />
    public abstract Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract void Play();

    /// <inheritdoc />
    public abstract void Pause();

    /// <inheritdoc />
    public abstract void Seek(TimeSpan position);

    /// <summary>Called when <see cref="Volume"/> or <see cref="IsMuted"/> changes; apply <see cref="EffectiveGain"/> to the output.</summary>
    protected virtual void OnOutputGainChanged()
    {
    }

    /// <summary>Called when <see cref="IsLooping"/> changes.</summary>
    protected virtual void OnLoopingChanged()
    {
    }

    /// <summary>Records a new state and raises <see cref="StateChanged"/> when it differs.</summary>
    protected void SetState(AudioPlaybackState state)
    {
        if (_state == state)
        {
            return;
        }
        _state = state;
        Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Records the audio's length and raises <see cref="DurationChanged"/> when it differs.</summary>
    protected void SetDuration(TimeSpan duration)
    {
        if (_duration == duration)
        {
            return;
        }
        _duration = duration;
        Post(() => DurationChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Raises <see cref="PositionChanged"/>.</summary>
    protected void RaisePositionChanged() => Post(() => PositionChanged?.Invoke(this, EventArgs.Empty));

    /// <summary>Moves to <see cref="AudioPlaybackState.Failed"/> and raises <see cref="Failed"/>.</summary>
    protected void Fail(string message, Exception? exception = null)
    {
        SetState(AudioPlaybackState.Failed);
        var args = new AudioErrorEventArgs(message, exception);
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

    /// <summary>Releases the output; called once.</summary>
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
