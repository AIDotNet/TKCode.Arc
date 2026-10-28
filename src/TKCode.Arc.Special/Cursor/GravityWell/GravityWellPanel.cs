using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The items panel of <see cref="GravityWell"/>: square tiles in rows, with a fixed column count or as many columns of
/// at least the minimum tile size as fit the width (CSS <c>repeat(auto-fill, minmax(min, 1fr))</c>). It reads its
/// settings from the well that hosts it and tells the well where the tiles landed after every arrange.
/// </summary>
internal sealed class GravityWellPanel : Panel
{
    private GravityWell? _owner;

    /// <summary>The number of columns of the last arrange.</summary>
    public int ColumnCount { get; private set; } = 1;

    /// <summary>The side of a tile in the last arrange.</summary>
    public double TileSize { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = this.FindAncestorOfType<GravityWell>();
        _owner?.AttachPanel(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner?.DetachPanel(this);
        _owner = null;
    }

    /// <summary>The number of columns for <paramref name="width"/>.</summary>
    internal static int ColumnsFor(double width, int columns, double minTileSize, double gap)
    {
        if (columns > 0)
        {
            return columns;
        }
        var minimum = Math.Max(1, minTileSize);
        if (!double.IsFinite(width))
        {
            return 1;
        }
        return Math.Max(1, (int)Math.Floor((width + gap) / (minimum + gap)));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var gap = Math.Max(0, _owner?.Gap ?? 8);
        var minimum = Math.Max(1, _owner?.MinTileSize ?? 56);
        var columns = ColumnsFor(availableSize.Width, _owner?.Columns ?? 0, minimum, gap);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : columns * minimum + (columns - 1) * gap;
        var tile = Math.Max(0, (width - (columns - 1) * gap) / columns);
        var count = Children.Count;
        var rows = (count + columns - 1) / columns;
        foreach (var child in Children)
        {
            child.Measure(new Size(tile, tile));
        }
        return new Size(width, rows == 0 ? 0 : rows * tile + (rows - 1) * gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var gap = Math.Max(0, _owner?.Gap ?? 8);
        var minimum = Math.Max(1, _owner?.MinTileSize ?? 56);
        var columns = ColumnsFor(finalSize.Width, _owner?.Columns ?? 0, minimum, gap);
        var tile = Math.Max(0, (finalSize.Width - (columns - 1) * gap) / columns);
        ColumnCount = columns;
        TileSize = tile;
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            Children[i].Arrange(new Rect(column * (tile + gap), row * (tile + gap), tile, tile));
        }
        _owner?.OnTilesArranged(this);
        return finalSize;
    }
}
