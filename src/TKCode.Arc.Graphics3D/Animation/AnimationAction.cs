namespace TKCode.Arc.Graphics3D;

/// <summary>What an action does when it reaches the end of its clip.</summary>
public enum AnimationLoop
{
    /// <summary>Plays once and finishes.</summary>
    Once,

    /// <summary>Starts over, <see cref="AnimationAction.Repetitions"/> times.</summary>
    Repeat,

    /// <summary>Plays forward and backward alternately, <see cref="AnimationAction.Repetitions"/> times.</summary>
    PingPong,
}

/// <summary>The playback of one <see cref="AnimationClip"/> in an <see cref="AnimationMixer"/>: its time, speed, weight
/// and looping. Get it from <see cref="AnimationMixer.ClipAction"/>. Nothing happens until <see cref="Play"/> is
/// called and the mixer is updated.</summary>
public sealed class AnimationAction
{
    private readonly AnimationMixer _mixer;
    private readonly AnimationClip _clip;
    private int _loopCount = -1;
    private bool _hasStartTime;
    private double _startTime;
    private bool _fading;
    private double _fadeStart;
    private double _fadeEnd;
    private float _fadeFrom;
    private float _fadeTo;
    private bool _warping;
    private double _warpStart;
    private double _warpEnd;
    private float _warpFrom;
    private float _warpTo;
    private float _effectiveWeight = 1f;
    private float _effectiveTimeScale = 1f;

    internal AnimationAction(AnimationMixer mixer, AnimationClip clip, PropertyBinding[] bindings)
    {
        _mixer = mixer;
        _clip = clip;
        Bindings = bindings;
    }

    /// <summary>One binding per track of the clip.</summary>
    internal PropertyBinding[] Bindings { get; }

    /// <summary>Index in the mixer's list of scheduled actions, or -1.</summary>
    internal int ActiveIndex { get; set; } = -1;

    /// <summary>The mixer that owns the action.</summary>
    public AnimationMixer Mixer => _mixer;

    /// <summary>The clip the action plays.</summary>
    public AnimationClip Clip => _clip;

    /// <summary>The position in the clip, in seconds.</summary>
    public float Time { get; set; }

    /// <summary>Playback speed; negative plays backward, 0 stands still.</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>How strongly the action contributes when several actions animate the same property, 0..1.</summary>
    public float Weight { get; set; } = 1f;

    /// <summary>What happens at the end of the clip.</summary>
    public AnimationLoop Loop { get; set; } = AnimationLoop.Repeat;

    /// <summary>How many times the clip plays with <see cref="AnimationLoop.Repeat"/> or
    /// <see cref="AnimationLoop.PingPong"/>; infinite by default.</summary>
    public float Repetitions { get; set; } = float.PositiveInfinity;

    /// <summary>True keeps the last frame when the action finishes (it is paused); false (default) disables the action,
    /// so its properties return to the other actions or to their rest values.</summary>
    public bool ClampWhenFinished { get; set; }

    /// <summary>True holds the action at its current time; it keeps contributing its pose.</summary>
    public bool Paused { get; set; }

    /// <summary>False removes the action's contribution without changing its time.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The weight used in the last update: <see cref="Weight"/> with fading applied, 0 when disabled.</summary>
    public float EffectiveWeight => _effectiveWeight;

    /// <summary>The speed used in the last update: <see cref="TimeScale"/> with warping applied, 0 when paused.</summary>
    public float EffectiveTimeScale => _effectiveTimeScale;

    /// <summary>True when the action is in the mixer's schedule (after <see cref="Play"/>, until <see cref="Stop"/>).</summary>
    public bool IsScheduled => ActiveIndex >= 0;

    /// <summary>True when the action is scheduled and its time is advancing.</summary>
    public bool IsRunning => Enabled && !Paused && TimeScale != 0f && !_hasStartTime && IsScheduled;

    /// <summary>Schedules the action. A finished or stopped action needs <see cref="Reset"/> first to play again.</summary>
    public AnimationAction Play()
    {
        _mixer.Activate(this);
        return this;
    }

    /// <summary>Removes the action from the schedule and resets it. Properties no other action animates return to
    /// their rest values.</summary>
    public AnimationAction Stop()
    {
        _mixer.Deactivate(this);
        return Reset();
    }

    /// <summary>Rewinds to time 0, enables and unpauses the action and cancels fading, warping and a delayed start.</summary>
    public AnimationAction Reset()
    {
        Paused = false;
        Enabled = true;
        Time = 0f;
        _loopCount = -1;
        _hasStartTime = false;
        return StopFading().StopWarping();
    }

    /// <summary>Delays the start until the mixer's <see cref="AnimationMixer.Time"/> reaches a value.</summary>
    public AnimationAction StartAt(double mixerTime)
    {
        _hasStartTime = true;
        _startTime = mixerTime;
        return this;
    }

