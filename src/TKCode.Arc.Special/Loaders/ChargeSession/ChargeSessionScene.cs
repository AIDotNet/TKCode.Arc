using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The hero scene of <see cref="ChargeSession"/> (Arc Charge session, design reference): a brushed-metal charger whose S
/// cable runs into a pack lying on its side, drawn in a 1000 × 240 unit box scaled to the width (height = 0.24 × width).
/// Thin lines (outlines, cell dividers, the front, the dashed limit) keep their pixel width at any scale, like SVG's
/// non-scaling strokes; the cable (14 u) and its core (3 u) scale. The drawing is split into six stacked layer visuals
/// (<see cref="ChargeSessionSceneLayerKind"/>) in paint order, so a frame re-records only what moved: the packets, the
/// front and the limit line every frame; the fill when the charge moves; the LED and cable core while availability
/// changes; the charger, cable, shell and well only on a size or palette change. Opacity is folded into alpha brushes and
/// pens cached per alpha byte (no save layers). Colors arrive as ink properties from the theme. The card owns the model
/// and the flow and calls <see cref="Update"/> after each step.
/// </summary>
internal sealed class ChargeSessionScene : Control
{
    /// <summary>Width of the unit box.</summary>
    public const double UnitWidth = 1000;

    /// <summary>Height of the unit box (aspect 0.24).</summary>
    public const double UnitHeight = 240;

    /// <summary>Left edge of the pack's well.</summary>
    public const double WellLeft = 244;

    /// <summary>Well width per percent (728 u for 100%).</summary>
    public const double PerPercent = 7.28;

    /// <summary>Top of the pack shell, where the limit pill's stem ends.</summary>
    public const double ShellTop = 20;

    /// <summary>Where the cable meets the nub; the packets' path continues straight from here into the pack.</summary>
    public const double CableEndX = 212;

