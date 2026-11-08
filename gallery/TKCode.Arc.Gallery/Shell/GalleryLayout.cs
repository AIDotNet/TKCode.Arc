using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Equal columns that follow the room: as many as fit <see cref="MinItemWidth"/>, at most <see cref="MaxColumns"/>; every
/// row is as tall as its tallest child, so cards in a row line up. A phone gets one column without anyone asking.
/// </summary>
public sealed class GalleryGrid : Panel
{
    public double MinItemWidth { get; init; } = 240;

    public int MaxColumns { get; init; } = 4;

    public double Spacing { get; init; } = 16;

    /// <summary>The columns of the last layout.</summary>
    public int ActualColumns { get; private set; } = 1;

    private readonly List<double> _rows = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MaxColumns * (MinItemWidth + Spacing) - Spacing;
        ActualColumns = Math.Clamp((int)((width + Spacing) / (MinItemWidth + Spacing)), 1, MaxColumns);
        var cell = (width - (ActualColumns - 1) * Spacing) / ActualColumns;

        _rows.Clear();
        var column = 0;
        var row = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(cell, double.PositiveInfinity));
            row = Math.Max(row, child.DesiredSize.Height);
            if (++column == ActualColumns)
            {
                _rows.Add(row);
                column = 0;
                row = 0;
            }
        }
        if (column > 0)
        {
            _rows.Add(row);
        }
        return new Size(width, _rows.Sum() + Math.Max(0, _rows.Count - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cell = (finalSize.Width - (ActualColumns - 1) * Spacing) / ActualColumns;
        var column = 0;
        var index = 0;
        var y = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var height = index < _rows.Count ? _rows[index] : child.DesiredSize.Height;
            child.Arrange(new Rect(column * (cell + Spacing), y, cell, height));
            if (++column == ActualColumns)
            {
                column = 0;
                y += height + Spacing;
                index++;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// Two children side by side, or one above the other below <see cref="StackBelow"/>. One side can have a fixed width
/// (<see cref="StartWidth"/>, <see cref="EndWidth"/>); the other takes the rest. Without either the room is split by
/// <see cref="StartShare"/>.
/// </summary>
public class GalleryTwoPane : Panel
{
    public double StackBelow { get; init; } = 760;

    public double Spacing { get; init; } = 16;

    public double? StartWidth { get; init; }

    public double? EndWidth { get; init; }

    public double StartShare { get; init; } = 0.5;

    /// <summary>Puts the second child on top when stacked.</summary>
    public bool EndFirstWhenStacked { get; init; }

    /// <summary>Whether the last layout put the children one above the other.</summary>
    public bool IsStacked { get; private set; }

    private (double Start, double End) Widths(double width)
    {
        var room = width - Spacing;
        if (StartWidth is { } start)
        {
            return (start, room - start);
        }
        if (EndWidth is { } end)
        {
            return (room - end, end);
        }
        return (room * StartShare, room * (1 - StartShare));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = Children.Where(c => c.IsVisible).ToList();
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : StackBelow;
        IsStacked = width < StackBelow || shown.Count < 2;
        if (IsStacked)
        {
            var height = 0.0;
            foreach (var child in shown)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                height += child.DesiredSize.Height;
            }
            return new Size(width, height + Math.Max(0, shown.Count - 1) * Spacing);
        }
        var (start, end) = Widths(width);
        shown[0].Measure(new Size(start, double.PositiveInfinity));
        shown[1].Measure(new Size(end, double.PositiveInfinity));
        return new Size(width, Math.Max(shown[0].DesiredSize.Height, shown[1].DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(c => c.IsVisible).ToList();
        if (IsStacked)
        {
            if (EndFirstWhenStacked)
            {
                shown.Reverse();
            }
            var y = 0.0;
            foreach (var child in shown)
            {
                child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + Spacing;
            }
            return finalSize;
        }
        var (start, end) = Widths(finalSize.Width);
        shown[0].Arrange(new Rect(0, 0, start, finalSize.Height));
        shown[1].Arrange(new Rect(start + Spacing, 0, end, finalSize.Height));
        return finalSize;
    }
}
