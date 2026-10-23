using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Column widths of an answer table, as a browser lays out an automatic table: every column gets at least its widest
/// word and at most its longest line until the table is as wide as it may be; past that the extra width goes to the
/// columns in proportion to their longest lines. Pure.
/// </summary>
internal static class AiAnswerTableLayout
{
    /// <summary>
    /// The width of each column and of the table for a box <paramref name="available"/> wide. The table is at least
    /// <paramref name="minTableWidth"/> wide (Arc: 28rem, so a narrow box scrolls rather than squeezing every cell) unless
    /// its longest lines need less, and never narrower than its widest words.
    /// </summary>
    public static (double[] Widths, double Table) Columns(double available, double minTableWidth, IReadOnlyList<double> mins, IReadOnlyList<double> maxes)
    {
        var count = mins.Count;
        var widths = new double[count];
        if (count == 0)
        {
            return (widths, 0);
        }
        var sumMin = 0.0;
        var sumMax = 0.0;
        for (var i = 0; i < count; i++)
        {
            sumMin += mins[i];
            sumMax += Math.Max(mins[i], maxes[i]);
        }
        var box = double.IsFinite(available) ? available : sumMax;
        var table = Math.Max(Math.Max(box, Math.Min(minTableWidth, sumMax)), sumMin);
        if (table >= sumMax)
        {
            for (var i = 0; i < count; i++)
            {
                var max = Math.Max(mins[i], maxes[i]);
                widths[i] = sumMax > 0 ? max * table / sumMax : table / count;
            }
        }
        else
        {
            var share = sumMax > sumMin ? (table - sumMin) / (sumMax - sumMin) : 0;
            for (var i = 0; i < count; i++)
            {
                widths[i] = mins[i] + (Math.Max(mins[i], maxes[i]) - mins[i]) * share;
            }
        }
        return (widths, table);
    }
}

