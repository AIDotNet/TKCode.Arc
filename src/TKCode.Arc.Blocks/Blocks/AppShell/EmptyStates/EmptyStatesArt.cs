// Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli): the vector-spring engine of
// empty-states.tsx (createEngine) and the path builder of empty-states-art.ts (shapePath, tilePath, trace).

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The morphing illustration of <see cref="EmptyStates"/>: ten primitives drawn in one <see cref="Render"/> pass on a
/// 320 × 216 view box scaled to fit (at most <see cref="MaxStageWidth"/> wide). Each primitive's geometry and its
/// [opacity, drawn fraction] are spring vectors (morph and smooth springs from Arc's visualDuration and bounce,
/// semi-implicit Euler at 240 Hz) that keep their velocity when retargeted, start after a per-primitive delay and
/// redraw strokes from zero when a scene asks for it. Fill and stroke colors cross over in 480ms; the mouse tilts the
/// layers by depth (parallax) under full motion. Reduced motion freezes the old drawing and crossfades it into the new
/// one; Off switches at once.
/// </summary>
/// <remarks>
/// Code-driven motion: a ControlTheme transition animates one property toward one value, but every primitive here is a
/// vector of up to 353 numbers that springs as a whole, keeps its velocity when a new target arrives mid-flight and is
/// turned into path geometry each frame. The frames come from a Core <see cref="LiveLoop"/> that stops as soon as
/// everything has settled; at rest a frame reuses the cached geometry, brushes and pens and allocates nothing.
/// </remarks>
internal sealed class EmptyStatesArt : Control
{
    /// <summary>Shapes lead, details trail: back tiles, body, front tiles, then lines and the mark. Totals stay under 0.4s.</summary>
    private static readonly double[] s_delay = [.05, .03, 0, .04, .07, .1, .06, .09, .11, .14];

    /// <summary>The same order, tighter, for a phase change within a scene.</summary>
    private static readonly double[] s_nudge = [.03, .015, 0, .02, .035, .05, .02, .04, .05, .07];

    /// <summary>How far the pointer tilts a layer of depth 1 (view-box units).</summary>
    private const double Drift = 2.5;

    /// <summary>Colors cross over on --duration-considered with --ease-standard.</summary>
    private const double ColorSeconds = 0.48;

    /// <summary>The reduced-motion crossfade (--duration-standard, --ease-standard).</summary>
    private const double CrossfadeSeconds = 0.24;

    private static readonly SpringSpec s_morph = ArcMotionPresets.Morph;
    private static readonly SpringSpec s_smooth = ArcMotionPresets.Smooth;
    private static readonly SpringSpec s_sway = SpringSpec.FromPhysical(90, 19);
    private static readonly Color s_transparent = Color.FromArgb(0, 0, 0, 0);

    #region Theme properties (set by the EmptyStates theme)

