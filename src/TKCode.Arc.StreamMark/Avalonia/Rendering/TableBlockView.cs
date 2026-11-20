using System.Collections.Immutable;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 表格：自定义布局（不使用 Grid，流式追加行时不会触发 Grid 的测量问题）。
/// 列宽取各列自然宽度，超出可用宽度时按比例收缩并换行，仍放不下时允许横向滚动。
/// 悬停时在表格边框上方（网格外）显示「复制为 Markdown」，需要时还有全屏。按钮不进单元格，避免被表头文字盖住。
/// </summary>
public sealed class TableBlockView : BlockView
{
    private const double CellPaddingX = 10;
    private const double CellPaddingY = 6;
    private const double MinColumn = 64;
    private const double MaxColumn = 420;

    /// <summary>网格上方操作条的高度（与 <see cref="IconButton"/> 一致）。按钮排在这里，而不是表头单元格里。会话行高估算也要算上它。</summary>
    internal const double ActionBarHeight = 26;

    private sealed class CellEntry
    {
        public readonly TextSegmentView View = new();
        public double NaturalWidth;
        public bool NaturalValid;
        public double MeasuredColWidth = double.NaN;
        public bool HeightValid;
    }

    private sealed class RowEntry
    {
        public readonly List<CellEntry> Cells = [];
        public TableRow? CachedRow;
        public int ColumnCount;
        public int FirstSegment;
        public int SegmentCount;
    }

    private readonly List<RowEntry> _rows = [];
    private readonly IconButton _copy = new() { Icon = MarkdownIcons.Copy, IsVisible = false };
    private readonly IconButton _fullscreen = new() { Icon = MarkdownIcons.Expand, IsVisible = false };
    private readonly BlockActionBar _actions;
    private double[] _naturalScratch = [];
    private double[] _columnWidths = [];
    private int _columnCount;
    private double[] _rowHeights = [];
    private double _totalRowHeight;
    private double _offsetX;
    // 外层 HTML 居中 / 右对齐且表格窄于可用宽度时整体右移
    private double _shift;
    private double _contentWidth;

