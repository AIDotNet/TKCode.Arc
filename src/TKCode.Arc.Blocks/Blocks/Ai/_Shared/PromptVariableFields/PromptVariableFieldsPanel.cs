using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The grid of variable fields: <see cref="Columns"/> equal columns <see cref="ColumnSpacing"/> apart, rows
/// <see cref="RowSpacing"/> apart. Fields are placed in order, left to right; a wide field
/// (<see cref="PromptVariableField.IsWide"/>) takes two columns and starts a new row when the current one has fewer than
/// two left (CSS grid auto-placement without backfilling). With one column every field takes the whole row.
/// </summary>
internal sealed class PromptVariableFieldsPanel : Panel
{
    /// <summary>The number of columns (4, 2 or 1).</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<PromptVariableFieldsPanel, int>(nameof(Columns), 4);

    /// <summary>The gap between two columns (10).</summary>
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<PromptVariableFieldsPanel, double>(nameof(ColumnSpacing), 10);

    /// <summary>The gap between two rows (12).</summary>
    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<PromptVariableFieldsPanel, double>(nameof(RowSpacing), 12);

    private readonly List<(int Row, int Column, int Span)> _cells = new();
    private readonly List<double> _rowHeights = new();
    private readonly List<int> _spans = new();

    static PromptVariableFieldsPanel()
    {
        AffectsMeasure<PromptVariableFieldsPanel>(ColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="ColumnSpacingProperty"/>
    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    /// <inheritdoc cref="RowSpacingProperty"/>
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>Where each visible child was placed in the last measure: row, first column, columns taken (tests).</summary>
    internal IReadOnlyList<(int Row, int Column, int Span)> Cells => _cells;

    /// <summary>
    /// Places <paramref name="spans"/> (one per field, in order) on a grid of <paramref name="columns"/> columns: a field
    /// that does not fit in what is left of the row starts the next row.
    /// </summary>
    internal static void Place(IReadOnlyList<int> spans, int columns, List<(int Row, int Column, int Span)> cells)
    {
        cells.Clear();
        columns = Math.Max(1, columns);
        var row = 0;
        var column = 0;
        foreach (var wanted in spans)
        {
            var span = Math.Clamp(wanted, 1, columns);
            if (column + span > columns)
            {
                row++;
                column = 0;
            }
            cells.Add((row, column, span));
            column += span;
            if (column >= columns)
            {
                row++;
                column = 0;
            }
        }
    }

    private double ColumnWidth(double width, int columns) =>
        Math.Max(0, (width - ColumnSpacing * (columns - 1)) / columns);

    private double CellWidth(double columnWidth, int span) => columnWidth * span + ColumnSpacing * (span - 1);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        _spans.Clear();
        foreach (var child in Children)
        {
            if (child.IsVisible)
            {
                _spans.Add(child is PromptVariableField { IsWide: true } ? 2 : 1);
            }
        }
        Place(_spans, columns, _cells);

        var finite = !double.IsInfinity(availableSize.Width);
        var columnWidth = finite ? ColumnWidth(availableSize.Width, columns) : double.PositiveInfinity;
        _rowHeights.Clear();
        var widest = 0.0;
        var index = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var (row, _, span) = _cells[index++];
            child.Measure(new Size(finite ? CellWidth(columnWidth, span) : double.PositiveInfinity, double.PositiveInfinity));
            while (_rowHeights.Count <= row)
            {
                _rowHeights.Add(0);
            }
            _rowHeights[row] = Math.Max(_rowHeights[row], child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width / span);
        }
        var height = 0.0;
        foreach (var rowHeight in _rowHeights)
        {
            height += rowHeight;
        }
        if (_rowHeights.Count > 1)
        {
            height += RowSpacing * (_rowHeights.Count - 1);
        }
        var width = finite ? availableSize.Width : widest * columns + ColumnSpacing * (columns - 1);
        return new Size(index == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var columnWidth = ColumnWidth(finalSize.Width, columns);
        var index = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible || index >= _cells.Count)
            {
                continue;
            }
            var (row, column, span) = _cells[index++];
            var y = 0.0;
            for (var r = 0; r < row && r < _rowHeights.Count; r++)
            {
                y += _rowHeights[r] + RowSpacing;
            }
            var x = column * (columnWidth + ColumnSpacing);
            var rowHeight = row < _rowHeights.Count ? _rowHeights[row] : child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, CellWidth(columnWidth, span), rowHeight));
        }
        return finalSize;
    }
}