    public static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> BackgroundInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(BackgroundInk));
    public static readonly StyledProperty<IBrush?> MetalInk0Property =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalInk0));
    public static readonly StyledProperty<IBrush?> MetalInk1Property =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalInk1));
    public static readonly StyledProperty<IBrush?> MetalInk2Property =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalInk2));
    public static readonly StyledProperty<IBrush?> MetalInk3Property =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalInk3));
    public static readonly StyledProperty<IBrush?> MetalInk4Property =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalInk4));
    public static readonly StyledProperty<IBrush?> MetalEdgeInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(MetalEdgeInk));
    public static readonly StyledProperty<IBrush?> ScreenInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(ScreenInk));
    public static readonly StyledProperty<IBrush?> CableInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(CableInk));
    public static readonly StyledProperty<IBrush?> ShellInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(ShellInk));
    public static readonly StyledProperty<IBrush?> ShellEdgeInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(ShellEdgeInk));
    public static readonly StyledProperty<IBrush?> WellInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(WellInk));
    public static readonly StyledProperty<IBrush?> BeyondInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(BeyondInk));
    public static readonly StyledProperty<IBrush?> SheenInkProperty =
        AvaloniaProperty.Register<ChargeSessionScene, IBrush?>(nameof(SheenInk));

    /// <summary>Opacity of the limit line at rest and while held (0.55 / 0.9).</summary>
    public static readonly StyledProperty<double> LimitOpacityProperty =
        AvaloniaProperty.Register<ChargeSessionScene, double>(nameof(LimitOpacity), 0.55);

    public static readonly StyledProperty<double> LimitHeldOpacityProperty =
        AvaloniaProperty.Register<ChargeSessionScene, double>(nameof(LimitHeldOpacity), 0.9);

    private static readonly ChargeSessionPath s_cable = new(new Point(84, 176), new Point(140, 238), new Point(168, 120), new Point(212, 120));

    private static readonly StreamGeometry s_cableGeometry = BuildCable();

    private ChargeSessionModel? _model;
    private ChargeSessionFlow? _flow;
    private static readonly AvaloniaProperty[] s_inks =
    {
        AccentInkProperty, ForegroundInkProperty, BackgroundInkProperty, MetalInk0Property, MetalInk1Property,
        MetalInk2Property, MetalInk3Property, MetalInk4Property, MetalEdgeInkProperty, ScreenInkProperty, CableInkProperty,
        ShellInkProperty, ShellEdgeInkProperty, WellInkProperty, BeyondInkProperty, SheenInkProperty,
    };

    private readonly ChargeSessionSceneLayer[] _layers = new ChargeSessionSceneLayer[6];
    private readonly ChargeSessionAlphaBrushes _accentAlpha = new();
    private readonly ChargeSessionAlphaBrushes _backgroundAlpha = new();
    private readonly ChargeSessionAlphaPens _coreAlpha = new();
    private readonly ChargeSessionAlphaPens _frontAlpha = new();
    private readonly ChargeSessionAlphaPens _limitAlpha = new();
    private IBrush _holster = Brushes.Black;
    private double _shownCharge = double.NaN;
    private double _shownAvailability = double.NaN;
    private double _shownLimit = double.NaN;
    private double _shownFlash = double.NaN;
    private int _shownPackets = -1;
    private MotionValue<double>? _held;
    private double _heldMix;
    private bool _paletteDirty = true;
    private double _penScale = -1;

    // Palette (rebuilt when an ink changes) and pens (rebuilt when the scale changes): nothing is allocated per frame.
    private IBrush _accent = Brushes.Black;
    private IBrush _background = Brushes.White;
    private IBrush _screen = Brushes.Black;
    private IBrush _shell = Brushes.White;
    private IBrush _well = Brushes.LightGray;
    private IBrush _beyond = Brushes.Transparent;
    private IBrush _metal = Brushes.LightGray;
    private Color _foregroundColor = Colors.Black;
    private Color _metalEdgeColor = Colors.Gray;
    private Color _cableColor = Colors.LightGray;
    private Color _shellEdgeColor = Colors.Gray;
    private Color _beyondColor = Colors.Transparent;
    private Color _sheenColor = Colors.Transparent;
    private Color _backgroundColor = Colors.White;
    private Color _accentColor = Colors.Black;
    private IPen? _metalPen;
    private IPen? _cablePen;
    private IPen? _shellPen;
    private IPen? _cellPen;
    private IPen? _sheenPen;

    public ChargeSessionScene()
    {
        for (var i = 0; i < _layers.Length; i++)
        {
            _layers[i] = new ChargeSessionSceneLayer(this, (ChargeSessionSceneLayerKind)i);
            VisualChildren.Add(_layers[i]);
        }
    }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }

    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }

    public IBrush? MetalInk0 { get => GetValue(MetalInk0Property); set => SetValue(MetalInk0Property, value); }

    public IBrush? MetalInk1 { get => GetValue(MetalInk1Property); set => SetValue(MetalInk1Property, value); }

    public IBrush? MetalInk2 { get => GetValue(MetalInk2Property); set => SetValue(MetalInk2Property, value); }

    public IBrush? MetalInk3 { get => GetValue(MetalInk3Property); set => SetValue(MetalInk3Property, value); }

    public IBrush? MetalInk4 { get => GetValue(MetalInk4Property); set => SetValue(MetalInk4Property, value); }

    public IBrush? MetalEdgeInk { get => GetValue(MetalEdgeInkProperty); set => SetValue(MetalEdgeInkProperty, value); }

    public IBrush? ScreenInk { get => GetValue(ScreenInkProperty); set => SetValue(ScreenInkProperty, value); }

    public IBrush? CableInk { get => GetValue(CableInkProperty); set => SetValue(CableInkProperty, value); }

    public IBrush? ShellInk { get => GetValue(ShellInkProperty); set => SetValue(ShellInkProperty, value); }

    public IBrush? ShellEdgeInk { get => GetValue(ShellEdgeInkProperty); set => SetValue(ShellEdgeInkProperty, value); }

    public IBrush? WellInk { get => GetValue(WellInkProperty); set => SetValue(WellInkProperty, value); }

    public IBrush? BeyondInk { get => GetValue(BeyondInkProperty); set => SetValue(BeyondInkProperty, value); }

    public IBrush? SheenInk { get => GetValue(SheenInkProperty); set => SetValue(SheenInkProperty, value); }

    public double LimitOpacity { get => GetValue(LimitOpacityProperty); set => SetValue(LimitOpacityProperty, value); }

    public double LimitHeldOpacity { get => GetValue(LimitHeldOpacityProperty); set => SetValue(LimitHeldOpacityProperty, value); }

    /// <summary>The cable's arc-length table (shared; the packets travel along it).</summary>
    public static ChargeSessionPath Cable => s_cable;

    /// <summary>How far along the packets' path the fill front is, for a charge in percent.</summary>
    public static double PathEnd(double charge) => s_cable.Length + FrontX(charge) - CableEndX;

    /// <summary>The fill front's x in units.</summary>
    public static double FrontX(double charge) => WellLeft + PerPercent * Math.Clamp(charge, 0, 100);

    /// <summary>The limit's x in units.</summary>
    public static double LimitX(double limit) => WellLeft + PerPercent * Math.Clamp(limit, 0, 100);

    /// <summary>The limit for a pointer at unit x (not yet rounded or clamped).</summary>
    public static double LimitAt(double unitX) => (unitX - WellLeft) / PerPercent;

    /// <summary>Whether the limit line is drawn held (0..1; tests read it).</summary>
    internal double HeldMix => _heldMix;

    /// <summary>Connects the state to draw.</summary>
    public void Attach(ChargeSessionModel model, ChargeSessionFlow flow)
    {
        _model = model;
        _flow = flow;
        InvalidateLayers();
    }

    /// <summary>Invalidates the layers whose state changed since the last call (the card calls it after every step).</summary>
    public void Update()
    {
        if (_model is not { } model || _flow is not { } flow)
        {
            return;
        }
        var charge = model.Charge;
        var a = model.Availability;
        var limit = model.Limit;
        var packets = flow.Count;
        var flash = flow.Flash;
        var chargeMoved = charge != _shownCharge;
        var limitMoved = limit != _shownLimit;
        if (a != _shownAvailability)
        {
            Layer(ChargeSessionSceneLayerKind.Glow).InvalidateVisual();
        }
        if (chargeMoved)
        {
            Layer(ChargeSessionSceneLayerKind.Fill).InvalidateVisual();
        }
        if (limitMoved)
        {
            Layer(ChargeSessionSceneLayerKind.Overlay).InvalidateVisual();
        }
        if (chargeMoved || limitMoved || packets > 0 || _shownPackets > 0 || flash != _shownFlash)
        {
            Layer(ChargeSessionSceneLayerKind.Front).InvalidateVisual();
        }
        _shownCharge = charge;
        _shownAvailability = a;
        _shownLimit = limit;
        _shownPackets = packets;
        _shownFlash = flash;
    }

    /// <summary>Redraws every layer (palette, size or state connected).</summary>
    public void InvalidateLayers()
    {
        foreach (var layer in _layers)
        {
            layer.InvalidateVisual();
        }
        _shownCharge = double.NaN;
    }

    private ChargeSessionSceneLayer Layer(ChargeSessionSceneLayerKind kind) => _layers[(int)kind];

    /// <summary>Raises the limit line to its held opacity (160 ms, standard easing) or lets it back down.</summary>
    public void SetHeld(bool held)
    {
        // The line is drawn here, not a styled element, so a property transition cannot reach it.
        _held ??= CreateHeld();
        _ = _held.AnimateTo(held ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private MotionValue<double> CreateHeld()
    {
        var value = new MotionValue<double>(this, _heldMix, MotionVectors.Double);
        value.Changed += v =>
        {
            _heldMix = v;
            Layer(ChargeSessionSceneLayerKind.Front).InvalidateVisual();
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : UnitWidth;
        var size = new Size(width, width * UnitHeight / UnitWidth);
        foreach (var layer in _layers)
        {
            layer.Measure(size);
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _layers)
        {
            layer.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (Array.IndexOf(s_inks, p) >= 0)
        {
            _paletteDirty = true;
            _penScale = -1;
            InvalidateLayers();
        }
        else if (p == LimitOpacityProperty || p == LimitHeldOpacityProperty)
        {
            Layer(ChargeSessionSceneLayerKind.Front).InvalidateVisual();
        }
    }

    /// <summary>Draws one layer (called by the layer visuals in paint order).</summary>
    internal void RenderLayer(ChargeSessionSceneLayerKind kind, DrawingContext context)
    {
        var width = Bounds.Width;
        if (width <= 0 || _model is null || _flow is null)
        {
            return;
        }
        var k = width / UnitWidth;
        EnsurePalette();
        EnsurePens(k);
        var a = _model.Availability;
        var charge = _model.Charge;
        var limit = _model.Limit;

        using (context.PushTransform(Matrix.CreateScale(k, k)))
        {
            switch (kind)
            {
                case ChargeSessionSceneLayerKind.Back:
                    // The charger: brushed metal post and screen; the cable.
                    context.DrawRectangle(_metal, _metalPen, new Rect(12, 28, 72, 184), 18, 18);
                    context.DrawRectangle(_screen, null, new Rect(28, 46, 40, 24), 6, 6);
                    context.DrawGeometry(null, _cablePen, s_cableGeometry);
                    break;
                case ChargeSessionSceneLayerKind.Glow:
                    // The LED and the cable core dim with the power (nothing static covers the LED, so it can sit here).
                    context.DrawRectangle(_accentAlpha.Get(0.18 + 0.82 * a), null, new Rect(44, 96, 8, 56), 4, 4);
                    if (a > 0)
                    {
                        context.DrawGeometry(null, _coreAlpha.Get(0.55 * a), s_cableGeometry);
                    }
                    break;
                case ChargeSessionSceneLayerKind.Body:
                    // The holster over the cable's start, the nub and the shell over its end, and the empty well.
                    context.DrawRectangle(_holster, null, new Rect(72, 166, 18, 20), 5, 5);
                    context.DrawRectangle(_shell, _shellPen, new Rect(210, 96, 42, 48), 10, 10);
                    context.DrawRectangle(_shell, _shellPen, new Rect(232, 20, 752, 200), 44, 44);
                    using (context.PushClip(WellClip))
                    {
                        context.DrawRectangle(_well, null, new Rect(WellLeft, 32, 728, 176));
                    }
                    break;
                case ChargeSessionSceneLayerKind.Fill:
                    var front = FrontX(charge);
                    if (front > WellLeft)
                    {
                        using (context.PushClip(WellClip))
                        {
                            context.DrawRectangle(_accent, null, new Rect(WellLeft, 32, front - WellLeft, 176));
                        }
                    }
                    break;
                case ChargeSessionSceneLayerKind.Overlay:
                    // The tint beyond the limit and the cell dividers over the fill; the dark theme's sheen.
                    using (context.PushClip(WellClip))
                    {
                        var limitX = LimitX(limit);
                        if (limitX < 972)
                        {
                            context.DrawRectangle(_beyond, null, new Rect(limitX, 32, 972 - limitX, 176));
                        }
                        for (var i = 1; i <= 9; i++)
                        {
                            var x = WellLeft + 72.8 * i;
                            context.DrawLine(_cellPen!, new Point(x, 46), new Point(x, 194));
                        }
                    }
                    if (_sheenColor.A > 0)
                    {
                        context.DrawLine(_sheenPen!, new Point(276, 25), new Point(940, 25));
                    }
                    break;
                case ChargeSessionSceneLayerKind.Front:
                    // The fill front (brighter after landings), the dashed limit line and the packets.
                    var frontX = FrontX(charge);
                    using (context.PushClip(WellClip))
                    {
                        context.DrawLine(_frontAlpha.Get(Math.Min(1, 0.5 + _flow.Flash)), new Point(frontX, 32), new Point(frontX, 208));
                    }
                    var limitLine = LimitX(limit);
                    var lineOpacity = LimitOpacity + (LimitHeldOpacity - LimitOpacity) * _heldMix;
                    context.DrawLine(_limitAlpha.Get(lineOpacity), new Point(limitLine, 20), new Point(limitLine, 220));
                    DrawPackets(context, charge);
                    break;
            }
        }
    }

    private static RoundedRect WellClip => new(new Rect(WellLeft, 32, 728, 176), 32);

    private void DrawPackets(DrawingContext context, double charge)
    {
        var flow = _flow!;
        if (flow.Count == 0)
        {
            return;
        }
        var end = PathEnd(charge);
        for (var i = 0; i < ChargeSessionFlow.PoolSize; i++)
        {
            if (!flow.IsAlive(i))
            {
                continue;
            }
            var opacity = flow.OpacityOf(i, end);
            if (opacity <= 0.002)
            {
                continue;
            }
            var point = s_cable.PointAt(flow.DistanceOf(i));
            var radius = PacketRadius(point.X);
            // Pre-multiplied alpha brushes, one per alpha byte, instead of an opacity layer per packet.
            var brush = point.X < 238 ? _accentAlpha.Get(opacity) : _backgroundAlpha.Get(opacity);
            context.DrawEllipse(brush, null, point, radius, radius);
        }
    }

    /// <summary>Packet radius at unit x: 5.5 on the cable easing to 4.5 inside the pack between x 212 and 244.</summary>
    public static double PacketRadius(double x) => 5.5 - ChargeSessionCurve.Smoothstep(CableEndX, WellLeft, x);

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        _accentColor = ColorOf(AccentInk, Color.FromRgb(0x33, 0x33, 0x33));
        _foregroundColor = ColorOf(ForegroundInk, Colors.Black);
        _backgroundColor = ColorOf(BackgroundInk, Colors.White);
        _metalEdgeColor = ColorOf(MetalEdgeInk, Color.FromArgb(41, 0, 0, 0));
        _cableColor = ColorOf(CableInk, Color.FromRgb(0xD6, 0xD6, 0xD6));
        _shellEdgeColor = ColorOf(ShellEdgeInk, Color.FromArgb(56, 0, 0, 0));
        _beyondColor = ColorOf(BeyondInk, Color.FromArgb(18, 0, 0, 0));
        _sheenColor = ColorOf(SheenInk, Colors.Transparent);
        _accent = new ImmutableSolidColorBrush(_accentColor);
        _background = new ImmutableSolidColorBrush(_backgroundColor);
        _screen = new ImmutableSolidColorBrush(ColorOf(ScreenInk, Color.FromRgb(0x2A, 0x2A, 0x2A)));
        _shell = new ImmutableSolidColorBrush(ColorOf(ShellInk, Colors.White));
        _well = new ImmutableSolidColorBrush(ColorOf(WellInk, Color.FromRgb(0xEA, 0xEA, 0xEA)));
        _beyond = new ImmutableSolidColorBrush(_beyondColor);
        // Brushed metal: five gray stops on the post's diagonal, light and dark bands alternating.
        var stops = new[]
        {
            new ImmutableGradientStop(0, ColorOf(MetalInk0, Colors.LightGray)),
            new ImmutableGradientStop(0.30, ColorOf(MetalInk1, Colors.WhiteSmoke)),
            new ImmutableGradientStop(0.55, ColorOf(MetalInk2, Colors.LightGray)),
            new ImmutableGradientStop(0.80, ColorOf(MetalInk3, Colors.WhiteSmoke)),
            new ImmutableGradientStop(1, ColorOf(MetalInk4, Colors.Silver)),
        };
        _accentAlpha.Reset(_accentColor);
        _backgroundAlpha.Reset(_backgroundColor);
        var screen = ColorOf(ScreenInk, Color.FromRgb(0x2A, 0x2A, 0x2A));
        _holster = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(screen.A * 0.85), screen.R, screen.G, screen.B));
        _metal = new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative));
    }

    private void EnsurePens(double k)
    {
        if (Math.Abs(_penScale - k) < 1e-9)
        {
            return;
        }
        _penScale = k;
        // Pixel-width strokes: the drawing is scaled by k, so a 1px line is 1/k units thick. Dashes count in line widths.
        var px = 1 / k;
        _metalPen = new ImmutablePen(new ImmutableSolidColorBrush(_metalEdgeColor), px);
        _cablePen = new ImmutablePen(new ImmutableSolidColorBrush(_cableColor), 14, lineCap: PenLineCap.Round);
        _coreAlpha.Reset(_accentColor, 3, PenLineCap.Round, null);
        _shellPen = new ImmutablePen(new ImmutableSolidColorBrush(_shellEdgeColor), 1.5 * px);
        _cellPen = new ImmutablePen(new ImmutableSolidColorBrush(_beyondColor), px);
        _frontAlpha.Reset(_backgroundColor, 2 * px, PenLineCap.Flat, null);
        _sheenPen = new ImmutablePen(new ImmutableSolidColorBrush(_sheenColor), px, lineCap: PenLineCap.Round);
        _limitAlpha.Reset(_foregroundColor, 1.5 * px, PenLineCap.Flat, new ImmutableDashStyle(new[] { 4 / 1.5, 4 / 1.5 }, 0));
    }

    private static StreamGeometry BuildCable()
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(new Point(84, 176), false);
        ctx.CubicBezierTo(new Point(140, 238), new Point(168, 120), new Point(212, 120));
        ctx.EndFigure(false);
        return geometry;
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
}

