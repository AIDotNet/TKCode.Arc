using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The line drawing of <see cref="OnboardingIllustration"/>: twelve shapes (seven rounded rectangles, a dotted trail,
/// three lines and a check mark) in a 320 × 200 view box scaled to fit, at most <see cref="MaxArtWidth"/> wide. Strokes
/// are in view-box units, so they thin out with the drawing. Switching scenes springs every shape from where it is (with
/// its velocity) to the new scene: centers and line ends on the position spring, sizes, radii, rotations and the trail on
/// the shape spring, opacities on a 240ms tween, colors crossing over in 480ms, the check drawing itself in. A switch from
/// rest staggers the shapes (A … G 30ms apart, in reverse when going back; trail 100ms; lines 120/160/200ms; check
/// 320ms); a switch within 660ms of the last one moves everything at once. A mouse over the stage shifts the shapes by
/// depth (<see cref="PointAt"/>). Reduced motion crossfades the whole scene in 240ms; Off switches at once.
/// </summary>
/// <remarks>
/// Code-driven motion: every shape is a vector of springs that keeps its velocity when retargeted mid-flight, starts
/// after a delay that depends on the direction and on how long ago the last switch was, and is drawn in one
/// <see cref="Render"/> pass. A ControlTheme transition animates one property toward one value and restarts from zero
/// velocity. The frames come from a Core <see cref="LiveLoop"/> that stops once everything has settled; frames reuse
/// the cached brushes and pens (only the trail's curve is rebuilt while it moves).
/// </remarks>
internal sealed class OnboardingArt : Control
{
    private const int RectCount = OnboardingArtScenes.RectCount;
    private const int LineCount = OnboardingArtScenes.LineCount;
    private const int RectStride = 7;

    /// <summary>Seconds between two shapes of a staggered switch.</summary>
    internal const double SlotStagger = .03;

    /// <summary>The trail's curve starts moving after 100ms; it fades in after 160ms (and out at once).</summary>
    internal const double TrailMoveDelay = .1;

    internal const double TrailShowDelay = .16;

    /// <summary>The check starts drawing 320ms after a switch from rest.</summary>
    internal const double CheckDelay = .32;

    /// <summary>A switch within this many seconds of the last one moves every shape at once (6 × 30ms + 480ms).</summary>
    internal const double RestGap = .66;

    private const double OpacitySeconds = .24;
    private const double ColorSeconds = .48;
    private const double CheckDrawSeconds = .48;
    private const double CheckShowSeconds = .01;
    private const double CheckHideSeconds = .16;
    private const double CrossfadeSeconds = .24;

    /// <summary>How far a shape of depth 1 follows the pointer at the stage's edge (view-box units).</summary>
    private const double DriftX = 6;

    private const double DriftY = 4;

    private static readonly double[] s_lineDelay = [.12, .16, .2];

    /// <summary>Centers and line ends (visualDuration .4, no bounce: ω ≈ 13.1).</summary>
    private static readonly SpringSpec s_position = SpringSpec.FromVisual(0.4, 0);

    /// <summary>Sizes, radii, rotations and the trail (visualDuration .42, no bounce: ω ≈ 12.5).</summary>
    private static readonly SpringSpec s_shape = SpringSpec.FromVisual(0.42, 0);

    /// <summary>The pointer follow (visualDuration .5, no bounce: ω ≈ 10.5).</summary>
    private static readonly SpringSpec s_parallax = SpringSpec.FromVisual(0.5, 0);

    /// <summary>CSS ease-out: the check's quick fade and its undraw.</summary>
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    private static readonly Color s_transparent = Color.FromArgb(0, 0, 0, 0);

    #region Theme properties (set by the OnboardingIllustration theme)

    internal static readonly StyledProperty<IBrush?> PaperBrushProperty =
        AvaloniaProperty.Register<OnboardingArt, IBrush?>(nameof(PaperBrush));

    internal static readonly StyledProperty<IBrush?> ShadeBrushProperty =
        AvaloniaProperty.Register<OnboardingArt, IBrush?>(nameof(ShadeBrush));

    internal static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<OnboardingArt, IBrush?>(nameof(InkBrush));

    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<OnboardingArt, IBrush?>(nameof(MutedBrush));

    internal static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<OnboardingArt, IBrush?>(nameof(AccentBrush));

    internal static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<OnboardingArt, double>(nameof(StrokeWidth), 1.36);

    internal static readonly StyledProperty<double> CheckStrokeWidthProperty =
        AvaloniaProperty.Register<OnboardingArt, double>(nameof(CheckStrokeWidth), 1.6);

    internal static readonly StyledProperty<double> MaxArtWidthProperty =
        AvaloniaProperty.Register<OnboardingArt, double>(nameof(MaxArtWidth), 352);

    /// <summary>Fill of pages, people and the board (the raised surface).</summary>
    internal IBrush? PaperBrush { get => GetValue(PaperBrushProperty); set => SetValue(PaperBrushProperty, value); }

    /// <summary>Fill of the shaded shapes (the border color).</summary>
    internal IBrush? ShadeBrush { get => GetValue(ShadeBrushProperty); set => SetValue(ShadeBrushProperty, value); }

    /// <summary>Outline of pages and people (the foreground).</summary>
    internal IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    /// <summary>Outline of shaded shapes, plain lines and the trail (the muted text color).</summary>
    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <summary>The note's outline, highlighted lines and the check.</summary>
    internal IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>Stroke of shapes, lines and trail in view-box units (1.5px at full size).</summary>
    internal double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    /// <summary>Stroke of the check in view-box units.</summary>
    internal double CheckStrokeWidth { get => GetValue(CheckStrokeWidthProperty); set => SetValue(CheckStrokeWidthProperty, value); }

    /// <summary>The widest the drawing gets; it keeps its 8:5 aspect ratio.</summary>
    internal double MaxArtWidth { get => GetValue(MaxArtWidthProperty); set => SetValue(MaxArtWidthProperty, value); }

    #endregion

    private readonly LiveLoop _loop;
    private readonly SpringChannel[] _center = new SpringChannel[RectCount];
    private readonly SpringChannel[] _shape = new SpringChannel[RectCount];
    private readonly Tween[] _opacity = new Tween[RectCount];
    private readonly OnboardingInk[] _ink = new OnboardingInk[RectCount];
    private readonly ColorTween[] _rectColor = new ColorTween[RectCount];
    private readonly SpringChannel _trail = new(6, s_shape);
    private Tween _trailOpacity;
    private readonly SpringChannel[] _line = new SpringChannel[LineCount];
    private readonly bool[] _lineAccent = new bool[LineCount];
    private readonly ColorTween[] _lineColor = new ColorTween[LineCount];
    private Tween _checkOpacity;
    private Tween _checkDrawn;
    private readonly SpringChannel _sway = new(2, s_parallax);

    private readonly Layer _live = new();
    private readonly Layer _ghost = new();
    private bool _crossfading;
    private double _crossfadeStart;

    private readonly Color[] _inkFill = new Color[3];
    private readonly Color[] _inkStroke = new Color[3];
    private Color _muted;
    private Color _accent;

    private OnboardingArtScene _scene = OnboardingArtScenes.Folder;
    private double _clock;
    private double _lastSwitch = double.NegativeInfinity;

    static OnboardingArt()
    {
        AffectsMeasure<OnboardingArt>(MaxArtWidthProperty);
        FocusableProperty.OverrideDefaultValue<OnboardingArt>(false);
    }

    public OnboardingArt()
    {
        for (var i = 0; i < RectCount; i++)
        {
            _center[i] = new SpringChannel(2, s_position);
            _shape[i] = new SpringChannel(4, s_shape);
        }
        for (var j = 0; j < LineCount; j++)
        {
            _line[j] = new SpringChannel(4, s_position);
        }
        CaptureColors();
        Jump();
        UpdatePens();
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += OnRunningChanged;
    }

    #region Test hooks

    internal OnboardingArtScene Scene => _scene;

    /// <summary>The engine's own time (seconds of frames run).</summary>
    internal double Clock => _clock;

    /// <summary>The engine wants frames: something is still moving, waiting, fading or crossing over.</summary>
    internal bool IsBusy => _loop.IsActive;

    internal bool IsCrossfading => _crossfading;

    internal (double X, double Y) CenterOf(int slot) => (_center[slot].Pos[0], _center[slot].Pos[1]);

    /// <summary>Width, height, radius, rotation.</summary>
    internal double[] ShapeOf(int slot) => _shape[slot].Pos;

    internal double OpacityOf(int slot) => _opacity[slot].Value(_clock);

    internal double[] LineOf(int line) => _line[line].Pos;

    internal double[] TrailPoints => _trail.Pos;

    internal double TrailOpacity => _trailOpacity.Value(_clock);

    internal double CheckOpacity => _checkOpacity.Value(_clock);

    internal double CheckDrawn => _checkDrawn.Value(_clock);

    internal Color FillOf(int slot) => _live.Fill[slot].Color;

    internal Color StrokeOf(int slot) => _live.Stroke[slot].Color;

    internal Color LineColorOf(int line) => _live.LineBrush[line].Color;

    internal (double X, double Y) Sway => (_sway.Pos[0], _sway.Pos[1]);

    /// <summary>The drawn position of a rectangle's center (with the pointer offset).</summary>
    internal (double X, double Y) DrawnCenterOf(int slot) => (_live.Rect[slot * RectStride], _live.Rect[slot * RectStride + 1]);

    /// <summary>The delay of rectangle <paramref name="slot"/> in a switch from rest.</summary>
    internal static double RectDelay(int slot, bool forward) => SlotStagger * (forward ? slot : RectCount - 1 - slot);

    /// <summary>The delay of line <paramref name="line"/> in a switch from rest.</summary>
    internal static double LineDelay(int line, bool forward) => s_lineDelay[forward ? line : LineCount - 1 - line];

    #endregion

    /// <summary>
    /// Moves the drawing to <paramref name="scene"/>, named <paramref name="label"/>. <paramref name="forward"/> (a later
    /// step) staggers the shapes A to G, otherwise G to A.
    /// </summary>
    public void Show(OnboardingScene scene, bool forward, string? label)
    {
        AutomationProperties.SetName(this, label ?? string.Empty);
        var next = OnboardingArtScenes.Get(scene);
        if (ReferenceEquals(next, _scene))
        {
            return;
        }
        _scene = next;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || !IsEffectivelyVisible || level == ArcMotionLevel.Off)
        {
            Jump();
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            Crossfade();
            return;
        }
        Morph(next, forward);
    }

    /// <summary>Points the parallax at a stage position in −1 … 1 (a mouse move over the stage, full motion only).</summary>
    public void PointAt(double x, double y)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _sway.Retarget(Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
        if (!_loop.IsActive)
        {
            _loop.Start();
        }
    }

    /// <summary>The pointer left the stage: the shapes drift back.</summary>
    public void ReleasePointer()
    {
        if (_sway.Target[0] == 0 && _sway.Target[1] == 0)
        {
            return;
        }
        _sway.Retarget(0, 0);
        if (!_loop.IsActive)
        {
            _loop.Start();
        }
    }

    private void Morph(OnboardingArtScene next, bool forward)
    {
        var now = _clock;
        // Arc staggers only a switch from rest; a quick second press retargets every shape at once.
        var rapid = now - _lastSwitch < RestGap;
        _lastSwitch = now;
        var centers = next.Centers;
        var shapes = next.Shapes;
        for (var i = 0; i < RectCount; i++)
        {
            var rect = next.Rects[i];
            var delay = rapid ? 0 : RectDelay(i, forward);
            _center[i].Queue(now + delay, centers[i]);
            _shape[i].Queue(now + delay, shapes[i]);
            _opacity[i].To(now, rect.Opacity, delay, OpacitySeconds, ArcEasings.Standard);
            if (rect.Ink != _ink[i])
            {
                // Colors cross over at once, without the stagger (a CSS transition on the class change).
                _rectColor[i].Retarget(_rectColor[i].Evaluate(now), _inkFill[(int)rect.Ink], _inkStroke[(int)rect.Ink], now);
                _ink[i] = rect.Ink;
            }
        }
        _trail.Queue(now + (rapid ? 0 : TrailMoveDelay), next.Trail);
        var showTrail = next.TrailOpacity > _trailOpacity.Target;
        _trailOpacity.To(now, next.TrailOpacity, rapid || !showTrail ? 0 : TrailShowDelay, OpacitySeconds, ArcEasings.Standard);
        for (var j = 0; j < LineCount; j++)
        {
            _line[j].Queue(now + (rapid ? 0 : LineDelay(j, forward)), next.Lines[j]);
            if (next.LineAccent[j] != _lineAccent[j])
            {
                var color = next.LineAccent[j] ? _accent : _muted;
                _lineColor[j].Retarget(_lineColor[j].Evaluate(now), s_transparent, color, now);
                _lineAccent[j] = next.LineAccent[j];
            }
        }
        if (next.HasCheck)
        {
            var delay = rapid ? 0 : CheckDelay;
            _checkOpacity.To(now, 1, delay, CheckShowSeconds, s_easeOut);
            _checkDrawn.To(now, 1, delay, CheckDrawSeconds, ArcEasings.Enter);
        }
        else
        {
            _checkOpacity.To(now, 0, 0, CheckHideSeconds, s_easeOut);
            _checkDrawn.To(now, 0, 0, CheckHideSeconds, s_easeOut);
        }
        _loop.Start();
    }

    /// <summary>Every shape at the current scene, at rest (motion Off, not yet shown, or a reduced-motion switch).</summary>
    private void Jump()
    {
        var scene = _scene;
        for (var i = 0; i < RectCount; i++)
        {
            var rect = scene.Rects[i];
            _center[i].Jump(scene.Centers[i]);
            _shape[i].Jump(scene.Shapes[i]);
            _opacity[i].Jump(rect.Opacity);
            _ink[i] = rect.Ink;
            _rectColor[i].Jump(_inkFill[(int)rect.Ink], _inkStroke[(int)rect.Ink]);
        }
        _trail.Jump(scene.Trail);
        _trailOpacity.Jump(scene.TrailOpacity);
        for (var j = 0; j < LineCount; j++)
        {
            _line[j].Jump(scene.Lines[j]);
            _lineAccent[j] = scene.LineAccent[j];
            _lineColor[j].Jump(s_transparent, scene.LineAccent[j] ? _accent : _muted);
        }
        _checkOpacity.Jump(scene.HasCheck ? 1 : 0);
        _checkDrawn.Jump(scene.HasCheck ? 1 : 0);
        _sway.Jump(0, 0);
        _lastSwitch = double.NegativeInfinity;
        EndCrossfade();
        Sync();
        InvalidateVisual();
    }

    /// <summary>Reduced motion: the old drawing freezes in a ghost layer while the new one fades in over it.</summary>
    private void Crossfade()
    {
        _ghost.CopyFrom(_live);
        SnapshotInks(_ghost);
        Jump();
        _crossfading = true;
        _crossfadeStart = _clock;
        _loop.Start();
    }

    private void EndCrossfade()
    {
        _crossfading = false;
        _ghost.TrailGeometry = null;
    }

    private void OnRunningChanged(bool running)
    {
        // Motion turned Off mid-flight: the loop stopped, so finish everything at once.
        if (!running && _loop.IsActive && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            Jump();
        }
    }

    /// <summary>One frame: integrate every spring and tween; false once everything is at rest.</summary>
    private bool Step(double seconds)
    {
        // A stalled frame resumes smoothly instead of jumping.
        var dt = Math.Min(.034, Math.Max(0, seconds));
        _clock += dt;
        var now = _clock;
        var busy = _sway.Advance(now, dt);
        for (var i = 0; i < RectCount; i++)
        {
            busy = _center[i].Advance(now, dt) | busy;
            busy = _shape[i].Advance(now, dt) | busy;
            busy = _opacity[i].IsRunning(now) | _rectColor[i].IsRunning(now) | busy;
        }
        busy = _trail.Advance(now, dt) | _trailOpacity.IsRunning(now) | busy;
        for (var j = 0; j < LineCount; j++)
        {
            busy = _line[j].Advance(now, dt) | _lineColor[j].IsRunning(now) | busy;
        }
        busy = _checkOpacity.IsRunning(now) | _checkDrawn.IsRunning(now) | busy;
        if (_crossfading)
        {
            if (now - _crossfadeStart >= CrossfadeSeconds)
            {
                EndCrossfade();
            }
            else
            {
                busy = true;
            }
        }
        Sync();
        InvalidateVisual();
        if (!busy)
        {
            // Everything settled long after the last switch began: the next one staggers again.
            _lastSwitch = double.NegativeInfinity;
        }
        return busy;
    }

    /// <summary>Writes the springs, tweens, colors and pointer offset into the drawn layer.</summary>
    private void Sync()
    {
        var now = _clock;
        double sx = _sway.Pos[0] * DriftX, sy = _sway.Pos[1] * DriftY;
        var live = _live;
        for (var i = 0; i < RectCount; i++)
        {
            var depth = OnboardingArtScenes.RectDepth[i];
            var o = i * RectStride;
            var shape = _shape[i].Pos;
            // The pointer offset is in the drawing's frame, applied before the shape's own rotation (so a tilted page
            // moves straight, not along its tilt).
            live.Rect[o] = _center[i].Pos[0] + sx * depth;
            live.Rect[o + 1] = _center[i].Pos[1] + sy * depth;
            live.Rect[o + 2] = shape[0];
            live.Rect[o + 3] = shape[1];
            live.Rect[o + 4] = shape[2];
            live.Rect[o + 5] = shape[3];
            live.Rect[o + 6] = Math.Round(Math.Clamp(_opacity[i].Value(now), 0, 1) * 1000) / 1000;
            var (fill, stroke) = _rectColor[i].Evaluate(now);
            SetColor(live.Fill[i], fill);
            SetColor(live.Stroke[i], stroke);
        }
        var trailMoved = false;
        for (var k = 0; k < 6; k++)
        {
            var value = _trail.Pos[k] + (k % 2 == 0 ? sx : sy);
            if (live.Trail[k] != value)
            {
                live.Trail[k] = value;
                trailMoved = true;
            }
        }
        if (trailMoved)
        {
            live.TrailGeometry = null;
        }
        live.TrailOpacity = Math.Round(Math.Clamp(_trailOpacity.Value(now), 0, 1) * 1000) / 1000;
        SetColor(live.TrailBrush, _muted);
        for (var j = 0; j < LineCount; j++)
        {
            var line = _line[j].Pos;
            live.Lines[j * 4] = line[0] + sx;
            live.Lines[j * 4 + 1] = line[1] + sy;
            live.Lines[j * 4 + 2] = line[2] + sx;
            live.Lines[j * 4 + 3] = line[3] + sy;
            SetColor(live.LineBrush[j], _lineColor[j].Evaluate(now).Stroke);
        }
        live.CheckOffsetX = sx;
        live.CheckOffsetY = sy;
        live.CheckOpacity = Math.Clamp(_checkOpacity.Value(now), 0, 1);
        live.CheckDrawn = Math.Clamp(_checkDrawn.Value(now), 0, 1);
        SetColor(live.CheckBrush, _accent);
    }

    private static void SetColor(SolidColorBrush brush, Color color)
    {
        if (brush.Color != color)
        {
            brush.Color = color;
        }
    }

    #region Colors

    private void CaptureColors()
    {
        var paper = ColorOf(PaperBrush, Colors.White);
        var shade = ColorOf(ShadeBrush, Color.FromRgb(0xE8, 0xE8, 0xE8));
        var ink = ColorOf(InkBrush, Color.FromRgb(0x17, 0x17, 0x17));
        _muted = ColorOf(MutedBrush, Color.FromRgb(0x80, 0x80, 0x80));
        _accent = ColorOf(AccentBrush, Color.FromRgb(0x26, 0x26, 0x26));
        _inkFill[(int)OnboardingInk.Shade] = shade;
        _inkStroke[(int)OnboardingInk.Shade] = _muted;
        _inkFill[(int)OnboardingInk.Paper] = paper;
        _inkStroke[(int)OnboardingInk.Paper] = ink;
        _inkFill[(int)OnboardingInk.Note] = paper;
        _inkStroke[(int)OnboardingInk.Note] = _accent;
    }

    /// <summary>A theme switch repaints at once (only scene changes cross colors over), the frozen ghost included.</summary>
    private void Repaint()
    {
        CaptureColors();
        for (var i = 0; i < RectCount; i++)
        {
            _rectColor[i].Jump(_inkFill[(int)_ink[i]], _inkStroke[(int)_ink[i]]);
            _ghost.Fill[i].Color = _inkFill[(int)_ghost.Ink[i]];
            _ghost.Stroke[i].Color = _inkStroke[(int)_ghost.Ink[i]];
        }
        for (var j = 0; j < LineCount; j++)
        {
            _lineColor[j].Jump(s_transparent, _lineAccent[j] ? _accent : _muted);
            _ghost.LineBrush[j].Color = _ghost.LineAccent[j] ? _accent : _muted;
        }
        _ghost.TrailBrush.Color = _muted;
        _ghost.CheckBrush.Color = _accent;
        Sync();
        InvalidateVisual();
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>Mixes two colors with premultiplied alpha, like a CSS color transition.</summary>
    internal static Color Mix(Color a, Color b, double t)
    {
        double aa = a.A / 255.0, ba = b.A / 255.0;
        var alpha = aa + (ba - aa) * t;
        if (alpha <= 0.0001)
        {
            return s_transparent;
        }
        byte Channel(byte from, byte to) => (byte)Math.Clamp(Math.Round((from * aa + (to * ba - from * aa) * t) / alpha), 0, 255);
        return Color.FromArgb((byte)Math.Clamp(Math.Round(alpha * 255), 0, 255), Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
    }

    #endregion

    #region Layout, properties, accessibility

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, Math.Min(MaxArtWidth, availableSize.Width));
        if (double.IsInfinity(width))
        {
            width = MaxArtWidth;
        }
        return new Size(width, width * OnboardingArtScenes.ViewHeight / OnboardingArtScenes.ViewWidth);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaperBrushProperty || change.Property == ShadeBrushProperty || change.Property == InkBrushProperty ||
            change.Property == MutedBrushProperty || change.Property == AccentBrushProperty)
        {
            Repaint();
        }
        else if (change.Property == StrokeWidthProperty || change.Property == CheckStrokeWidthProperty)
        {
            UpdatePens();
            InvalidateVisual();
        }
    }

    private void UpdatePens()
    {
        _live.SetThickness(StrokeWidth, CheckStrokeWidth);
        _ghost.SetThickness(StrokeWidth, CheckStrokeWidth);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArtAutomationPeer(this);

    private sealed class ArtAutomationPeer(OnboardingArt owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    #endregion

    #region Rendering

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var scale = Math.Min(size.Width / OnboardingArtScenes.ViewWidth, size.Height / OnboardingArtScenes.ViewHeight);
        if (!(scale > 0))
        {
            return;
        }
        var x = (size.Width - OnboardingArtScenes.ViewWidth * scale) / 2;
        var y = (size.Height - OnboardingArtScenes.ViewHeight * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            if (!_crossfading)
            {
                Draw(context, _live);
                return;
            }
            var eased = ArcEasings.Standard.Ease(Math.Clamp((_clock - _crossfadeStart) / CrossfadeSeconds, 0, 1));
            if (eased < .999)
            {
                using (context.PushOpacity(1 - eased))
                {
                    Draw(context, _ghost);
                }
            }
            if (eased > .001)
            {
                using (context.PushOpacity(eased))
                {
                    Draw(context, _live);
                }
            }
        }
    }

    private static void Draw(DrawingContext context, Layer layer)
    {
        for (var i = 0; i < RectCount; i++)
        {
            DrawRect(context, layer, i);
        }
        if (layer.TrailOpacity > .001)
        {
            var geometry = layer.TrailGeometry ??= TrailPath(layer.Trail);
            if (geometry is not null)
            {
                DrawWithOpacity(context, layer.TrailOpacity, geometry, layer.TrailPen);
            }
        }
        for (var j = 0; j < LineCount; j++)
        {
            var o = j * 4;
            Point a = new(layer.Lines[o], layer.Lines[o + 1]), b = new(layer.Lines[o + 2], layer.Lines[o + 3]);
            if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) > .01)
            {
                context.DrawLine(layer.LinePen[j], a, b);
            }
        }
        DrawCheck(context, layer);
    }

    private static void DrawRect(DrawingContext context, Layer layer, int slot)
    {
        var o = slot * RectStride;
        var opacity = layer.Rect[o + 6];
        double w = Math.Max(0, layer.Rect[o + 2]), h = Math.Max(0, layer.Rect[o + 3]);
        if (opacity <= .001 || w < .05 || h < .05)
        {
            return;
        }
        var r = Math.Clamp(layer.Rect[o + 4], 0, Math.Min(w, h) / 2);
        var transform = Matrix.CreateRotation(layer.Rect[o + 5] * Math.PI / 180) * Matrix.CreateTranslation(layer.Rect[o], layer.Rect[o + 1]);
        var fill = layer.Fill[slot].Color.A > 0 ? layer.Fill[slot] : null;
        var rect = new Rect(-w / 2, -h / 2, w, h);
        using (context.PushTransform(transform))
        {
            // Like an SVG shape's opacity: fill and outline fade together.
            if (opacity < 1)
            {
                using (context.PushOpacity(opacity))
                {
                    context.DrawRectangle(fill, layer.Pen[slot], rect, r, r);
                }
            }
            else
            {
                context.DrawRectangle(fill, layer.Pen[slot], rect, r, r);
            }
        }
    }

    private static void DrawWithOpacity(DrawingContext context, double opacity, Geometry geometry, Pen pen)
    {
        if (opacity < 1)
        {
            using (context.PushOpacity(opacity))
            {
                context.DrawGeometry(null, pen, geometry);
            }
        }
        else
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    /// <summary>The dotted trail: a quadratic curve from the start through the control point to the end.</summary>
    private static StreamGeometry? TrailPath(double[] t)
    {
        var chord = Math.Abs(t[0] - t[4]) + Math.Abs(t[1] - t[5]) + Math.Abs(t[0] - t[2]) + Math.Abs(t[1] - t[3]);
        if (chord < .05)
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(t[0], t[1]), false);
            context.QuadraticBezierTo(new Point(t[2], t[3]), new Point(t[4], t[5]));
            context.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>The check drawn up to its drawn fraction: two strokes with round ends meeting in a round corner.</summary>
    private static void DrawCheck(DrawingContext context, Layer layer)
    {
        if (layer.CheckOpacity <= .001 || layer.CheckDrawn <= .001)
        {
            return;
        }
        var c = OnboardingArtScenes.Check;
        double ox = layer.CheckOffsetX, oy = layer.CheckOffsetY;
        Point p0 = new(c[0] + ox, c[1] + oy), p1 = new(c[2] + ox, c[3] + oy), p2 = new(c[4] + ox, c[5] + oy);
        var first = Distance(p0, p1);
        var second = Distance(p1, p2);
        var budget = layer.CheckDrawn * (first + second);
        if (layer.CheckOpacity < 1)
        {
            using (context.PushOpacity(layer.CheckOpacity))
            {
                Strokes();
            }
        }
        else
        {
            Strokes();
        }

        void Strokes()
        {
            context.DrawLine(layer.CheckPen, p0, Lerp(p0, p1, Math.Min(1, budget / first)));
            if (budget > first)
            {
                context.DrawLine(layer.CheckPen, p1, Lerp(p1, p2, Math.Min(1, (budget - first) / second)));
            }
        }
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    #endregion

    #region Engine

    /// <summary>What one frame draws: plain numbers, cached brushes and pens. The live layer and the reduced-motion ghost.</summary>
    private sealed class Layer
    {
        public readonly double[] Rect = new double[RectCount * RectStride];
        public readonly SolidColorBrush[] Fill = new SolidColorBrush[RectCount];
        public readonly SolidColorBrush[] Stroke = new SolidColorBrush[RectCount];
        public readonly Pen[] Pen = new Pen[RectCount];
        public readonly OnboardingInk[] Ink = new OnboardingInk[RectCount];
        public readonly double[] Trail = new double[6];
        public readonly SolidColorBrush TrailBrush = new();
        public readonly Pen TrailPen;
        public readonly double[] Lines = new double[LineCount * 4];
        public readonly SolidColorBrush[] LineBrush = new SolidColorBrush[LineCount];
        public readonly Pen[] LinePen = new Pen[LineCount];
        public readonly bool[] LineAccent = new bool[LineCount];
        public readonly SolidColorBrush CheckBrush = new();
        public readonly Pen CheckPen;
        public StreamGeometry? TrailGeometry;
        public double TrailOpacity;
        public double CheckOpacity;
        public double CheckDrawn;
        public double CheckOffsetX;
        public double CheckOffsetY;
        private double _thickness = -1;

        public Layer()
        {
            for (var i = 0; i < RectCount; i++)
            {
                Fill[i] = new SolidColorBrush();
                Stroke[i] = new SolidColorBrush();
                Pen[i] = new Pen(Stroke[i], 1.36, null, PenLineCap.Round, PenLineJoin.Round);
            }
            for (var j = 0; j < LineCount; j++)
            {
                LineBrush[j] = new SolidColorBrush();
                LinePen[j] = new Pen(LineBrush[j], 1.36, null, PenLineCap.Round, PenLineJoin.Round);
            }
            TrailPen = new Pen(TrailBrush, 1.36, null, PenLineCap.Round, PenLineJoin.Round);
            CheckPen = new Pen(CheckBrush, 1.6, null, PenLineCap.Round, PenLineJoin.Round);
        }

        public void SetThickness(double stroke, double check)
        {
            CheckPen.Thickness = check;
            if (stroke == _thickness)
            {
                return;
            }
            _thickness = stroke;
            foreach (var pen in Pen)
            {
                pen.Thickness = stroke;
            }
            foreach (var pen in LinePen)
            {
                pen.Thickness = stroke;
            }
            TrailPen.Thickness = stroke;
            // A round-capped half-unit dash every five units: a row of small dots. Avalonia counts dashes in stroke widths.
            var width = Math.Max(.01, stroke);
            TrailPen.DashStyle = new DashStyle([.5 / width, 4.5 / width], 0);
        }

        public void CopyFrom(Layer other)
        {
            Array.Copy(other.Rect, Rect, Rect.Length);
            Array.Copy(other.Trail, Trail, Trail.Length);
            Array.Copy(other.Lines, Lines, Lines.Length);
            for (var i = 0; i < RectCount; i++)
            {
                Fill[i].Color = other.Fill[i].Color;
                Stroke[i].Color = other.Stroke[i].Color;
            }
            for (var j = 0; j < LineCount; j++)
            {
                LineBrush[j].Color = other.LineBrush[j].Color;
            }
            TrailBrush.Color = other.TrailBrush.Color;
            CheckBrush.Color = other.CheckBrush.Color;
            TrailGeometry = other.TrailGeometry;
            TrailOpacity = other.TrailOpacity;
            CheckOpacity = other.CheckOpacity;
            CheckDrawn = other.CheckDrawn;
            CheckOffsetX = other.CheckOffsetX;
            CheckOffsetY = other.CheckOffsetY;
        }
    }

    /// <summary>Copies the ink kinds into the ghost too, so a theme switch during a crossfade repaints it.</summary>
    private void SnapshotInks(Layer layer)
    {
        Array.Copy(_ink, layer.Ink, RectCount);
        Array.Copy(_lineAccent, layer.LineAccent, LineCount);
    }

    /// <summary>
    /// A vector of springs: position, velocity and target plus one queued target due at a later time. A new target keeps
    /// the current velocity, so a shape turns smoothly when the step changes mid-flight; a new queue replaces the old one,
    /// so a delayed start that was overtaken never happens.
    /// </summary>
    private sealed class SpringChannel
    {
        private readonly double _k;
        private readonly double _c;
        private double _queueAt;
        private double[]? _queued;

        public SpringChannel(int length, SpringSpec spring)
        {
            Pos = new double[length];
            Vel = new double[length];
            Target = new double[length];
            _k = spring.Stiffness / spring.Mass;
            _c = spring.Damping / spring.Mass;
        }

        public double[] Pos { get; }

        public double[] Vel { get; }

        public double[] Target { get; }

        private bool _still = true;

        public void Jump(double[] values)
        {
            _queued = null;
            Array.Copy(values, Pos, Pos.Length);
            Array.Copy(values, Target, Target.Length);
            Array.Clear(Vel);
            _still = true;
        }

        public void Jump(double x, double y)
        {
            _queued = null;
            Pos[0] = Target[0] = x;
            Pos[1] = Target[1] = y;
            Array.Clear(Vel);
            _still = true;
        }

        /// <summary>Sets a new target at once (the pointer), dropping anything queued.</summary>
        public void Retarget(double x, double y)
        {
            _queued = null;
            Target[0] = x;
            Target[1] = y;
            _still = false;
        }

        /// <summary>Replaces the queue with one target due at <paramref name="at"/>.</summary>
        public void Queue(double at, double[] values)
        {
            _queueAt = at;
            _queued = values;
        }

        /// <summary>Integrates one frame (semi-implicit Euler in 240 Hz sub-steps). True while moving or waiting.</summary>
        public bool Advance(double now, double dt)
        {
            if (_queued is not null && _queueAt <= now + 1e-9)
            {
                Array.Copy(_queued, Target, Target.Length);
                _queued = null;
                _still = false;
            }
            if (!_still && dt > 0)
            {
                var steps = Math.Max(1, (int)Math.Ceiling(dt * 240));
                var h = dt / steps;
                for (var s = 0; s < steps; s++)
                {
                    for (var i = 0; i < Pos.Length; i++)
                    {
                        Vel[i] += (-_k * (Pos[i] - Target[i]) - _c * Vel[i]) * h;
                        Pos[i] += Vel[i] * h;
                    }
                }
                var settled = true;
                for (var i = 0; i < Pos.Length; i++)
                {
                    if (Math.Abs(Pos[i] - Target[i]) > .004 || Math.Abs(Vel[i]) > .02)
                    {
                        settled = false;
                        break;
                    }
                }
                if (settled)
                {
                    Array.Copy(Target, Pos, Pos.Length);
                    Array.Clear(Vel);
                    _still = true;
                }
            }
            return !_still || _queued is not null;
        }
    }

    /// <summary>A number easing from one value to another after a delay; retargeting starts from the current value.</summary>
    private struct Tween
    {
        private double _from;
        private double _start;
        private double _duration;
        private Easing? _easing;

        public double Target { get; private set; }

        public void Jump(double value)
        {
            _from = Target = value;
            _duration = 0;
        }

        public readonly double Value(double now)
        {
            if (_duration <= 0 || now >= _start + _duration)
            {
                return Target;
            }
            if (now <= _start)
            {
                return _from;
            }
            var eased = _easing?.Ease((now - _start) / _duration) ?? (now - _start) / _duration;
            return _from + (Target - _from) * eased;
        }

        public readonly bool IsRunning(double now) => _duration > 0 && now < _start + _duration;

        /// <summary>Heads for <paramref name="target"/> unless already there or on the way.</summary>
        public void To(double now, double target, double delay, double duration, Easing easing)
        {
            if (target == Target)
            {
                return;
            }
            _from = Value(now);
            Target = target;
            _start = now + delay;
            _duration = duration;
            _easing = easing;
        }
    }

    /// <summary>A fill and stroke color crossing over (a CSS color transition on --duration-considered, standard curve).</summary>
    private struct ColorTween
    {
        private Color _fillFrom;
        private Color _fillTo;
        private Color _strokeFrom;
        private Color _strokeTo;
        private double _start;
        private bool _active;

        public void Jump(Color fill, Color stroke)
        {
            _fillFrom = _fillTo = fill;
            _strokeFrom = _strokeTo = stroke;
            _active = false;
        }

        public void Retarget((Color Fill, Color Stroke) from, Color fill, Color stroke, double start)
        {
            _fillFrom = from.Fill;
            _strokeFrom = from.Stroke;
            _fillTo = fill;
            _strokeTo = stroke;
            _start = start;
            _active = _fillFrom != _fillTo || _strokeFrom != _strokeTo;
        }

        public readonly bool IsRunning(double now) => _active && now - _start < ColorSeconds;

        public readonly (Color Fill, Color Stroke) Evaluate(double now)
        {
            if (!_active)
            {
                return (_fillTo, _strokeTo);
            }
            var p = (now - _start) / ColorSeconds;
            if (p >= 1)
            {
                return (_fillTo, _strokeTo);
            }
            if (p <= 0)
            {
                return (_fillFrom, _strokeFrom);
            }
            var eased = ArcEasings.Standard.Ease(p);
            return (Mix(_fillFrom, _fillTo, eased), Mix(_strokeFrom, _strokeTo, eased));
        }
    }

    #endregion
}
