using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The body of a table block: a header row of column titles over the rows, laid out like a browser's automatic table
/// filling the column (<see cref="AiAnswerTableLayout.Columns"/>: past the longest lines the extra width goes to the
/// columns in proportion to them; below that every column keeps at least its widest word and long cells wrap). Cells are
/// <see cref="AiPanelText"/>s: header 12 / 16.8 TextMuted with 8px under it, rows 8px above and below, 12px between
/// columns, the first column as row headers in Foreground 500, right-aligned columns pinned right with tabular numbers;
/// a 1px <see cref="LineBrush"/> line under the header and between rows, none under the last. Cells are centered
/// vertically in their row. Not interactive: the page owns the selection.
/// </summary>
internal sealed class AiPanelTableView : Control
{
    /// <summary>The space after every column but the last (Arc's cell padding-right).</summary>
    public const double ColumnGap = 12;

    /// <summary>The space under the header text.</summary>
    public const double HeaderBottom = 8;

    /// <summary>The space above and below the text of a row.</summary>
    public const double RowPadding = 8;

    /// <summary>The row lines.</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<AiPanelTableView, IBrush?>(nameof(LineBrush));

    private readonly List<AiPanelText> _headers = [];
    private readonly List<AiPanelText[]> _rows = [];
    private readonly List<AiPanelColumnAlignment> _alignments = [];
    private double[] _widths = [];
    private double[] _heights = [];
    private double[]? _mins;
    private double[]? _maxes;

    static AiPanelTableView()
    {
        AffectsRender<AiPanelTableView>(LineBrushProperty);
    }

    public AiPanelTableView(AiPanelTable table)
    {
        Build(table);
    }

    protected override Type StyleKeyOverride => typeof(AiPanelTableView);

    /// <inheritdoc cref="LineBrushProperty"/>
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <summary>The header cells, in column order.</summary>
    public IReadOnlyList<AiPanelText> Headers => _headers;

    /// <summary>The cells of each row, in column order.</summary>
    public IReadOnlyList<AiPanelText[]> Rows => _rows;

    /// <summary>The column widths of the last layout, the space after each column included (tests).</summary>
    internal IReadOnlyList<double> ColumnWidths => _widths;

    /// <summary>The heights of the header row and of every row, lines included (tests).</summary>
    internal IReadOnlyList<double> RowHeights => _heights;

    /// <summary>Every cell, header first, row by row: the reading order of the selection.</summary>
    public IEnumerable<(AiPanelText Text, int Row, int Column)> Cells()
    {
        for (var c = 0; c < _headers.Count; c++)
        {
            yield return (_headers[c], 0, c);
        }
        for (var r = 0; r < _rows.Count; r++)
        {
            for (var c = 0; c < _rows[r].Length; c++)
            {
                yield return (_rows[r][c], r + 1, c);
            }
        }
    }

    private void Build(AiPanelTable table)
    {
        var count = table.Columns.Count;
        for (var c = 0; c < count; c++)
        {
            var column = table.Columns[c];
            var numeric = column.Alignment == AiPanelColumnAlignment.Right;
            _alignments.Add(column.Alignment);
            var header = new AiPanelText(AiPanelTextRole.ColumnHeader, numeric) { Text = column.Header };
            _headers.Add(header);
            Add(header);
        }
        foreach (var row in table.Rows)
        {
            var cells = new AiPanelText[count];
            for (var c = 0; c < count; c++)
            {
                var column = table.Columns[c];
                var role = column.IsRowHeader ? AiPanelTextRole.RowHeader : AiPanelTextRole.Cell;
                cells[c] = new AiPanelText(role, column.Alignment == AiPanelColumnAlignment.Right)
                {
                    Text = c < row.Cells.Count ? row.Cells[c] : string.Empty,
                    TextWrapping = TextWrapping.Wrap
                };
                Add(cells[c]);
            }
            _rows.Add(cells);
        }
        foreach (var header in _headers)
        {
            header.TextWrapping = TextWrapping.Wrap;
        }
    }