    internal static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(SurfaceBrush));

    internal static readonly StyledProperty<IBrush?> SurfaceMutedBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(SurfaceMutedBrush));

    internal static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(InkBrush));

    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(MutedBrush));

    internal static readonly StyledProperty<IBrush?> StrongLineBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(StrongLineBrush));

    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(SuccessBrush));

    internal static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<EmptyStatesArt, IBrush?>(nameof(WarningBrush));

    internal static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<EmptyStatesArt, double>(nameof(StrokeWidth), 1.5);

    internal static readonly StyledProperty<double> MaxStageWidthProperty =
        AvaloniaProperty.Register<EmptyStatesArt, double>(nameof(MaxStageWidth), 360);

    internal IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    internal IBrush? SurfaceMutedBrush { get => GetValue(SurfaceMutedBrushProperty); set => SetValue(SurfaceMutedBrushProperty, value); }
    internal IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }
    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    internal IBrush? StrongLineBrush { get => GetValue(StrongLineBrushProperty); set => SetValue(StrongLineBrushProperty, value); }
    internal IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    internal IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    internal double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }
    internal double MaxStageWidth { get => GetValue(MaxStageWidthProperty); set => SetValue(MaxStageWidthProperty, value); }

    #endregion

    private const int Count = EmptyStatesArtFrames.PrimCount;
    private static readonly int s_toneCount = Enum.GetValues<EmptyStatesTone>().Length;

    private readonly LiveLoop _loop;
    private readonly SpringChannel[] _geo = new SpringChannel[Count];
    private readonly SpringChannel[] _fx = new SpringChannel[Count];
    private readonly SpringChannel _sway = new(2, s_sway);

    private readonly EmptyStatesTone[] _tone = new EmptyStatesTone[Count];
    private readonly ColorTween[] _colors = new ColorTween[Count];
    private readonly Color[] _toneFill = new Color[s_toneCount];
    private readonly Color[] _toneStroke = new Color[s_toneCount];

    private readonly SolidColorBrush[] _fillBrush = new SolidColorBrush[Count];
    private readonly SolidColorBrush[] _strokeBrush = new SolidColorBrush[Count];
    private readonly Pen[] _pen = new Pen[Count];
    private readonly double[] _penGap = new double[Count];
    private readonly StreamGeometry?[] _geometry = new StreamGeometry?[Count];
    private readonly bool[] _dirty = new bool[Count];

    // The frozen old drawing of the reduced-motion crossfade.
    private readonly SolidColorBrush[] _ghostFill = new SolidColorBrush[Count];
    private readonly SolidColorBrush[] _ghostStroke = new SolidColorBrush[Count];
    private readonly Pen[] _ghostPen = new Pen[Count];
    private readonly StreamGeometry?[] _ghostGeometry = new StreamGeometry?[Count];
    private readonly double[] _ghostOpacity = new double[Count];
    private readonly EmptyStatesTone[] _ghostTone = new EmptyStatesTone[Count];
    private bool _crossfading;
    private double _crossfadeStart;

    private EmptyStatesFrame _frame;
    private EmptyStatesScene _scene = EmptyStatesScene.NoResults;
    private double _clock;
    private double _builtScale = double.NaN;
    private double _builtX = double.NaN;
    private double _builtY = double.NaN;
    private double _builtDriftX;
    private double _builtDriftY;

    static EmptyStatesArt()
    {
        AffectsMeasure<EmptyStatesArt>(MaxStageWidthProperty);
        AffectsRender<EmptyStatesArt>(StrokeWidthProperty);
        FocusableProperty.OverrideDefaultValue<EmptyStatesArt>(false);
    }

    public EmptyStatesArt()
    {
        _frame = EmptyStatesArtFrames.Get(EmptyStatesScene.NoResults, EmptyStatesPhase.Idle);
        for (var i = 0; i < Count; i++)
        {
            var prim = _frame.Prims[i];
            _geo[i] = new SpringChannel(prim.Geo.Length, s_morph);
            _geo[i].Jump(prim.Geo);
            _fx[i] = new SpringChannel(2, s_smooth);
            _fx[i].Jump(prim.Fx);
            _tone[i] = prim.Tone;
            _fillBrush[i] = new SolidColorBrush();
            _strokeBrush[i] = new SolidColorBrush();
            _pen[i] = new Pen(_strokeBrush[i], 1.5, null, PenLineCap.Round, PenLineJoin.Round);
            _ghostFill[i] = new SolidColorBrush();
            _ghostStroke[i] = new SolidColorBrush();
            _ghostPen[i] = new Pen(_ghostStroke[i], 1.5, null, PenLineCap.Round, PenLineJoin.Round);
            _dirty[i] = true;
        }
        CaptureColors();
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += OnRunningChanged;
        Avalonia.Automation.AutomationProperties.SetName(this, _frame.Label);
    }

    #region Test hooks

    /// <summary>The frame the drawing is moving to (or shows).</summary>
    internal EmptyStatesFrame Frame => _frame;

    /// <summary>The engine wants frames: something is still moving, queued, fading or crossing over.</summary>
    internal bool IsBusy => _loop.IsActive;

    internal bool IsCrossfading => _crossfading;

    internal double[] GeometryOf(int index) => _geo[index].Pos;

    internal double OpacityOf(int index) => _fx[index].Pos[0];

    internal double DrawnOf(int index) => _fx[index].Pos[1];

    internal Color FillOf(int index) => _fillBrush[index].Color;

    internal Color StrokeOf(int index) => _strokeBrush[index].Color;

    internal Vector Sway => new(_sway.Pos[0], _sway.Pos[1]);

    /// <summary>Points the parallax at a stage position in −1…1 (what a mouse move does).</summary>
    internal void PointAt(double x, double y)
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _sway.Retarget(x, y);
        _loop.Start();
    }

    #endregion

    /// <summary>Moves the drawing to <paramref name="scene"/> in <paramref name="phase"/>, named <paramref name="label"/>.</summary>
    public void Show(EmptyStatesScene scene, EmptyStatesPhase phase, string label)
    {
        Avalonia.Automation.AutomationProperties.SetName(this, label);
        var next = EmptyStatesArtFrames.Get(scene, phase);
        if (ReferenceEquals(next, _frame))
        {
            return;
        }
        var sceneChanged = scene != _scene;
        _scene = scene;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || !IsEffectivelyVisible || level == ArcMotionLevel.Off)
        {
            _frame = next;
            Snap();
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            Crossfade(next);
            return;
        }
        _frame = next;
        var now = _clock;
        var delays = sceneChanged ? s_delay : s_nudge;
        for (var i = 0; i < Count; i++)
        {
            var prim = next.Prims[i];
            var delay = delays[i];
            var fx = _fx[i];
            _geo[i].Queue(now + delay, prim.Geo);
            if (sceneChanged && prim.Redraw)
            {
                // Strokes that redraw are cleared first, then drawn in a beat after their shape lands.
                fx.Queue(now, prim.RedrawStart, now + delay + .18, prim.Fx);
            }
            else
            {
                fx.Queue(now + delay + (prim.Fx[1] > fx.Target[1] + .01 ? .12 : 0), prim.Fx);
            }
            if (prim.Tone != _tone[i])
            {
                // CSS transition-delay is the scene-change delay of the primitive, whatever moved it.
                _colors[i].Retarget(_colors[i].Evaluate(now), ToneFill(i, prim.Tone), ToneStroke(prim.Tone), now + s_delay[i]);
                _tone[i] = prim.Tone;
            }
        }
        _loop.Start();
    }

    /// <summary>Jumps every primitive to the current frame (motion Off, not yet shown, or motion turned off mid-flight).</summary>
    private void Snap()
    {
        for (var i = 0; i < Count; i++)
        {
            var prim = _frame.Prims[i];
            _geo[i].Jump(prim.Geo);
            _fx[i].Jump(prim.Fx);
            _tone[i] = prim.Tone;
            _colors[i].Jump(ToneFill(i, prim.Tone), ToneStroke(prim.Tone));
            _dirty[i] = true;
        }
        _sway.Jump(0, 0);
        EndCrossfade();
        ApplyColors();
        InvalidateVisual();
    }

    /// <summary>Reduced motion: the old drawing freezes in a ghost layer, the new one snaps in, and they crossfade.</summary>
    private void Crossfade(EmptyStatesFrame next)
    {
        var scale = Scale(Bounds.Size, out var x, out var y);
        if (scale <= 0)
        {
            _frame = next;
            Snap();
            return;
        }
        for (var i = 0; i < Count; i++)
        {
            _ghostGeometry[i] = Geometry(i, scale, x, y);
            _ghostOpacity[i] = OpacityValue(i);
            _ghostTone[i] = _tone[i];
            _ghostFill[i].Color = _fillBrush[i].Color;
            _ghostStroke[i].Color = _strokeBrush[i].Color;
            _ghostPen[i].Thickness = _pen[i].Thickness;
            _ghostPen[i].DashStyle = _pen[i].DashStyle;
        }
        _frame = next;
        Snap();
        _crossfading = true;
        _crossfadeStart = _clock;
        _loop.Start();
    }

    private void EndCrossfade()
    {
        _crossfading = false;
        Array.Clear(_ghostGeometry);
    }

    private void OnRunningChanged(bool running)
    {
        // Motion turned Off mid-flight: the loop stopped, so finish everything at once.
        if (!running && (_loop.IsActive || _crossfading) && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            Snap();
        }
    }

    /// <summary>One frame: integrate every spring, advance colors and the crossfade; false once all is at rest.</summary>
    private bool Step(double seconds)
    {
        // Arc caps a frame at 34ms so a stalled tab resumes smoothly instead of jumping.
        var dt = Math.Min(.034, Math.Max(0, seconds));
        _clock += dt;
        var now = _clock;
        var busy = _sway.Advance(now, dt);
        var drift = _sway.Moved;
        for (var i = 0; i < Count; i++)
        {
            busy = _geo[i].Advance(now, dt) | busy;
            busy = _fx[i].Advance(now, dt) | busy;
            if (_geo[i].Moved || _fx[i].Moved || drift)
            {
                _dirty[i] = true;
            }
            busy = _colors[i].IsRunning(now) | busy;
        }
        ApplyColors();
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
        InvalidateVisual();
        return busy;
    }

    #region Colors

    private void CaptureColors()
    {
        var surface = ColorOf(SurfaceBrush, Colors.White);
        var surfaceMuted = ColorOf(SurfaceMutedBrush, Color.FromRgb(0xF5, 0xF5, 0xF5));
        var ink = ColorOf(InkBrush, Color.FromRgb(0x17, 0x17, 0x17));
        var muted = ColorOf(MutedBrush, Color.FromRgb(0x73, 0x73, 0x73));
        var strong = ColorOf(StrongLineBrush, Color.FromRgb(0xC4, 0xC4, 0xC4));
        var success = ColorOf(SuccessBrush, Color.FromRgb(0x16, 0xA3, 0x4A));
        var warning = ColorOf(WarningBrush, Color.FromRgb(0xD9, 0x77, 0x06));
        for (var t = 0; t < s_toneCount; t++)
        {
            var tone = (EmptyStatesTone)t;
            _toneFill[t] = tone switch
            {
                EmptyStatesTone.Query => muted,
                EmptyStatesTone.Bar => strong,
                EmptyStatesTone.Shade => surfaceMuted,
                EmptyStatesTone.Dot => ink,
                _ => surface
            };
            _toneStroke[t] = tone switch
            {
                EmptyStatesTone.Tuck or EmptyStatesTone.Slot or EmptyStatesTone.Subtle => strong,
                EmptyStatesTone.Query or EmptyStatesTone.Bar or EmptyStatesTone.Dot => s_transparent,
                EmptyStatesTone.Back or EmptyStatesTone.Muted => muted,
                EmptyStatesTone.Success => success,
                EmptyStatesTone.Warning => warning,
                _ => ink
            };
        }
        // A theme switch repaints at once; only tone changes cross over.
        for (var i = 0; i < Count; i++)
        {
            _colors[i].Jump(ToneFill(i, _tone[i]), ToneStroke(_tone[i]));
            _ghostFill[i].Color = ToneFill(i, _ghostTone[i]);
            _ghostStroke[i].Color = ToneStroke(_ghostTone[i]);
        }
        ApplyColors();
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>Strokes and marks are never filled.</summary>
    private Color ToneFill(int index, EmptyStatesTone tone)
        => EmptyStatesArtFrames.Kinds[index] is EmptyStatesPrimKind.Stroke or EmptyStatesPrimKind.Mark ? s_transparent : _toneFill[(int)tone];

    private Color ToneStroke(EmptyStatesTone tone) => _toneStroke[(int)tone];

    /// <summary>Writes the current colors into the cached brushes (only when they changed).</summary>
    private void ApplyColors()
    {
        for (var i = 0; i < Count; i++)
        {
            var (fill, stroke) = _colors[i].Evaluate(_clock);
            if (_fillBrush[i].Color != fill)
            {
                _fillBrush[i].Color = fill;
            }
            if (_strokeBrush[i].Color != stroke)
            {
                _strokeBrush[i].Color = stroke;
            }
        }
    }

    /// <summary>Mixes two colors with premultiplied alpha, like a CSS color transition (a color fades into transparent).</summary>
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

    #region Layout, input, accessibility

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, Math.Min(MaxStageWidth, availableSize.Width));
        if (double.IsInfinity(width))
        {
            width = MaxStageWidth;
        }
        return new Size(width, width * EmptyStatesArtFrames.ViewHeight / EmptyStatesArtFrames.ViewWidth);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SurfaceBrushProperty || change.Property == SurfaceMutedBrushProperty || change.Property == InkBrushProperty ||
            change.Property == MutedBrushProperty || change.Property == StrongLineBrushProperty || change.Property == SuccessBrushProperty ||
            change.Property == WarningBrushProperty)
        {
            CaptureColors();
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        // Parallax follows a fine pointer only ((hover: hover) and (pointer: fine)), and only under full motion.
        var size = Bounds.Size;
        if (e.Pointer.Type != PointerType.Mouse || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var p = e.GetPosition(this);
        PointAt((p.X / size.Width - .5) * 2, (p.Y / size.Height - .5) * 2);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_sway.Target[0] != 0 || _sway.Target[1] != 0)
        {
            _sway.Retarget(0, 0);
            _loop.Start();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ArtAutomationPeer(this);

    private sealed class ArtAutomationPeer(EmptyStatesArt owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    #endregion

    #region Rendering

    /// <summary>View-box scale and offset that center the 320 × 216 drawing in <paramref name="size"/>.</summary>
    private static double Scale(Size size, out double x, out double y)
    {
        var scale = Math.Min(size.Width / EmptyStatesArtFrames.ViewWidth, size.Height / EmptyStatesArtFrames.ViewHeight);
        if (!(scale > 0))
        {
            x = y = 0;
            return 0;
        }
        x = (size.Width - EmptyStatesArtFrames.ViewWidth * scale) / 2;
        y = (size.Height - EmptyStatesArtFrames.ViewHeight * scale) / 2;
        return scale;
    }

    private double OpacityValue(int index) => Math.Round(Math.Clamp(_fx[index].Pos[0], 0, 1) * 1000) / 1000;

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // The whole stage takes the pointer (parallax), not only the painted shapes.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        var scale = Scale(size, out var x, out var y);
        if (scale <= 0)
        {
            return;
        }
        if (!_crossfading)
        {
            DrawLayer(context, scale, x, y);
            return;
        }
        var eased = ArcEasings.Standard.Ease(Math.Clamp((_clock - _crossfadeStart) / CrossfadeSeconds, 0, 1));
        if (eased < 0.999)
        {
            using (context.PushOpacity(1 - eased))
            {
                DrawGhost(context);
            }
        }
        if (eased > 0.001)
        {
            using (context.PushOpacity(eased))
            {
                DrawLayer(context, scale, x, y);
            }
        }
    }

    private void DrawLayer(DrawingContext context, double scale, double x, double y)
    {
        for (var i = 0; i < Count; i++)
        {
            var opacity = OpacityValue(i);
            if (opacity <= 0)
            {
                continue;
            }
            var geometry = Geometry(i, scale, x, y);
            if (geometry is null)
            {
                continue;
            }
            Draw(context, geometry, _fillBrush[i], _pen[i], opacity);
        }
    }

    private void DrawGhost(DrawingContext context)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_ghostGeometry[i] is { } geometry && _ghostOpacity[i] > 0)
            {
                Draw(context, geometry, _ghostFill[i], _ghostPen[i], _ghostOpacity[i]);
            }
        }
    }

    private static void Draw(DrawingContext context, Geometry geometry, SolidColorBrush fill, Pen pen, double opacity)
    {
        var fillBrush = fill.Color.A > 0 ? fill : null;
        var stroke = pen.Brush is ISolidColorBrush { Color.A: > 0 } ? pen : null;
        if (fillBrush is null && stroke is null)
        {
            return;
        }
        if (opacity >= 1)
        {
            context.DrawGeometry(fillBrush, stroke, geometry);
            return;
        }
        // Like an SVG path's opacity: fill and stroke fade together, as one group.
        using (context.PushOpacity(opacity))
        {
            context.DrawGeometry(fillBrush, stroke, geometry);
        }
    }

    /// <summary>The primitive's path for the current springs, rebuilt only after they moved (or the size or tilt changed).</summary>
    private StreamGeometry? Geometry(int index, double scale, double x, double y)
    {
        var driftX = _sway.Pos[0] * Drift;
        var driftY = _sway.Pos[1] * Drift;
        if (scale != _builtScale || x != _builtX || y != _builtY || driftX != _builtDriftX || driftY != _builtDriftY)
        {
            _builtScale = scale;
            _builtX = x;
            _builtY = y;
            _builtDriftX = driftX;
            _builtDriftY = driftY;
            Array.Fill(_dirty, true);
        }
        var width = StrokeWidth;
        if (_pen[index].Thickness != width)
        {
            _pen[index].Thickness = width;
            _penGap[index] = -1;
            _dirty[index] = true;
        }
        if (!_dirty[index])
        {
            return _geometry[index];
        }
        _dirty[index] = false;
        var g = _geo[index].Pos;
        var path = new PathWriter(scale, x, y);
        StreamGeometry? geometry = null;
        switch (EmptyStatesArtFrames.Kinds[index])
        {
            case EmptyStatesPrimKind.Tile:
                geometry = TilePath(g, driftX, driftY, path);
                break;
            case EmptyStatesPrimKind.Body:
                geometry = BodyPath(g, driftX, driftY, path);
                break;
            default:
                geometry = StrokePath(index, g, driftX, driftY, path);
                break;
        }
        _geometry[index] = geometry;
        return geometry;
    }

    /// <summary>A rounded rectangle with rotation and skew, as 4 × 7 corner points (Arc's tilePath).</summary>
    private static StreamGeometry? TilePath(double[] g, double ox, double oy, PathWriter path)
    {
        double w = Math.Max(0, g[2]), h = Math.Max(0, g[3]);
        if (w < .05 || h < .05)
        {
            return null;
        }
        const double rad = Math.PI / 180;
        double r = Math.Min(Math.Min(Math.Max(0, g[4]), w / 2), h / 2), tan = Math.Tan(g[6] * rad), cos = Math.Cos(g[5] * rad), sin = Math.Sin(g[5] * rad);
        double px = g[0] + ox * g[7], py = g[1] + oy * g[7], hw = w / 2, hh = h / 2;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            for (var corner = 0; corner < 4; corner++)
            {
                var (cx, cy, start) = corner switch
                {
                    0 => (hw - r, -hh + r, -90.0),
                    1 => (hw - r, hh - r, 0.0),
                    2 => (-hw + r, hh - r, 90.0),
                    _ => (-hw + r, -hh + r, 180.0)
                };
                for (var s = 0; s <= 6; s++)
                {
                    var a = (start + s * 15) * rad;
                    double lx = cx + r * Math.Cos(a), ly = cy + r * Math.Sin(a) + (cx + r * Math.Cos(a)) * tan;
                    var point = path.Map(lx * cos - ly * sin + px, lx * sin + ly * cos + py);
                    if (first)
                    {
                        context.BeginFigure(point, true);
                        first = false;
                    }
                    else
                    {
                        context.LineTo(point);
                    }
                }
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    private static StreamGeometry BodyPath(double[] g, double ox, double oy, PathWriter path)
    {
        var z = g[2 * EmptyStatesArtFrames.BodyPoints];
        double dx = ox * z, dy = oy * z;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(path.Map(g[0] + dx, g[1] + dy), true);
            for (var i = 1; i < EmptyStatesArtFrames.BodyPoints; i++)
            {
                context.LineTo(path.Map(g[2 * i] + dx, g[2 * i + 1] + dy));
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>A line or a two-stroke mark, drawn up to its drawn fraction (Arc's shapePath for strokes).</summary>
    private StreamGeometry? StrokePath(int index, double[] g, double ox, double oy, PathWriter path)
    {
        var draw = Math.Clamp(_fx[index].Pos[1], 0, 1);
        int count = (g.Length - 3) / 2;
        double gap = g[^3], z0 = g[^2], z1 = g[^1];
        UpdateDash(index, gap);
        var geometry = new StreamGeometry();
        var drawn = false;
        using (var context = geometry.Open())
        {
            if (EmptyStatesArtFrames.Kinds[index] == EmptyStatesPrimKind.Stroke)
            {
                drawn = Trace(context, path, g, 0, count, draw * Span(g, 0, count), ox, oy, z0, z1, count);
            }
            else
            {
                const int m = EmptyStatesArtFrames.MarkPoints;
                var first = Span(g, 0, m);
                var budget = draw * (first + Span(g, m, m));
                drawn = Trace(context, path, g, 0, m, Math.Min(budget, first), ox, oy, z0, z1, 1);
                drawn = Trace(context, path, g, m, m, budget - first, ox, oy, z0, z1, 1) | drawn;
            }
        }
        return drawn ? geometry : null;
    }

    /// <summary>Arc dashes strokes "3 gap" in screen pixels; Avalonia measures dashes in stroke widths.</summary>
    private void UpdateDash(int index, double gap)
    {
        var quantized = gap > .2 ? Math.Round(gap * 100) / 100 : 0;
        if (quantized == _penGap[index])
        {
            return;
        }
        _penGap[index] = quantized;
        var width = Math.Max(0.01, _pen[index].Thickness);
        _pen[index].DashStyle = quantized > 0 ? new DashStyle([3 / width, quantized / width], 0) : null;
    }

    private static double Span(double[] g, int from, int count)
    {
        var length = 0.0;
        for (var i = from + 1; i < from + count; i++)
        {
            double dx = g[2 * i] - g[2 * i - 2], dy = g[2 * i + 1] - g[2 * i - 1];
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }

    /// <summary>Draws the first <paramref name="budget"/> units of a polyline. Each point drifts by its own depth, so a line can stretch between two layers.</summary>
    private static bool Trace(StreamGeometryContext context, PathWriter path, double[] g, int from, int count, double budget,
                              double ox, double oy, double z0, double z1, int total)
    {
        if (budget <= .05)
        {
            return false;
        }
        double AtX(int i) => g[2 * (from + i)] + ox * Depth(i);
        double AtY(int i) => g[2 * (from + i) + 1] + oy * Depth(i);
        double Depth(int i) => z0 + (z1 - z0) * (total > 1 ? (double)(from + i) / (total - 1) : 0);
        double px = AtX(0), py = AtY(0), left = budget;
        context.BeginFigure(path.Map(px, py), false);
        for (var i = 1; i < count; i++)
        {
            double x = AtX(i), y = AtY(i), dx = x - px, dy = y - py, length = Math.Sqrt(dx * dx + dy * dy);
            if (length >= left)
            {
                var u = length > 0 ? left / length : 0;
                context.LineTo(path.Map(px + dx * u, py + dy * u));
                context.EndFigure(false);
                return true;
            }
            left -= length;
            context.LineTo(path.Map(x, y));
            px = x;
            py = y;
        }
        context.EndFigure(false);
        return true;
    }

    /// <summary>Maps view-box coordinates to the control (strokes are drawn in control pixels, so they never scale).</summary>
    private readonly struct PathWriter(double scale, double x, double y)
    {
        public Point Map(double vx, double vy) => new(vx * scale + x, vy * scale + y);
    }

    #endregion

    #region Engine

    /// <summary>
    /// One spring vector (Arc's Channel): position, velocity and target, a small queue of delayed targets, and
    /// stiffness and damping. A new target keeps the current velocity, so shapes retarget mid-flight without a hitch.
    /// </summary>
    private sealed class SpringChannel
    {
        private readonly double _k;
        private readonly double _c;
        private readonly double[] _queueAt = new double[2];
        private readonly double[]?[] _queueValues = new double[2][];
        private int _queued;

        public SpringChannel(int length, SpringSpec spring)
        {
            Pos = new double[length];
            Vel = new double[length];
            Target = new double[length];
            _k = spring.Stiffness;
            _c = spring.Damping;
        }

        public double[] Pos { get; }

        public double[] Vel { get; }

        public double[] Target { get; }

        public bool Still { get; private set; } = true;

        /// <summary>The position changed during the last <see cref="Advance"/>.</summary>
        public bool Moved { get; private set; }

        public void Jump(double[] values)
        {
            _queued = 0;
            Array.Copy(values, Pos, Pos.Length);
            Array.Copy(values, Target, Target.Length);
            Array.Clear(Vel);
            Still = true;
        }

        public void Jump(double x, double y)
        {
            _queued = 0;
            Pos[0] = Target[0] = x;
            Pos[1] = Target[1] = y;
            Array.Clear(Vel);
            Still = true;
        }

        /// <summary>Sets a new target at once (the pointer), dropping anything queued.</summary>
        public void Retarget(double x, double y)
        {
            _queued = 0;
            Target[0] = x;
            Target[1] = y;
            Still = false;
        }

        /// <summary>Replaces the queue with one target due at <paramref name="at"/>.</summary>
        public void Queue(double at, double[] values)
        {
            _queueAt[0] = at;
            _queueValues[0] = values;
            _queueValues[1] = null;
            _queued = 1;
        }

        /// <summary>Replaces the queue with two targets in order.</summary>
        public void Queue(double at, double[] values, double nextAt, double[] next)
        {
            _queueAt[0] = at;
            _queueValues[0] = values;
            _queueAt[1] = nextAt;
            _queueValues[1] = next;
            _queued = 2;
        }

        /// <summary>Integrates one frame (semi-implicit Euler in 240 Hz sub-steps). True while moving or waiting.</summary>
        public bool Advance(double now, double dt)
        {
            Moved = false;
            while (_queued > 0 && _queueAt[0] <= now)
            {
                Array.Copy(_queueValues[0]!, Target, Target.Length);
                Still = false;
                _queueAt[0] = _queueAt[1];
                _queueValues[0] = _queueValues[1];
                _queueValues[1] = null;
                _queued--;
            }
            if (!Still && dt > 0)
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
                Moved = true;
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
                    Still = true;
                }
            }
            return !Still || _queued > 0;
        }
    }

    /// <summary>A fill and stroke color crossing over from one tone to another (a CSS color transition with a delay).</summary>
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

        public (Color Fill, Color Stroke) Evaluate(double now)
        {
            if (!_active)
            {
                return (_fillTo, _strokeTo);
            }
            var p = (now - _start) / ColorSeconds;
            if (p >= 1)
            {
                _active = false;
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
