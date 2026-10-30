using Avalonia;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The motion of <see cref="GhostMascot"/>. Everything runs on one Core <see cref="LiveLoop"/>, which delivers frames
/// only while the ghost is attached, visible and on screen, so its clock stops off screen and resumes on the same frame.
/// <para>
/// Every loop (the float, the two hem waves, the swirl and the writing cycle) is a function of a phase that only grows;
/// the loop rates are pose values, so a status change blends the rates and the phases integrate them: speed changes
/// never jump and nothing restarts. A status change blends every pose value from the frame on screen (value and
/// velocity) to the new pose over 400ms on a cubic Hermite that is the classic ease in and out from rest. The eyes ride
/// a critically damped spring whose stiffness is itself a pose value. This is code-driven because a ControlTheme
/// transition cannot blend toward per-frame targets (the pen tip), integrate rates into phases or keep velocity across
/// interruptions, and the drawing is one self-drawn control with no animatable child properties.
/// </para>
/// </summary>
public partial class GhostMascot
{
    private LiveLoop _loop = null!;
    private readonly double[] _pose = new double[GhostMascotPose.Count];
    private readonly double[] _from = new double[GhostMascotPose.Count];
    private readonly double[] _fromVelocity = new double[GhostMascotPose.Count];
    private readonly double[] _to = new double[GhostMascotPose.Count];
    private readonly double[] _velocity = new double[GhostMascotPose.Count];

    private ArcMotionLevel _level = ArcMotionLevel.Off;
    private GhostMascotStatus _poseStatus = GhostMascotStatus.Idle;
    private double _clock;

    private bool _blending;
    private double _blendStart;

    // Phases that only grow (cycles).
    private double _floatPhase;
    private double _hemPhase;
    private double _swirlPhase;
    private double _writePhase;

    // The eyes' spring (x, y in -1 to 1) and its target.
    private double _gazeX;
    private double _gazeY;
    private double _gazeVelocityX;
    private double _gazeVelocityY;
    private double _gazeTargetX;
    private double _gazeTargetY;

    // Blinks and idle glances.
    private double _blinkStart = double.NegativeInfinity;
    private double _nextBlink = 1.6;
    private int _blinkCount;
    private int _glanceIndex;
    private double _nextGlance = 1.2;

    // One-shot gestures on the real clock.
    private double _sparkleStart = double.NaN;
    private double _wobbleStart = double.NaN;

    // Self-play while no status is set.
    private int _playIndex;
    private double _playTime;

    // Test hooks.
    internal bool IsLoopRunning => _loop.IsRunning;
    internal bool IsLoopActive => _loop.IsActive;
    internal bool IsBlending => _blending;
    internal bool IsStill => _level != ArcMotionLevel.Full;
    internal ReadOnlySpan<double> Pose => _pose;
    internal double Clock => _clock;
    internal double FloatPhase => _floatPhase;
    internal double HemPhase => _hemPhase;
    internal double SwirlPhase => _swirlPhase;
    internal double WritePhase => _writePhase;
    internal (double X, double Y) Gaze => (_gazeX, _gazeY);
    internal double WobbleNow => double.IsNaN(_wobbleStart) ? 0 : GhostMascotPose.Wobble(_clock - _wobbleStart);
    internal double SparkleGrowNow => IsStill || double.IsNaN(_sparkleStart) ? 1 : GhostMascotPose.SparkleGrow(_clock - _sparkleStart);
    internal double BlinkNow => IsStill ? 0 : GhostMascotPose.BlinkClosure(_clock - _blinkStart) * _pose[GhostMascotPose.Blink];

    /// <summary>Advances the clocks by <paramref name="seconds"/> as one frame would (tests drive the motion with it).</summary>
    internal bool Advance(double seconds) => Step(seconds);

