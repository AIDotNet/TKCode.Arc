using System.Diagnostics;

namespace TKCode.Arc;

/// <summary>
/// The default backend: players keep time with a clock and make no sound, so a player's UI works the same on every platform
/// (and in tests). The length comes from a WAVE file's header, else <see cref="SimulatedAudioBackend.FallbackDuration"/>.
/// </summary>
public sealed class SimulatedAudioBackend : IAudioBackend
{
    /// <summary>The shared instance.</summary>
    public static SimulatedAudioBackend Instance { get; } = new();

    /// <summary>The length reported for audio whose length cannot be read without decoding it.</summary>
    public static TimeSpan FallbackDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public string Name => "simulated";

    /// <inheritdoc />
    public bool IsAudible => false;

    /// <inheritdoc />
    public IAudioPlayer CreatePlayer() => new SimulatedAudioPlayer();
}

/// <summary>A silent <see cref="IAudioPlayer"/> driven by a stopwatch; reports the position every 250 ms while playing.</summary>
public sealed class SimulatedAudioPlayer : AudioPlayerBase
{
    private static readonly TimeSpan s_tick = TimeSpan.FromMilliseconds(250);
    private readonly Stopwatch _clock = new();
    private readonly Timer _timer;
    private TimeSpan _origin;

    /// <summary>Creates a player; events are raised on the current synchronization context.</summary>
    public SimulatedAudioPlayer()
    {
        _timer = new Timer(_ => OnTick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    public override TimeSpan Position
    {
        get
        {
            var position = _origin + _clock.Elapsed;
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
    public override Task LoadAsync(AudioSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        StopClock();
        _origin = TimeSpan.Zero;
        var duration = source is BytesAudioSource bytes ? WavFile.TryGetDuration(bytes.Data.Span) : null;
        SetDuration(duration ?? SimulatedAudioBackend.FallbackDuration);
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Play()
    {
        if (State is AudioPlaybackState.Idle or AudioPlaybackState.Failed or AudioPlaybackState.Playing)
        {
            return;
        }
        if (State == AudioPlaybackState.Ended)
        {
            _origin = TimeSpan.Zero;
        }
        _clock.Restart();
        _timer.Change(s_tick, s_tick);
        SetState(AudioPlaybackState.Playing);
    }

    /// <inheritdoc />
    public override void Pause()
    {
        if (State != AudioPlaybackState.Playing)
        {
            return;
        }
        _origin = Position;
        StopClock();
        SetState(AudioPlaybackState.Paused);
        RaisePositionChanged();
    }

    /// <inheritdoc />
    public override void Seek(TimeSpan position)
    {
        if (State is AudioPlaybackState.Idle or AudioPlaybackState.Failed)
        {
            return;
        }
        _origin = position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;
        if (State == AudioPlaybackState.Playing)
        {
            _clock.Restart();
        }
        else if (State == AudioPlaybackState.Ended && _origin < Duration)
        {
            SetState(AudioPlaybackState.Paused);
        }
        RaisePositionChanged();
    }

    private void OnTick()
    {
        if (IsDisposed || State != AudioPlaybackState.Playing)
        {
            return;
        }
        if (!IsLooping && _origin + _clock.Elapsed >= Duration)
        {
            _origin = Duration;
            StopClock();
            SetState(AudioPlaybackState.Ended);
        }
        RaisePositionChanged();
    }

    private void StopClock()
    {
        _clock.Reset();
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _timer.Dispose();
    }
}