/// <summary>The paint-order layers of <see cref="ChargeSessionScene"/>.</summary>
internal enum ChargeSessionSceneLayerKind
{
    /// <summary>Charger post, screen and cable (size and palette only).</summary>
    Back,

    /// <summary>LED and cable core (while availability changes).</summary>
    Glow,

    /// <summary>Holster, nub, shell and the empty well (size and palette only).</summary>
    Body,

    /// <summary>The fill up to the charge.</summary>
    Fill,

    /// <summary>Tint beyond the limit, cell dividers, sheen (limit changes).</summary>
    Overlay,

    /// <summary>Fill front, limit line and packets (every frame while anything moves).</summary>
    Front,
}

/// <summary>One layer visual of the scene: it only forwards its render to the scene, so it is re-recorded on its own.</summary>
internal sealed class ChargeSessionSceneLayer : Control
{
    private readonly ChargeSessionScene _scene;

    public ChargeSessionSceneLayer(ChargeSessionScene scene, ChargeSessionSceneLayerKind kind)
    {
        _scene = scene;
        Kind = kind;
        IsHitTestVisible = false;
    }

    public ChargeSessionSceneLayerKind Kind { get; }

    public override void Render(DrawingContext context) => _scene.RenderLayer(Kind, context);
}

/// <summary>Immutable solid brushes of one color at every alpha byte, created on first use (opacity without save layers).</summary>
internal sealed class ChargeSessionAlphaBrushes
{
    private readonly IBrush?[] _brushes = new IBrush?[256];
    private Color _color = Colors.Black;

