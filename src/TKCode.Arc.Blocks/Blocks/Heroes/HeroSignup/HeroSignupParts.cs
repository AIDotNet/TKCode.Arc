using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The overlapping faces beside the proof line of a <see cref="HeroSignup"/>: one generated portrait per name, each in a
/// ring of the page background so it cuts cleanly from its neighbour. Decorative.
/// </summary>
internal sealed class HeroSignupFaces : Panel
{
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<HeroSignupFaces, double>(nameof(Diameter), 30);

    public static readonly StyledProperty<double> OverlapProperty =
        AvaloniaProperty.Register<HeroSignupFaces, double>(nameof(Overlap), 9);

    static HeroSignupFaces()
    {
        AffectsMeasure<HeroSignupFaces>(DiameterProperty, OverlapProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<HeroSignupFaces>(false);
    }

    public HeroSignupFaces()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public double Diameter { get => GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    public double Overlap { get => GetValue(OverlapProperty); set => SetValue(OverlapProperty, value); }

    /// <summary>The names drawn, in order.</summary>
    public IReadOnlyList<string> Names { get; private set; } = [];

    /// <summary>Replaces the faces.</summary>
    public void SetNames(IReadOnlyList<string> names)
    {
        Names = names;
        Children.Clear();
        foreach (var name in names)
        {
            var ring = new Border
            {
                ClipToBounds = true,
                Child = new TeamShowcasePortrait { Seed = TeamShowcasePortrait.SeedOf(name) }.BindCornerRadius(ArcDesignTokenKind.RadiusPill)
            }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(ring, Border.BorderBrushProperty, ArcDesignTokenKind.Background);
            ArcTokens.Bind(ring, Border.BackgroundProperty, ArcDesignTokenKind.Background);
            ArcTokens.Bind(ring, Border.BorderThicknessProperty, ArcDesignTokenKind.FocusEdgeThickness);
            Children.Add(ring);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var d = Diameter;
        foreach (var child in Children)
        {
            child.Measure(new Size(d, d));
        }
        var count = Children.Count;
        return count == 0 ? default : new Size(d + (count - 1) * (d - Overlap), d);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var d = Diameter;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(i * (d - Overlap), 0, d, d));
        }
        return finalSize;
    }
}

/// <summary>
/// Lays the address field and the button of a <see cref="HeroSignup"/> side by side, or stacks them at full width when
/// <see cref="IsStacked"/> (the narrow layout). The button keeps its own width beside the field.
/// </summary>
internal sealed class HeroSignupFormPanel : Panel
{
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<HeroSignupFormPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<HeroSignupFormPanel, double>(nameof(Gap), 10);