    private void Add(AiPanelText text)
    {
        LogicalChildren.Add(text);
        VisualChildren.Add(text);
    }

    private double PaddingAfter(int column) => column < _headers.Count - 1 ? ColumnGap : 0;

    private void MeasureExtremes()
    {
        // The widest word and the longest line of each column do not depend on the width offered: measured once.
        var count = _headers.Count;
        _mins = new double[count];
        _maxes = new double[count];
        foreach (var (text, _, column) in Cells())
        {
            var gap = PaddingAfter(column);
            text.Measure(new Size(0, double.PositiveInfinity));
            _mins[column] = Math.Max(_mins[column], Math.Ceiling(text.DesiredSize.Width) + gap);
            text.Measure(Size.Infinity);
            _maxes[column] = Math.Max(_maxes[column], Math.Ceiling(text.DesiredSize.Width) + gap);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = _headers.Count;
        if (count == 0)
        {
            _widths = [];
            _heights = [];
            return default;
        }
        if (_mins is null)
        {
            MeasureExtremes();
        }
        var (widths, table) = AiAnswerTableLayout.Columns(availableSize.Width, 0, _mins!, _maxes!);
        _widths = widths;
        _heights = new double[_rows.Count + 1];
        var header = 0.0;
        for (var c = 0; c < count; c++)
        {
            _headers[c].Measure(new Size(Math.Max(0, widths[c] - PaddingAfter(c)), double.PositiveInfinity));
            header = Math.Max(header, _headers[c].DesiredSize.Height);
        }
        _heights[0] = header + HeaderBottom + 1;
        var height = _heights[0];
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = 0.0;
            for (var c = 0; c < count; c++)
            {
                var cell = _rows[r][c];
                cell.Measure(new Size(Math.Max(0, widths[c] - PaddingAfter(c)), double.PositiveInfinity));
                row = Math.Max(row, cell.DesiredSize.Height);
            }
            _heights[r + 1] = RowPadding + row + RowPadding + (r < _rows.Count - 1 ? 1 : 0);
            height += _heights[r + 1];
        }
        var width = double.IsFinite(availableSize.Width) ? Math.Max(availableSize.Width, table) : table;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = _headers.Count;
        if (count == 0 || _heights.Length == 0)
        {
            return finalSize;
        }
        var x = 0.0;
        for (var c = 0; c < count; c++)
        {
            var slot = Math.Max(0, _widths[c] - PaddingAfter(c));
            Place(_headers[c], c, x, slot, 0, _heights[0] - HeaderBottom - 1);
            var y = _heights[0];
            for (var r = 0; r < _rows.Count; r++)
            {
                var lines = r < _rows.Count - 1 ? 1 : 0;
                Place(_rows[r][c], c, x, slot, y + RowPadding, _heights[r + 1] - 2 * RowPadding - lines);
                y += _heights[r + 1];
            }
            x += _widths[c];
        }
        return finalSize;
    }

    private void Place(AiPanelText text, int column, double x, double slot, double top, double rowHeight)
    {
        var size = text.DesiredSize;
        var width = Math.Min(size.Width, slot);
        var left = _alignments[column] == AiPanelColumnAlignment.Right ? x + slot - width : x;
        text.Arrange(new Rect(left, top + Math.Max(0, (rowHeight - size.Height) / 2), _alignments[column] == AiPanelColumnAlignment.Right ? width : slot, size.Height));
    }

    public override void Render(DrawingContext context)
    {
        if (LineBrush is not { } brush || _heights.Length == 0)
        {
            return;
        }
        var width = Bounds.Width;
        var y = 0.0;
        // The page lays out without rounding: aliased edges keep a 1px line on one row of pixels.
        using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
        {
            for (var i = 0; i < _heights.Length - 1; i++)
            {
                y += _heights[i];
                // The line is the last pixel of the row above it.
                context.FillRectangle(brush, new Rect(0, y - 1, width, 1));
            }
        }
    }
}
