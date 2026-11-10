using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One feature card of a <see cref="SpotlightGrid"/>: the icon tile, the title, the description and an optional badge.
/// The card itself is transparent: the surface under it paints its face, its border and the spotlight.
/// </summary>
internal sealed class SpotlightGridCard : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SpotlightGridCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SpotlightGridCard, string?>(nameof(Description));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SpotlightGridCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SpotlightGridCard, string?>(nameof(Badge));

    static SpotlightGridCard()
    {
        FocusableProperty.OverrideDefaultValue<SpotlightGridCard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<SpotlightGridCard>(false);
    }

    public SpotlightGridCard(SpotlightGridItem item)
    {
        Item = item;
        Title = item.Title;
        Description = item.Description;
        Icon = item.Icon;
        Badge = item.Badge;
        Avalonia.Automation.AutomationProperties.SetName(this, item.Title);
        Avalonia.Automation.AutomationProperties.SetHelpText(this, item.Description);
    }

    public SpotlightGridItem Item { get; }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    /// <summary>Raised on a click, a tap, Enter or Space.</summary>
    public event EventHandler? Invoked;

    private LazyFocusRing? _ring;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring?.Attach(null, null);
        if (e.NameScope.Find<Panel>("PART_RingHost") is { } host)
        {
            _ring = new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep24));
            _ring.Attach(ring =>
            {
                ring.Margin = new Thickness(-4);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring?.Update();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Invoked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            Invoked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

/// <summary>
/// The grid of a <see cref="SpotlightGrid"/>: it lays out the cards in equal columns and paints, under them, each card's
/// face and border and the spotlight, a soft accent light that follows the pointer (or the keyboard focus) across the
/// whole grid. Where the light passes, card faces warm a little and card borders glow, including the borders of
/// neighbouring cards, so the light reads as one source moving over the grid.
/// </summary>
/// <remarks>
/// The light is two radial gradients centred on the origin, cached per color; each frame the surface only moves the
/// drawing origin to the light's position and draws every card shifted the other way, so following the pointer allocates
/// nothing. The position follows on a quick spring and the light fades in and out over 160 ms (both jump at reduced
/// motion and off, except the fade, which reduced motion keeps).
/// </remarks>
internal sealed class SpotlightGridSurface : Control
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, double>(nameof(MinColumnWidth), 260);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, int>(nameof(MaxColumns), 3);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, double>(nameof(Gap), 16);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<SpotlightGridSurface>();

    public static readonly StyledProperty<double> SpotlightRadiusProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, double>(nameof(SpotlightRadius), 240);

    public static readonly StyledProperty<double> BorderWidthProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, double>(nameof(BorderWidth), 1);

    public static readonly StyledProperty<IBrush?> FaceBrushProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, IBrush?>(nameof(FaceBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> LightBrushProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, IBrush?>(nameof(LightBrush));

    public static readonly StyledProperty<bool> IsLightEnabledProperty =
        AvaloniaProperty.Register<SpotlightGridSurface, bool>(nameof(IsLightEnabled), true);

    private readonly List<SpotlightGridCard> _cards = new();
    private MotionValue<Point>? _position;
    private MotionValue<double>? _intensity;
    private IBrush? _glow;
    private IPen? _edgeGlow;
    private IPen? _edge;
    private bool _pointerInside;

    static SpotlightGridSurface()
    {
        AffectsMeasure<SpotlightGridSurface>(MinColumnWidthProperty, MaxColumnsProperty, GapProperty);
        AffectsRender<SpotlightGridSurface>(CornerRadiusProperty, FaceBrushProperty);
    }

    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public bool IsLightEnabled { get => GetValue(IsLightEnabledProperty); set => SetValue(IsLightEnabledProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public double SpotlightRadius { get => GetValue(SpotlightRadiusProperty); set => SetValue(SpotlightRadiusProperty, value); }

    public double BorderWidth { get => GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }

    public IBrush? FaceBrush { get => GetValue(FaceBrushProperty); set => SetValue(FaceBrushProperty, value); }

    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public IBrush? LightBrush { get => GetValue(LightBrushProperty); set => SetValue(LightBrushProperty, value); }

    /// <summary>The cards, in order.</summary>
    public IReadOnlyList<SpotlightGridCard> Cards => _cards;

    /// <summary>The columns of the last layout.</summary>
    public int Columns { get; private set; } = 1;

    /// <summary>Where the light is (its target), in this control's coordinates.</summary>
    public Point SpotlightTarget => _position?.Target ?? default;

    /// <summary>How strong the light is now, 0 to 1.</summary>
    public double Intensity => _intensity?.Value ?? 0;

    /// <summary>Replaces the cards.</summary>
    public void SetCards(IEnumerable<SpotlightGridCard> cards)
    {
        foreach (var card in _cards)
        {
            VisualChildren.Remove(card);
            LogicalChildren.Remove(card);
        }
        _cards.Clear();
        _cards.AddRange(cards);
        foreach (var card in _cards)
        {
            LogicalChildren.Add(card);
            VisualChildren.Add(card);
        }
        InvalidateMeasure();
    }

    /// <summary>Moves the light to <paramref name="point"/> and brings it up (the pointer or a focused card).</summary>
    public void LightAt(Point point, bool jump = false)
    {
        if (!IsLightEnabled)
        {
            return;
        }
        _position ??= CreatePosition(point);
        _intensity ??= CreateIntensity();
        if (jump || _intensity.Value <= 0.01)
        {
            // A light that was off appears where it is needed instead of sliding in from its last place.
            _position.Jump(point);
        }
        else
        {
            _ = _position.AnimateTo(point, MotionSpec.Spring(SpringSpec.FromVisual(0.22, 0)));
        }
        if (_intensity.Target < 1)
        {
            _ = _intensity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    /// <summary>Fades the light out.</summary>
    public void Dim()
    {
        if (_intensity is { } intensity && intensity.Target > 0)
        {
            _ = intensity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
        }
    }

    internal static int ColumnsFor(double width, double min, int max, double gap)
    {
        if (double.IsInfinity(width) || width <= 0)
        {
            return Math.Max(1, max);
        }
        return Math.Clamp((int)Math.Floor((width + gap) / (Math.Max(1, min) + gap)), 1, Math.Max(1, max));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLightEnabledProperty && !IsLightEnabled)
        {
            _intensity?.Jump(0);
            InvalidateVisual();
        }
        if (change.Property == LightBrushProperty || change.Property == SpotlightRadiusProperty || change.Property == EdgeBrushProperty
            || change.Property == BorderWidthProperty)
        {
            _glow = null;
            _edgeGlow = null;
            _edge = null;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * MaxColumns + Gap * (MaxColumns - 1) : availableSize.Width;
        var columns = ColumnsFor(width, MinColumnWidth, MaxColumns, Gap);
        Columns = columns;
        var cell = Math.Max(0, (width - Gap * (columns - 1)) / columns);
        var height = 0.0;
        for (var start = 0; start < _cards.Count; start += columns)
        {
            var row = 0.0;
            for (var i = start; i < Math.Min(_cards.Count, start + columns); i++)
            {
                _cards[i].Measure(new Size(cell, double.PositiveInfinity));
                row = Math.Max(row, _cards[i].DesiredSize.Height);
            }
            height += row + (start > 0 ? Gap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width, MinColumnWidth, MaxColumns, Gap);
        Columns = columns;
        var cell = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var y = 0.0;
        for (var start = 0; start < _cards.Count; start += columns)
        {
            var row = 0.0;
            for (var i = start; i < Math.Min(_cards.Count, start + columns); i++)
            {
                row = Math.Max(row, _cards[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(_cards.Count, start + columns); i++)
            {
                _cards[i].Arrange(new Rect((i - start) * (cell + Gap), y, cell, row));
            }
            y += row + Gap;
        }
        return finalSize;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerInside = true;
        LightAt(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        if (!IsKeyboardFocusWithin)
        {
            Dim();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _pointerInside = false;
            Dim();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source is SpotlightGridCard card && card.Classes.Contains(":focus-visible") && !_pointerInside)
        {
            LightAt(card.Bounds.Center);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_pointerInside && !IsKeyboardFocusWithin)
        {
            Dim();
        }
    }

    public override void Render(DrawingContext context)
    {
        EnsureBrushes();
        var radius = CornerRadius;
        foreach (var card in _cards)
        {
            var rect = card.Bounds;
            var shape = new RoundedRect(rect.Deflate(BorderWidth / 2), radius);
            context.DrawRectangle(FaceBrush, _edge, shape);
        }
        var intensity = Intensity;
        if (intensity <= 0.001 || _glow is null || _edgeGlow is null || _position is null)
        {
            return;
        }
        var at = _position.Value;
        using (context.PushOpacity(intensity))
        using (context.PushTransform(Matrix.CreateTranslation(at.X, at.Y)))
        {
            foreach (var card in _cards)
            {
                var rect = card.Bounds.Translate(new Vector(-at.X, -at.Y));
                var shape = new RoundedRect(rect.Deflate(BorderWidth / 2), radius);
                context.DrawRectangle(_glow, _edgeGlow, shape);
            }
        }
    }

    private void EnsureBrushes()
    {
        if (_edge is null && EdgeBrush is { } edge)
        {
            _edge = new ImmutablePen(edge.ToImmutable(), BorderWidth);
        }
        if (_glow is not null || LightBrush is not ISolidColorBrush light)
        {
            return;
        }
        var r = Math.Max(1, SpotlightRadius);
        var color = light.Color;
        _glow = Radial(color, 0.13, r);
        _edgeGlow = new ImmutablePen(Radial(color, 0.95, r * 0.8), Math.Max(1, BorderWidth * 1.5));
    }

    private static IImmutableBrush Radial(Color color, double alpha, double radius) => new RadialGradientBrush
    {
        Center = new RelativePoint(0, 0, RelativeUnit.Absolute),
        GradientOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
        RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
        RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
        GradientStops =
        {
            new GradientStop(Oklch.WithAlpha(color, alpha), 0),
            new GradientStop(Oklch.WithAlpha(color, alpha * 0.35), 0.55),
            new GradientStop(Oklch.WithAlpha(color, 0), 1)
        }
    }.ToImmutable();

    private MotionValue<Point> CreatePosition(Point initial)
    {
        var value = new MotionValue<Point>(this, initial, MotionVectors.Point);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private MotionValue<double> CreateIntensity()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }
}