    /// <summary>Sets the loop mode and the number of repetitions.</summary>
    public AnimationAction SetLoop(AnimationLoop loop, float repetitions = float.PositiveInfinity)
    {
        Loop = loop;
        Repetitions = repetitions;
        return this;
    }

    /// <summary>Sets <see cref="Weight"/> and cancels fading.</summary>
    public AnimationAction SetEffectiveWeight(float weight)
    {
        Weight = weight;
        _effectiveWeight = Enabled ? weight : 0f;
        return StopFading();
    }

    /// <summary>Sets <see cref="TimeScale"/> and cancels warping.</summary>
    public AnimationAction SetEffectiveTimeScale(float timeScale)
    {
        TimeScale = timeScale;
        _effectiveTimeScale = Paused ? 0f : timeScale;
        return StopWarping();
    }

    /// <summary>Chooses the time scale that makes one pass of the clip take <paramref name="seconds"/>.</summary>
    public AnimationAction SetDuration(float seconds)
    {
        TimeScale = _clip.Duration / seconds;
        return StopWarping();
    }

    /// <summary>Copies time and time scale from another action, so both run in step.</summary>
    public AnimationAction SyncWith(AnimationAction other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Time = other.Time;
        TimeScale = other.TimeScale;
        return StopWarping();
    }

    /// <summary>Raises the action's contribution from 0 to <see cref="Weight"/> over a duration in mixer seconds.</summary>
    public AnimationAction FadeIn(float duration) => ScheduleFading(duration, 0f, 1f);

    /// <summary>Lowers the action's contribution to 0 over a duration in mixer seconds, then disables the action.</summary>
    public AnimationAction FadeOut(float duration) => ScheduleFading(duration, 1f, 0f);

    /// <summary>Fades this action in while fading another out. With <paramref name="warp"/> the two speeds are bent
    /// toward each other so that clips of different lengths (walk, run) stay in step during the blend.</summary>
    public AnimationAction CrossFadeFrom(AnimationAction fadeOutAction, float duration, bool warp = false)
    {
        ArgumentNullException.ThrowIfNull(fadeOutAction);
        fadeOutAction.FadeOut(duration);
        FadeIn(duration);
        if (warp)
        {
            var fadeInDuration = _clip.Duration;
            var fadeOutDuration = fadeOutAction._clip.Duration;
            if (fadeInDuration > 0f && fadeOutDuration > 0f)
            {
                fadeOutAction.Warp(1f, fadeOutDuration / fadeInDuration, duration);
                Warp(fadeInDuration / fadeOutDuration, 1f, duration);
            }
        }

        return this;
    }

    /// <summary>Fades this action out while fading another in. See <see cref="CrossFadeFrom"/>.</summary>
    public AnimationAction CrossFadeTo(AnimationAction fadeInAction, float duration, bool warp = false)
    {
        ArgumentNullException.ThrowIfNull(fadeInAction);
        fadeInAction.CrossFadeFrom(this, duration, warp);
        return this;
    }

    /// <summary>Cancels a fade; the action keeps its full <see cref="Weight"/>.</summary>
    public AnimationAction StopFading()
    {
        _fading = false;
        return this;
    }

    /// <summary>Changes the speed gradually from one time scale to another over a duration in mixer seconds. When the
    /// end scale is 0 the action is paused at the end.</summary>
    public AnimationAction Warp(float startTimeScale, float endTimeScale, float duration)
    {
        var now = _mixer.Time;
        var timeScale = TimeScale;
        if (timeScale == 0f)
        {
            // The warp is a factor on TimeScale; a zero base could not express it.
            timeScale = TimeScale = 1f;
        }

        _warping = true;
        _warpStart = now;
        _warpEnd = now + duration;
        _warpFrom = startTimeScale / timeScale;
        _warpTo = endTimeScale / timeScale;
        return this;
    }

    /// <summary>Slows the action down to a standstill over a duration in mixer seconds, then pauses it.</summary>
    public AnimationAction Halt(float duration) => Warp(_effectiveTimeScale, 0f, duration);

    /// <summary>Cancels a warp; the action keeps <see cref="TimeScale"/>.</summary>
    public AnimationAction StopWarping()
    {
        _warping = false;
        return this;
    }

    private AnimationAction ScheduleFading(float duration, float from, float to)
    {
        var now = _mixer.Time;
        _fading = true;
        _fadeStart = now;
        _fadeEnd = now + duration;
        _fadeFrom = from;
        _fadeTo = to;
        return this;
    }

    private static float Ramp(double time, double start, double end, float from, float to)
    {
        if (time <= start)
        {
            return from;
        }

        if (time >= end)
        {
            return to;
        }

        return from + ((to - from) * (float)((time - start) / (end - start)));
    }