    public void Reset(Color color)
    {
        _color = color;
        Array.Clear(_brushes);
    }

    /// <summary>The color with its alpha multiplied by <paramref name="opacity"/> (0..1).</summary>
    public IBrush Get(double opacity)
    {
        var alpha = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * _color.A);
        return _brushes[alpha] ??= new ImmutableSolidColorBrush(Color.FromArgb(alpha, _color.R, _color.G, _color.B));
    }
}

/// <summary>Immutable pens of one color, width, cap and dash at every alpha byte, created on first use.</summary>
internal sealed class ChargeSessionAlphaPens
{
    private readonly IPen?[] _pens = new IPen?[256];
    private Color _color = Colors.Black;
    private double _thickness = 1;
    private PenLineCap _cap;
    private ImmutableDashStyle? _dash;

    public void Reset(Color color, double thickness, PenLineCap cap, ImmutableDashStyle? dash)
    {
        _color = color;
        _thickness = thickness;
        _cap = cap;
        _dash = dash;
        Array.Clear(_pens);
    }

    /// <summary>The pen with its color's alpha multiplied by <paramref name="opacity"/> (0..1).</summary>
    public IPen Get(double opacity)
    {
        var alpha = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * _color.A);
        return _pens[alpha] ??= new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(alpha, _color.R, _color.G, _color.B)),
            _thickness, _dash, _cap);
    }
}
