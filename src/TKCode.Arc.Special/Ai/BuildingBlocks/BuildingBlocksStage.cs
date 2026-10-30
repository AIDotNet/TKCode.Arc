using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The frame of a <see cref="BuildingBlocks"/>: a tiny app (a nav bar, a hero card with a product photo, a chart, an
/// avatar row, a settings row with a switch and a button) drawn by one control on one clock instead of about sixty
/// elements with transitions of their own. Every block keeps a few CSS-style transitions (shown, content, drawn, styled)
/// and a fly-in on a bounce-free spring; the owner advances the clock and says which step to show. Everything is laid out
/// in units of the width, so the layout, the radii and the strokes scale together. Frames allocate nothing: brushes, pens
/// and geometries are built when the colors or the width change.
/// </summary>
internal sealed class BuildingBlocksStage : Control
{
    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(Aspect), 0.7);

    public static readonly StyledProperty<CornerRadius> FrameCornerRadiusProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, CornerRadius>(nameof(FrameCornerRadius), new CornerRadius(16));

    public static readonly StyledProperty<CornerRadius> BlockCornerRadiusProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, CornerRadius>(nameof(BlockCornerRadius), new CornerRadius(8));

    public static readonly StyledProperty<double> ReferenceWidthProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(ReferenceWidth), 400);

    public static readonly StyledProperty<double> WidthSMProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(WidthSM), 280);

    public static readonly StyledProperty<double> WidthMDProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(WidthMD), 400);

    public static readonly StyledProperty<double> WidthLGProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(WidthLG), 560);

    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(NarrowWidth), 320);

    public static readonly StyledProperty<double> PlaceholderOpacityProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(PlaceholderOpacity), 0.16);

    public static readonly StyledProperty<double> FillOpacityProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(FillOpacity), 0.14);

    public static readonly StyledProperty<double> TitleInkOpacityProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(TitleInkOpacity), 0.5);

    public static readonly StyledProperty<double> LineInkOpacityProperty =
        AvaloniaProperty.Register<BuildingBlocksStage, double>(nameof(LineInkOpacity), 0.26);

    public static readonly StyledProperty<BuildingBlocksTone> ToneProperty =
        BuildingBlocks.ToneProperty.AddOwner<BuildingBlocksStage>();

    public static readonly StyledProperty<Color?> TintProperty =
        BuildingBlocks.TintProperty.AddOwner<BuildingBlocksStage>();

    public static readonly StyledProperty<IBrush?> AccentInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> OnAccentInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(OnAccentInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> BackgroundInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(BackgroundInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> CanvasInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(CanvasInk));
    public static readonly StyledProperty<IBrush?> EdgeInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(EdgeInk));
    public static readonly StyledProperty<IBrush?> SlotInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(SlotInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> PhotoInk1Property = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(PhotoInk1));
    public static readonly StyledProperty<IBrush?> PhotoInk2Property = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(PhotoInk2));
    public static readonly StyledProperty<IBrush?> PhotoInk3Property = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(PhotoInk3));
    public static readonly StyledProperty<IBrush?> PhotoInk4Property = AvaloniaProperty.Register<BuildingBlocksStage, IBrush?>(nameof(PhotoInk4));

    /// <summary>How many blocks the mini app has.</summary>
    public const int BlockCount = 6;

    /// <summary>The blocks leave 40ms apart, so the whole layout lands within 200ms of the first one.</summary>
    public const double FlyStagger = 0.04;

    /// <summary>A block fades in over 160ms as it starts to fly.</summary>
    public const double AppearDuration = 0.16;

    /// <summary>Placeholders cross to content over 320ms, 60ms apart; bars and the chart line draw over 600ms, 80ms later.</summary>
    public const double ContentDuration = 0.32;

    public const double ContentStagger = 0.06;

    public const double DrawDuration = 0.6;

    public const double DrawDelay = 0.08;

    /// <summary>The styling pass fades in over 400ms, 50ms apart.</summary>
    public const double StyleDuration = 0.4;

    public const double StyleStagger = 0.05;

    /// <summary>Reduced motion: every step is one 240ms crossfade with no travel and no stagger.</summary>
    public const double ReducedDuration = 0.24;

    /// <summary>
    /// The fly-in: 520ms, critically damped (the original's expo-out landing with no overshoot), from just outside the slot
    /// and a little smaller to the slot.
    /// </summary>
    public static readonly SpringSpec FlySpring = SpringSpec.FromVisual(0.52, 0);

    /// <summary>The scale a block starts its flight at.</summary>
    public const double FlyScale = 0.92;

    private static readonly double s_flySettle = FlySpring.EstimateSettle(0.001, 0.01).TotalSeconds;

    // The slots in units of the width (the frame is 1 wide and 0.7 high; heights scale with the actual aspect).
    private static readonly Rect[] s_slots =
    [
        new(0.04, 0.04, 0.92, 0.09),      // nav bar
        new(0.04, 0.165, 0.555, 0.25),    // hero card
        new(0.625, 0.165, 0.335, 0.335),  // chart
        new(0.04, 0.445, 0.555, 0.1),     // avatar row
        new(0.04, 0.575, 0.555, 0.085),   // settings row
        new(0.625, 0.545, 0.335, 0.115),  // button
    ];

    // Where each block flies in from, in units of the width: just outside its slot.
    private static readonly Vector[] s_origins =
    [
        new(0, -0.05),
        new(-0.05, 0.01),
        new(0.05, -0.01),
        new(-0.05, 0.02),
        new(0, 0.05),
        new(0.04, 0.05),
    ];

    // The chart's line, x and y in 0..1 of the plot (y down).
    private static readonly Point[] s_chart =
    [
        new(0, 0.74), new(0.17, 0.56), new(0.33, 0.64), new(0.5, 0.36), new(0.67, 0.46), new(0.83, 0.18), new(1, 0.26),
    ];

    private readonly Block[] _blocks = new Block[BlockCount];
    private readonly Rect[] _rects = new Rect[BlockCount];
    private double _now;
    private bool _laid;
    private bool _filled;
    private bool _styled;
    private bool _full = true;
    private bool _wasMoving;

    private bool _paletteDirty = true;
    private double _geometryWidth = -1;
    private double _geometryHeight = -1;
    private IBrush? _canvas;
    private IBrush? _surface;
    private IBrush? _tint;
    private IBrush? _tintSoft;
    private IBrush? _tintFill;
    private IBrush? _onTint;
    private IBrush? _title;
    private IBrush? _line;
    private IBrush? _photo;
    private IBrush? _hill;
    private IBrush? _sun;
    private readonly IBrush?[] _portraits = new IBrush?[3];
    private IPen? _edgePen;
    private IPen? _slotPen;
    private IPen? _inkPen;
    private IPen? _tintPen;
    private Color _titleColor;
    private Color _tintColor;
    private Geometry? _chartLine;
    private Geometry? _chartArea;
    private Geometry? _hills;

    public BuildingBlocksStage()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        Focusable = false;
        // The frame is decoration; the status line announces the phase.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        for (var i = 0; i < BlockCount; i++)
        {
            _blocks[i].Show.Jump(0);
            _blocks[i].Content.Jump(0);
            _blocks[i].Draw.Jump(0);
            _blocks[i].Style.Jump(0);
        }
    }

    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    public CornerRadius FrameCornerRadius { get => GetValue(FrameCornerRadiusProperty); set => SetValue(FrameCornerRadiusProperty, value); }

    public CornerRadius BlockCornerRadius { get => GetValue(BlockCornerRadiusProperty); set => SetValue(BlockCornerRadiusProperty, value); }

    public double ReferenceWidth { get => GetValue(ReferenceWidthProperty); set => SetValue(ReferenceWidthProperty, value); }

    public double WidthSM { get => GetValue(WidthSMProperty); set => SetValue(WidthSMProperty, value); }

    public double WidthMD { get => GetValue(WidthMDProperty); set => SetValue(WidthMDProperty, value); }

    public double WidthLG { get => GetValue(WidthLGProperty); set => SetValue(WidthLGProperty, value); }

    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <summary>The width a size grows to.</summary>
    internal double WidthFor(BuildingBlocksSize size) => size switch
    {
        BuildingBlocksSize.Small => WidthSM,
        BuildingBlocksSize.Large => WidthLG,
        _ => WidthMD
    };

    public double PlaceholderOpacity { get => GetValue(PlaceholderOpacityProperty); set => SetValue(PlaceholderOpacityProperty, value); }

    public double FillOpacity { get => GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }

    public double TitleInkOpacity { get => GetValue(TitleInkOpacityProperty); set => SetValue(TitleInkOpacityProperty, value); }

    public double LineInkOpacity { get => GetValue(LineInkOpacityProperty); set => SetValue(LineInkOpacityProperty, value); }

    public BuildingBlocksTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public Color? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? OnAccentInk { get => GetValue(OnAccentInkProperty); set => SetValue(OnAccentInkProperty, value); }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }

    public IBrush? BackgroundInk { get => GetValue(BackgroundInkProperty); set => SetValue(BackgroundInkProperty, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? CanvasInk { get => GetValue(CanvasInkProperty); set => SetValue(CanvasInkProperty, value); }

    public IBrush? EdgeInk { get => GetValue(EdgeInkProperty); set => SetValue(EdgeInkProperty, value); }

    public IBrush? SlotInk { get => GetValue(SlotInkProperty); set => SetValue(SlotInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public IBrush? PhotoInk1 { get => GetValue(PhotoInk1Property); set => SetValue(PhotoInk1Property, value); }

    public IBrush? PhotoInk2 { get => GetValue(PhotoInk2Property); set => SetValue(PhotoInk2Property, value); }

    public IBrush? PhotoInk3 { get => GetValue(PhotoInk3Property); set => SetValue(PhotoInk3Property, value); }

    public IBrush? PhotoInk4 { get => GetValue(PhotoInk4Property); set => SetValue(PhotoInk4Property, value); }

    /// <summary>The stage's clock in seconds (advances only through <see cref="Advance"/>).</summary>
    internal double Now => _now;

    /// <summary>Something still moves: a transition, a delay or a flight.</summary>
    internal bool IsMoving
    {
        get
        {
            for (var i = 0; i < BlockCount; i++)
            {
                ref var b = ref _blocks[i];
                if (b.Flying && _now - b.FlyStart < s_flySettle)
                {
                    return true;
                }
                if (b.Show.IsMoving(_now) || b.Content.IsMoving(_now) || b.Draw.IsMoving(_now) || b.Style.IsMoving(_now))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Some block is (still) visible, so a new build has to clear it first.</summary>
    internal bool HasVisibleBlocks
    {
        get
        {
            for (var i = 0; i < BlockCount; i++)
            {
                if (_blocks[i].Show.Value(_now) > 0.001)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Block <paramref name="index"/>'s opacity now.</summary>
    internal double ShownOf(int index) => _blocks[index].Show.Value(_now);

    /// <summary>How far block <paramref name="index"/> has crossed from placeholder to content (0..1).</summary>
    internal double ContentOf(int index) => _blocks[index].Content.Value(_now);

    /// <summary>How far block <paramref name="index"/>'s bars and line have drawn in (0..1).</summary>
    internal double DrawnOf(int index) => _blocks[index].Draw.Value(_now);

    /// <summary>How far block <paramref name="index"/> has taken the styling pass (0..1).</summary>
    internal double StyledOf(int index) => _blocks[index].Style.Value(_now);

    /// <summary>How far block <paramref name="index"/> has flown into its slot (0..1, never past 1).</summary>
    internal double FlightOf(int index) => FlightProgress(ref _blocks[index]);

    /// <summary>The switch knob's travel, 0 (off) to 1 (on).</summary>
    internal double KnobOf() => KnobTravel(ref _blocks[4]);

    /// <summary>The tint the blocks take.</summary>
    internal Color TintColor
    {
        get
        {
            EnsurePalette();
            return _tintColor;
        }
    }

    /// <summary>The slot of block <paramref name="index"/> for a frame of the given size.</summary>
    internal static Rect SlotFor(int index, double width, double height)
    {
        var s = s_slots[index];
        var ys = height / 0.7;
        return new Rect(s.X * width, s.Y * ys, s.Width * width, s.Height * ys);
    }

    /// <summary>Moves the clock on (the owner's frame loop, or tests).</summary>
    internal void Advance(double seconds)
    {
        if (seconds <= 0)
        {
            return;
        }
        _now += seconds;
        var moving = IsMoving;
        if (moving || _wasMoving)
        {
            InvalidateVisual();
        }
        _wasMoving = moving;
    }

    /// <summary>
    /// Shows <paramref name="step"/>. Transitions start from what is on screen. Entering a build after an empty frame
    /// resets the blocks out of sight and flies them in; later steps only add their layer, so a Done that arrives mid-build
    /// fast forwards everything that is left.
    /// </summary>
    internal void Show(BuildingBlocksStep step, ArcMotionLevel level)
    {
        _full = level == ArcMotionLevel.Full;
        var off = level == ArcMotionLevel.Off;
        switch (step)
        {
            case BuildingBlocksStep.Empty:
            case BuildingBlocksStep.Clearing:
                for (var i = 0; i < BlockCount; i++)
                {
                    _blocks[i].Show.To(0, _now, Duration(BuildingBlocksSchedule.Fade, off));
                }
                _laid = _filled = _styled = false;
                break;
            default:
                Lay(off);
                if (step is BuildingBlocksStep.Components or BuildingBlocksStep.Styling or BuildingBlocksStep.Ready)
                {
                    Fill(off);
                }
                if (step is BuildingBlocksStep.Styling or BuildingBlocksStep.Ready)
                {
                    Style(off);
                }
                break;
        }
        _wasMoving = true;
        InvalidateVisual();
    }

    /// <summary>Lands every transition and flight at once (motion turned down mid-way, the stage left the screen).</summary>
    internal void Settle()
    {
        for (var i = 0; i < BlockCount; i++)
        {
            ref var b = ref _blocks[i];
            b.Flying = false;
            b.Show.Jump(b.Show.Target);
            b.Content.Jump(b.Content.Target);
            b.Draw.Jump(b.Draw.Target);
            b.Style.Jump(b.Style.Target);
        }
        InvalidateVisual();
    }

    private double Duration(double full, bool off) => off ? 0 : _full ? full : ReducedDuration;

    private void Lay(bool off)
    {
        if (_laid)
        {
            return;
        }
        _laid = true;
        _filled = _styled = false;
        for (var i = 0; i < BlockCount; i++)
        {
            ref var b = ref _blocks[i];
            // Reset out of sight (the previous layout has faded out by now), then fly in.
            b.Show.Jump(0);
            b.Content.Jump(0);
            b.Draw.Jump(0);
            b.Style.Jump(0);
            var delay = _full ? i * FlyStagger : 0;
            b.Flying = _full;
            b.FlyStart = _now + delay;
            b.Show.To(1, _now, Duration(AppearDuration, off), delay);
        }
    }

    private void Fill(bool off)
    {
        if (_filled)
        {
            return;
        }
        _filled = true;
        for (var i = 0; i < BlockCount; i++)
        {
            ref var b = ref _blocks[i];
            var delay = _full ? i * ContentStagger : 0;
            b.Content.To(1, _now, Duration(ContentDuration, off), delay);
            if (_full)
            {
                b.Draw.To(1, _now, DrawDuration, delay + DrawDelay, ArcEasings.Enter);
            }
            else
            {
                b.Draw.Jump(1);
            }
        }
    }

    private void Style(bool off)
    {
        if (_styled)
        {
            return;
        }
        _styled = true;
        for (var i = 0; i < BlockCount; i++)
        {
            var delay = _full ? i * StyleStagger : 0;
            _blocks[i].Style.To(1, _now, Duration(StyleDuration, off), delay);
        }
    }

    private double FlightProgress(ref Block b)
    {
        if (!b.Flying)
        {
            return 1;
        }
        var t = _now - b.FlyStart;
        if (t <= 0)
        {
            return 0;
        }
        if (t >= s_flySettle)
        {
            return 1;
        }
        var displacement = SpringSolver.Evaluate(FlySpring, 1, 0, t).Displacement;
        return Math.Clamp(1 - displacement, 0, 1);
    }

    private double KnobTravel(ref Block b)
    {
        var style = b.Style.Value(_now);
        // The switch's single slide rides the styling pass; without full motion it changes sides without travel.
        return _full ? style : style >= 0.5 ? 1 : 0;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : ReferenceWidth;
        return new Size(width, width * Math.Max(0.2, Aspect));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == AspectProperty || p == ReferenceWidthProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (p == FrameCornerRadiusProperty || p == BlockCornerRadiusProperty)
        {
            InvalidateVisual();
        }
        else if (p == ToneProperty || p == TintProperty || p == PlaceholderOpacityProperty || p == FillOpacityProperty
                 || p == TitleInkOpacityProperty || p == LineInkOpacityProperty
                 || (p.OwnerType == typeof(BuildingBlocksStage) && p.Name.EndsWith("Ink", StringComparison.Ordinal)))
        {
            _paletteDirty = true;
            _geometryWidth = -1;
            InvalidateVisual();
        }
        else if (p == BoundsProperty)
        {
            EnsureGeometry(Bounds.Width, Bounds.Height);
        }
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        var tint = BuildingBlocksPalette.Tint(Tint, Tone, AccentInk, ForegroundInk);
        var onTint = BuildingBlocksPalette.OnTint(Tint, Tone, OnAccentInk, BackgroundInk);
        var surface = BuildingBlocksPalette.ColorOf(SurfaceInk, Colors.White);
        var canvas = BuildingBlocksPalette.ColorOf(CanvasInk, Color.Parse("#F6F6F6"));
        var edge = BuildingBlocksPalette.ColorOf(EdgeInk, Color.Parse("#E4E4E4"));
        var slot = BuildingBlocksPalette.ColorOf(SlotInk, Color.Parse("#C8C8C8"));
        var muted = BuildingBlocksPalette.ColorOf(MutedInk, Color.Parse("#7D7D7D"));
        var foreground = BuildingBlocksPalette.ColorOf(ForegroundInk, Colors.Black);
        var p1 = BuildingBlocksPalette.ColorOf(PhotoInk1, Color.Parse("#1F7DCF"));
        var p2 = BuildingBlocksPalette.ColorOf(PhotoInk2, Color.Parse("#7128A5"));
        var p3 = BuildingBlocksPalette.ColorOf(PhotoInk3, Color.Parse("#E58F00"));
        var p4 = BuildingBlocksPalette.ColorOf(PhotoInk4, Color.Parse("#009E84"));
        _tintColor = tint;
        _canvas = new ImmutableSolidColorBrush(canvas);
        _surface = new ImmutableSolidColorBrush(surface);
        _tint = new ImmutableSolidColorBrush(tint);
        _tintSoft = new ImmutableSolidColorBrush(Oklch.WithAlpha(tint, PlaceholderOpacity));
        _tintFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(tint, FillOpacity));
        _onTint = new ImmutableSolidColorBrush(onTint);
        _titleColor = Oklch.WithAlpha(muted, TitleInkOpacity);
        _title = new ImmutableSolidColorBrush(_titleColor);
        _line = new ImmutableSolidColorBrush(Oklch.WithAlpha(muted, LineInkOpacity));
        _edgePen = new ImmutablePen(new ImmutableSolidColorBrush(edge), 1);
        _slotPen = new ImmutablePen(new ImmutableSolidColorBrush(slot), 1, new ImmutableDashStyle([3, 3], 0));
        // The "photos": generated gradients, softened toward the surface so they sit quietly in both themes.
        _photo = Gradient(Oklch.MixOklab(p1, surface, 0.25), Oklch.MixOklab(p2, surface, 0.35));
        _hill = new ImmutableSolidColorBrush(Oklch.WithAlpha(Oklch.MixOklab(p2, foreground, 0.35), 0.75));
        _sun = new ImmutableSolidColorBrush(Oklch.WithAlpha(Oklch.MixOklab(p3, surface, 0.2), 0.95));
        _portraits[0] = Gradient(Oklch.MixOklab(p3, surface, 0.2), Oklch.MixOklab(p2, surface, 0.3));
        _portraits[1] = Gradient(Oklch.MixOklab(p4, surface, 0.2), Oklch.MixOklab(p1, surface, 0.3));
        _portraits[2] = Gradient(Oklch.MixOklab(p2, surface, 0.25), Oklch.MixOklab(p3, surface, 0.3));
    }

    private static ImmutableLinearGradientBrush Gradient(Color from, Color to) =>
        new([new ImmutableGradientStop(0, from), new ImmutableGradientStop(1, to)],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));

    /// <summary>Slots, pens and the chart's and the photo's shapes for this size (on size or color changes, never per frame).</summary>
    private void EnsureGeometry(double width, double height)
    {
        EnsurePalette();
        if (width <= 0 || height <= 0 || (width == _geometryWidth && height == _geometryHeight))
        {
            return;
        }
        _geometryWidth = width;
        _geometryHeight = height;
        for (var i = 0; i < BlockCount; i++)
        {
            _rects[i] = SlotFor(i, width, height);
        }
        var k = Math.Max(0.75, width / Math.Max(1, ReferenceWidth));
        _inkPen = new ImmutablePen(new ImmutableSolidColorBrush(_titleColor), 1.5 * k, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _tintPen = new ImmutablePen(new ImmutableSolidColorBrush(_tintColor), 2 * k, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var plot = PlotRect(_rects[2], width);
        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(At(plot, s_chart[0]), false);
            for (var i = 1; i < s_chart.Length; i++)
            {
                g.LineTo(At(plot, s_chart[i]));
            }
            g.EndFigure(false);
        }
        var area = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(plot.Left, plot.Bottom), true);
            for (var i = 0; i < s_chart.Length; i++)
            {
                g.LineTo(At(plot, s_chart[i]));
            }
            g.LineTo(new Point(plot.Right, plot.Bottom));
            g.EndFigure(true);
        }
        _chartLine = line;
        _chartArea = area;

        var photo = PhotoRect(_rects[1], width);
        var hills = new StreamGeometry();
        using (var g = hills.Open())
        {
            g.BeginFigure(At(photo, new Point(0, 1)), true);
            g.LineTo(At(photo, new Point(0, 0.72)));
            g.LineTo(At(photo, new Point(0.3, 0.5)));
            g.LineTo(At(photo, new Point(0.52, 0.72)));
            g.LineTo(At(photo, new Point(0.74, 0.42)));
            g.LineTo(At(photo, new Point(1, 0.74)));
            g.LineTo(At(photo, new Point(1, 1)));
            g.EndFigure(true);
        }
        _hills = hills;

        static Point At(Rect r, Point p) => new(r.X + p.X * r.Width, r.Y + p.Y * r.Height);
    }

    private static Rect PlotRect(Rect s, double u)
    {
        var pad = u * 0.022;
        var top = s.Y + s.Height * 0.42;
        return new Rect(s.X + pad, top, s.Width - 2 * pad, s.Bottom - pad - top);
    }

    private static Rect PhotoRect(Rect s, double u)
    {
        var pad = u * 0.018;
        return new Rect(s.X + pad, s.Y + pad, s.Width * 0.42, s.Height - 2 * pad);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        EnsureGeometry(w, h);
        var scale = w / Math.Max(1, ReferenceWidth);
        var frameRadius = FrameCornerRadius.TopLeft * scale;
        var blockRadius = BlockCornerRadius.TopLeft * scale;
        context.DrawRectangle(_canvas, _edgePen, new Rect(0.5, 0.5, w - 1, h - 1), frameRadius, frameRadius);
        for (var i = 0; i < BlockCount; i++)
        {
            var r = RadiusOf(i, _rects[i], blockRadius);
            context.DrawRectangle(null, _slotPen, _rects[i].Deflate(0.5), r, r);
        }
        for (var i = 0; i < BlockCount; i++)
        {
            DrawBlock(context, i, w, blockRadius);
        }
    }

    /// <summary>The button is a pill; every other block uses the block radius.</summary>
    private static double RadiusOf(int index, Rect slot, double blockRadius) =>
        index == 5 ? Math.Min(slot.Height / 2, blockRadius * 1.5) : Math.Min(blockRadius, slot.Height / 2);

    private void DrawBlock(DrawingContext context, int i, double u, double blockRadius)
    {
        ref var b = ref _blocks[i];
        var shown = b.Show.Value(_now);
        if (shown <= 0.001)
        {
            return;
        }
        var slot = _rects[i];
        var radius = RadiusOf(i, slot, blockRadius);
        var flight = FlightProgress(ref b);
        var matrix = Matrix.Identity;
        if (flight < 1)
        {
            // Position and size spring together: from just outside the slot and a little smaller, into the slot.
            var rest = 1 - flight;
            var s = FlyScale + (1 - FlyScale) * flight;
            var c = slot.Center;
            var o = s_origins[i];
            matrix = Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateScale(s, s)
                     * Matrix.CreateTranslation(c.X + o.X * u * rest, c.Y + o.Y * u * rest);
        }
        var content = b.Content.Value(_now);
        var drawn = b.Draw.Value(_now);
        var styled = b.Style.Value(_now);
        using (context.PushTransform(matrix))
        using (context.PushOpacity(shown))
        {
            if (content < 0.999)
            {
                using (context.PushOpacity(1 - content))
                {
                    context.DrawRectangle(_tintSoft, null, slot, radius, radius);
                }
            }
            if (content > 0.001)
            {
                using (context.PushOpacity(content))
                {
                    switch (i)
                    {
                        case 0:
                            DrawNav(context, slot, u, radius, drawn, styled);
                            break;
                        case 1:
                            DrawHero(context, slot, u, radius, drawn, styled);
                            break;
                        case 2:
                            DrawChart(context, slot, u, radius, drawn, styled);
                            break;
                        case 3:
                            DrawAvatars(context, slot, u, radius, drawn, styled);
                            break;
                        case 4:
                            DrawSettings(context, slot, u, radius, drawn, styled, KnobTravel(ref b));
                            break;
                        default:
                            DrawButton(context, slot, u, radius, drawn, styled);
                            break;
                    }
                }
            }
        }
    }

    private void Card(DrawingContext context, Rect slot, double radius) =>
        context.DrawRectangle(_surface, _edgePen, slot.Deflate(0.5), radius, radius);

    private static void Bar(DrawingContext context, IBrush? brush, double x, double y, double width, double height)
    {
        if (width <= 0.25)
        {
            return;
        }
        var r = Math.Min(height / 2, width / 2);
        context.DrawRectangle(brush, null, new Rect(x, y, width, height), r, r);
    }

    private void DrawNav(DrawingContext context, Rect s, double u, double radius, double drawn, double styled)
    {
        Card(context, s, radius);
        var h = s.Height;
        var cy = s.Center.Y;
        var logo = h * 0.46;
        var logoRect = new Rect(s.X + h * 0.27, cy - logo / 2, logo, logo);
        context.DrawRectangle(_title, null, logoRect, logo * 0.3, logo * 0.3);
        var bh = u * 0.012;
        var x = logoRect.Right + u * 0.032;
        var first = u * 0.075;
        Bar(context, _line, x, cy - bh / 2, first * drawn, bh);
        var x2 = x + first + u * 0.022;
        Bar(context, _line, x2, cy - bh / 2, u * 0.058 * drawn, bh);
        var x3 = x2 + u * 0.058 + u * 0.022;
        Bar(context, _line, x3, cy - bh / 2, u * 0.066 * drawn, bh);
        var d = h * 0.5;
        var avatar = new Rect(s.Right - h * 0.27 - d, cy - d / 2, d, d);
        context.DrawRectangle(_line, null, avatar, d / 2, d / 2);
        if (styled > 0.001)
        {
            using (context.PushOpacity(styled))
            {
                context.DrawRectangle(_tint, null, logoRect, logo * 0.3, logo * 0.3);
                Bar(context, _tint, x, cy - bh / 2, first * drawn, bh);
                Portrait(context, avatar, _portraits[0]);
            }
        }
    }

    private void DrawHero(DrawingContext context, Rect s, double u, double radius, double drawn, double styled)
    {
        Card(context, s, radius);
        var pad = u * 0.018;
        var photo = PhotoRect(s, u);
        var photoRadius = Math.Max(0, radius - pad / 2);
        context.DrawRectangle(_canvas, null, photo, photoRadius, photoRadius);
        var x0 = photo.Right + u * 0.026;
        var avail = s.Right - pad - x0;
        var y = s.Y + pad + u * 0.006;
        Bar(context, _title, x0, y, avail * 0.8 * drawn, u * 0.022);
        y += u * 0.044;
        Bar(context, _line, x0, y, avail * 0.95 * drawn, u * 0.012);
        y += u * 0.024;
        Bar(context, _line, x0, y, avail * 0.66 * drawn, u * 0.012);
        var chipH = u * 0.036;
        var chip = new Rect(x0, s.Bottom - pad - chipH, Math.Min(avail * 0.6, u * 0.11), chipH);
        context.DrawRectangle(_canvas, _edgePen, chip.Deflate(0.5), chipH / 2, chipH / 2);
        if (styled > 0.001)
        {
            using (context.PushOpacity(styled))
            {
                // The product photo fades in: a generated gradient with a sun and hills, clipped to its rounded frame.
                using (context.PushClip(new RoundedRect(photo, photoRadius)))
                {
                    context.DrawRectangle(_photo, null, photo);
                    var sun = photo.Height * 0.2;
                    context.DrawEllipse(_sun, null, new Point(photo.X + photo.Width * 0.72, photo.Y + photo.Height * 0.3), sun / 2, sun / 2);
                    if (_hills is not null)
                    {
                        context.DrawGeometry(_hill, null, _hills);
                    }
                }
                context.DrawRectangle(_tintFill, null, chip, chipH / 2, chipH / 2);
                Bar(context, _tint, chip.X + chip.Width * 0.2, chip.Center.Y - chipH * 0.15, chip.Width * 0.6, chipH * 0.3);
            }
        }
    }

    private void DrawChart(DrawingContext context, Rect s, double u, double radius, double drawn, double styled)
    {
        Card(context, s, radius);
        var pad = u * 0.022;
        Bar(context, _line, s.X + pad, s.Y + pad, s.Width * 0.36 * drawn, u * 0.012);
        Bar(context, _title, s.X + pad, s.Y + pad + u * 0.026, s.Width * 0.5 * drawn, u * 0.024);
        var plot = PlotRect(s, u);
        if (_chartLine is null || _chartArea is null || drawn <= 0.001)
        {
            return;
        }
        // The line draws in from the left edge: a clip that widens with the drawn progress.
        var reveal = new Rect(plot.X - u * 0.01, s.Y, plot.Width * drawn + u * 0.02, s.Height);
        using (context.PushClip(reveal))
        {
            context.DrawGeometry(null, _inkPen, _chartLine);
            if (styled > 0.001)
            {
                using (context.PushOpacity(styled))
                {
                    context.DrawGeometry(_tintFill, null, _chartArea);
                    context.DrawGeometry(null, _tintPen, _chartLine);
                    var end = s_chart[^1];
                    var dot = u * 0.009;
                    context.DrawEllipse(_tint, null, new Point(plot.X + end.X * plot.Width, plot.Y + end.Y * plot.Height), dot, dot);
                }
            }
        }
    }

    private void DrawAvatars(DrawingContext context, Rect s, double u, double radius, double drawn, double styled)
    {
        Card(context, s, radius);
        var cy = s.Center.Y;
        var d = s.Height * 0.56;
        var step = d * 0.68;
        var ring = Math.Max(1, u * 0.004);
        var x = s.X + s.Height * 0.24;
        for (var k = 0; k < 3; k++)
        {
            var rect = new Rect(x + k * step, cy - d / 2, d, d);
            var outer = rect.Inflate(ring);
            context.DrawRectangle(_surface, null, outer, outer.Width / 2, outer.Height / 2);
            context.DrawRectangle(_line, null, rect, d / 2, d / 2);
            if (styled > 0.001)
            {
                using (context.PushOpacity(styled))
                {
                    Portrait(context, rect, _portraits[k]);
                }
            }
        }
        var tx = x + 2 * step + d + u * 0.03;
        Bar(context, _title, tx, cy - u * 0.016, u * 0.11 * drawn, u * 0.014);
        Bar(context, _line, tx, cy + u * 0.006, u * 0.075 * drawn, u * 0.01);
    }

    private void DrawSettings(DrawingContext context, Rect s, double u, double radius, double drawn, double styled, double knob)
    {
        Card(context, s, radius);
        var cy = s.Center.Y;
        var pad = s.Height * 0.32;
        Bar(context, _title, s.X + pad, cy - u * 0.014, u * 0.13 * drawn, u * 0.013);
        Bar(context, _line, s.X + pad, cy + u * 0.006, u * 0.09 * drawn, u * 0.01);
        var th = s.Height * 0.46;
        var tw = th * 1.8;
        var track = new Rect(s.Right - pad - tw, cy - th / 2, tw, th);
        context.DrawRectangle(_line, null, track, th / 2, th / 2);
        if (styled > 0.001)
        {
            using (context.PushOpacity(styled))
            {
                context.DrawRectangle(_tint, null, track, th / 2, th / 2);
            }
        }
        var inset = th * 0.14;
        var kd = th - 2 * inset;
        var kx = track.X + inset + (tw - th) * knob;
        context.DrawRectangle(_surface, null, new Rect(kx, track.Y + inset, kd, kd), kd / 2, kd / 2);
    }

    private void DrawButton(DrawingContext context, Rect s, double u, double radius, double drawn, double styled)
    {
        context.DrawRectangle(_canvas, _edgePen, s.Deflate(0.5), radius, radius);
        var bw = s.Width * 0.42;
        var bh = u * 0.014;
        var x = s.Center.X - bw / 2;
        Bar(context, _title, x, s.Center.Y - bh / 2, bw * drawn, bh);
        if (styled > 0.001)
        {
            using (context.PushOpacity(styled))
            {
                context.DrawRectangle(_tint, null, s, radius, radius);
                Bar(context, _onTint, x, s.Center.Y - bh / 2, bw * drawn, bh);
            }
        }
    }

    /// <summary>A generated portrait: a gradient circle with a head and shoulders, never a downloaded photo.</summary>
    private void Portrait(DrawingContext context, Rect rect, IBrush? brush)
    {
        var d = rect.Width;
        using (context.PushClip(new RoundedRect(rect, d / 2)))
        {
            context.DrawRectangle(brush, null, rect);
            context.DrawEllipse(_sun, null, new Point(rect.Center.X, rect.Y + d * 0.4), d * 0.18, d * 0.18);
            context.DrawEllipse(_sun, null, new Point(rect.Center.X, rect.Bottom + d * 0.08), d * 0.34, d * 0.26);
        }
    }

    private struct Block
    {
        public BuildingBlocksTween Show;
        public BuildingBlocksTween Content;
        public BuildingBlocksTween Draw;
        public BuildingBlocksTween Style;
        public double FlyStart;
        public bool Flying;
    }
}