    /// <summary>Advances the action and adds its weighted pose to the bindings.</summary>
    internal void Update(double time, float deltaTime, int timeDirection)
    {
        if (!Enabled)
        {
            // Keeps a running fade moving and the effective weight at zero.
            UpdateWeight(time);
            return;
        }

        if (_hasStartTime)
        {
            var timeRunning = (time - _startTime) * timeDirection;
            if (timeRunning < 0d || timeDirection == 0)
            {
                deltaTime = 0f;
            }
            else
            {
                _hasStartTime = false;
                deltaTime = (float)(timeDirection * timeRunning);
            }
        }

        deltaTime *= UpdateTimeScale(time);
        var clipTime = UpdateTime(deltaTime);
        var weight = UpdateWeight(time);
        if (weight <= 0f)
        {
            return;
        }

        var tracks = _clip.Tracks;
        var bindings = Bindings;
        for (var i = 0; i < tracks.Length; i++)
        {
            var binding = bindings[i];
            tracks[i].Sample(clipTime, binding.Incoming);
            binding.Accumulate(weight);
        }
    }

    private float UpdateWeight(double time)
    {
        var weight = 0f;
        if (Enabled)
        {
            weight = Weight;
            if (_fading)
            {
                var factor = Ramp(time, _fadeStart, _fadeEnd, _fadeFrom, _fadeTo);
                weight *= factor;
                if (time > _fadeEnd)
                {
                    _fading = false;
                    if (factor == 0f)
                    {
                        // Faded out completely.
                        Enabled = false;
                    }
                }
            }
        }

        _effectiveWeight = weight;
        return weight;
    }

    private float UpdateTimeScale(double time)
    {
        var timeScale = 0f;
        if (!Paused)
        {
            timeScale = TimeScale;
            if (_warping)
            {
                timeScale *= Ramp(time, _warpStart, _warpEnd, _warpFrom, _warpTo);
                if (time > _warpEnd)
                {
                    _warping = false;
                    if (timeScale == 0f)
                    {
                        // Halted.
                        Paused = true;
                    }
                    else
                    {
                        // The warp is over: its end speed becomes the action's speed.
                        TimeScale = timeScale;
                    }
                }
            }
        }

        _effectiveTimeScale = timeScale;
        return timeScale;
    }

    /// <summary>Moves <see cref="Time"/> by a delta, handles the end of the clip and returns the time to sample at.</summary>
    private float UpdateTime(float deltaTime)
    {
        var duration = _clip.Duration;
        var loop = Loop;
        var time = Time + deltaTime;
        var loopCount = _loopCount;
        var pingPong = loop == AnimationLoop.PingPong;
        if (deltaTime == 0f)
        {
            if (loopCount == -1)
            {
                return time;
            }

            return pingPong && (loopCount & 1) == 1 ? duration - time : time;
        }

        if (loop == AnimationLoop.Once)
        {
            if (loopCount == -1)
            {
                _loopCount = 0;
            }

            if (time >= duration)
            {
                time = duration;
            }
            else if (time < 0f)
            {
                time = 0f;
            }
            else
            {
                Time = time;
                return time;
            }

            Finish(time, deltaTime < 0f ? -1 : 1);
            return time;
        }

        if (loopCount == -1 && deltaTime >= 0f)
        {
            loopCount = 0;
        }

        if (duration <= 0f)
        {
            // Nothing to loop through: a clip of a single pose.
            _loopCount = loopCount;
            Time = 0f;
            return 0f;
        }

        if (time >= duration || time < 0f)
        {
            // Wrap around, counting the passes.
            var loopDelta = (int)MathF.Floor(time / duration);
            time -= duration * loopDelta;
            loopCount += Math.Abs(loopDelta);
            var pending = Repetitions - loopCount;
            if (pending <= 0f)
            {
                // Clamp to the end of the last pass that was allowed (its parity decides the ping-pong direction below).
                time = deltaTime > 0f ? duration : 0f;
                loopCount = (int)MathF.Max(MathF.Ceiling(Repetitions) - 1f, 0f);
                _loopCount = loopCount;
                Finish(time, deltaTime > 0f ? 1 : -1);
            }
            else
            {
                _loopCount = loopCount;
                Time = time;
                _mixer.QueueLooped(this, loopDelta);
            }
        }
        else
        {
            _loopCount = loopCount;
            Time = time;
        }

        if (pingPong && (loopCount & 1) == 1)
        {
            // Odd passes run backward.
            return duration - time;
        }

        return time;
    }

    private void Finish(float time, int direction)
    {
        if (ClampWhenFinished)
        {
            Paused = true;
        }
        else
        {
            Enabled = false;
        }

        Time = time;
        _mixer.QueueFinished(this, direction);
    }
}
