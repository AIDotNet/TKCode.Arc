using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The shared seekable clock of the Special › AI components (the replay clock of <see cref="AgentTimeline"/>): a seekable story time (seconds of the run) on a Core
/// <see cref="LiveLoop"/>. Unlike <see cref="Explainer"/>'s forward-only beat script, the timeline's picture is a pure
/// function of the time, so the clock seeks both ways at once. It plays at <see cref="Speed"/>, caps a single frame at
/// <see cref="MaxStep"/> of story time per unit of speed (a stalled frame loses time instead of jumping, as measured:
/// 0.1 s), stops at <see cref="Duration"/>, and only runs while its owner is on screen (the loop's viewport gate: off
/// screen a playing replay waits). In live mode the time is the wall clock since the run started and speed does not
/// apply. Wall time (<see cref="WallTime"/>) keeps counting on every frame, also while paused, for the view's own
/// transitions (completion fades, connector reveals, the shine); the owner asks for those frames with
/// <see cref="RequestFrames"/> or <see cref="KeepsRunning"/>. Its loop runs at every motion level: the replay is
/// content, not decoration.
/// </summary>
internal sealed class AiPlaybackClock : IDisposable
{
    /// <summary>The most story time one frame can add at speed 1 (seconds).</summary>
    public const double MaxStep = 0.1;

    private readonly LiveLoop _loop;
    private readonly TimeProvider _time;
    private double _now;
    private double _duration;
    private double _speed = 1;
    private double _wall;
    private double _framesUntil;
    private bool _playing;
    private bool _live;
    private bool _keepsRunning;
    private DateTimeOffset _liveOrigin;

    public AiPlaybackClock(Visual owner, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _loop = new LiveLoop(owner, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <summary>Raised after every frame, with the frame's wall seconds (also when only wall time moved).</summary>
    public event Action<double>? Ticked;

    /// <summary>Raised when playing reaches <see cref="Duration"/> (the clock has stopped there).</summary>
    public event Action? ReachedEnd;

    /// <summary>The story time in seconds.</summary>
    public double Time => _now;

    /// <summary>Seconds of frames seen so far (also while paused): the clock of the view's own transitions.</summary>
    public double WallTime => _wall;

    /// <summary>The end of the replay; playing stops there.</summary>
    public double Duration
    {
        get => _duration;
        set
        {
            _duration = Math.Max(0, double.IsFinite(value) ? value : 0);
            if (!_live && _now > _duration)
            {
                _now = _duration;
            }
        }
    }

    /// <summary>The playback speed (story seconds per wall second), above zero.</summary>
    public double Speed
    {
        get => _speed;
        set => _speed = double.IsFinite(value) && value > 0 ? value : 1;
    }

    /// <summary>True while the replay plays (it may still be waiting off screen).</summary>
    public bool IsPlaying => _playing;

    /// <summary>True in live mode.</summary>
    public bool IsLive => _live;

    /// <summary>True at the end of a replay (never in live mode).</summary>
    public bool IsAtEnd => !_live && _now >= _duration - 1e-9;

    /// <summary>Keeps frames coming while set (a looping shine on screen).</summary>
    public bool KeepsRunning
    {
        get => _keepsRunning;
        set
        {
            if (_keepsRunning != value)
            {
                _keepsRunning = value;
                Update();
            }
        }
    }

    /// <summary>True while the loop is running (attached, on screen and wanted).</summary>
    public bool IsRunning => _loop.IsRunning;

    /// <summary>Plays from the current time; at the end it starts over from zero.</summary>
    public void Play()
    {
        if (_live)
        {
            return;
        }
        if (IsAtEnd)
        {
            _now = 0;
        }
        _playing = true;
        Update();
    }

    /// <summary>Freezes the story time.</summary>
    public void Pause()
    {
        _playing = false;
        Update();
    }

    /// <summary>Moves to <paramref name="time"/> (clamped to the run) and pauses.</summary>
    public void Seek(double time)
    {
        if (_live)
        {
            return;
        }
        _playing = false;
        _now = Math.Clamp(double.IsFinite(time) ? time : 0, 0, _duration);
        Update();
    }

    /// <summary>Moves to <paramref name="time"/> and plays on from there.</summary>
    public void PlayFrom(double time)
    {
        if (_live)
        {
            return;
        }
        _now = Math.Clamp(double.IsFinite(time) ? time : 0, 0, _duration);
        _playing = _now < _duration;
        Update();
    }

    /// <summary>
    /// Follows a run in progress: the time becomes the wall clock since <paramref name="startedAt"/> (or since now when
    /// null) and keeps running until <see cref="StopLive"/>.
    /// </summary>
    public void StartLive(DateTimeOffset? startedAt)
    {
        _live = true;
        _playing = false;
        _liveOrigin = startedAt ?? _time.GetUtcNow();
        _now = LiveNow();
        Update();
    }

    /// <summary>Leaves live mode and rests at the end (<see cref="Duration"/>).</summary>
    public void StopLive()
    {
        _live = false;
        _playing = false;
        _now = _duration;
        Update();
    }

    /// <summary>Keeps frames coming for <paramref name="seconds"/> of wall time (a transition after a change).</summary>
    public void RequestFrames(double seconds)
    {
        _framesUntil = Math.Max(_framesUntil, _wall + seconds);
        Update();
    }

    /// <summary>Runs one frame of <paramref name="seconds"/> by hand (tests, and catching up the live time on demand).</summary>
    internal void Step(double seconds) => OnFrame(seconds);

    private double LiveNow() => Math.Max(0, (_time.GetUtcNow() - _liveOrigin).TotalSeconds);

    private bool OnFrame(double dt)
    {
        dt = Math.Max(0, dt);
        _wall += dt;
        if (_live)
        {
            _now = Math.Max(_now, LiveNow());
        }
        else if (_playing)
        {
            _now = Math.Min(_now + Math.Min(dt, MaxStep) * _speed, _duration);
            if (_now >= _duration)
            {
                _playing = false;
                ReachedEnd?.Invoke();
            }
        }
        Ticked?.Invoke(dt);
        return WantsFrames();
    }

    private bool WantsFrames() => _live || _playing || _keepsRunning || _wall < _framesUntil;

    private void Update()
    {
        if (WantsFrames())
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    public void Dispose() => _loop.Dispose();
}
