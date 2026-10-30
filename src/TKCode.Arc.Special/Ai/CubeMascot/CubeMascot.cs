using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An isometric cube character that shows what an assistant is doing (Arc cube mascot), drawn from real 3D math: a
/// rounded silhouette, three faces shaded by how they face the light, and a face painted on the front side through its
/// projected plane. Idle tilts, floats and blinks; thinking turns the cube a quarter turn in eased steps and the face
/// fades in on the side that arrives at the front; writing tips the face down while lines print onto the ground in front
/// of it; done settles with a happy squint and a sparkle; error shakes once and shows worried brows. Status changes blend
/// from the pose on screen over 420ms, never a hard cut. Leave <see cref="Status"/> unset and the cube plays through every
/// state on a loop (for showcases).
/// </summary>
/// <remarks>
/// All motion runs on one Core <see cref="LiveLoop"/>, which pauses off screen and while hidden and keeps its clock, so
/// the cube resumes on the same frame. It is code-driven because a ControlTheme transition cannot blend between moving,
/// periodic poses or drive a 3D projection. Every frame moves cached paths in place and recolors cached brushes, so a
/// frame allocates nothing.
/// </remarks>
public class CubeMascot : Control
{
    /// <summary>The default size (96px).</summary>
    internal const double DefaultSize = 96;

    /// <summary>
    /// What the assistant is doing. Leave it unset (null) and the cube plays through idle, thinking, writing, done and
    /// error on a loop, which is meant for showcases; <see cref="StatusChanged"/> reports each step.
    /// </summary>
    public static readonly StyledProperty<CubeMascotStatus?> StatusProperty =
        AvaloniaProperty.Register<CubeMascot, CubeMascotStatus?>(nameof(Status));