/// <summary>
/// The comparison table of an answer: a focusable region (a Tab stop; the arrow keys scroll it) with a 1px border and an
/// 18 radius that scrolls sideways when the table is wider than the column. Cells are texts of the answer: citations in
/// them are pills, and their sentences light like any other.
/// </summary>
[TemplatePart(CellsPart, typeof(AiAnswerTablePanel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
internal sealed class AiAnswerTable : TemplatedControl
{
    public const string CellsPart = "PART_Cells";
    public const string ScrollPart = "PART_Scroll";

    /// <summary>How far an arrow key scrolls.</summary>
    private const double ScrollStep = 40;

    private readonly AiAnswerDocument _owner;
    private readonly List<AiAnswerText> _cells = [];
    private readonly List<int> _rows = [];
    private AiAnswerTablePanel? _panel;
    private ScrollViewer? _scroll;
    private bool _hasHeader;

    static AiAnswerTable()
    {
        FocusableProperty.OverrideDefaultValue<AiAnswerTable>(true);
        FocusAdornerProperty.OverrideDefaultValue<AiAnswerTable>(null);
    }

    public AiAnswerTable(AiAnswerDocument owner)
    {
        _owner = owner;
        RefreshLabels();
    }

    protected override Type StyleKeyOverride => typeof(AiAnswerTable);

    /// <summary>The cells, row by row.</summary>
    public IReadOnlyList<AiAnswerText> Cells => _cells;

    /// <summary>The number of cells in each row.</summary>
    public IReadOnlyList<int> Rows => _rows;

    /// <summary>The cell panel (once the template is applied).</summary>
    internal AiAnswerTablePanel? Panel => _panel;

    /// <summary>The scroll viewer (once the template is applied).</summary>
    internal ScrollViewer? Scroll => _scroll;

    /// <summary>
    /// Shows the rows of <paramref name="block"/>. Cells that exist update in place; a new row (it arrives whole) fades in
    /// with <paramref name="fade"/>. A change in the number of columns builds every cell again.
    /// </summary>
    public void Update(AiAnswerBlock block, bool fade)
    {
        var columns = block.Rows.Count > 0 ? block.Rows[0] : 0;
        var oldColumns = _rows.Count > 0 ? _rows[0] : 0;
        if (columns != oldColumns || block.HasHeader != _hasHeader)
        {
            foreach (var cell in _cells)
            {
                foreach (var pill in cell.Pills)
                {
                    _owner.ForgetCitation(pill);
                }
            }
            _cells.Clear();
            _rows.Clear();
        }
        _hasHeader = block.HasHeader;
        var index = 0;
        for (var r = 0; r < block.Rows.Count; r++)
        {
            for (var c = 0; c < block.Rows[r]; c++)
            {
                if (index == _cells.Count)
                {
                    _cells.Add(new AiAnswerText(_owner, RoleOf(r, c)) { TextWrapping = TextWrapping.WrapWithOverflow });
                }
                index++;
            }
        }
        while (_cells.Count > index)
        {
            foreach (var pill in _cells[^1].Pills)
            {
                _owner.ForgetCitation(pill);
            }
            _cells.RemoveAt(_cells.Count - 1);
        }
        _rows.Clear();
        _rows.AddRange(block.Rows);
        _panel?.SetCells(_cells, _rows, _hasHeader);
        for (var i = 0; i < _cells.Count; i++)
        {
            _cells[i].SetContent(block.Texts[i], fade);
        }
    }

    private AiAnswerTextRole RoleOf(int row, int column) =>
        row == 0 && _hasHeader ? AiAnswerTextRole.HeaderCell : column == 0 ? AiAnswerTextRole.RowHeaderCell : AiAnswerTextRole.Cell;

    /// <summary>The brushes of the document: header fill and row lines.</summary>
    public void ApplyBrushes(AiAnswerDocument owner)
    {
        if (_panel is not null)
        {
            _panel.HeaderBrush = owner.MutedBrush;
            _panel.LineBrush = owner.HairlineBrush;
        }
    }

    /// <summary>The region's accessible name.</summary>
    public void RefreshLabels() => AutomationProperties.SetName(this, _owner.TableName);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.SetCells([], [], false);
        _panel = e.NameScope.Find<AiAnswerTablePanel>(CellsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        if (_panel is not null)
        {
            ApplyBrushes(_owner);
            _panel.SetCells(_cells, _rows, _hasHeader);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_panel is not null)
        {
            // The scroll viewer measures its content without a width limit; the cells still need the box's width.
            _panel.ViewportWidth = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width - 2) : double.PositiveInfinity;
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _scroll is null || e.Source != this)
        {
            return;
        }
        var offset = _scroll.Offset;
        var max = Math.Max(0, _scroll.Extent.Width - _scroll.Viewport.Width);
        var x = e.Key switch
        {
            Key.Left => offset.X - ScrollStep,
            Key.Right => offset.X + ScrollStep,
            Key.Home => 0,
            Key.End => max,
            _ => double.NaN
        };
        if (!double.IsNaN(x))
        {
            _scroll.Offset = new Vector(Math.Clamp(x, 0, max), offset.Y);
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    /// <summary>A named region (Arc: <c>role=region</c> with an <c>aria-label</c>).</summary>
    private sealed class TablePeer(AiAnswerTable owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

        protected override bool IsContentElementCore() => true;
    }
}

/// <summary>
/// Lays out the cells of an <see cref="AiAnswerTable"/> in rows and automatic columns (<see cref="AiAnswerTableLayout"/>),
/// with 10 / 14 padding, and draws the header row's fill (the muted surface at 60%) and a hairline above every other row.
/// </summary>
internal sealed class AiAnswerTablePanel : Control
{
    /// <summary>The width of the box the table sits in; the table fills it or, at least 448 wide, scrolls.</summary>
    public static readonly StyledProperty<double> ViewportWidthProperty =
        AvaloniaProperty.Register<AiAnswerTablePanel, double>(nameof(ViewportWidth), double.PositiveInfinity);

    /// <summary>The narrowest the table gets before it scrolls (Arc: 28rem).</summary>
    public static readonly StyledProperty<double> MinTableWidthProperty =
        AvaloniaProperty.Register<AiAnswerTablePanel, double>(nameof(MinTableWidth), 448);

    /// <summary>Padding of every cell (Arc 10 14).</summary>
    public static readonly StyledProperty<Thickness> CellPaddingProperty =
        AvaloniaProperty.Register<AiAnswerTablePanel, Thickness>(nameof(CellPadding), new Thickness(14, 10));

    /// <summary>The header row's fill (drawn at 60%).</summary>
    public static readonly StyledProperty<IBrush?> HeaderBrushProperty =
        AvaloniaProperty.Register<AiAnswerTablePanel, IBrush?>(nameof(HeaderBrush));

    /// <summary>The line above every row after the first.</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<AiAnswerTablePanel, IBrush?>(nameof(LineBrush));

    private readonly List<AiAnswerText> _cells = [];
    private readonly List<int> _rows = [];
    private double[] _widths = [];
    private double[] _heights = [];
    private bool _hasHeader;

    static AiAnswerTablePanel()
    {
        AffectsMeasure<AiAnswerTablePanel>(ViewportWidthProperty, MinTableWidthProperty, CellPaddingProperty);
        AffectsRender<AiAnswerTablePanel>(HeaderBrushProperty, LineBrushProperty);
    }

    /// <inheritdoc cref="ViewportWidthProperty"/>
    public double ViewportWidth { get => GetValue(ViewportWidthProperty); set => SetValue(ViewportWidthProperty, value); }

    /// <inheritdoc cref="MinTableWidthProperty"/>
    public double MinTableWidth { get => GetValue(MinTableWidthProperty); set => SetValue(MinTableWidthProperty, value); }

    /// <inheritdoc cref="CellPaddingProperty"/>
    public Thickness CellPadding { get => GetValue(CellPaddingProperty); set => SetValue(CellPaddingProperty, value); }

    /// <inheritdoc cref="HeaderBrushProperty"/>
    public IBrush? HeaderBrush { get => GetValue(HeaderBrushProperty); set => SetValue(HeaderBrushProperty, value); }

    /// <inheritdoc cref="LineBrushProperty"/>
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <summary>The column widths of the last layout (tests).</summary>
    internal IReadOnlyList<double> ColumnWidths => _widths;

    /// <summary>The row heights of the last layout, lines included (tests).</summary>
    internal IReadOnlyList<double> RowHeights => _heights;

    /// <summary>Shows <paramref name="cells"/> (row by row, <paramref name="rows"/> cells in each row).</summary>
    public void SetCells(IReadOnlyList<AiAnswerText> cells, IReadOnlyList<int> rows, bool hasHeader)
    {
        var same = cells.Count == _cells.Count && rows.SequenceEqual(_rows) && hasHeader == _hasHeader;
        for (var i = 0; same && i < cells.Count; i++)
        {
            same = ReferenceEquals(cells[i], _cells[i]);
        }
        if (same)
        {
            return;
        }
        foreach (var cell in _cells)
        {
            if (!cells.Contains(cell))
            {
                LogicalChildren.Remove(cell);
                VisualChildren.Remove(cell);
            }
        }
        foreach (var cell in cells)
        {
            if (!_cells.Contains(cell))
            {
                LogicalChildren.Add(cell);
                VisualChildren.Add(cell);
            }
        }
        _cells.Clear();
        _cells.AddRange(cells);
        _rows.Clear();
        _rows.AddRange(rows);
        _hasHeader = hasHeader;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = _rows.Count > 0 ? _rows[0] : 0;
        if (columns == 0)
        {
            _widths = [];
            _heights = [];
            return default;
        }
        var padding = CellPadding;
        var horizontal = padding.Left + padding.Right;
        var mins = new double[columns];
        var maxes = new double[columns];
        var index = 0;
        foreach (var count in _rows)
        {
            for (var c = 0; c < count; c++)
            {
                var cell = _cells[index++];
                if (c >= columns)
                {
                    continue;
                }
                cell.Measure(new Size(0, double.PositiveInfinity));
                mins[c] = Math.Max(mins[c], Math.Ceiling(cell.DesiredSize.Width) + horizontal);
                cell.Measure(Size.Infinity);
                maxes[c] = Math.Max(maxes[c], Math.Ceiling(cell.DesiredSize.Width) + horizontal);
            }
        }
        var (widths, table) = AiAnswerTableLayout.Columns(ViewportWidth, MinTableWidth, mins, maxes);
        _widths = widths;
        _heights = new double[_rows.Count];
        index = 0;
        var height = 0.0;
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = 0.0;
            for (var c = 0; c < _rows[r]; c++)
            {
                var cell = _cells[index++];
                var width = c < columns ? widths[c] : 0;
                cell.Measure(new Size(Math.Max(0, width - horizontal), double.PositiveInfinity));
                row = Math.Max(row, cell.DesiredSize.Height);
            }
            _heights[r] = row + padding.Top + padding.Bottom + (r > 0 ? 1 : 0);
            height += _heights[r];
        }
        return new Size(table, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = CellPadding;
        var index = 0;
        var y = 0.0;
        for (var r = 0; r < _rows.Count && r < _heights.Length; r++)
        {
            var x = 0.0;
            var top = y + (r > 0 ? 1 : 0) + padding.Top;
            for (var c = 0; c < _rows[r]; c++)
            {
                var cell = _cells[index++];
                var width = c < _widths.Length ? _widths[c] : 0;
                cell.Arrange(new Rect(x + padding.Left, top, Math.Max(0, width - padding.Left - padding.Right), cell.DesiredSize.Height));
                x += width;
            }
            y += _heights[r];
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        if (_heights.Length == 0)
        {
            return;
        }
        if (_hasHeader && HeaderBrush is { } header)
        {
            using (context.PushOpacity(AiAnswerDocument.TableHeaderAlpha))
            {
                context.FillRectangle(header, new Rect(0, 0, width, _heights[0]));
            }
        }
        if (LineBrush is { } line)
        {
            var y = 0.0;
            for (var r = 0; r < _heights.Length; r++)
            {
                if (r > 0)
                {
                    context.FillRectangle(line, new Rect(0, y, width, 1));
                }
                y += _heights[r];
            }
        }
    }
}
