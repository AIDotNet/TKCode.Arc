using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The main picture's focusable frame (Left and Right change the picture).</summary>
internal sealed class ProductDetailStage : ContentControl
{
    protected override Type StyleKeyOverride => typeof(ProductDetailStage);
}

/// <summary>A choice button: a thumbnail, a color swatch or a size (classes thumb, swatch, selected, sold-out).</summary>
internal sealed class ProductDetailOption : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ProductDetailOption);

    /// <summary>The position of the option in its group.</summary>
    public int Index { get; init; }
}

/// <summary>One bar of the rating breakdown: a pill track filled to <see cref="Fraction"/>.</summary>
internal sealed class ProductDetailBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ProductDetailBar, double>(nameof(Fraction));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ProductDetailBar, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<ProductDetailBar, IBrush?>(nameof(FillBrush));

    static ProductDetailBar()
    {
        AffectsRender<ProductDetailBar>(FractionProperty, TrackBrushProperty, FillBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProductDetailBar>(false);
    }

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var r = size.Height / 2;
        context.DrawRectangle(TrackBrush, null, new Rect(size), r, r);
        var fill = Math.Clamp(Fraction, 0, 1) * size.Width;
        if (fill > 0.5)
        {
            context.DrawRectangle(FillBrush, null, new Rect(0, 0, Math.Max(fill, size.Height), size.Height), r, r);
        }
    }
}