    public TableBlockView()
    {
        ClipToBounds = true;
        _actions = new BlockActionBar(this, AddChild, RemoveChild);
        AddChild(_fullscreen);
        AddChild(_copy);
        global::Avalonia.Automation.AutomationProperties.SetName(_copy, "Copy");
        global::Avalonia.Automation.AutomationProperties.SetName(_fullscreen, "Fullscreen");
        _fullscreen.Click += (_, _) => OpenFullscreen();
        _copy.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && Block is TableBlock table)
            {
                await clipboard.SetTextAsync(ToMarkdown(table));
            }
        };
    }

    private void InsertCellChild(Control child)
    {
        var actionCount = 2 + _actions.Buttons.Count;
        var visualIndex = Math.Max(0, VisualChildren.Count - actionCount);
        var logicalIndex = Math.Max(0, LogicalChildren.Count - actionCount);
        LogicalChildren.Insert(logicalIndex, child);
        VisualChildren.Insert(visualIndex, child);
    }

    private static bool SameColumns(ImmutableArray<TableAlignment> a, ImmutableArray<TableAlignment> b)
    {
        if (a == b)
        {
            return true;
        }
        if (a.IsDefault || b.IsDefault || a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var table = (TableBlock)block;
        _copy.Foreground = context.Style.SecondaryForeground;
        _copy.HoverBackground = context.Style.HoverBackground;
        _fullscreen.Foreground = context.Style.SecondaryForeground;
        _fullscreen.HoverBackground = context.Style.HoverBackground;
        var actionsRebuilt = false;
        if (contextChanged || previous is null)
        {
            // 插件操作与复制 / 全屏一样只在悬停时显示
            _actions.Rebuild(context, Plugins.BlockActionTargets.Table, table, _copy.IsVisible);
            actionsRebuilt = true;
        }
        var columnsChanged = previous is not TableBlock prevTable || !SameColumns(prevTable.Columns, table.Columns);
        var anyChanged = contextChanged || previous is null || _rows.Count != table.Rows.Length || columnsChanged;
        var structureChanged = actionsRebuilt;
        for (var r = 0; r < table.Rows.Length; r++)
        {
            if (r >= _rows.Count)
            {
                _rows.Add(new RowEntry());
                structureChanged = true;
            }
            var row = table.Rows[r];
            var rowEntry = _rows[r];
            var columns = Math.Max(row.Cells.Length, table.Columns.Length);
            // 整行模型引用、列数与起始片段号均未变化时 O(1) 跳过整行单元格
            if (!contextChanged && !columnsChanged && ReferenceEquals(rowEntry.CachedRow, row)
                && rowEntry.ColumnCount == columns && rowEntry.FirstSegment == segment && rowEntry.SegmentCount > 0)
            {
                segment += rowEntry.SegmentCount;
                continue;
            }
            var views = rowEntry.Cells;
            while (views.Count < columns)
            {
                var cell = new CellEntry();
                views.Add(cell);
                InsertCellChild(cell.View);
                anyChanged = true;
            }
            while (views.Count > columns)
            {
                RemoveChild(views[^1].View);
                views.RemoveAt(views.Count - 1);
                anyChanged = true;
            }
            var startSegment = segment;
            for (var c = 0; c < columns; c++)
            {
                var content = c < row.Cells.Length ? row.Cells[c] : InlineContent.Empty;
                var alignment = c < table.Columns.Length ? table.Columns[c] switch
                {
                    TableAlignment.Center => TextAlignment.Center,
                    TableAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left,
                } : TextAlignment.Left;
                var entry = views[c];
                if (entry.View.SetTracked(context, KeyFor(context, topKey, ref segment), content, context.Style.FontSize,
                    row.IsHeader ? FontWeight.SemiBold : FontWeight.Normal, alignment: alignment))
                {
                    entry.NaturalValid = false;
                    entry.HeightValid = false;
                    anyChanged = true;
                }
            }
            rowEntry.CachedRow = row;
            rowEntry.ColumnCount = columns;
            rowEntry.FirstSegment = startSegment;
            rowEntry.SegmentCount = segment - startSegment;
        }
        while (_rows.Count > table.Rows.Length)
        {
            foreach (var cell in _rows[^1].Cells)
            {
                RemoveChild(cell.View);
            }
            _rows.RemoveAt(_rows.Count - 1);
            anyChanged = true;
        }
        if (structureChanged)
        {
            // 仅在操作按钮重建或结构兜底时确保按钮在最上层（新增单元格已直接插在按钮之前）
            BringActionsToFront();
            anyChanged = true;
        }
        if (anyChanged)
        {
            InvalidateMeasure();
        }
    }

    private void BringActionsToFront()
    {
        var count = VisualChildren.Count;
        if (count >= 2 && ReferenceEquals(VisualChildren[count - 2], _fullscreen) && ReferenceEquals(VisualChildren[count - 1], _copy)
            && (_actions.Buttons.Count == 0 || count >= 3 && ReferenceEquals(VisualChildren[count - 3], _actions.Buttons[^1])))
        {
            return;
        }
        _actions.BringToFront();
        RemoveChild(_fullscreen);
        RemoveChild(_copy);
        AddChild(_fullscreen);
        AddChild(_copy);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = 0;
        for (var r = 0; r < _rows.Count; r++)
        {
            var count = _rows[r].Cells.Count;
            if (count > columns)
            {
                columns = count;
            }
        }
        _columnCount = columns;
        if (_naturalScratch.Length < columns)
        {
            _naturalScratch = new double[columns];
        }
        Array.Clear(_naturalScratch, 0, columns);
        if (_columnWidths.Length < columns)
        {
            _columnWidths = new double[columns];
        }
        for (var r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r].Cells;
            for (var c = 0; c < row.Count; c++)
            {
                var cell = row[c];
                if (!cell.NaturalValid)
                {
                    cell.View.Measure(Size.Infinity);
                    cell.NaturalWidth = Math.Min(MaxColumn, cell.View.DesiredSize.Width + CellPaddingX * 2);
                    cell.NaturalValid = true;
                    cell.HeightValid = false;
                }
                if (cell.NaturalWidth > _naturalScratch[c])
                {
                    _naturalScratch[c] = cell.NaturalWidth;
                }
            }
        }
        var total = 0.0;
        for (var c = 0; c < columns; c++)
        {
            if (_naturalScratch[c] < MinColumn)
            {
                _naturalScratch[c] = MinColumn;
            }
            _columnWidths[c] = _naturalScratch[c];
            total += _naturalScratch[c];
        }

        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : total;
        if (total > available && total > 0)
        {
            // 按比例收缩，但不小于最小列宽
            var shrinkable = 0.0;
            for (var c = 0; c < columns; c++)
            {
                shrinkable += _naturalScratch[c] - MinColumn;
            }
            var excess = total - available;
            if (shrinkable > 0)
            {
                var factor = Math.Min(excess, shrinkable) / shrinkable;
                for (var c = 0; c < columns; c++)
                {
                    _columnWidths[c] = _naturalScratch[c] - (_naturalScratch[c] - MinColumn) * factor;
                }
            }
        }
        _contentWidth = 0;
        for (var c = 0; c < columns; c++)
        {
            _contentWidth += _columnWidths[c];
        }

        if (_rowHeights.Length != _rows.Count)
        {
            _rowHeights = new double[_rows.Count];
        }
        var totalHeight = 0.0;
        for (var r = 0; r < _rows.Count; r++)
        {
            var height = 0.0;
            var row = _rows[r].Cells;
            for (var c = 0; c < row.Count; c++)
            {
                var cell = row[c];
                var cellWidth = Math.Max(1, _columnWidths[c] - CellPaddingX * 2);
                if (!cell.HeightValid || !cell.MeasuredColWidth.Equals(cellWidth) || !cell.View.IsMeasureValid)
                {
                    cell.View.Measure(new Size(cellWidth, double.PositiveInfinity));
                    cell.MeasuredColWidth = cellWidth;
                    cell.HeightValid = true;
                }
                if (cell.View.DesiredSize.Height > height)
                {
                    height = cell.View.DesiredSize.Height;
                }
            }
            _rowHeights[r] = height + CellPaddingY * 2;
            totalHeight += _rowHeights[r];
        }
        _totalRowHeight = totalHeight;
        _copy.Measure(Size.Infinity);
        _fullscreen.Measure(Size.Infinity);
        foreach (var button in _actions.Buttons)
        {
            button.Measure(Size.Infinity);
        }
        _offsetX = Math.Clamp(_offsetX, 0, Math.Max(0, _contentWidth - available));
        return new Size(Math.Min(_contentWidth, available) + 1, totalHeight + 1 + ActionBarHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spare = Math.Max(0, finalSize.Width - _contentWidth - 1);
        _shift = Context?.Inherited?.Align switch
        {
            TextAlign.Center => Math.Floor(spare / 2),
            TextAlign.Right => spare,
            _ => 0,
        };
        double y = ActionBarHeight;
        for (var r = 0; r < _rows.Count; r++)
        {
            var x = _shift - _offsetX;
            var row = _rows[r].Cells;
            for (var c = 0; c < row.Count; c++)
            {
                row[c].View.Arrange(new Rect(x + CellPaddingX, y + CellPaddingY, Math.Max(1, _columnWidths[c] - CellPaddingX * 2), _rowHeights[r] - CellPaddingY * 2));
                x += _columnWidths[c];
            }
            y += _rowHeights[r];
        }
        var visibleRight = Math.Min(finalSize.Width, _shift + _contentWidth + 1);
        var buttonY = Math.Max(0, ActionBarHeight - _copy.DesiredSize.Height);
        _copy.Arrange(new Rect(visibleRight - _copy.DesiredSize.Width - 3, buttonY, _copy.DesiredSize.Width, _copy.DesiredSize.Height));
        _fullscreen.Arrange(new Rect(visibleRight - _copy.DesiredSize.Width - _fullscreen.DesiredSize.Width - 5, buttonY, _fullscreen.DesiredSize.Width, _fullscreen.DesiredSize.Height));
        // 插件操作排在复制（及可见时的全屏）左边
        var actionX = visibleRight - _copy.DesiredSize.Width - 3 - (_fullscreen.IsVisible ? _fullscreen.DesiredSize.Width + 2 : 0);
        foreach (var button in _actions.Buttons)
        {
            actionX -= button.DesiredSize.Width + 2;
            button.Arrange(new Rect(actionX, buttonY, button.DesiredSize.Width, button.DesiredSize.Height));
        }
        return finalSize;
    }

    private IPen? _gridPen;
    private IBrush? _gridPenBrush;

    public override void Render(DrawingContext context)
    {
        if (Context is null || _rowHeights.Length == 0)
        {
            return;
        }
        var style = Context.Style;
        // 块移动（上方内容展开）时 Render 每帧都会重跑：笔按画刷缓存，不每次新建
        if (_gridPen is null || !ReferenceEquals(_gridPenBrush, style.TableBorderBrush))
        {
            _gridPenBrush = style.TableBorderBrush;
            _gridPen = new Pen(style.TableBorderBrush, 1);
        }
        var pen = _gridPen;
        var width = _contentWidth;
        var height = _totalRowHeight;
        using (context.PushTransform(Matrix.CreateTranslation(_shift - _offsetX, ActionBarHeight)))
        {
            if (Block is TableBlock { Rows.Length: > 0 } table && table.Rows[0].IsHeader)
            {
                context.FillRectangle(style.TableHeaderBackground, new Rect(0, 0, width, _rowHeights[0]));
            }
            var y = 0.5;
            context.DrawLine(pen, new Point(0, y), new Point(width, y));
            for (var r = 0; r < _rowHeights.Length; r++)
            {
                y += _rowHeights[r];
                context.DrawLine(pen, new Point(0, Math.Round(y) - 0.5), new Point(width, Math.Round(y) - 0.5));
            }
            var x = 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
            for (var c = 0; c < _columnCount; c++)
            {
                x += _columnWidths[c];
                context.DrawLine(pen, new Point(Math.Round(x) - 0.5, 0), new Point(Math.Round(x) - 0.5, height));
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _copy.IsVisible = true;
        // 表格放不下（需要横向滚动）或较大时才提供全屏
        _fullscreen.IsVisible = !IsInOverlay && (_contentWidth > Bounds.Width + 1 || _rowHeights.Length > 8);
        _actions.SetVisible(true);
        InvalidateArrange();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _copy.IsVisible = false;
        _fullscreen.IsVisible = false;
        _actions.SetVisible(false);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var dx = e.Delta.X != 0 ? -e.Delta.X : e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -e.Delta.Y : 0;
        if (dx == 0 || _contentWidth <= Bounds.Width)
        {
            base.OnPointerWheelChanged(e);
            return;
        }
        _offsetX = Math.Clamp(_offsetX + dx * 40, 0, Math.Max(0, _contentWidth - Bounds.Width + 1));
        InvalidateArrange();
        InvalidateVisual();
        e.Handled = true;
    }

    public override TextSegmentView? LastSegment() => _rows.Count > 0 && _rows[^1].Cells.Count > 0 ? _rows[^1].Cells[^1].View : null;

    public override IEnumerable<ISelectableSegment> Segments() => _rows.SelectMany(r => r.Cells.Select(c => c.View));

    // 全屏覆盖层中的副本不再提供全屏按钮
    internal bool IsInOverlay { get; init; }

    /// <summary>在覆盖层中全屏查看表格（按自然列宽排版，可滚动）。</summary>
    public BlockOverlay? OpenFullscreen() =>
        Context is not null && Block is TableBlock table ? BlockOverlay.Show(this, Context, table, () => new TableBlockView { IsInOverlay = true }) : null;

    /// <summary>以 GitHub 表格语法输出。</summary>
    public static string ToMarkdown(TableBlock table)
    {
        var sb = new StringBuilder();
        for (var r = 0; r < table.Rows.Length; r++)
        {
            sb.Append('|');
            foreach (var cell in table.Rows[r].Cells)
            {
                sb.Append(' ').Append(BlockText.PlainText(cell).Replace("|", "\\|")).Append(" |");
            }
            sb.Append('\n');
            if (r == 0 && table.Rows[0].IsHeader)
            {
                sb.Append('|');
                foreach (var column in table.Columns)
                {
                    sb.Append(column switch
                    {
                        TableAlignment.Left => " :--- |",
                        TableAlignment.Center => " :---: |",
                        TableAlignment.Right => " ---: |",
                        _ => " --- |",
                    });
                }
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }
}