    /// <summary>
    /// Width and height in px, from 24 (inline next to a message) to 160 (empty state). Below 56 a compact drawing takes
    /// over: a larger cube in the frame, bolder eyes, no mouth, shadow or printed lines.
    /// </summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(Size), DefaultSize,
            coerce: (_, v) => double.IsFinite(v) && v > 0 ? v : DefaultSize);

    /// <summary>The cube's body color; the theme sets the accent. The top and the shaded side are mixed from it.</summary>
    public static readonly StyledProperty<IBrush?> BodyBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(BodyBrush));

    /// <summary>The color of the eyes, brows and mouth; the theme sets the accent foreground.</summary>
    public static readonly StyledProperty<IBrush?> FaceBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(FaceBrush));

    /// <summary>Overrides the top face's shading anchor; null mixes it from <see cref="BodyBrush"/> toward white.</summary>
    public static readonly StyledProperty<IBrush?> TopBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(TopBrush));

    /// <summary>Overrides the front face's shading anchor; null uses <see cref="BodyBrush"/>.</summary>
    public static readonly StyledProperty<IBrush?> FrontBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(FrontBrush));

    /// <summary>Overrides the shaded side's anchor; null mixes it from <see cref="BodyBrush"/> toward black.</summary>
    public static readonly StyledProperty<IBrush?> SideBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(SideBrush));

    /// <summary>The color of the lines printed while writing; the theme sets the muted text color.</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(LineBrush));

    /// <summary>The color of the contact shadow (drawn translucent); null (default) uses the theme's shadow tone.</summary>
    public static readonly StyledProperty<IBrush?> ShadowBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(ShadowBrush));

    /// <summary>The color of the done sparkle; the theme sets the accent.</summary>
    public static readonly StyledProperty<IBrush?> SparkleBrushProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(SparkleBrush));

    /// <summary>Draws the contact shadow and the lines printed while writing (at 56px and up).</summary>
    public static readonly StyledProperty<bool> ShowsGroundProperty =
        AvaloniaProperty.Register<CubeMascot, bool>(nameof(ShowsGround), true);

    /// <summary>
    /// Freezes the cube on the current frame. It also pauses by itself off screen and while hidden, and resumes without
    /// a jump.
    /// </summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<CubeMascot, bool>(nameof(IsPaused));

    /// <summary>The assistant's name, used in the accessible label ("Assistant, thinking").</summary>
    public static readonly StyledProperty<string> AssistantNameProperty =
        AvaloniaProperty.Register<CubeMascot, string>(nameof(AssistantName), "Assistant");

    /// <summary>Overrides the whole accessible label; null (default) builds it from the name and the status.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<CubeMascot, string?>(nameof(AccessibleName));

    /// <summary>
    /// Announces status changes in a polite live region. Turn it on for the one mascot that carries status, not for
    /// decorative copies.
    /// </summary>
    public static readonly StyledProperty<bool> AnnounceProperty =
        AvaloniaProperty.Register<CubeMascot, bool>(nameof(Announce));

    /// <summary>Every word the cube speaks.</summary>
    public static readonly StyledProperty<CubeMascotLabels> LabelsProperty =
        AvaloniaProperty.Register<CubeMascot, CubeMascotLabels>(nameof(Labels), new CubeMascotLabels());

    /// <summary>The status the cube shows: <see cref="Status"/>, or the current self-play step while it is unset.</summary>
    public static readonly DirectProperty<CubeMascot, CubeMascotStatus> EffectiveStatusProperty =
        AvaloniaProperty.RegisterDirect<CubeMascot, CubeMascotStatus>(nameof(EffectiveStatus), o => o.EffectiveStatus);

    /// <summary>Raised when the self-playing loop (status unset) moves to the next state.</summary>
    public static readonly RoutedEvent<CubeMascotStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<CubeMascot, CubeMascotStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    // Token values, set by the theme.
    internal static readonly StyledProperty<double> CompactBelowProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(CompactBelow), 56);

    internal static readonly StyledProperty<double> TopLiftProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(TopLift), 0.2);

    internal static readonly StyledProperty<double> FrontShadeProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(FrontShade));

    internal static readonly StyledProperty<IBrush?> ShadowToneProperty =
        AvaloniaProperty.Register<CubeMascot, IBrush?>(nameof(ShadowTone));

    internal static readonly StyledProperty<double> SideShadeProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(SideShade), 0.26);

    internal static readonly StyledProperty<double> ShadowOpacityProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(ShadowOpacity), 0.12);

    internal static readonly StyledProperty<double> LineOpacityProperty =
        AvaloniaProperty.Register<CubeMascot, double>(nameof(LineOpacity), 0.7);

    // A four-point sparkle in a unit box, shared by every cube (never changed).
    private static readonly Geometry s_sparkle = BuildSparkle();

    private readonly LiveLoop _loop;
    private readonly CubeMascotScene _scene = new();
    private readonly CubeMascotShape _hull = new(8);
    private readonly CubeMascotShape[] _faces = [new(4), new(4), new(4), new(4), new(4), new(4)];
    private readonly PathGeometry _mouth = new();
    private readonly PathFigure _mouthFigure = new() { IsClosed = false, IsFilled = false };
    private readonly QuadraticBezierSegment _mouthCurve = new();
    private readonly double[] _pose = new double[CubeMascotPose.Count];
    private readonly double[] _target = new double[CubeMascotPose.Count];
    private readonly double[] _from = new double[CubeMascotPose.Count];
    private readonly SolidColorBrush[] _faceBrushes =
        [new(Colors.Gray), new(Colors.Gray), new(Colors.Gray), new(Colors.Gray), new(Colors.Gray), new(Colors.Gray)];
    private readonly SolidColorBrush _baseBrush = new(Colors.Gray);
    private readonly SolidColorBrush _inkBrush = new(Colors.White);
    private readonly SolidColorBrush _lineBrush = new(Colors.Gray);
    private readonly SolidColorBrush _shadowBrush = new(Colors.Black);
    private readonly SolidColorBrush _sparkleBrush = new(Colors.Gray);
    private readonly Pen _inkPen;
    private readonly Pen _linePen;
    private readonly Point[] _lineFrom = new Point[4];
    private readonly Point[] _lineTo = new Point[4];
    private readonly double[] _lineOpacity = new double[4];
    private readonly double[] _sideVisibility = new double[4];
    private readonly double[] _sideEyes = new double[4];

    private CubeMascotStatus _effectiveStatus;
    private ArcMotionLevel _level = ArcMotionLevel.Off;
    private double _clock;
    private double _statusStart;
    private double _writeStart;

    // The blend from the pose on screen toward the new status.
    private bool _blending;
    private double _blendStart;
    private double _blendDuration = CubeMascotPose.Blend;

    // The quarter turns: the yaw at rest between turns, and the turn in progress.
    private double _yaw = CubeMascotPose.HomeYaw;
    private bool _turning;
    private double _turnStart;
    private double _nextTurnAt;

    // One-shot gestures on the clock (NaN when idle).
    private double _shakeStart = double.NaN;
    private double _dipStart = double.NaN;

    // Blinks: the next one, the one in progress, and a second one that sometimes follows.
    private double _nextBlink = 1.4;
    private double _blinkStart = double.NaN;
    private bool _blinkTwice;
    private ulong _random = 0x2545F4914F6CDD1DUL;

    // Self-play while no status is requested.
    private int _playIndex;
    private double _playTime;

    // Drawing state composed outside Render.
    private Color _body;
    private Color _top;
    private Color _front;
    private Color _side;
    private bool _compact;
    private double _unit;
    private double _shadowOpacity;
    private Rect _shadowRect;
    private Point _sparkleAt;
    private double _sparkleScale;
    private double _sparkleTurn;
    private double _eyeOpen;
    private double _squint;
    private double _brow;
    private double _mouthCurveAmount;
    private double _gazeX;
    private double _gazeY;
    private double _ink;
    private bool _composed;

    static CubeMascot()
    {
        FocusableProperty.OverrideDefaultValue<CubeMascot>(false);
        ClipToBoundsProperty.OverrideDefaultValue<CubeMascot>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<CubeMascot>(Avalonia.Layout.HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<CubeMascot>(Avalonia.Layout.VerticalAlignment.Center);
        AffectsMeasure<CubeMascot>(SizeProperty);
    }

    /// <summary>Creates a cube mascot; with no status it plays through every state on a loop.</summary>
    public CubeMascot()
    {
        _inkPen = new Pen(_inkBrush, 0.055, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _linePen = new Pen(_lineBrush, 2, lineCap: PenLineCap.Round);
        _mouthFigure.Segments!.Add(_mouthCurve);
        _mouth.Figures!.Add(_mouthFigure);
        _loop = new LiveLoop(this, Step);
        // The motion level can also come from the theme's resources (a motion token): re-read it when they change.
        ResourcesChanged += (_, _) => RefreshLevel();
        CubeMascotPose.Rest(_pose);
        _effectiveStatus = CubeMascotSamples.StatusAt(0);
        WriteTarget();
        Array.Copy(_target, _pose, _pose.Length);
        UpdateColors();
        UpdateAccessibility();
    }

    /// <inheritdoc cref="StatusProperty"/>
    public CubeMascotStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="BodyBrushProperty"/>
    public IBrush? BodyBrush { get => GetValue(BodyBrushProperty); set => SetValue(BodyBrushProperty, value); }

    /// <inheritdoc cref="FaceBrushProperty"/>
    public IBrush? FaceBrush { get => GetValue(FaceBrushProperty); set => SetValue(FaceBrushProperty, value); }

    /// <inheritdoc cref="TopBrushProperty"/>
    public IBrush? TopBrush { get => GetValue(TopBrushProperty); set => SetValue(TopBrushProperty, value); }

    /// <inheritdoc cref="FrontBrushProperty"/>
    public IBrush? FrontBrush { get => GetValue(FrontBrushProperty); set => SetValue(FrontBrushProperty, value); }

    /// <inheritdoc cref="SideBrushProperty"/>
    public IBrush? SideBrush { get => GetValue(SideBrushProperty); set => SetValue(SideBrushProperty, value); }

    /// <inheritdoc cref="LineBrushProperty"/>
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <inheritdoc cref="ShadowBrushProperty"/>
    public IBrush? ShadowBrush { get => GetValue(ShadowBrushProperty); set => SetValue(ShadowBrushProperty, value); }

    /// <inheritdoc cref="SparkleBrushProperty"/>
    public IBrush? SparkleBrush { get => GetValue(SparkleBrushProperty); set => SetValue(SparkleBrushProperty, value); }

    /// <inheritdoc cref="ShowsGroundProperty"/>
    public bool ShowsGround { get => GetValue(ShowsGroundProperty); set => SetValue(ShowsGroundProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="AnnounceProperty"/>
    public bool Announce { get => GetValue(AnnounceProperty); set => SetValue(AnnounceProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CubeMascotLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="EffectiveStatusProperty"/>
    public CubeMascotStatus EffectiveStatus
    {
        get => _effectiveStatus;
        private set => SetAndRaise(EffectiveStatusProperty, ref _effectiveStatus, value);
    }

    /// <summary>Raised when the self-playing loop (status unset) moves to the next state.</summary>
    public event EventHandler<CubeMascotStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    internal double CompactBelow { get => GetValue(CompactBelowProperty); set => SetValue(CompactBelowProperty, value); }

    internal double TopLift { get => GetValue(TopLiftProperty); set => SetValue(TopLiftProperty, value); }

    internal double FrontShade { get => GetValue(FrontShadeProperty); set => SetValue(FrontShadeProperty, value); }

    internal IBrush? ShadowTone { get => GetValue(ShadowToneProperty); set => SetValue(ShadowToneProperty, value); }

    internal double SideShade { get => GetValue(SideShadeProperty); set => SetValue(SideShadeProperty, value); }

    internal double ShadowOpacity { get => GetValue(ShadowOpacityProperty); set => SetValue(ShadowOpacityProperty, value); }

    internal double LineOpacity { get => GetValue(LineOpacityProperty); set => SetValue(LineOpacityProperty, value); }

    // Test hooks.
    internal bool IsSelfPlaying => Status is null;
    internal bool IsLooping => _loop.IsRunning;
    internal bool IsBlending => _blending;
    internal bool IsTurning => _turning;
    internal bool IsCompact => _compact;
    internal double Yaw => CurrentYaw(out _);
    internal double ShakeNow => double.IsNaN(_shakeStart) ? 0 : CubeMascotPose.Shake(_clock - _shakeStart);
    internal double DipNow => double.IsNaN(_dipStart) ? 0 : CubeMascotPose.Dip(_clock - _dipStart);
    internal ReadOnlySpan<double> Pose => _pose;
    internal CubeMascotScene Scene => _scene;
    internal ReadOnlySpan<double> SideVisibility => _sideVisibility;
    internal ReadOnlySpan<double> LineOpacities => _lineOpacity;
    internal Color FaceColor(int face) => _faceBrushes[face].Color;
    internal Color InkColor => _inkBrush.Color;
    internal string SpokenName => string.IsNullOrEmpty(AccessibleName)
        ? (Labels ?? new CubeMascotLabels()).LabelFor(string.IsNullOrEmpty(AssistantName) ? "Assistant" : AssistantName, EffectiveStatus)
        : AccessibleName;

    /// <summary>Advances the clock by <paramref name="seconds"/> as one frame would (tests drive the motion with it).</summary>
    internal bool Advance(double seconds) => Step(seconds);

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override Size ArrangeOverride(Size finalSize) => new(Size, Size);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == StatusProperty)
        {
            if (change.GetNewValue<CubeMascotStatus?>() is { } requested)
            {
                SetStatus(requested);
            }
            else
            {
                _playIndex = 0;
                _playTime = 0;
                SetStatus(CubeMascotSamples.StatusAt(0));
            }
            UpdateLoop();
        }
        else if (property == IsPausedProperty)
        {
            UpdateLoop();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
        else if (property == BodyBrushProperty || property == FaceBrushProperty || property == TopBrushProperty ||
                 property == FrontBrushProperty || property == SideBrushProperty || property == LineBrushProperty ||
                 property == ShadowBrushProperty || property == SparkleBrushProperty || property == TopLiftProperty ||
                 property == SideShadeProperty || property == FrontShadeProperty || property == ShadowToneProperty)
        {
            UpdateColors();
            Compose();
            InvalidateVisual();
        }
        else if (property == BoundsProperty || property == ShowsGroundProperty || property == CompactBelowProperty ||
                 property == ShadowOpacityProperty || property == LineOpacityProperty)
        {
            Compose();
            InvalidateVisual();
        }
        else if (property == AssistantNameProperty || property == AccessibleNameProperty || property == AnnounceProperty ||
                 property == LabelsProperty)
        {
            UpdateAccessibility();
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
        // Off screen nothing is shown, so the blend and the one-shot gestures end where they would have ended.
        SettleNow();
    }

    private bool Animates => _level == ArcMotionLevel.Full && !IsPaused && this.IsAttachedToVisualTree();

    private bool Still => _level != ArcMotionLevel.Full;

    private void RefreshLevel()
    {
        var level = this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level != _level)
        {
            _level = level;
            if (level != ArcMotionLevel.Full)
            {
                // Reduced and off: every state is a still pose that switches in place.
                SettleNow();
                _yaw = CubeMascotPose.HomeYaw;
                _turning = false;
            }
            Compose();
            InvalidateVisual();
        }
        UpdateLoop();
    }

    private void SetStatus(CubeMascotStatus status)
    {
        var previous = _effectiveStatus;
        if (Animates)
        {
            // Blend every pose value from the frame on screen.
            Array.Copy(_pose, _from, _pose.Length);
            _blending = true;
            _blendStart = _clock;
            _blendDuration = status == CubeMascotStatus.Error ? CubeMascotPose.ErrorBlend : CubeMascotPose.Blend;
            if (status != previous)
            {
                if (status == CubeMascotStatus.Done)
                {
                    _dipStart = _clock;
                }
                if (status == CubeMascotStatus.Error)
                {
                    _shakeStart = _clock;
                }
            }
        }
        else
        {
            _blending = false;
            if (Still)
            {
                _turning = false;
                _yaw = CubeMascotPose.HomeYaw;
            }
        }
        if (status != previous)
        {
            _statusStart = _clock;
            if (status == CubeMascotStatus.Writing)
            {
                _writeStart = _clock;
            }
            if (status == CubeMascotStatus.Thinking)
            {
                // A turn still finishing goes on; the next one starts after the hold.
                _nextTurnAt = (_turning ? _turnStart + CubeMascotPose.TurnDuration : _clock) + CubeMascotPose.TurnHold;
            }
        }
        EffectiveStatus = status;
        Compose();
        InvalidateVisual();
        if (status != previous)
        {
            UpdateAccessibility();
        }
    }

    private void SettleNow()
    {
        _blending = false;
        _shakeStart = double.NaN;
        _dipStart = double.NaN;
        _blinkStart = double.NaN;
        if (_turning)
        {
            _turning = false;
            _yaw = Normalize(_yaw + 90);
        }
        Compose();
    }

    /// <summary>Whether anything still moves: every status but a settled error loops, plus self-play, blends and gestures.</summary>
    internal bool NeedsFrames()
    {
        if (_level != ArcMotionLevel.Full)
        {
            return false;
        }
        if (IsSelfPlaying || _blending || _turning)
        {
            return true;
        }
        if (!double.IsNaN(_shakeStart) || !double.IsNaN(_dipStart))
        {
            return true;
        }
        return _effectiveStatus != CubeMascotStatus.Error;
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
        if (IsSelfPlaying)
        {
            _playTime += dt;
            if (_playTime >= CubeMascotSamples.SecondsAt(_playIndex))
            {
                _playTime = 0;
                _playIndex = (_playIndex + 1) % CubeMascotSamples.Count;
                var next = CubeMascotSamples.StatusAt(_playIndex);
                SetStatus(next);
                RaiseEvent(new CubeMascotStatusChangedEventArgs(StatusChangedEvent, next));
            }
        }
        StepTurn();
        StepBlink();
        if (_blending && _clock - _blendStart >= _blendDuration)
        {
            _blending = false;
        }
        if (!double.IsNaN(_shakeStart) && _clock - _shakeStart >= CubeMascotPose.ShakeDuration)
        {
            _shakeStart = double.NaN;
        }
        if (!double.IsNaN(_dipStart) && _clock - _dipStart >= CubeMascotPose.DipDuration)
        {
            _dipStart = double.NaN;
        }
        Compose();
        InvalidateVisual();
        return !IsPaused && NeedsFrames();
    }

    private void StepTurn()
    {
        if (_turning && _clock - _turnStart >= CubeMascotPose.TurnDuration)
        {
            _turning = false;
            _yaw = Normalize(_yaw + 90);
            _nextTurnAt = _turnStart + CubeMascotPose.TurnDuration + CubeMascotPose.TurnHold;
        }
        if (!_turning && _effectiveStatus == CubeMascotStatus.Thinking && _clock >= _nextTurnAt)
        {
            _turning = true;
            _turnStart = _clock;
        }
    }

    private void StepBlink()
    {
        if (!double.IsNaN(_blinkStart) && _clock - _blinkStart >= CubeMascotPose.BlinkDuration)
        {
            _blinkStart = double.NaN;
            if (_blinkTwice)
            {
                _blinkTwice = false;
                _nextBlink = _clock + 0.12;
            }
            else
            {
                _nextBlink = _clock + CubeMascotPose.BlinkMin + (CubeMascotPose.BlinkMax - CubeMascotPose.BlinkMin) * Random();
            }
        }
        if (double.IsNaN(_blinkStart) && _clock >= _nextBlink)
        {
            if (_effectiveStatus == CubeMascotStatus.Idle && !_blending)
            {
                _blinkStart = _clock;
                _blinkTwice = _nextBlink - _clock < 0.5 && Random() < 0.28;
            }
            else
            {
                _nextBlink = _clock + CubeMascotPose.BlinkMin;
            }
        }
    }

    // xorshift64: blink timing varies per cube without allocating.
    private double Random()
    {
        _random ^= _random << 13;
        _random ^= _random >> 7;
        _random ^= _random << 17;
        return (_random >> 11) * (1.0 / (1UL << 53));
    }

    private double CurrentYaw(out double progress)
    {
        progress = 0;
        if (!_turning)
        {
            return _yaw;
        }
        progress = Math.Clamp((_clock - _turnStart) / CubeMascotPose.TurnDuration, 0, 1);
        return _yaw + 90 * CubeMascotPose.EaseInOutCubic(progress);
    }

    private static double Normalize(double yaw)
    {
        // Every quarter turn looks the same: keep the rest yaw near home so it never grows.
        var offset = (yaw - CubeMascotPose.HomeYaw) % 360;
        if (offset < 0)
        {
            offset += 360;
        }
        return CubeMascotPose.HomeYaw + offset;
    }

    private void WriteTarget()
    {
        CurrentYaw(out var progress);
        var since = _clock - _statusStart;
        CubeMascotPose.Write(_effectiveStatus, Still, _clock, since, _turning ? progress : 0, _target);
    }

    /// <summary>Computes the pose for the clock and moves every path, point and brush the next render draws.</summary>
    private void Compose()
    {
        WriteTarget();
        if (_blending)
        {
            var e = CubeMascotPose.EaseInOutCubic((_clock - _blendStart) / _blendDuration);
            for (var i = 0; i < CubeMascotPose.Count; i++)
            {
                _pose[i] = _from[i] + (_target[i] - _from[i]) * e;
            }
        }
        else
        {
            Array.Copy(_target, _pose, _pose.Length);
        }

        var width = Bounds.Width > 0 ? Bounds.Width : Size;
        var height = Bounds.Height > 0 ? Bounds.Height : Size;
        var side = Math.Min(width, height);
        _compact = side < CompactBelow;
        _unit = side * (_compact ? 0.5 : 0.36);
        var origin = new Point(width / 2, height * (_compact ? 0.5 : 0.42));

        var shake = ShakeNow;
        var lift = _pose[CubeMascotPose.Lift] + DipNow;
        var yaw = CurrentYaw(out _) + _pose[CubeMascotPose.Sway];
        _scene.Update(yaw, _pose[CubeMascotPose.Roll] + 40 * shake, _pose[CubeMascotPose.Pitch], lift, shake, _unit, origin);
        _hull.Set(_scene.Hull, _scene.HullCount, _unit * (_compact ? 0.12 : 0.14));
        for (var f = 0; f < CubeMascotScene.FaceCount; f++)
        {
            if (_scene.Facing[f] > 0.001)
            {
                _faces[f].SetQuad(_scene.Corners, CubeMascotScene.FaceCorners(f));
                Recolor(_faceBrushes[f], Shade(_scene.Light[f]));
            }
        }
        Recolor(_baseBrush, _front);

        // The painted face: visible on the side at the front, fading on a side that turns away, opening on one that arrives.
        var blink = double.IsNaN(_blinkStart) ? 0 : CubeMascotPose.Blink(_clock - _blinkStart);
        for (var s = 0; s < 4; s++)
        {
            var angle = _scene.SideAngles[s];
            _sideVisibility[s] = CubeMascotPose.FaceVisibility(angle) * CubeMascotPose.SmoothStep(0.04, 0.22, _scene.Facing[s]);
            _sideEyes[s] = CubeMascotPose.FaceEyes(angle);
        }
        _eyeOpen = Math.Max(0, _pose[CubeMascotPose.EyeOpen]) * (1 - blink);
        _squint = Math.Clamp(_pose[CubeMascotPose.Squint], 0, 1);
        _brow = Math.Clamp(_pose[CubeMascotPose.Brow], 0, 1);
        _mouthCurveAmount = _pose[CubeMascotPose.Mouth];
        _gazeX = _pose[CubeMascotPose.GazeX];
        _gazeY = _pose[CubeMascotPose.GazeY];
        var thickness = _compact ? 0.075 : 0.055;
        if (_inkPen.Thickness != thickness)
        {
            _inkPen.Thickness = thickness;
        }
        UpdateMouth();

        // The ground: the contact shadow and the printed lines (drawn at 56px and up).
        var ground = ShowsGround && !_compact;
        var center = CubeMascotScene.Ground(0, 0, _unit, origin, shake);
        var rx = _unit * 0.74 * Math.Clamp(1 - 1.4 * Math.Max(0, lift), 0.6, 1.1);
        var ry = rx * Math.Sin(CubeMascotPose.CameraPitch * Math.PI / 180);
        _shadowRect = new Rect(center.X - rx, center.Y - ry, rx * 2, ry * 2);
        _shadowOpacity = ground ? ShadowOpacity * Math.Clamp(1 - 2.5 * lift, 0.4, 1.2) : 0;
        _ink = ground ? Math.Clamp(_pose[CubeMascotPose.Ink], 0, 1) : 0;
        UpdateLines(origin);
        var lineThickness = Math.Max(1, _unit * 0.07);
        if (_linePen.Thickness != lineThickness)
        {
            _linePen.Thickness = lineThickness;
        }

        // The sparkle: grows and turns in when done, then twinkles slowly.
        var sparkle = Math.Clamp(_pose[CubeMascotPose.Sparkle], 0, 1);
        var twinkle = Still ? 0 : Math.Sin(2 * Math.PI * _clock / 1.9);
        _sparkleScale = sparkle * side * (_compact ? 0.11 : 0.085) * (1 + 0.1 * twinkle);
        _sparkleTurn = (-90 * (1 - sparkle) + (Still ? 0 : 8 * Math.Sin(2 * Math.PI * _clock / 2.6))) * Math.PI / 180;
        _sparkleAt = new Point(width * (_compact ? 0.86 : 0.83), height * (_compact ? 0.14 : 0.16));
        _composed = true;
    }

    private void UpdateMouth()
    {
        const double y = 0.2;
        const double half = 0.075;
        var dx = _gazeX * 0.5;
        var dy = _gazeY * 0.5;
        var start = new Point(-half + dx, y + dy);
        if (_mouthFigure.StartPoint != start)
        {
            _mouthFigure.StartPoint = start;
        }
        var control = new Point(dx, y + dy + 0.11 * _mouthCurveAmount);
        var end = new Point(half + dx, y + dy);
        if (_mouthCurve.Point1 != control)
        {
            _mouthCurve.Point1 = control;
        }
        if (_mouthCurve.Point2 != end)
        {
            _mouthCurve.Point2 = end;
        }
    }

    // Four lines in flight: each slides out from under the face along the ground for 1.44s, fading in and out.
    private void UpdateLines(Point origin)
    {
        var home = CubeMascotPose.HomeYaw * Math.PI / 180;
        var (fx, fz) = (Math.Sin(home), Math.Cos(home));
        var (rx, rz) = (Math.Cos(home), -Math.Sin(home));
        var time = _clock - _writeStart;
        var newest = (long)Math.Floor(time / CubeMascotPose.LineInterval);
        for (var i = 0; i < 4; i++)
        {
            double travel;
            double opacity;
            long index;
            if (Still)
            {
                // The still pose: three printed lines at rest.
                index = i;
                travel = 0.3 + 0.25 * i;
                opacity = i < 3 ? 1 : 0;
            }
            else
            {
                index = newest - i;
                (travel, opacity) = index < 0 ? (0, 0) : CubeMascotPose.Line(time - index * CubeMascotPose.LineInterval);
            }
            _lineOpacity[i] = opacity * _ink * LineOpacity;
            var d = 0.36 + 0.95 * travel;
            var start = -0.34;
            var end = start + CubeMascotPose.LineLength(index);
            _lineFrom[i] = CubeMascotScene.Ground(d * fx + start * rx, d * fz + start * rz, _unit, origin, 0);
            _lineTo[i] = CubeMascotScene.Ground(d * fx + end * rx, d * fz + end * rz, _unit, origin, 0);
        }
    }

    private Color Shade(double light)
    {
        var top = CubeMascotScene.TopAnchor;
        var front = CubeMascotScene.FrontAnchor;
        var side = CubeMascotScene.SideAnchor;
        return light >= front
            ? Mix(_front, _top, (light - front) / (top - front))
            : Mix(_side, _front, (light - side) / (front - side));
    }

    private static Color Mix(Color a, Color b, double t) => t <= 0 ? a : t >= 1 ? b : Oklch.MixOklab(a, b, t);

    private void UpdateColors()
    {
        _body = ColorOf(BodyBrush, Color.FromRgb(0x3f, 0x3f, 0x46));
        _front = FrontBrush is null
            ? FrontShade > 0 ? Oklch.MixOklab(_body, Colors.Black, FrontShade) : _body
            : ColorOf(FrontBrush, _body);
        _top = TopBrush is null ? Oklch.MixOklab(_body, Colors.White, TopLift) : ColorOf(TopBrush, _body);
        _side = SideBrush is null ? Oklch.MixOklab(_body, Colors.Black, SideShade) : ColorOf(SideBrush, _body);
        Recolor(_inkBrush, ColorOf(FaceBrush, Colors.White));
        Recolor(_lineBrush, ColorOf(LineBrush, Colors.Gray));
        Recolor(_shadowBrush, ColorOf(ShadowBrush, ColorOf(ShadowTone, Colors.Black)));
        Recolor(_sparkleBrush, ColorOf(SparkleBrush, _body));
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
        ISolidColorBrush solid => Oklch.WithAlpha(solid.Color, solid.Color.A / 255.0 * solid.Opacity),
        IGradientBrush { GradientStops.Count: > 0 } gradient => gradient.GradientStops[0].Color,
        _ => fallback
    };

    private void UpdateAccessibility()
    {
        AutomationProperties.SetName(this, SpokenName);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Image);
        AutomationProperties.SetLiveSetting(this, Announce ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
    }

    public override void Render(DrawingContext context)
    {
        if (!_composed || _unit <= 0 || _scene.HullCount < 3)
        {
            return;
        }
        if (_shadowOpacity > 0.001)
        {
            using (context.PushOpacity(Math.Min(1, _shadowOpacity)))
            {
                context.DrawEllipse(_shadowBrush, null, _shadowRect);
            }
        }
        if (_ink > 0.001)
        {
            for (var i = 3; i >= 0; i--)
            {
                if (_lineOpacity[i] > 0.002)
                {
                    using (context.PushOpacity(Math.Min(1, _lineOpacity[i])))
                    {
                        context.DrawLine(_linePen, _lineFrom[i], _lineTo[i]);
                    }
                }
            }
        }

        // The cube: the rounded silhouette clips the faces, so the outline is soft and the edges between faces exact.
        using (context.PushGeometryClip(_hull.Geometry))
        {
            context.DrawGeometry(_baseBrush, null, _hull.Geometry);
            for (var f = 0; f < CubeMascotScene.FaceCount; f++)
            {
                if (_scene.Facing[f] > 0.001)
                {
                    context.DrawGeometry(_faceBrushes[f], null, _faces[f].Geometry);
                }
            }
            for (var s = 0; s < 4; s++)
            {
                var visibility = _sideVisibility[s];
                if (visibility > 0.01 && _scene.Facing[s] > 0)
                {
                    using (context.PushTransform(_scene.SideFrames[s]))
                    using (context.PushOpacity(Math.Min(1, visibility)))
                    {
                        DrawFace(context, _sideEyes[s]);
                    }
                }
            }
        }

        if (_sparkleScale > 0.05)
        {
            var transform = Matrix.CreateRotation(_sparkleTurn) * Matrix.CreateScale(_sparkleScale, _sparkleScale) *
                            Matrix.CreateTranslation(_sparkleAt.X, _sparkleAt.Y);
            using (context.PushTransform(transform))
            {
                context.DrawGeometry(_sparkleBrush, null, s_sparkle);
                context.DrawEllipse(_sparkleBrush, null, new Point(1.35, 1.3), 0.2, 0.2);
            }
        }
    }

    // The eyes, brows and mouth in face coordinates (-0.5 to 0.5, y down), drawn through the side's projected plane.
    private void DrawFace(DrawingContext context, double eyes)
    {
        var compact = _compact;
        var ex = compact ? 0.19 : 0.17;
        var ey = (compact ? -0.03 : -0.05) + _gazeY;
        var ew = compact ? 0.15 : 0.1;
        var eh = compact ? 0.28 : 0.21;
        var line = _inkPen.Thickness;
        var open = _eyeOpen * eyes;
        // Open pills pass through a closed line into the squint (and back), never a jump.
        var pill = Math.Max(0, open - _squint);
        var chevron = Math.Max(0, _squint - open);
        for (var k = -1; k <= 1; k += 2)
        {
            var cx = k * ex + _gazeX;
            if (chevron <= 0)
            {
                var h = line + (eh - line) * pill;
                var w = ew * (1 + 0.5 * (1 - pill));
                var r = Math.Min(w, h) / 2;
                context.DrawRectangle(_inkBrush, null, new Rect(cx - w / 2, ey - h / 2, w, h), r, r);
            }
            else
            {
                var hw = ew * 0.75;
                var rise = 0.1 * chevron;
                context.DrawLine(_inkPen, new Point(cx - hw, ey + rise / 2), new Point(cx, ey - rise / 2));
                context.DrawLine(_inkPen, new Point(cx, ey - rise / 2), new Point(cx + hw, ey + rise / 2));
            }
        }
        if (_brow > 0.01)
        {
            using (context.PushOpacity(_brow))
            {
                for (var k = -1; k <= 1; k += 2)
                {
                    var outer = new Point(k * (ex + 0.08) + _gazeX, ey - eh / 2 - 0.06);
                    var inner = new Point(k * (ex - 0.06) + _gazeX, ey - eh / 2 - 0.13);
                    context.DrawLine(_inkPen, outer, inner);
                }
            }
        }
        if (!compact)
        {
            context.DrawGeometry(null, _inkPen, _mouth);
        }
    }

    private static Geometry BuildSparkle()
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            const double pinch = 0.16;
            ctx.BeginFigure(new Point(0, -1), true);
            ctx.QuadraticBezierTo(new Point(pinch, -pinch), new Point(1, 0));
            ctx.QuadraticBezierTo(new Point(pinch, pinch), new Point(0, 1));
            ctx.QuadraticBezierTo(new Point(-pinch, pinch), new Point(-1, 0));
            ctx.QuadraticBezierTo(new Point(-pinch, -pinch), new Point(0, -1));
            ctx.EndFigure(true);
        }
        return geometry;
    }
}