    private void InitializeMotion()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        // The motion level can also come from the theme's resources (a motion token): re-read it when they change.
        ResourcesChanged += (_, _) => RefreshLevel();
        GhostMascotPose.Write(GhostMascotStatus.Idle, _to);
        GhostMascotPose.Write(GhostMascotStatus.Idle, _pose);
        CreateDrawing();
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshLevel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateLoop();
    }

    private bool Animates => _level == ArcMotionLevel.Full && this.IsAttachedToVisualTree();

    private void OnRequestedStatusChanged(GhostMascotStatus? requested)
    {
        if (requested is { } status)
        {
            ShowStatus(status, false);
        }
        else
        {
            _playIndex = 0;
            _playTime = 0;
            ShowStatus(GhostMascotSamples.StatusAt(0), false);
        }
        UpdateLoop();
    }

    private void RefreshLevel()
    {
        var level = this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level != _level)
        {
            _level = level;
            if (level != ArcMotionLevel.Full)
            {
                // Reduced and off: a still pose for the status, switched in place; no loops, glances, blinks or wobble.
                SettleNow();
            }
            Sync();
            InvalidateVisual();
        }
        UpdateLoop();
    }

    /// <summary>Points the pose at <paramref name="status"/>, blending from the frame on screen under full motion.</summary>
    private void SetPoseStatus(GhostMascotStatus status)
    {
        var previous = _poseStatus;
        _poseStatus = status;
        if (Animates)
        {
            // The blend starts exactly where the ghost is, with the velocity it has.
            for (var i = 0; i < GhostMascotPose.Count; i++)
            {
                _from[i] = _pose[i];
                _fromVelocity[i] = _velocity[i];
            }
            GhostMascotPose.Write(status, _to);
            _blending = true;
            _blendStart = _clock;
            if (status != previous)
            {
                if (status == GhostMascotStatus.Done)
                {
                    _sparkleStart = _clock;
                }
                if (status == GhostMascotStatus.Error)
                {
                    _wobbleStart = _clock;
                }
            }
        }
        else
        {
            GhostMascotPose.Write(status, _to);
            SettleNow();
        }
        Sync();
        InvalidateVisual();
    }

    private void SettleNow()
    {
        _blending = false;
        _sparkleStart = double.NaN;
        _wobbleStart = double.NaN;
        _blinkStart = double.NegativeInfinity;
        for (var i = 0; i < GhostMascotPose.Count; i++)
        {
            _pose[i] = _to[i];
            _velocity[i] = 0;
        }
        UpdateGazeTarget();
        _gazeX = _gazeTargetX;
        _gazeY = _gazeTargetY;
        _gazeVelocityX = 0;
        _gazeVelocityY = 0;
    }

    /// <summary>Whether frames are needed: always under full motion (the ghost floats); under reduced only to self-play.</summary>
    internal bool NeedsFrames() => _level switch
    {
        ArcMotionLevel.Full => true,
        ArcMotionLevel.Reduced => IsSelfPlaying,
        _ => false
    };

    private void UpdateLoop()
    {
        if (!IsPaused && NeedsFrames())
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 0.1);
        _clock += dt;
        if (IsSelfPlaying)
        {
            _playTime += dt;
            if (_playTime >= GhostMascotSamples.SecondsAt(_playIndex))
            {
                // Keep the overshoot so the steps keep their lengths over many loops.
                _playTime -= GhostMascotSamples.SecondsAt(_playIndex);
                _playIndex = (_playIndex + 1) % GhostMascotSamples.Count;
                ShowStatus(GhostMascotSamples.StatusAt(_playIndex), true);
            }
        }
        if (_level != ArcMotionLevel.Full)
        {
            // Reduced: the loop only keeps self-play going; the still pose repaints when the status changes.
            return !IsPaused && NeedsFrames();
        }
        EvaluatePose();
        _floatPhase += _pose[GhostMascotPose.FloatRate] * dt;
        _hemPhase += _pose[GhostMascotPose.HemRate] * dt;
        _swirlPhase += _pose[GhostMascotPose.SwirlRate] * dt;
        _writePhase += _pose[GhostMascotPose.WriteRate] * dt;
        StepBlinkAndGlance();
        UpdateGazeTarget();
        var omega = Math.Max(1, _pose[GhostMascotPose.GazeStiffness]);
        (_gazeX, _gazeVelocityX) = GhostMascotPose.Spring(_gazeX, _gazeVelocityX, _gazeTargetX, omega, dt);
        (_gazeY, _gazeVelocityY) = GhostMascotPose.Spring(_gazeY, _gazeVelocityY, _gazeTargetY, omega, dt);
        if (!double.IsNaN(_wobbleStart) && _clock - _wobbleStart >= GhostMascotPose.WobbleDuration)
        {
            _wobbleStart = double.NaN;
        }
        if (!double.IsNaN(_sparkleStart) && _clock - _sparkleStart >= GhostMascotPose.SparkleGrowDuration)
        {
            _sparkleStart = double.NaN;
        }
        Sync();
        InvalidateVisual();
        return !IsPaused && NeedsFrames();
    }

    private void EvaluatePose()
    {
        if (!_blending)
        {
            for (var i = 0; i < GhostMascotPose.Count; i++)
            {
                _pose[i] = _to[i];
                _velocity[i] = 0;
            }
            return;
        }
        var p = (_clock - _blendStart) / GhostMascotPose.BlendDuration;
        for (var i = 0; i < GhostMascotPose.Count; i++)
        {
            (_pose[i], _velocity[i]) = GhostMascotPose.Blend(_from[i], _fromVelocity[i], _to[i], p);
        }
        // The eye spring's stiffness switches at once: a slow drift starts slow, a quick saccade starts quick.
        _pose[GhostMascotPose.GazeStiffness] = _to[GhostMascotPose.GazeStiffness];
        _velocity[GhostMascotPose.GazeStiffness] = 0;
        if (p >= 1)
        {
            _blending = false;
        }
    }

    private void StepBlinkAndGlance()
    {
        if (_clock >= _nextBlink)
        {
            _blinkStart = _clock;
            _blinkCount++;
            _nextBlink = _clock + 2.4 + 2.8 * GhostMascotPose.Hash(_blinkCount);
        }
        if (_clock >= _nextGlance)
        {
            _glanceIndex++;
            _nextGlance = _clock + 1.1 + 1.4 * GhostMascotPose.Hash(_glanceIndex + 101);
        }
    }

    /// <summary>
    /// The eyes' target: the status's gaze, the idle glance (weighted by the pose's glance value) and, while writing, the
    /// pen tip (weighted by the pen's visibility).
    /// </summary>
    private void UpdateGazeTarget()
    {
        var (x, y) = GhostMascotPose.Gaze(_poseStatus);
        if (!IsStill)
        {
            var glance = _pose[GhostMascotPose.Glance];
            if (glance > 0.001)
            {
                var (gx, gy) = GhostMascotPose.GlanceAt(_glanceIndex);
                x += glance * gx;
                y += glance * gy;
            }
        }
        var pen = _pose[GhostMascotPose.Pen];
        if (pen > 0.001)
        {
            var layout = Layout;
            var (tipX, tipY) = PenTip(layout);
            var eyeX = 50 + _pose[GhostMascotPose.Shift] * layout.Motion;
            var eyeY = BodyEyeY + (_pose[GhostMascotPose.Lift] + FloatOffset) * layout.Motion;
            var dx = tipX - eyeX;
            var dy = tipY - eyeY;
            var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            x = x * (1 - pen) + pen * dx / length;
            y = y * (1 - pen) + pen * dy / length;
        }
        _gazeTargetX = Math.Clamp(x, -1, 1);
        _gazeTargetY = Math.Clamp(y, -1, 1);
    }

    /// <summary>The float's vertical offset now (drawing units, negative is up).</summary>
    internal double FloatOffset => IsStill
        ? 0
        : -2.6 * _pose[GhostMascotPose.FloatAmp] * Math.Sin(2 * Math.PI * _floatPhase);

    /// <summary>The float's vertical velocity now (units per second, positive is down).</summary>
    internal double FloatVelocity => IsStill
        ? 0
        : -2.6 * _pose[GhostMascotPose.FloatAmp] * 2 * Math.PI * _pose[GhostMascotPose.FloatRate] * Math.Cos(2 * Math.PI * _floatPhase);

    /// <summary>The swirl's drawn window (from its outer end) now; a half drawn swirl when still.</summary>
    internal (double Tail, double Head) SwirlNow => IsStill ? (0, 0.5) : GhostMascotPose.SwirlWindow(_swirlPhase);

    /// <summary>The writing cycle now; half a written line with the pen down when still.</summary>
    internal (double Written, double Pen, double PenLift) WritingNow =>
        GhostMascotPose.Writing(IsStill ? GhostMascotPose.StillWritingPhase : _writePhase);
}
