using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn spark of <see cref="AiSpark"/>: four tapered primary rays, four lighter diagonals (dots under 18px), a soft
/// core, two orbiting particles while writing (40px and up) and, for the avatar, a round tinted tile behind it.
/// <para>
/// Everything runs on one Core <see cref="LiveLoop"/> (Reduced and up) that only delivers frames while something moves
/// and the mark is attached, visible and on screen, and stops entirely once a resting status has settled. Each status is
/// a periodic target pose (<see cref="AiSparkPose"/>); a status change adds an offset that starts at the difference
/// between the pose the mark is in (with its velocity) and the new target, and decays on a critically damped spring
/// (<see cref="SpringSolver"/>), so blends never jump and an interrupted blend becomes the next blend's start. This is
/// code-driven because a ControlTheme transition cannot blend toward a moving, periodic target or keep velocity across
/// interruptions, and a <see cref="MotionValue{T}"/> needs a fixed target. The gather and the shake are one-shot
/// gestures on the real clock that always finish. The ray paths are moved in place and the brushes are recolored, so a
/// frame allocates nothing.
/// </para>
/// </summary>
internal sealed class AiSparkMark : Control
{
    public static readonly StyledProperty<AiSparkStatus?> RequestedStatusProperty =
        AvaloniaProperty.Register<AiSparkMark, AiSparkStatus?>(nameof(RequestedStatus));

    public static readonly StyledProperty<AiSparkTone> ToneProperty =
        AvaloniaProperty.Register<AiSparkMark, AiSparkTone>(nameof(Tone));

