namespace TKCode.Arc;

/// <summary>
/// The playhead of a <see cref="RevenueGlobe"/> as a pure state machine (seconds since the start of the day), stepped
/// by the block's frame loop:
/// <list type="bullet">
/// <item>Live: the playhead is the present (<see cref="Now"/>).</item>
/// <item>Replaying: the playhead runs toward the present at <see cref="ReplaySpeed"/> (1500×: an hour in 2.4 s). The
/// speed rises from its start (0, or the release speed of a drag) in about a second (0.2 s → 950×, 0.6 s → 1220×,
/// 1.6 s → 1470× from rest), then brakes at a constant rate that would stop the cruise in 1.7 s, landing exactly on
/// the present, where it goes live.</item>
/// <item>Go live from the past is a fast-forward on a trapezoid (0.4 s up, 0.35 s down) lasting 600 ms + 85 ms per
/// hour, at most 2 s.</item>
/// <item>Paused and Scrubbing hold the playhead (a drag moves it directly).</item>
/// </list>
/// </summary>
internal sealed class RevenueGlobePlayback
{
    /// <summary>Share of the slow part of the speed ramp, and its two time constants (fitted to the replay samples).</summary>
    public const double RampSlowShare = 0.51;

    public const double RampSlowTime = 0.59;

    public const double RampFastTime = 0.06;

    /// <summary>Braking from the cruise speed to rest takes this long.</summary>
    public const double BrakeTime = 1.7;

    public const double FastForwardAccelerate = 0.4;

    public const double FastForwardDecelerate = 0.35;

    private double _rampElapsed;
    private double _rampStart;
    private bool _fastForward;
    private double _ffFrom;
    private double _ffDistance;
    private double _ffDuration;
    private double _ffElapsed;

    /// <summary>What the playhead is doing.</summary>
    public RevenueGlobePlaybackState State { get; private set; } = RevenueGlobePlaybackState.Live;

    /// <summary>The playhead, seconds since the day started.</summary>
    public double Time { get; private set; }

    /// <summary>How far the data goes (the present), seconds since the day started.</summary>
    public double Now { get; private set; }

    /// <summary>Simulated seconds per real second while replaying (default 1500).</summary>
    public double ReplaySpeed { get; set; } = 1500;

    /// <summary>Playhead speed of the last step (simulated seconds per second; 1 while live).</summary>
    public double Velocity { get; private set; } = 1;

    /// <summary>A Go live fast-forward is running.</summary>
    public bool IsFastForwarding => _fastForward;

    /// <summary>The playhead is the present.</summary>
    public bool IsLive => State == RevenueGlobePlaybackState.Live;

    /// <summary>The playhead moves by itself (live, replaying or fast-forwarding).</summary>
    public bool IsRunning => State is RevenueGlobePlaybackState.Live or RevenueGlobePlaybackState.Replaying;

    /// <summary>Moves the present; a live playhead follows it, any other playhead stays at or before it.</summary>
    public void SetNow(double now)
    {
        Now = now;
        if (State == RevenueGlobePlaybackState.Live || Time > now)
        {
            Time = now;
        }
    }

    /// <summary>Starts over at 00:00 and replays toward the present (from rest).</summary>
    public void Replay()
    {
        Time = 0;
        StartReplay(0);
    }

    /// <summary>Holds the playhead.</summary>
    public void Pause()
    {
        _fastForward = false;
        State = RevenueGlobePlaybackState.Paused;
        Velocity = 0;
    }

    /// <summary>Resumes after a pause: replays from the paused moment (live when nothing is left to replay).</summary>
    public void Play()
    {
        if (State != RevenueGlobePlaybackState.Paused)
        {
            return;
        }
        if (Time >= Now)
        {
            GoLiveNow();
            return;
        }
        StartReplay(0);
    }

    /// <summary>Back to the present: a fast-forward when <paramref name="animate"/>, otherwise at once.</summary>
    public void GoLive(bool animate)
    {
        var distance = Now - Time;
        if (!animate || distance <= 0.5)
        {
            GoLiveNow();
            return;
        }
        _fastForward = true;
        _ffFrom = Time;
        _ffDistance = distance;
        _ffDuration = FastForwardDuration(distance / 3600);
        _ffElapsed = 0;
        State = RevenueGlobePlaybackState.Replaying;
    }

    /// <summary>
    /// Jumps to <paramref name="time"/> (clamped to the day so far). In the past it replays on from there when
    /// <paramref name="continueReplay"/>, otherwise it holds; at the present it goes live.
    /// </summary>
    public void Seek(double time, bool continueReplay)
    {
        Time = Math.Clamp(time, 0, Math.Max(0, Now));
        if (Time >= Now)
        {
            GoLiveNow();
            return;
        }
        if (continueReplay)
        {
            StartReplay(0);
        }
        else
        {
            Pause();
        }
    }

