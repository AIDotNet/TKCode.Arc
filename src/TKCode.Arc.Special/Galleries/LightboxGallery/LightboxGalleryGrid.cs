using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The tile grid of a <see cref="LightboxGallery"/>: equal tiles in rows, as many columns as fit at the minimum tile
/// width (or a fixed number), each tile as tall as its width divided by the tile proportions.
/// </summary>
internal sealed class LightboxGalleryGrid : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<LightboxGalleryGrid, int>(nameof(Columns));

    public static readonly StyledProperty<double> MinTileWidthProperty =
        AvaloniaProperty.Register<LightboxGalleryGrid, double>(nameof(MinTileWidth), 160);

    public static readonly StyledProperty<double> TileAspectRatioProperty =
        AvaloniaProperty.Register<LightboxGalleryGrid, double>(nameof(TileAspectRatio), 1);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<LightboxGalleryGrid, double>(nameof(Spacing), 8);

    static LightboxGalleryGrid()
    {
        AffectsMeasure<LightboxGalleryGrid>(ColumnsProperty, MinTileWidthProperty, TileAspectRatioProperty, SpacingProperty);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double MinTileWidth
    {
        get => GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public double TileAspectRatio
    {
        get => GetValue(TileAspectRatioProperty);
        set => SetValue(TileAspectRatioProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>The number of columns of the last layout.</summary>
    internal int ColumnCount { get; private set; } = 1;

    /// <summary>The columns that fit in <paramref name="width"/>.</summary>
    internal static int ColumnsFor(double width, int columns, double minTile, double spacing)
    {
        if (columns > 0)
        {
            return columns;
        }
        if (!double.IsFinite(width) || width <= 0)
        {
            return 3;
        }
        return Math.Max(1, (int)Math.Floor((width + spacing) / (Math.Max(1, minTile) + spacing)));
    }

    private (double Width, double Height, int Columns) Tile(double width)
    {
        var spacing = Math.Max(0, Spacing);
        var columns = ColumnsFor(width, Columns, MinTileWidth, spacing);
        var tileWidth = double.IsFinite(width) ? Math.Max(0, (width - (columns - 1) * spacing) / columns) : Math.Max(1, MinTileWidth);
        var ratio = TileAspectRatio > 0 && double.IsFinite(TileAspectRatio) ? TileAspectRatio : 1;
        return (tileWidth, tileWidth / ratio, columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, height, columns) = Tile(availableSize.Width);
        ColumnCount = columns;
        var size = new Size(width, height);
        foreach (var child in Children)
        {
            child.Measure(size);
        }
        var rows = (Children.Count + columns - 1) / columns;
        var spacing = Math.Max(0, Spacing);
        var total = rows == 0 ? 0 : rows * height + (rows - 1) * spacing;
        var fullWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : columns * width + (columns - 1) * spacing;
        return new Size(fullWidth, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (width, height, columns) = Tile(finalSize.Width);
        ColumnCount = columns;
        var spacing = Math.Max(0, Spacing);
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            Children[i].Arrange(new Rect(column * (width + spacing), row * (height + spacing), width, height));
        }
        return finalSize;
    }
}
