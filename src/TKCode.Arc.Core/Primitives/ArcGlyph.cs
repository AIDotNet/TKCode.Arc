using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// Draws a stroked Lucide glyph (24x24 viewBox). <see cref="StrokeThickness"/> is in viewBox units, so the
/// rendered stroke scales with <see cref="Size"/> exactly like lucide-react without absoluteStrokeWidth. The stroke takes
/// <see cref="Foreground"/>, which is inherited (<c>TextElement.Foreground</c>); where nothing above the glyph sets one, it
/// takes the theme's text color (the <c>Foreground</c> token) so it never draws invisibly.
/// </summary>
public class ArcGlyph : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<ArcGlyph, Geometry?>(nameof(Data));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcGlyph, double>(nameof(Size), 16);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ArcGlyph, double>(nameof(StrokeThickness), 1.75);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<ArcGlyph>();

    // The theme's Foreground token, bound only while Foreground resolves to null (#192), so it follows theme changes.
    private static readonly StyledProperty<IBrush?> FallbackForegroundProperty =
        AvaloniaProperty.Register<ArcGlyph, IBrush?>("FallbackForeground");

    static ArcGlyph()
    {
        AffectsMeasure<ArcGlyph>(SizeProperty);
        AffectsRender<ArcGlyph>(DataProperty, StrokeThicknessProperty, ForegroundProperty, FallbackForegroundProperty);
        // Null rather than TextElement's black when nothing above the glyph sets a Foreground (#192): the glyph then takes
        // the theme's text color, which black was not on a dark theme. An inherited or explicit Foreground still wins.
        ForegroundProperty.OverrideDefaultValue<ArcGlyph>(null);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArcGlyph>(false);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// The stroke brush. Inherited like <c>TextElement.Foreground</c>, but null (not black) when nothing above the glyph sets
    /// one; while it is null the glyph draws with the theme's text color (the <c>Foreground</c> token) instead.
    /// </summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private IDisposable? _fallbackBinding;
    private Pen? _pen;
    private IBrush? _penBrush;
    private double _penThickness;

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFallback();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _fallbackBinding?.Dispose();
        _fallbackBinding = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            UpdateFallback();
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty && Foreground is null)
        {
            // The last-resort brush (no Arc theme in reach) follows the light or dark variant.
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Binds the theme's text color while the glyph is in a tree and has no Foreground of its own or from above; most glyphs
    /// inherit one and never bind.
    /// </summary>
    private void UpdateFallback()
    {
        var wanted = Foreground is null && VisualRoot is not null;
        if (wanted && _fallbackBinding is null)
        {
            _fallbackBinding = Bind(FallbackForegroundProperty,
                this.GetResourceObservable(ArcDesignTokenKind.Foreground, static value => value as IBrush));
        }
        else if (!wanted && _fallbackBinding is not null)
        {
            _fallbackBinding.Dispose();
            _fallbackBinding = null;
            ClearValue(FallbackForegroundProperty);
        }
    }

    /// <summary>
    /// The brush the stroke takes: <see cref="Foreground"/>, else the theme's Foreground token, else black (white on a dark
    /// theme variant) when no Arc theme is in reach.
    /// </summary>
    internal IBrush EffectiveForeground =>
        Foreground
        ?? GetValue(FallbackForegroundProperty)
        ?? (ActualThemeVariant == ThemeVariant.Dark ? Brushes.White : Brushes.Black);

    public override void Render(DrawingContext context)
    {
        var data = Data;
        if (data is null)
        {
            return;
        }
        var brush = EffectiveForeground;
        if (_pen is null || !ReferenceEquals(_penBrush, brush) || Math.Abs(_penThickness - StrokeThickness) > double.Epsilon)
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
            _penThickness = StrokeThickness;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(null, _pen, data);
        }
    }
}
