using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>The slider logic behind a rail (pointer input is forwarded to it).</summary>
internal interface ISliderRailOwner
{
    void OnRailPressed(SliderRailBase rail, PointerPressedEventArgs e);

    void OnRailMoved(SliderRailBase rail, PointerEventArgs e);

    void OnRailReleased(SliderRailBase rail, PointerReleasedEventArgs e);

    void OnRailCaptureLost(SliderRailBase rail);

    /// <summary>Layout changed: place thumbs and bubbles again.</summary>
    void OnRailArranged(SliderRailBase rail);
}

/// <summary>
/// The control row of a slider (Arc <c>.control</c>): the whole row takes the press, the track is inset by half a thumb
/// so a thumb at either limit stays inside, and thumbs and value bubbles are children placed with render transforms.
/// Subclasses draw the track, fill and ticks in <see cref="Visual.Render"/>.
/// </summary>
internal abstract class SliderRailBase : Control, ICustomHitTest
{
    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(FillBrush));

    /// <summary>Ticks outside the fill (foreground at ~30%, a neutral control token).</summary>
    public static readonly StyledProperty<IBrush?> TickBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(TickBrush));

    /// <summary><c>ControlGlyph</c>; ticks inside the fill use it at 60% (accent-dependent, so derived here).</summary>
    public static readonly StyledProperty<IBrush?> TickOnSourceProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(TickOnSource));

    public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(ThumbBrush));

    public static readonly StyledProperty<BoxShadows> ThumbShadowProperty =
        AvaloniaProperty.Register<SliderRailBase, BoxShadows>(nameof(ThumbShadow));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<double> ThumbDiameterProperty =
        AvaloniaProperty.Register<SliderRailBase, double>(nameof(ThumbDiameter), 22);

    public static readonly StyledProperty<double> TrackThicknessProperty =
        AvaloniaProperty.Register<SliderRailBase, double>(nameof(TrackThickness), 6);

    public static readonly StyledProperty<double> TickDiameterProperty =
        AvaloniaProperty.Register<SliderRailBase, double>(nameof(TickDiameter), 4);

    /// <summary>Space between the thumb's top and the bubble's bottom.</summary>
    public static readonly StyledProperty<double> BubbleGapProperty =
        AvaloniaProperty.Register<SliderRailBase, double>(nameof(BubbleGap), 8);

    public static readonly StyledProperty<IBrush?> BubbleBackgroundProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(BubbleBackground));

    public static readonly StyledProperty<IBrush?> BubbleForegroundProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(BubbleForeground));

    public static readonly StyledProperty<IBrush?> BubbleBorderBrushProperty =
        AvaloniaProperty.Register<SliderRailBase, IBrush?>(nameof(BubbleBorderBrush));

    public static readonly StyledProperty<Thickness> BubbleBorderThicknessProperty =
        AvaloniaProperty.Register<SliderRailBase, Thickness>(nameof(BubbleBorderThickness));

    public static readonly StyledProperty<BoxShadows> BubbleShadowProperty =
        AvaloniaProperty.Register<SliderRailBase, BoxShadows>(nameof(BubbleShadow));

    public static readonly StyledProperty<double> BubbleMinHeightProperty =
        AvaloniaProperty.Register<SliderRailBase, double>(nameof(BubbleMinHeight), 28);

    public static readonly StyledProperty<Thickness> BubblePaddingProperty =
        AvaloniaProperty.Register<SliderRailBase, Thickness>(nameof(BubblePadding), new Thickness(12, 0));

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? TickOnSource { get => GetValue(TickOnSourceProperty); set => SetValue(TickOnSourceProperty, value); }
    public IBrush? ThumbBrush { get => GetValue(ThumbBrushProperty); set => SetValue(ThumbBrushProperty, value); }
    public BoxShadows ThumbShadow { get => GetValue(ThumbShadowProperty); set => SetValue(ThumbShadowProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public double ThumbDiameter { get => GetValue(ThumbDiameterProperty); set => SetValue(ThumbDiameterProperty, value); }
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    public double TickDiameter { get => GetValue(TickDiameterProperty); set => SetValue(TickDiameterProperty, value); }
    public double BubbleGap { get => GetValue(BubbleGapProperty); set => SetValue(BubbleGapProperty, value); }
    public IBrush? BubbleBackground { get => GetValue(BubbleBackgroundProperty); set => SetValue(BubbleBackgroundProperty, value); }
    public IBrush? BubbleForeground { get => GetValue(BubbleForegroundProperty); set => SetValue(BubbleForegroundProperty, value); }
    public IBrush? BubbleBorderBrush { get => GetValue(BubbleBorderBrushProperty); set => SetValue(BubbleBorderBrushProperty, value); }
    public Thickness BubbleBorderThickness { get => GetValue(BubbleBorderThicknessProperty); set => SetValue(BubbleBorderThicknessProperty, value); }
    public BoxShadows BubbleShadow { get => GetValue(BubbleShadowProperty); set => SetValue(BubbleShadowProperty, value); }
    public double BubbleMinHeight { get => GetValue(BubbleMinHeightProperty); set => SetValue(BubbleMinHeightProperty, value); }
    public Thickness BubblePadding { get => GetValue(BubblePaddingProperty); set => SetValue(BubblePaddingProperty, value); }

    private readonly List<SliderThumb> _thumbs = new();
    private readonly List<SliderBubble> _bubbles = new();
    private ISliderRailOwner? _owner;
    private IBrush? _tickOn;

    static SliderRailBase()
    {
        AffectsRender<SliderRailBase>(TrackBrushProperty, FillBrushProperty, TickBrushProperty, TickOnSourceProperty, TrackThicknessProperty, TickDiameterProperty);
        AffectsArrange<SliderRailBase>(ThumbDiameterProperty, BubbleGapProperty);
    }

    public IReadOnlyList<SliderThumb> Thumbs => _thumbs;

    public IReadOnlyList<SliderBubble> Bubbles => _bubbles;

    /// <summary>Ticks inside the fill: <c>color-mix(in oklab, var(--control-glyph) 60%, transparent)</c>.</summary>
    protected IBrush? TickOnBrush => _tickOn ??= TickOnSource is ISolidColorBrush solid
        ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * 0.6)
        : TickOnSource;

    /// <summary>Creates the thumbs and their bubbles (once).</summary>
    public void Setup(ISliderRailOwner owner, ISliderThumbHost host, int thumbCount, Func<int, Control> bubbleContent, Action<SliderBubble> configureBubble)
    {
        _owner = owner;
        if (_thumbs.Count > 0)
        {
            return;
        }
        for (var i = 0; i < thumbCount; i++)
        {
            var thumb = new SliderThumb(host, i);
            _thumbs.Add(thumb);
            VisualChildren.Add(thumb);
        }
        for (var i = 0; i < thumbCount; i++)
        {
            var content = bubbleContent(i);
            content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            var bubble = new SliderBubble { Child = content, ZIndex = 10 };
            configureBubble(bubble);
            _bubbles.Add(bubble);
            VisualChildren.Add(bubble);
        }
        ApplyChildStyles();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TickOnSourceProperty)
        {
            _tickOn = null;
        }
        if (change.Property == ThumbBrushProperty || change.Property == ThumbShadowProperty || change.Property == RingBrushProperty ||
            change.Property == ThumbDiameterProperty || change.Property == BubbleBackgroundProperty ||
            change.Property == BubbleForegroundProperty || change.Property == BubbleBorderBrushProperty ||
            change.Property == BubbleBorderThicknessProperty || change.Property == BubbleShadowProperty ||
            change.Property == BubbleMinHeightProperty || change.Property == BubblePaddingProperty)
        {
            ApplyChildStyles();
        }
    }

    private void ApplyChildStyles()
    {
        foreach (var thumb in _thumbs)
        {
            thumb.Fill = ThumbBrush;
            thumb.Shadow = ThumbShadow;
            thumb.RingBrush = RingBrush;
            thumb.Diameter = ThumbDiameter;
        }
        foreach (var bubble in _bubbles)
        {
            bubble.Background = BubbleBackground;
            bubble.BorderBrush = BubbleBorderBrush;
            bubble.BorderThickness = BubbleBorderThickness;
            bubble.BoxShadow = BubbleShadow;
            bubble.MinHeight = BubbleMinHeight;
            bubble.Padding = BubblePadding;
            bubble.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            bubble.SetValue(Avalonia.Controls.Documents.TextElement.ForegroundProperty, BubbleForeground);
        }
    }

    /// <summary>Left edge of the track (half a thumb in).</summary>
    public double TrackLeft => ThumbDiameter / 2;

    private Size _size;

    /// <summary>The arranged size (valid inside <see cref="ArrangeOverride"/>, before <see cref="Visual.Bounds"/> updates).</summary>
    public Size RailSize => _size;

    /// <summary>Track length in pixels (never zero).</summary>
    public double TrackWidth => Math.Max(1, _size.Width - ThumbDiameter);

    /// <summary>Track position (0..1000) under <paramref name="x"/>.</summary>
    public double ToPosition(double x) => (x - TrackLeft) / TrackWidth * SliderMath.Span;

    /// <summary>Pixel x of a track position.</summary>
    public double ToPixel(double position) => TrackLeft + position / SliderMath.Span * TrackWidth;

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var thumb in _thumbs)
        {
            thumb.Measure(Size.Infinity);
        }
        foreach (var bubble in _bubbles)
        {
            bubble.Measure(Size.Infinity);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _size = finalSize;
        var d = ThumbDiameter;
        var top = (finalSize.Height - d) / 2;
        foreach (var thumb in _thumbs)
        {
            thumb.Arrange(new Rect(0, top, d, d));
        }
        foreach (var bubble in _bubbles)
        {
            bubble.Arrange(new Rect(bubble.DesiredSize));
        }
        _owner?.OnRailArranged(this);
        return finalSize;
    }

    /// <summary>
    /// Puts bubble <paramref name="index"/> above a thumb centered at <paramref name="centerX"/>, held inside the row,
    /// then moves it by <paramref name="shift"/> (an elastic stretch carries the tag past the edge with the thumb).
    /// </summary>
    public void PlaceBubble(int index, double centerX, double shift = 0, double angle = 0)
    {
        var bubble = _bubbles[index];
        var width = bubble.Bounds.Width;
        var height = bubble.Bounds.Height;
        var left = Math.Clamp(centerX - width / 2, 0, Math.Max(0, _size.Width - width)) + shift;
        // Arc floats the bubble 10px higher on coarse pointers so a fingertip never hides it.
        var gap = BubbleGap + (IsCoarsePointer ? 10 : 0);
        var top = _size.Height / 2 - ThumbDiameter / 2 - gap - height;
        bubble.SetPlacement(left, top, angle);
    }

    /// <summary>The last press came from touch or a pen (Arc's <c>pointer: coarse</c>).</summary>
    public bool IsCoarsePointer { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsCoarsePointer = e.Pointer.Type != PointerType.Mouse;
        _owner?.OnRailPressed(this, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _owner?.OnRailMoved(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _owner?.OnRailReleased(this, e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner?.OnRailCaptureLost(this);
    }
}