    static HeroSignupFormPanel()
    {
        AffectsMeasure<HeroSignupFormPanel>(IsStackedProperty, GapProperty);
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var field = Children[0];
        var button = Children[1];
        if (IsStacked)
        {
            field.Measure(availableSize);
            button.Measure(availableSize);
            return new Size(Math.Max(field.DesiredSize.Width, button.DesiredSize.Width),
                field.DesiredSize.Height + Gap + button.DesiredSize.Height);
        }
        button.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var rest = Math.Max(0, availableSize.Width - button.DesiredSize.Width - Gap);
        field.Measure(new Size(rest, availableSize.Height));
        return new Size(field.DesiredSize.Width + Gap + button.DesiredSize.Width,
            Math.Max(field.DesiredSize.Height, button.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var field = Children[0];
        var button = Children[1];
        if (IsStacked)
        {
            field.Arrange(new Rect(0, 0, finalSize.Width, field.DesiredSize.Height));
            button.Arrange(new Rect(0, field.DesiredSize.Height + Gap, finalSize.Width, button.DesiredSize.Height));
            return finalSize;
        }
        var buttonWidth = button.DesiredSize.Width;
        var fieldWidth = Math.Max(0, finalSize.Width - buttonWidth - Gap);
        field.Arrange(new Rect(0, 0, fieldWidth, finalSize.Height));
        // The button lines up with the field's box, not with its message row below.
        button.Arrange(new Rect(fieldWidth + Gap, 0, buttonWidth, button.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The quiet field behind a <see cref="HeroSignup"/>: a dot grid that fades out from the top center, and a soft glow of
/// the accent above the headline. Static: drawn again only when the size or the colors change. The dots are grouped
/// into a few opacity bands, each one geometry, so a frame is a handful of draw calls.
/// </summary>
internal sealed class HeroSignupBackdrop : Control
{
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<HeroSignupBackdrop, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<IBrush?> GlowBrushProperty =
        AvaloniaProperty.Register<HeroSignupBackdrop, IBrush?>(nameof(GlowBrush));

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<HeroSignupBackdrop, double>(nameof(Spacing), 22);

    public static readonly StyledProperty<double> DotRadiusProperty =
        AvaloniaProperty.Register<HeroSignupBackdrop, double>(nameof(DotRadius), 1);

    public static readonly StyledProperty<double> GlowOpacityProperty =
        AvaloniaProperty.Register<HeroSignupBackdrop, double>(nameof(GlowOpacity), 0.16);

    private const int Bands = 6;
    private readonly StreamGeometry?[] _bands = new StreamGeometry?[Bands];
    private readonly IBrush?[] _bandBrushes = new IBrush?[Bands];
    private IBrush? _glow;
    private Size _builtFor;

    static HeroSignupBackdrop()
    {
        AffectsRender<HeroSignupBackdrop>(DotBrushProperty, GlowBrushProperty, GlowOpacityProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<HeroSignupBackdrop>(false);
    }

    public HeroSignupBackdrop()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public IBrush? GlowBrush { get => GetValue(GlowBrushProperty); set => SetValue(GlowBrushProperty, value); }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    public double DotRadius { get => GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }

    public double GlowOpacity { get => GetValue(GlowOpacityProperty); set => SetValue(GlowOpacityProperty, value); }

    /// <summary>The number of dots of the last build (tests read it).</summary>
    internal int DotCount { get; private set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DotBrushProperty || change.Property == GlowBrushProperty || change.Property == GlowOpacityProperty)
        {
            Array.Clear(_bandBrushes);
            _glow = null;
        }
        else if (change.Property == SpacingProperty || change.Property == DotRadiusProperty)
        {
            _builtFor = default;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        if (_builtFor != size)
        {
            Build(size);
        }
        if (_glow is null && GlowBrush is ISolidColorBrush accent)
        {
            var color = accent.Color;
            _glow = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Oklch.WithAlpha(color, GlowOpacity), 0),
                    new GradientStop(Oklch.WithAlpha(color, 0), 1)
                }
            }.ToImmutable();
        }
        if (_glow is not null)
        {
            context.FillRectangle(_glow, new Rect(size));
        }
        if (DotBrush is not ISolidColorBrush dots)
        {
            return;
        }
        for (var i = 0; i < Bands; i++)
        {
            if (_bands[i] is not { } geometry)
            {
                continue;
            }
            _bandBrushes[i] ??= new ImmutableSolidColorBrush(Oklch.WithAlpha(dots.Color, dots.Color.A / 255.0 * (Bands - i) / Bands));
            context.DrawGeometry(_bandBrushes[i], null, geometry);
        }
    }

    private void Build(Size size)
    {
        _builtFor = size;
        var spacing = Math.Max(6, Spacing);
        var r = Math.Max(0.25, DotRadius);
        var cx = size.Width / 2;
        var reachX = Math.Max(1, size.Width * 0.55);
        var reachY = Math.Max(1, size.Height * 0.95);
        var contexts = new StreamGeometryContext?[Bands];
        var count = 0;
        for (var i = 0; i < Bands; i++)
        {
            _bands[i] = new StreamGeometry();
        }
        try
        {
            for (var y = spacing / 2; y < size.Height; y += spacing)
            {
                for (var x = cx - Math.Floor(cx / spacing) * spacing; x < size.Width; x += spacing)
                {
                    var dx = (x - cx) / reachX;
                    var dy = y / reachY;
                    var d = Math.Sqrt(dx * dx + dy * dy);
                    if (d >= 1)
                    {
                        continue;
                    }
                    var band = Math.Min(Bands - 1, (int)(d * Bands));
                    var g = contexts[band] ??= _bands[band]!.Open();
                    g.BeginFigure(new Point(x - r, y), true);
                    g.ArcTo(new Point(x + r, y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    g.ArcTo(new Point(x - r, y), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    g.EndFigure(true);
                    count++;
                }
            }
        }
        finally
        {
            for (var i = 0; i < Bands; i++)
            {
                if (contexts[i] is { } open)
                {
                    open.Dispose();
                }
                else
                {
                    _bands[i] = null;
                }
            }
        }
        DotCount = count;
    }
}