    /// <summary>A drag on the timeline begins: the playhead holds and follows <see cref="ScrubTo"/>.</summary>
    public void BeginScrub()
    {
        _fastForward = false;
        State = RevenueGlobePlaybackState.Scrubbing;
        Velocity = 0;
    }

    /// <summary>Moves the playhead under the pointer (clamped to the day so far).</summary>
    public void ScrubTo(double time) => Time = Math.Clamp(time, 0, Math.Max(0, Now));

    /// <summary>
    /// The drag ended: replays on with the release speed easing to <see cref="ReplaySpeed"/> when
    /// <paramref name="continueReplay"/>, otherwise holds; live when released at the present.
    /// </summary>
    public void EndScrub(double releaseSpeed, bool continueReplay)
    {
        if (Time >= Now)
        {
            GoLiveNow();
        }
        else if (continueReplay)
        {
            StartReplay(Math.Max(0, releaseSpeed));
        }
        else
        {
            Pause();
        }
    }

    private void StartReplay(double startSpeed)
    {
        _fastForward = false;
        _rampElapsed = 0;
        _rampStart = startSpeed;
        Velocity = startSpeed;
        State = RevenueGlobePlaybackState.Replaying;
    }

    private void GoLiveNow()
    {
        _fastForward = false;
        Time = Now;
        Velocity = 1;
        State = RevenueGlobePlaybackState.Live;
    }

    /// <summary>
    /// Steps the playhead by <paramref name="dt"/> seconds (after <see cref="SetNow"/>). Returns true when it reached the
    /// present and went live during this step.
    /// </summary>
    public bool Advance(double dt)
    {
        if (!(dt > 0) || State != RevenueGlobePlaybackState.Replaying)
        {
            if (State == RevenueGlobePlaybackState.Live)
            {
                Time = Now;
            }
            return false;
        }
        if (_fastForward)
        {
            _ffElapsed += dt;
            var u = _ffDuration > 0 ? Math.Min(1, _ffElapsed / _ffDuration) : 1;
            var previous = Time;
            Time = Math.Min(Now, _ffFrom + _ffDistance * Trapezoid(u, FastForwardAccelerate / _ffDuration, FastForwardDecelerate / _ffDuration));
            Velocity = (Time - previous) / dt;
            if (u >= 1)
            {
                GoLiveNow();
                return true;
            }
            return false;
        }
        _rampElapsed += dt;
        var cruise = ReplaySpeed;
        var ramp = RampSpeed(_rampStart, cruise, _rampElapsed);
        var remaining = Now - Time;
        // Constant braking that would stop the cruise in BrakeTime; +1 keeps up with the present, which moves on at 1×.
        var brake = Math.Sqrt(2 * (cruise / BrakeTime) * Math.Max(0, remaining)) + 1;
        var speed = Math.Max(1, Math.Min(ramp, brake));
        Velocity = speed;
        var step = speed * dt;
        if (step >= remaining)
        {
            GoLiveNow();
            return true;
        }
        Time += step;
        return false;
    }

    /// <summary>
    /// Replay speed <paramref name="elapsed"/> seconds after it started from <paramref name="start"/>, easing to
    /// <paramref name="cruise"/>: cruise + (start − cruise)·(0.51·e^(−t/0.59) + 0.49·e^(−t/0.06)).
    /// </summary>
    public static double RampSpeed(double start, double cruise, double elapsed)
    {
        var left = RampSlowShare * Math.Exp(-elapsed / RampSlowTime) + (1 - RampSlowShare) * Math.Exp(-elapsed / RampFastTime);
        return cruise + (start - cruise) * left;
    }

    /// <summary>Length of a Go live fast-forward over <paramref name="hours"/>: 600 ms + 85 ms per hour, at most 2 s.</summary>
    public static double FastForwardDuration(double hours) => Math.Min(2.0, 0.6 + 0.085 * Math.Max(0, hours));

    /// <summary>
    /// Share of the distance covered at <paramref name="u"/> (0…1 of the duration) on a trapezoid that speeds up for
    /// <paramref name="accelerate"/> and slows down for <paramref name="decelerate"/> (shares of the duration, scaled
    /// down together when they do not fit).
    /// </summary>
    public static double Trapezoid(double u, double accelerate, double decelerate)
    {
        u = Math.Clamp(u, 0, 1);
        var a = Math.Max(1e-6, accelerate);
        var b = Math.Max(1e-6, decelerate);
        if (a + b > 1)
        {
            var k = 1 / (a + b);
            a *= k;
            b *= k;
        }
        var top = 1 / (1 - a / 2 - b / 2);
        if (u < a)
        {
            return top * u * u / (2 * a);
        }
        if (u <= 1 - b)
        {
            return top * (a / 2 + (u - a));
        }
        var rest = 1 - u;
        return 1 - top * rest * rest / (2 * b);
    }
}
