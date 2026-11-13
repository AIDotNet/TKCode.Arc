using Avalonia;
using Avalonia.Controls;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Lays the cards out in equal columns: as many of at least <see cref="MinItemWidth"/> as fit, at most <see cref="MaxColumns"/>.</summary>
internal sealed class ProductListingGrid : Panel
{
    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<ProductListingGrid, double>(nameof(MinItemWidth), 200);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ProductListingGrid, double>(nameof(Gap), 20);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<ProductListingGrid, int>(nameof(MaxColumns), 4);

    static ProductListingGrid()
    {
        AffectsMeasure<ProductListingGrid>(MinItemWidthProperty, GapProperty, MaxColumnsProperty);
    }

    public double MinItemWidth { get => GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    /// <summary>The column count of the last measure.</summary>
    public int ColumnCount { get; private set; } = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinItemWidth * MaxColumns + Gap * (MaxColumns - 1) : availableSize.Width;
        ColumnCount = ProductListingMath.Columns(width, MinItemWidth, Gap, MaxColumns);
        var itemWidth = (width - Gap * (ColumnCount - 1)) / ColumnCount;
        double height = 0, row = 0;
        var visible = Children.Where(c => c.IsVisible).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(itemWidth, double.PositiveInfinity));
            row = Math.Max(row, visible[i].DesiredSize.Height);
            if ((i + 1) % ColumnCount == 0 || i == visible.Count - 1)
            {
                height += row + (i == visible.Count - 1 ? 0 : Gap);
                row = 0;
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemWidth = (finalSize.Width - Gap * (ColumnCount - 1)) / ColumnCount;
        var visible = Children.Where(c => c.IsVisible).ToList();
        double y = 0;
        for (var start = 0; start < visible.Count; start += ColumnCount)
        {
            var count = Math.Min(ColumnCount, visible.Count - start);
            double row = 0;
            for (var i = 0; i < count; i++)
            {
                row = Math.Max(row, visible[start + i].DesiredSize.Height);
            }
            for (var i = 0; i < count; i++)
            {
                visible[start + i].Arrange(new Rect(i * (itemWidth + Gap), y, itemWidth, row));
            }
            y += row + Gap;
        }
        return finalSize;
    }
}

/// <summary>Gives its child a fixed aspect: as tall as <see cref="Ratio"/> times its width (1.25 for 4:5 pictures).</summary>
internal sealed class ProductListingAspect : Decorator
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<ProductListingAspect, double>(nameof(Ratio), 1.25);

    static ProductListingAspect()
    {
        AffectsMeasure<ProductListingAspect>(RatioProperty);
    }

    public double Ratio { get => GetValue(RatioProperty); set => SetValue(RatioProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        var size = new Size(width, width * Ratio);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = new Size(finalSize.Width, finalSize.Width * Ratio);
        Child?.Arrange(new Rect(size));
        return size;
    }
}

/// <summary>The clickable part of a card (picture, name, price) that opens the product.</summary>
internal sealed class ProductListingCardButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ProductListingCardButton);

    public ListingProduct? Product { get; set; }
}

/// <summary>A small round color dot under a card's price.</summary>
internal sealed class ProductListingSwatch : Border
{
    protected override Type StyleKeyOverride => typeof(ProductListingSwatch);
}

/// <summary>The rounded, clipped frame of a card's picture.</summary>
internal sealed class ProductListingPicture : Border
{
    protected override Type StyleKeyOverride => typeof(ProductListingPicture);
}

/// <summary>A product card (the open button and the quick add button); it fades in when it joins the grid.</summary>
internal sealed class ProductListingCard : StackPanel
{
    protected override Type StyleKeyOverride => typeof(ProductListingCard);
}