    public static readonly StyledProperty<IBrush?> OverrideBrushProperty =
        AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(OverrideBrush));

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AiSparkMark, double>(nameof(Speed), 1);

    public static readonly StyledProperty<bool> TwinkleProperty =
        AvaloniaProperty.Register<AiSparkMark, bool>(nameof(Twinkle), true);

    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<AiSparkMark, bool>(nameof(IsPaused));

    public static readonly StyledProperty<bool> IsTileProperty =
        AvaloniaProperty.Register<AiSparkMark, bool>(nameof(IsTile));

    public static readonly StyledProperty<double> TileMarkRatioProperty =
        AvaloniaProperty.Register<AiSparkMark, double>(nameof(TileMarkRatio), 0.56);

    public static readonly StyledProperty<double> TileTintProperty =
        AvaloniaProperty.Register<AiSparkMark, double>(nameof(TileTint), 0.13);

    public static readonly StyledProperty<double> SoftToneMixProperty =
        AvaloniaProperty.Register<AiSparkMark, double>(nameof(SoftToneMix), 0.42);

    public static readonly StyledProperty<double> ErrorMuteMixProperty =
        AvaloniaProperty.Register<AiSparkMark, double>(nameof(ErrorMuteMix), 0.35);

    public static readonly StyledProperty<IBrush?> AccentInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> DangerInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(DangerInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> InheritInkProperty = AvaloniaProperty.Register<AiSparkMark, IBrush?>(nameof(InheritInk));

    // The primary rays sit on the axes, the diagonals between them (radians, clockwise from 12 o'clock).
    private static readonly Matrix[] s_primaryTurns = [Turn(0), Turn(90), Turn(180), Turn(270)];
    private static readonly Matrix[] s_diagonalTurns = [Turn(45), Turn(135), Turn(225), Turn(315)];

    // Geometry in a 100 unit box centered on the origin.
    private const double PrimaryStart = 4;
    private const double PrimaryReach = 42;
    private const double PrimaryHalfWidth = 7.2;
    private const double DiagonalStart = 14;
    private const double DiagonalReach = 16;
    private const double DiagonalHalfWidth = 4.6;
    private const double DotRadius = 25;
    private const double CoreRadius = 12.5;
    private const double ParticleOrbit = 50;
    private const double ParticleRadius = 2.3;
    private const double ParticleTurn = 2.08;
    private const double ParticleFade = 0.3;
    private const double SpinEase = 0.25;
    private const double MaxGlide = 3;

    private readonly LiveLoop _loop;
    private readonly AiSparkRay[] _primary = [new(), new(), new(), new()];
    private readonly AiSparkRay[] _diagonal = [new(), new(), new(), new()];
    private readonly double[] _pose = new double[AiSparkPose.Count];
    private readonly double[] _target = new double[AiSparkPose.Count];
    private readonly double[] _ahead = new double[AiSparkPose.Count];
    private readonly double[] _offset = new double[AiSparkPose.Count];
    private readonly double[] _offsetVelocity = new double[AiSparkPose.Count];
    private readonly SolidColorBrush _rayBrush = new(Colors.Black);
    private readonly SolidColorBrush _softBrush = new(Colors.Gray);
    private readonly SolidColorBrush _dimBrush = new(Colors.Gray);
    private readonly SolidColorBrush _tileBrush = new(Colors.Transparent);

    private ArcMotionLevel _level = ArcMotionLevel.Off;
    private double _clock;
    private double _phase;

    // The target the pose follows.
    private AiSparkStatus _status = AiSparkStatus.Idle;
    private bool _twinkle = true;
    private bool _still = true;

    // The blend offset: decays from (_offset, _offsetVelocity) since _blendStart.
    private bool _blending;
    private bool _blendReduced;
    private double _blendStart;

    private double _gatherStart = double.NaN;
    private double _shakeStart = double.NaN;

    // The error tone (0 = the tone's color, 1 = the muted danger tone).
    private double _errorFrom;
    private double _errorTo;
    private double _errorStart = double.NegativeInfinity;
    private double _errorDuration = AiSparkPose.ErrorToneDuration;

    // The spin: an angle and its speed; without a spinning status it glides home on an ease-out.
    private double _angle;
    private double _spinSpeed;
    private bool _gliding;
    private double _glideFrom;
    private double _glideDistance;
    private double _glideDuration;
    private double _glideStart;
    private double _glideSpeed;

    // The writing particles: opacity tweened toward 0 or 1.
    private double _particleFrom;
    private double _particleTo;
    private double _particleStart = double.NegativeInfinity;

    // Self-play while no status is requested.
    private int _playIndex;
    private double _playTime;

    static AiSparkMark()
    {
        AffectsRender<AiSparkMark>(IsTileProperty, TileMarkRatioProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AiSparkMark>(false);
        FocusableProperty.OverrideDefaultValue<AiSparkMark>(false);
    }

    public AiSparkMark()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        ClipToBounds = false;
        // The motion level can also come from the theme's resources (a motion token): re-read it when they change.
        ResourcesChanged += (_, _) => RefreshLevel();
        AiSparkPose.Rest(_pose);
    }

    /// <summary>Raised when <see cref="EffectiveStatus"/> changes (a new requested status, or the next self-play step).</summary>
    public event Action<AiSparkStatus>? EffectiveStatusChanged;

    public AiSparkStatus? RequestedStatus { get => GetValue(RequestedStatusProperty); set => SetValue(RequestedStatusProperty, value); }
    public AiSparkTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public IBrush? OverrideBrush { get => GetValue(OverrideBrushProperty); set => SetValue(OverrideBrushProperty, value); }
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }
    public bool Twinkle { get => GetValue(TwinkleProperty); set => SetValue(TwinkleProperty, value); }
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
    public bool IsTile { get => GetValue(IsTileProperty); set => SetValue(IsTileProperty, value); }
    public double TileMarkRatio { get => GetValue(TileMarkRatioProperty); set => SetValue(TileMarkRatioProperty, value); }
    public double TileTint { get => GetValue(TileTintProperty); set => SetValue(TileTintProperty, value); }
    public double SoftToneMix { get => GetValue(SoftToneMixProperty); set => SetValue(SoftToneMixProperty, value); }
    public double ErrorMuteMix { get => GetValue(ErrorMuteMixProperty); set => SetValue(ErrorMuteMixProperty, value); }
    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }
    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }
    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    public IBrush? InheritInk { get => GetValue(InheritInkProperty); set => SetValue(InheritInkProperty, value); }

    /// <summary>The status the mark shows: the requested one, or the current self-play step.</summary>
    public AiSparkStatus EffectiveStatus => _status;

    /// <summary>True while no status is requested and the mark plays through its sample sequence.</summary>
    public bool IsSelfPlaying => RequestedStatus is null;

    // Test hooks.
    internal bool IsLooping => _loop.IsRunning;
    internal bool IsLoopActive => _loop.IsActive;
    internal double Angle => _angle;
    internal double SpinSpeedNow => _spinSpeed;
    internal bool IsBlending => _blending;
    internal double ErrorMix => ErrorMixAt(_clock);
    internal double ParticleOpacity => ParticleAt(_clock);
    internal double GatherNow => double.IsNaN(_gatherStart) ? 0 : AiSparkPose.Gather(_clock - _gatherStart);
    internal double ShakeNow => double.IsNaN(_shakeStart) ? 0 : AiSparkPose.Shake(_clock - _shakeStart);
    internal ReadOnlySpan<double> Pose => _pose;
    internal Color RayColor => _rayBrush.Color;
    internal Color TileColor => _tileBrush.Color;

    /// <summary>Advances the clocks by <paramref name="seconds"/> as one frame would (tests drive the motion with it).</summary>
    internal bool Advance(double seconds) => Step(seconds);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == RequestedStatusProperty)
        {
            if (change.GetNewValue<AiSparkStatus?>() is { } requested)
            {
                SetStatus(requested);
            }
            else
            {
                _playIndex = 0;
                _playTime = 0;
                SetStatus(AiSparkSamples.StatusAt(0));
            }
            UpdateLoop();
        }
        else if (property == TwinkleProperty)
        {
            Retarget(_status, change.GetNewValue<bool>(), _still, Animates);
            Evaluate();
            UpdateLoop();
            InvalidateVisual();
        }
        else if (property == IsPausedProperty || property == SpeedProperty)
        {
            UpdateLoop();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
        else if (property == ToneProperty || property == OverrideBrushProperty || property == AccentInkProperty ||
                 property == ForegroundInkProperty || property == MutedInkProperty || property == DangerInkProperty ||
                 property == SurfaceInkProperty || property == InheritInkProperty || property == SoftToneMixProperty ||
                 property == ErrorMuteMixProperty || property == TileTintProperty)
        {
            Evaluate();
            InvalidateVisual();
        }
        else if (property == BoundsProperty)
        {
            // The particles show from 40px up: a resize can start or stop them.
            UpdateParticles(Animates);
            UpdateLoop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshLevel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Off screen nothing is shown, so the one-shot motion ends where it would have ended.
        SettleNow();
    }

    private bool Animates => _level != ArcMotionLevel.Off && this.IsAttachedToVisualTree();

    private void RefreshLevel()
    {
        var level = this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == _level)
        {
            UpdateLoop();
            return;
        }
        var was = _level;
        _level = level;
        Retarget(_status, _twinkle, level != ArcMotionLevel.Full, was != ArcMotionLevel.Off && level != ArcMotionLevel.Off);
        if (level != ArcMotionLevel.Full)
        {
            // Reduced and off: no spin, gather, shake or particles.
            _angle = 0;
            _spinSpeed = 0;
            _gliding = false;
            _gatherStart = double.NaN;
            _shakeStart = double.NaN;
            _particleFrom = _particleTo = 0;
            _particleStart = double.NegativeInfinity;
        }
        if (level == ArcMotionLevel.Off)
        {
            _errorFrom = _errorTo;
            _errorStart = double.NegativeInfinity;
        }
        UpdateParticles(false);
        Evaluate();
        UpdateLoop();
        InvalidateVisual();
    }

    private void SetStatus(AiSparkStatus status)
    {
        var previous = _status;
        var animate = Animates;
        Retarget(status, _twinkle, _still, animate);
        if (status != previous)
        {
            var full = animate && _level == ArcMotionLevel.Full;
            if (status == AiSparkStatus.Done && full)
            {
                _gatherStart = _clock;
            }
            if (status == AiSparkStatus.Error && full)
            {
                _shakeStart = _clock;
            }
            var errorNow = ErrorMixAt(_clock);
            _errorTo = status == AiSparkStatus.Error ? 1 : 0;
            if (animate)
            {
                _errorFrom = errorNow;
                _errorStart = _clock;
                _errorDuration = _level == ArcMotionLevel.Full ? AiSparkPose.ErrorToneDuration : AiSparkPose.ReducedBlend;
            }
            else
            {
                _errorFrom = _errorTo;
                _errorStart = double.NegativeInfinity;
            }
            UpdateParticles(animate);
            }
        Evaluate();
        InvalidateVisual();
        if (status != previous)
        {
            EffectiveStatusChanged?.Invoke(status);
        }
    }

    /// <summary>
    /// Points the pose at a new target. With <paramref name="animate"/>, the offset starts at the pose (and velocity) the
    /// mark is in minus the new target's, so the blend begins exactly where the mark is.
    /// </summary>
    private void Retarget(AiSparkStatus status, bool twinkle, bool still, bool animate)
    {
        if (!animate)
        {
            _status = status;
            _twinkle = twinkle;
            _still = still;
            _blending = false;
            return;
        }
        const double h = 1.0 / 240;
        var rate = _still ? 0 : Math.Max(0, Speed);
        // Where the mark is and how fast each value moves (old target plus the running offset).
        AiSparkPose.Write(_status, _twinkle, _still, _phase, _target);
        AiSparkPose.Write(_status, _twinkle, _still, _phase + h, _ahead);
        for (var i = 0; i < AiSparkPose.Count; i++)
        {
            var (x, v) = OffsetAt(i, _clock);
            _offset[i] = _target[i] + x;
            _offsetVelocity[i] = (_ahead[i] - _target[i]) / h * rate + v;
        }
        _status = status;
        _twinkle = twinkle;
        _still = still;
        rate = _still ? 0 : Math.Max(0, Speed);
        AiSparkPose.Write(_status, _twinkle, _still, _phase, _target);
        AiSparkPose.Write(_status, _twinkle, _still, _phase + h, _ahead);
        for (var i = 0; i < AiSparkPose.Count; i++)
        {
            _offset[i] -= _target[i];
            _offsetVelocity[i] -= (_ahead[i] - _target[i]) / h * rate;
        }
        _blending = true;
        _blendReduced = _level != ArcMotionLevel.Full;
        _blendStart = _clock;
    }

    private (double X, double V) OffsetAt(int index, double clock)
    {
        if (!_blending)
        {
            return (0, 0);
        }
        var tau = clock - _blendStart;
        if (_blendReduced)
        {
            var p = tau / AiSparkPose.ReducedBlend;
            return p >= 1 ? (0, 0) : (_offset[index] * (1 - AiSparkPose.EaseOut(p)), 0);
        }
        return SpringSolver.Evaluate(AiSparkPose.Blend, _offset[index], _offsetVelocity[index], tau);
    }

    private bool BlendSettled(double clock) =>
        !_blending || clock - _blendStart >= (_blendReduced ? AiSparkPose.ReducedBlend : AiSparkPose.BlendSettle);

    private double ErrorMixAt(double clock)
    {
        var p = (clock - _errorStart) / _errorDuration;
        return p >= 1 ? _errorTo : _errorFrom + (_errorTo - _errorFrom) * AiSparkPose.EaseOut(p);
    }

    private double ParticleAt(double clock)
    {
        var p = (clock - _particleStart) / ParticleFade;
        return p >= 1 ? _particleTo : _particleFrom + (_particleTo - _particleFrom) * AiSparkPose.EaseOut(p);
    }

    private void UpdateParticles(bool animate)
    {
        var markSide = MarkSide;
        var target = _status == AiSparkStatus.Generating && _level == ArcMotionLevel.Full && markSide >= AiSparkPose.ParticleMinSize ? 1 : 0;
        if (target == _particleTo)
        {
            return;
        }
        var now = ParticleAt(_clock);
        _particleTo = target;
        if (animate)
        {
            _particleFrom = now;
            _particleStart = _clock;
        }
        else
        {
            _particleFrom = target;
            _particleStart = double.NegativeInfinity;
        }
    }

    private double MarkSide
    {
        get
        {
            var side = Math.Min(Bounds.Width, Bounds.Height);
            return IsTile ? side * TileMarkRatio : side;
        }
    }

    private void SettleNow()
    {
        _blending = false;
        _gatherStart = double.NaN;
        _shakeStart = double.NaN;
        _errorFrom = _errorTo;
        _errorStart = double.NegativeInfinity;
        _particleFrom = _particleTo;
        _particleStart = double.NegativeInfinity;
        if (AiSparkPose.SpinSpeed(_status) == 0)
        {
            _angle = 0;
            _spinSpeed = 0;
            _gliding = false;
        }
        Evaluate();
    }

    /// <summary>Whether anything still moves: a looping status, self-play, a blend, a gesture, the tone, the spin or the particles.</summary>
    internal bool NeedsFrames()
    {
        if (_level == ArcMotionLevel.Off)
        {
            return false;
        }
        if (IsSelfPlaying)
        {
            return true;
        }
        if (_level == ArcMotionLevel.Full && AiSparkPose.Loops(_status, _twinkle))
        {
            return true;
        }
        if (!BlendSettled(_clock))
        {
            return true;
        }
        if (GatherNow > 0 || (!double.IsNaN(_gatherStart) && _clock - _gatherStart < AiSparkPose.GatherIn + AiSparkPose.GatherOut))
        {
            return true;
        }
        if (!double.IsNaN(_shakeStart) && _clock - _shakeStart < AiSparkPose.ShakeDuration)
        {
            return true;
        }
        if (_clock - _errorStart < _errorDuration || _clock - _particleStart < ParticleFade)
        {
            return true;
        }
        return _level == ArcMotionLevel.Full && (_spinSpeed > 0.01 || _gliding || Math.Abs(HomeDistance(_angle)) > 0.01);
    }

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
        if (!_still)
        {
            _phase += dt * Math.Max(0, Speed);
        }
        if (IsSelfPlaying)
        {
            _playTime += dt;
            if (_playTime >= AiSparkSamples.SecondsAt(_playIndex))
            {
                _playTime = 0;
                _playIndex = (_playIndex + 1) % AiSparkSamples.Count;
                SetStatus(AiSparkSamples.StatusAt(_playIndex));
            }
        }
        StepSpin(dt);
        if (_blending && BlendSettled(_clock))
        {
            _blending = false;
        }
        if (!double.IsNaN(_gatherStart) && _clock - _gatherStart >= AiSparkPose.GatherIn + AiSparkPose.GatherOut)
        {
            _gatherStart = double.NaN;
        }
        if (!double.IsNaN(_shakeStart) && _clock - _shakeStart >= AiSparkPose.ShakeDuration)
        {
            _shakeStart = double.NaN;
        }
        Evaluate();
        InvalidateVisual();
        return !IsPaused && NeedsFrames();
    }

    private void StepSpin(double dt)
    {
        if (_level != ArcMotionLevel.Full)
        {
            return;
        }
        var target = AiSparkPose.SpinSpeed(_status) * Math.Max(0, Speed);
        if (target > 0)
        {
            if (_gliding)
            {
                _gliding = false;
            }
            // Speed eases toward the status's rate (thinking → writing speeds up smoothly, never jumps).
            _spinSpeed += (target - _spinSpeed) * (1 - Math.Exp(-dt / SpinEase));
            _angle = Wrap(_angle + _spinSpeed * dt);
            return;
        }
        if (!_gliding)
        {
            if (_spinSpeed <= 0.01 && Math.Abs(HomeDistance(_angle)) <= 0.01)
            {
                _spinSpeed = 0;
                return;
            }
            StartGlide();
        }
        var p = (_clock - _glideStart) / _glideDuration;
        if (p >= 1)
        {
            _angle = Wrap(_glideFrom + _glideDistance);
            _spinSpeed = 0;
            _gliding = false;
            return;
        }
        // A cubic Hermite from (angle, speed v) to (home, 0). With r = vT/d between 1.5 and 3 its speed only ever falls
        // (r = 2 is the plain quadratic ease-out); at rest (v = 0) it is a smoothstep.
        var d = _glideDistance;
        var vt = _glideSpeed * _glideDuration;
        var p2 = p * p;
        var p3 = p2 * p;
        _angle = Wrap(_glideFrom + d * (3 * p2 - 2 * p3) + vt * (p - 2 * p2 + p3));
        _spinSpeed = Math.Max(0, (d * (6 * p - 6 * p2) + vt * (1 - 4 * p + 3 * p2)) / _glideDuration);
    }

    private void StartGlide()
    {
        _gliding = true;
        _glideFrom = _angle;
        _glideStart = _clock;
        var v = _spinSpeed;
        if (v > 1)
        {
            // Forward to the next quarter turn a little ahead, in 2d/v (the quadratic ease-out that starts at v), at least
            // 0.35s and at most 3s. Only a slow spin far from home (r below 1.5) speeds up slightly before it settles.
            var home = Math.Ceiling((_angle + v * 0.12) / 90) * 90;
            _glideDistance = home - _angle;
            _glideDuration = Math.Clamp(2 * _glideDistance / v, 0.35, MaxGlide);
            _glideSpeed = v;
        }
        else
        {
            // Nearly still: the nearest quarter turn on a smoothstep.
            _glideDistance = HomeDistance(_angle);
            _glideDuration = AiSparkPose.GatherOut;
            _glideSpeed = 0;
        }
    }

    // The signed turn to the nearest quarter turn (the mark looks the same every 90 degrees).
    private static double HomeDistance(double angle) => Math.Round(angle / 90) * 90 - angle;

    private static double Wrap(double angle) => angle >= 360 ? angle - 360 * Math.Floor(angle / 360) : angle;

    private void Evaluate()
    {
        AiSparkPose.Write(_status, _twinkle, _still, _phase, _pose);
        if (_blending)
        {
            for (var i = 0; i < AiSparkPose.Count; i++)
            {
                _pose[i] += OffsetAt(i, _clock).X;
            }
        }
        var gather = GatherNow;
        if (gather > 0)
        {
            for (var i = 0; i < 4; i++)
            {
                _pose[AiSparkPose.Primary + i] *= 1 - 0.32 * gather;
                _pose[AiSparkPose.Diagonal + i] *= 1 - 0.42 * gather;
            }
            _pose[AiSparkPose.Core] *= 1 + 0.2 * gather;
            _pose[AiSparkPose.Scale] *= 1 - 0.05 * gather;
        }
        UpdateColors();
    }

    // Colors are set here (property changes and frames), never in Render: the brushes are reused and only recolored.
    private void UpdateColors()
    {
        var surface = ColorOf(SurfaceInk, Colors.White);
        var muted = ColorOf(MutedInk, Colors.Gray);
        var tone = OverrideBrush is { } custom
            ? ColorOf(custom, Colors.Gray)
            : Tone switch
            {
                AiSparkTone.Foreground => ColorOf(ForegroundInk, Colors.Black),
                AiSparkTone.Muted => muted,
                AiSparkTone.Inherit => ColorOf(InheritInk, ColorOf(ForegroundInk, Colors.Black)),
                _ => ColorOf(AccentInk, Colors.Black)
            };
        var error = ErrorMixAt(_clock);
        var color = tone;
        if (error > 0)
        {
            var danger = Oklch.MixOklab(ColorOf(DangerInk, Colors.Red), muted, ErrorMuteMix);
            color = Oklch.MixOklab(tone, danger, error);
        }
        var soft = Oklch.MixOklab(color, surface, SoftToneMix);
        Recolor(_rayBrush, color);
        Recolor(_softBrush, soft);
        Recolor(_tileBrush, Oklch.WithAlpha(color, TileTint * color.A / 255.0));
        // The diagonals' tone dims with the pose (error), mixing further toward the surface.
        var dim = Math.Clamp(1 - _pose[AiSparkPose.DiagonalTone], 0, 1);
        Recolor(_dimBrush, dim <= 0.001 ? soft : Oklch.MixOklab(color, surface, Math.Min(0.92, SoftToneMix + 0.45 * dim)));
    }

    private static void Recolor(SolidColorBrush brush, Color color)
    {
        if (brush.Color != color)
        {
            brush.Color = color;
        }
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush switch
    {
        ISolidColorBrush solid => solid.Color,
        IGradientBrush { GradientStops.Count: > 0 } gradient => gradient.GradientStops[0].Color,
        _ => fallback
    };

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0)
        {
            return;
        }
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        if (IsTile)
        {
            context.DrawEllipse(_tileBrush, null, center, side / 2, side / 2);
        }
        var markSide = IsTile ? side * TileMarkRatio : side;
        var weight = AiSparkPose.Weight(markSide);
        var dots = markSide < AiSparkPose.DotSize;
        var unit = markSide / 100 * _pose[AiSparkPose.Scale];
        var shake = ShakeNow;
        var transform = Matrix.CreateRotation(_angle * Math.PI / 180) * Matrix.CreateScale(unit, unit) *
                        Matrix.CreateTranslation(center.X + shake * unit, center.Y);

        using (context.PushTransform(transform))
        {
            // The soft core sits under the rays and fills the space between their bases.
            var core = CoreRadius * Math.Max(0, _pose[AiSparkPose.Core]) * (0.9 + 0.1 * weight);
            context.DrawEllipse(_softBrush, null, default, core, core);
            for (var i = 0; i < 4; i++)
            {
                var length = Math.Max(0, _pose[AiSparkPose.Diagonal + i]);
                if (dots)
                {
                    // Under 18px a diagonal ray would be a smear: a dot at the same place reads better.
                    var radius = 4.4 * weight * (0.7 + 0.3 * length);
                    var turn = s_diagonalTurns[i];
                    var at = turn.Transform(new Point(0, -(DotRadius + 3 * (length - 1))));
                    context.DrawEllipse(_dimBrush, null, at, radius, radius);
                }
                else
                {
                    var ray = _diagonal[i];
                    ray.Update(DiagonalStart, DiagonalStart + DiagonalReach * length, DiagonalHalfWidth * weight);
                    using (context.PushTransform(s_diagonalTurns[i]))
                    {
                        context.DrawGeometry(_dimBrush, null, ray.Geometry);
                    }
                }
            }
            for (var i = 0; i < 4; i++)
            {
                var ray = _primary[i];
                ray.Update(PrimaryStart, PrimaryStart + PrimaryReach * Math.Max(0, _pose[AiSparkPose.Primary + i]), PrimaryHalfWidth * weight);
                using (context.PushTransform(s_primaryTurns[i]))
                {
                    context.DrawGeometry(_rayBrush, null, ray.Geometry);
                }
            }

            var particles = ParticleAt(_clock);
            if (particles > 0.001)
            {
                using (context.PushOpacity(particles))
                {
                    var orbit = 2 * Math.PI * _phase / ParticleTurn - _angle * Math.PI / 180;
                    for (var k = 0; k < 2; k++)
                    {
                        var a = orbit + k * Math.PI;
                        var r = ParticleOrbit + 1.5 * Math.Sin(2 * a + _phase * 3);
                        context.DrawEllipse(_softBrush, null, new Point(Math.Sin(a) * r, -Math.Cos(a) * r), ParticleRadius, ParticleRadius);
                    }
                }
            }
        }
    }

    private static Matrix Turn(double degrees) => Matrix.CreateRotation(degrees * Math.PI / 180);
}
