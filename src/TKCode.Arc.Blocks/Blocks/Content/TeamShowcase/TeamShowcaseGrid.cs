using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The grid of a <see cref="TeamShowcase"/>: equal columns (as many as fit at <see cref="MinColumnWidth"/>, at most
/// <see cref="MaxColumns"/>), rows as tall as their tallest card. Visible cards fill the slots in order; a card that is
/// fading out keeps its old slot outside the flow. After <see cref="AnimateNextArrange"/> the next arrange slides every
/// card that changed slot from the old one (FLIP: the layout jumps, the render transform carries the card back and
/// springs to zero).
/// </summary>
internal sealed class TeamShowcaseGrid : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<TeamShowcaseGrid, double>(nameof(MinColumnWidth), 200);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<TeamShowcaseGrid, int>(nameof(MaxColumns), 4);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<TeamShowcaseGrid, double>(nameof(ColumnGap), 20);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<TeamShowcaseGrid, double>(nameof(RowGap), 32);

    private bool _animate;

    static TeamShowcaseGrid()
    {
        AffectsMeasure<TeamShowcaseGrid>(MinColumnWidthProperty, MaxColumnsProperty, ColumnGapProperty, RowGapProperty);
    }

    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The number of columns of the last measure.</summary>
    public int Columns { get; private set; } = 1;

    /// <summary>The next arrange animates the cards that moved.</summary>
    public void AnimateNextArrange() => _animate = true;

    /// <summary>The cards that take part in the layout, in order.</summary>
    public IEnumerable<TeamShowcaseCard> Placed()
    {
        foreach (var child in Children)
        {
            if (child is TeamShowcaseCard { IsVisible: true, IsLeaving: false } card)
            {
                yield return card;
            }
        }
    }

    internal static int ColumnsFor(double width, double min, int max, double gap)
    {
        if (double.IsInfinity(width) || width <= 0)
        {
            return Math.Max(1, max);
        }
        var fit = (int)Math.Floor((width + gap) / (Math.Max(1, min) + gap));
        return Math.Clamp(fit, 1, Math.Max(1, max));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * MaxColumns + ColumnGap * (MaxColumns - 1) : availableSize.Width;
        Columns = ColumnsFor(width, MinColumnWidth, MaxColumns, ColumnGap);
        var cell = CellWidth(width);
        var height = 0.0;
        var rowHeight = 0.0;
        var index = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cell, double.PositiveInfinity));
            if (child is not TeamShowcaseCard { IsVisible: true, IsLeaving: false })
            {
                continue;
            }
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            index++;
            if (index % Columns == 0)
            {
                height += rowHeight + RowGap;
                rowHeight = 0;
            }
        }
        if (index % Columns != 0)
        {
            height += rowHeight;
        }
        else if (index > 0)
        {
            height -= RowGap;
        }
        return new Size(width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width, MinColumnWidth, MaxColumns, ColumnGap);
        Columns = columns;
        var cell = CellWidth(finalSize.Width, columns);
        var placed = Placed().ToList();
        var y = 0.0;
        for (var row = 0; row * columns < placed.Count; row++)
        {
            var rowHeight = 0.0;
            for (var c = 0; c < columns && row * columns + c < placed.Count; c++)
            {
                rowHeight = Math.Max(rowHeight, placed[row * columns + c].DesiredSize.Height);
            }
            for (var c = 0; c < columns && row * columns + c < placed.Count; c++)
            {
                var card = placed[row * columns + c];
                var rect = new Rect(c * (cell + ColumnGap), y, cell, rowHeight);
                if (_animate && card.Slot is { } old && old.Size.Width > 0)
                {
                    card.ShiftFrom(old.Position - rect.Position);
                }
                else if (!_animate && card.Slot is { } previous && previous.Position != rect.Position)
                {
                    // A resize re-flows the grid: cards jump to their new slots instead of sliding.
                    card.Settle();
                }
                card.Slot = rect;
                card.Arrange(rect);
            }
            y += rowHeight + RowGap;
        }
        foreach (var child in Children)
        {
            if (child is TeamShowcaseCard { IsLeaving: true, Slot: { } slot } leaving)
            {
                leaving.Arrange(slot);
            }
            else if (child is not TeamShowcaseCard)
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        _animate = false;
        return finalSize;
    }

    private double CellWidth(double width) => CellWidth(width, Columns);

    private double CellWidth(double width, int columns) => Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
}
