using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The line under a table row.</summary>
internal enum ImportTableLine
{
    /// <summary>No line (the last row).</summary>
    None,

    /// <summary>The header's line (Border).</summary>
    Header,

    /// <summary>A row's line (BorderSubtle).</summary>
    Row
}

/// <summary>
/// A light table for the file preview: rows are <see cref="ImportWizardTableRow"/> panels (so a row can fade and move as
/// one), columns are as wide as their widest cell, and when the table is narrower than <see cref="FillWidth"/> the extra
/// width is shared in proportion to the columns (CSS <c>min-width: 100%</c> on an auto table). Put it in a horizontal
/// scroll viewer and keep <see cref="FillWidth"/> at the viewport's width.
/// </summary>
internal sealed class ImportWizardTable : Panel
{
    private double[] _widths = [];
    private double _fillWidth;

    /// <summary>The width the columns stretch to when they need less.</summary>
    public double FillWidth
    {
        get => _fillWidth;
        set
        {
            if (Math.Abs(_fillWidth - value) < 0.5)
            {
                return;
            }
            _fillWidth = value;
            InvalidateMeasure();
        }
    }

    /// <summary>The column widths of the last measure (tests).</summary>
    public IReadOnlyList<double> ColumnWidths => _widths;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = 0;
        foreach (var child in Children)
        {
            if (child is ImportWizardTableRow row)
            {
                columns = Math.Max(columns, row.Cells.Count);
            }
        }
        var widths = new double[columns];
        foreach (var child in Children)
        {
            if (child is not ImportWizardTableRow row)
            {
                continue;
            }
            for (var c = 0; c < row.Cells.Count; c++)
            {
                var cell = row.Cells[c];
                cell.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widths[c] = Math.Max(widths[c], Math.Min(cell.DesiredSize.Width, row.MaxCellWidth(c)));
            }
        }
        var total = widths.Sum();
        if (_fillWidth > total && total > 0)
        {
            var extra = _fillWidth - total;
            for (var c = 0; c < widths.Length; c++)
            {
                widths[c] += extra * widths[c] / total;
            }
            total = _fillWidth;
        }
        _widths = widths;
        var height = 0.0;
        foreach (var child in Children)
        {
            if (child is ImportWizardTableRow row)
            {
                row.Widths = widths;
            }
            child.Measure(new Size(total, double.PositiveInfinity));
            height += child.DesiredSize.Height;
        }
        return new Size(total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var child in Children)
        {
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += child.DesiredSize.Height;
        }
        return finalSize;
    }
}

/// <summary>
/// One row of an <see cref="ImportWizardTable"/>: its cells side by side at the table's column widths and a 1px line under
/// it. A <see cref="Control"/> that owns its cells (a Panel cannot draw the line: its Render is sealed).
/// </summary>
internal sealed class ImportWizardTableRow : Control
{
    private readonly List<Control> _cells = new();
    private readonly Dictionary<int, double> _maxWidths = new();
    private ImportTableLine _line;

    public ImportWizardTableRow(ImportTableLine line)
    {
        _line = line;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The cells, left to right.</summary>
    public IReadOnlyList<Control> Cells => _cells;

    /// <summary>Adds a cell at the right.</summary>
    public void Add(Control cell)
    {
        _cells.Add(cell);
        LogicalChildren.Add(cell);
        VisualChildren.Add(cell);
        InvalidateMeasure();
    }

    /// <summary>The line under the row.</summary>
    public ImportTableLine Line
    {
        get => _line;
        set
        {
            if (_line == value)
            {
                return;
            }
            _line = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The column widths the table chose.</summary>
    internal double[] Widths { get; set; } = [];

    /// <summary>Caps how wide a column's cell may ask to be (the review's 220).</summary>
    public void SetMaxCellWidth(int column, double width) => _maxWidths[column] = width;

    internal double MaxCellWidth(int column) => _maxWidths.TryGetValue(column, out var width) ? width : double.PositiveInfinity;

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        for (var c = 0; c < _cells.Count; c++)
        {
            var w = c < Widths.Length ? Widths[c] : 0;
            _cells[c].Measure(new Size(w, double.PositiveInfinity));
            height = Math.Max(height, _cells[c].DesiredSize.Height);
            width += w;
        }
        return new Size(width, height + (_line == ImportTableLine.None ? 0 : 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var height = finalSize.Height - (_line == ImportTableLine.None ? 0 : 1);
        for (var c = 0; c < _cells.Count; c++)
        {
            var w = c < Widths.Length ? Widths[c] : 0;
            _cells[c].Arrange(new Rect(x, 0, w, height));
            x += w;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_line == ImportTableLine.None)
        {
            return;
        }
        var color = ArcTokens.GetColor(this, _line == ImportTableLine.Header ? ArcDesignTokenKind.Border : ArcDesignTokenKind.BorderSubtle);
        context.FillRectangle(new ImmutableSolidColorBrush(color), new Rect(0, Bounds.Height - 1, Bounds.Width, 1));
    }
}
