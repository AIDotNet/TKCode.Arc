using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Draws the body rows of one side of the grid: the gutter and pinned columns (<c>pinned</c>), or the scrolling columns in
/// view. Rows are drawn, never realized as controls: backgrounds, grid lines, cell text (numbers right-aligned with
/// tabular figures), the gutter's row numbers and checkboxes, group rows with their chevron, label, count and subtotals,
/// and placeholder rows while loading.
/// </summary>
internal sealed class DataGridCellsLayer : Avalonia.Controls.Control
{
    private const double CheckboxSize = 16;
    private const int LabelSlot = -1;
    private const int CountSlot = -2;
    private const int NumberSlot = -3;

    private readonly DataGrid _grid;
    private readonly DataGridViewport _viewport;
    private readonly bool _pinned;
    private readonly DataGridTextCache _texts = new();
    private readonly Dictionary<string, MotionValue<double>> _chevrons = new(StringComparer.Ordinal);
    private LiveLoop? _pulse;
    private double _pulseTime;
    private IBrush? _shadowBrush;
    private Color _shadowColor;

    private readonly record struct CellState(DataGrid Grid, int Row, int Accessor);

    private readonly record struct AggregateState(DataGrid Grid, DataGridGroupNode Node, int Accessor);

    private static readonly Func<CellState, string> s_cellText = static s => s.Grid.CellText(s.Row, s.Accessor);

    private static readonly Func<AggregateState, string> s_subtotalText =
        static s => s.Grid.FormatAggregate(s.Grid.SubtotalOf(s.Node, s.Accessor), s.Accessor);

    private static readonly Func<(DataGrid Grid, int Row), string> s_rowNumber =
        static s => (s.Row + 1).ToString("N0", s.Grid.EffectiveCulture);

    private static readonly Func<(DataGrid Grid, DataGridGroupNode Node), string> s_groupLabel = static s =>
    {
        var text = s.Grid.FormatValue(s.Node.Value, s.Node.Accessor);
        return string.IsNullOrEmpty(text) ? DataGridText.Get(DataGridLangResourceKind.EmptyValue) : text;
    };

    private static readonly Func<(DataGrid Grid, DataGridGroupNode Node), string> s_groupCount =
        static s => s.Node.Rows.Length.ToString("N0", s.Grid.EffectiveCulture);

    public DataGridCellsLayer(DataGrid grid, DataGridViewport viewport, bool pinned)
    {
        _grid = grid;
        _viewport = viewport;
        _pinned = pinned;
        ClipToBounds = true;
    }

    public void ClearText() => _texts.Clear();

    internal int CachedTextCount => _texts.Count;

    /// <summary>Turns a group's chevron (0° folded, 90° open) on the CSS spring.</summary>
    public void AnimateChevron(string groupId, bool expanded)
    {
        var target = expanded ? 90.0 : 0.0;
        if (!_chevrons.TryGetValue(groupId, out var angle))
        {
            angle = new MotionValue<double>(this, expanded ? 0 : 90, MotionVectors.Double);
            angle.Changed += _ => InvalidateVisual();
            _chevrons[groupId] = angle;
        }
        if (_grid.IsMotionFull)
        {
            _ = angle.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
        else
        {
            angle.Jump(target);
        }
    }

    /// <summary>Whether a body point (layer coordinates of the pinned layer) is over the gutter checkbox of a row.</summary>
    public bool IsOverCheckbox(Point point, int row)
    {
        var layout = _grid.Layout;
        if (layout.Gutter <= 0 || point.X >= layout.Gutter)
        {
            return false;
        }
        var rowHeight = _grid.RowHeight;
        var line = _grid.LineOfRow(row);
        var top = line * rowHeight - _viewport.Offset.Y;
        var box = CheckboxRect(layout.Gutter, top, rowHeight).Inflate(6);
        return ShowsCheckbox(row) && box.Contains(point);
    }

    private static Rect CheckboxRect(double gutter, double top, double rowHeight) =>
        new(Math.Round((gutter - CheckboxSize) / 2), Math.Round(top + (rowHeight - CheckboxSize) / 2), CheckboxSize, CheckboxSize);

    private bool ShowsCheckbox(int row)
    {
        if (!_grid.IsRowCheckEnabled)
        {
            return false;
        }
        return !_grid.ShowRowNumbers || _viewport.TouchMode || _grid.CheckedCount > 0 || _viewport.HoverRow == row ||
               _grid.IsChecked(_grid.KeyOfRow(row));
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        _grid.EnsureView();
        var palette = _grid.Palette;
        var layout = _grid.Layout;
        var rowHeight = _grid.RowHeight;
        _texts.BeginFrame();
        if (_grid.IsShowingSkeleton)
        {
            RenderSkeleton(context, layout, rowHeight, size, palette);
            return;
        }
        StopPulse();
        var (firstColumn, lastColumn, shift) = ColumnRange(layout, size.Width);
        var (firstLine, lastLine) = _viewport.LineWindow();
        var offsetY = _viewport.Offset.Y;
        for (var line = firstLine; line <= lastLine; line++)
        {
            var group = _grid.GroupAtLine(line);
            if (group is not null)
            {
                RenderGroup(context, group, line * rowHeight - offsetY, rowHeight, layout, firstColumn, lastColumn, shift, size, palette);
                continue;
            }
            var row = _grid.RowOfLine(line);
            var key = _grid.KeyOfRow(row);
            var y = line * rowHeight - offsetY + _viewport.SlideOffset(key);
            if (y + rowHeight < 0 || y > size.Height)
            {
                continue;
            }
            var opacity = _viewport.ArriveOpacity(key);
            using (opacity < 0.999 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
            {
                RenderRow(context, row, key, y, rowHeight, layout, firstColumn, lastColumn, shift, size, palette);
            }
        }
        var contentBottom = Math.Min(size.Height, Math.Max(0, _grid.LineCount * rowHeight - offsetY));
        if (_pinned)
        {
            if (layout.PinEdge > 0 && contentBottom > 0)
            {
                context.DrawLine(palette.BorderPen, new Point(size.Width - 0.5, 0), new Point(size.Width - 0.5, contentBottom));
            }
        }
        else if (_viewport.Offset.X > 0.5 && layout.PinEdge > 0)
        {
            var color = palette.EdgeShadow;
            if (_shadowBrush is null || color != _shadowColor)
            {
                _shadowColor = color;
                _shadowBrush = new ImmutableLinearGradientBrush(
                    [new ImmutableGradientStop(0, color), new ImmutableGradientStop(1, Colors.Transparent)],
                    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));
            }
            context.FillRectangle(_shadowBrush, new Rect(0, 0, 10, contentBottom));
        }
    }

    /// <summary>The visible columns this layer draws and the x shift from content to layer coordinates.</summary>
    private (int First, int Last, double Shift) ColumnRange(DataGridLayout layout, double width)
    {
        if (_pinned)
        {
            return (0, layout.PinCount - 1, 0);
        }
        var offsetX = _viewport.Offset.X;
        var shift = -offsetX - layout.PinEdge;
        var left = offsetX + layout.PinEdge;
        var right = left + width;
        var first = layout.PinCount;
        while (first < layout.Count && layout.Right(first) <= left)
        {
            first++;
        }
        var last = first - 1;
        while (last + 1 < layout.Count && layout.Lefts[last + 1] < right)
        {
            last++;
        }
        return (first, last, shift);
    }

    private void RenderRow(DrawingContext context, int row, object? key, double y, double rowHeight, DataGridLayout layout,
                           int firstColumn, int lastColumn, double shift, Size size, DataGridPalette palette)
    {
        var isChecked = _grid.IsChecked(key);
        var cellsLeft = firstColumn <= lastColumn ? layout.Lefts[firstColumn] + shift : 0;
        var cellsRight = firstColumn <= lastColumn ? layout.Right(lastColumn) + shift : 0;
        if (_pinned && layout.Gutter > 0)
        {
            RenderGutter(context, row, key, y, rowHeight, layout.Gutter, isChecked, palette);
        }
        if (firstColumn > lastColumn)
        {
            return;
        }
        context.FillRectangle(isChecked ? palette.CheckedBrush : palette.CellBrush, new Rect(cellsLeft, y, cellsRight - cellsLeft, rowHeight));
        context.DrawLine(palette.GridPen, new Point(cellsLeft, y + rowHeight - 0.5), new Point(cellsRight, y + rowHeight - 0.5));
        if (key is null)
        {
            RenderPlaceholderCells(context, row, y, rowHeight, layout, firstColumn, lastColumn, shift, palette);
            return;
        }
        var version = _grid.CellVersion(row);
        var padding = _grid.CellInlinePadding;
        var fontSize = _grid.CellFontSize;
        var styled = _grid.HasStyling;
        var errors = _grid.HasErrors;
        var finding = _grid.IsFindHighlighting;
        var item = styled ? _grid.ItemOfRow(row) : null;
        if (item is not null && _grid.RowStyleSelector is { } rowSelector && rowSelector(item) is { } rowTone)
        {
            context.FillRectangle(palette.Tone(rowTone).Fill, new Rect(cellsLeft, y, cellsRight - cellsLeft, rowHeight - 1));
        }
        for (var c = firstColumn; c <= lastColumn; c++)
        {
            var left = layout.Lefts[c] + shift;
            var width = layout.Widths[c];
            var right = left + width;
            if (c != layout.PinCount - 1 || !_pinned)
            {
                context.DrawLine(palette.GridPen, new Point(right - 0.5, y), new Point(right - 0.5, y + rowHeight));
            }
            var accessor = layout.Accessors[c];
            var column = _grid.ColumnOf(accessor);
            var numeric = column.IsNumeric;
            var inner = width - padding * 2;
            DataGridCellStyle? style = null;
            if (item is not null)
            {
                style = _grid.StyleOf(item, key, accessor, _grid.ValueAt(row, accessor));
                if (style is { } tinted)
                {
                    context.FillRectangle(palette.Tone(tinted.Tone).Fill, new Rect(left, y, width - 1, rowHeight - 1));
                }
            }
            if (finding && _grid.IsFindMatch(row, c))
            {
                context.FillRectangle(palette.WashFill, new Rect(left, y, width - 1, rowHeight - 1));
            }
            if (errors && _grid.ErrorOf(key, accessor) is not null)
            {
                context.DrawRectangle(null, palette.ErrorPen, new Rect(left + 1.5, y + 1.5, Math.Max(0, width - 4), Math.Max(0, rowHeight - 4)));
                context.DrawRectangle(palette.Tone(DataGridTone.Danger).Foreground, null, new Rect(right - 10, y + 4, 6, 6), 3, 3);
            }
            if (inner < 2)
            {
                continue;
            }
            if (column.EffectiveRenderer is { } renderer)
            {
                RenderWithRenderer(context, renderer, row, key, accessor, c, left, width, y, rowHeight, padding, version);
                continue;
            }
            var bold = style is { Bold: true };
            var text = _texts.Get(key, accessor, version, bold ? 1 : 0, new CellState(_grid, row, accessor), s_cellText, bold ? palette.Medium : palette.Regular,
                fontSize, style is { } toned ? palette.Tone(toned.Tone).Foreground : palette.ForegroundBrush, numeric, inner);
            if (text is null)
            {
                continue;
            }
            var x = numeric ? right - padding - Math.Min(text.WidthIncludingTrailingWhitespace, inner) : left + padding;
            text.Draw(context, new Point(x, Math.Round(y + (rowHeight - text.Height) / 2)));
        }
    }

    /// <summary>Slots of the text layouts a renderer asks for start here (cell text uses the column's accessor index as its slot).</summary>
    private const int RendererSlotBase = 4096;

    /// <summary>One line of text for a cell renderer, cached like the cell text; a null brush reuses whatever brush the layout has.</summary>
    internal TextLayout? RendererText(object key, int accessor, int part, int version, string text, double maxWidth, IBrush? brush, bool medium,
                                      double size)
    {
        var palette = _grid.Palette;
        return _texts.Get(key, accessor + RendererSlotBase * (part + 1), version, medium ? 1 : 0, text, static s => s,
            medium ? palette.Medium : palette.Regular, size, brush ?? palette.ForegroundBrush, false, maxWidth, anyBrush: brush is null);
    }

    private void RenderWithRenderer(DrawingContext context, DataGridCellRenderer renderer, int row, object key, int accessor, int column, double left,
                                    double width, double y, double rowHeight, double padding, int version)
    {
        var item = _grid.ItemOfRow(row);
        if (item is null)
        {
            return;
        }
        var text = _texts.StringOf(key, accessor, version, new CellState(_grid, row, accessor), s_cellText);
        Point? pointer = null;
        if (_viewport.HoverCell == (row, column) && _viewport.TranslatePoint(_viewport.HoverPoint, this) is { } local)
        {
            pointer = local;
        }
        var bounds = new Rect(left + padding, y, Math.Max(0, width - padding * 2), rowHeight);
        var render = new DataGridCellRenderContext(_grid, this, context, bounds, _grid.ValueAt(row, accessor), item, key, row, accessor, version, text,
            _grid.EffectiveCulture, _grid.RenderPalette, _grid.CellFontSize, _grid.SmallFontSize, pointer);
        using (context.PushClip(new Rect(left, y, width - 1, rowHeight - 1)))
        {
            renderer.Render(render);
        }
    }

    private void RenderGutter(DrawingContext context, int row, object? key, double y, double rowHeight, double gutter, bool isChecked,
                              DataGridPalette palette)
    {
        context.FillRectangle(isChecked ? palette.CheckedBrush : palette.SurfaceBrush, new Rect(0, y, gutter, rowHeight));
        context.DrawLine(palette.GridPen, new Point(0, y + rowHeight - 0.5), new Point(gutter, y + rowHeight - 0.5));
        context.DrawLine(palette.BorderPen, new Point(gutter - 0.5, y), new Point(gutter - 0.5, y + rowHeight));
        if (ShowsCheckbox(row))
        {
            DataGridDrawing.Checkbox(context, CheckboxRect(gutter, y, rowHeight), isChecked ? true : false, palette);
            return;
        }
        if (!_grid.ShowRowNumbers || key is null)
        {
            return;
        }
        var range = _grid.Range;
        var inRange = _grid.HasCells && row >= range.Row0 && row <= range.Row1;
        var text = _texts.Get(key, NumberSlot, row * 2 + (inRange ? 1 : 0), 0, (_grid, row), s_rowNumber, palette.Regular,
            _grid.SmallFontSize, inRange ? palette.ForegroundBrush : palette.MutedBrush, true, gutter - 4);
        if (text is not null)
        {
            text.Draw(context, new Point(Math.Round((gutter - text.WidthIncludingTrailingWhitespace) / 2),
                Math.Round(y + (rowHeight - text.Height) / 2)));
        }
    }

    private void RenderGroup(DrawingContext context, DataGridGroupNode node, double y, double rowHeight, DataGridLayout layout,
                             int firstColumn, int lastColumn, double shift, Size size, DataGridPalette palette)
    {
        if (y + rowHeight < 0 || y > size.Height)
        {
            return;
        }
        var left = _pinned ? 0 : firstColumn <= lastColumn ? layout.Lefts[firstColumn] + shift : 0;
        var right = firstColumn <= lastColumn ? layout.Right(lastColumn) + shift : _pinned ? layout.Gutter : 0;
        context.FillRectangle(palette.GroupBrush, new Rect(left, y, Math.Max(0, right - left), rowHeight));
        context.DrawLine(palette.GridPen, new Point(left, y + rowHeight - 0.5), new Point(right, y + rowHeight - 0.5));
        if (_pinned && layout.Gutter > 0)
        {
            context.DrawLine(palette.BorderPen, new Point(layout.Gutter - 0.5, y), new Point(layout.Gutter - 0.5, y + rowHeight));
        }
        var padding = _grid.CellInlinePadding;
        var version = _grid.EditVersion * 7 + _grid.ColumnsVersion;
        for (var c = firstColumn; c <= lastColumn; c++)
        {
            var cellLeft = layout.Lefts[c] + shift;
            var width = layout.Widths[c];
            var accessor = layout.Accessors[c];
            if (c == 0)
            {
                RenderGroupLabel(context, node, cellLeft, y, width, rowHeight, palette);
                continue;
            }
            var column = _grid.ColumnOf(accessor);
            if (column.EffectiveAggregate == DataGridAggregate.None)
            {
                continue;
            }
            var inner = width - padding * 2;
            var text = _texts.Get(node.Id, accessor, version, 1, new AggregateState(_grid, node, accessor), s_subtotalText, palette.Medium,
                _grid.CellFontSize, palette.SecondaryBrush, true, inner);
            if (text is null)
            {
                continue;
            }
            var x = column.IsNumeric || column.Type == DataGridColumnType.Date
                ? cellLeft + width - padding - Math.Min(text.WidthIncludingTrailingWhitespace, inner)
                : cellLeft + padding;
            text.Draw(context, new Point(x, Math.Round(y + (rowHeight - text.Height) / 2)));
        }
    }

    private void RenderGroupLabel(DrawingContext context, DataGridGroupNode node, double left, double y, double width, double rowHeight,
                                  DataGridPalette palette)
    {
        var indent = 4 + _grid.GroupIndent * node.Level;
        var expanded = !_grid.IsCollapsed(node);
        var angle = _chevrons.TryGetValue(node.Id, out var motion) ? motion.Value : expanded ? 90 : 0;
        var chevronCenter = new Point(left + indent + 10, y + rowHeight / 2);
        DataGridDrawing.Chevron(context, chevronCenter, angle, palette.ChevronPen);
        var textLeft = left + indent + 22;
        var available = width - (textLeft - left) - 8;
        var count = _texts.Get(node.Id, CountSlot, node.Rows.Length, 2, (_grid, node), s_groupCount, palette.Regular, _grid.SmallFontSize,
            palette.SecondaryBrush, true, 120);
        var countWidth = count is null ? 0 : count.WidthIncludingTrailingWhitespace + 12;
        var label = _texts.Get(node.Id, LabelSlot, _grid.ColumnsVersion, 3, (_grid, node), s_groupLabel, palette.Medium, _grid.CellFontSize,
            DataGridSorting.IsEmpty(node.Value) ? palette.MutedBrush : palette.ForegroundBrush, false, Math.Max(1, available - countWidth - 8));
        var x = textLeft;
        if (label is not null)
        {
            label.Draw(context, new Point(x, Math.Round(y + (rowHeight - label.Height) / 2)));
            x += Math.Min(label.WidthIncludingTrailingWhitespace, Math.Max(1, available - countWidth - 8)) + 8;
        }
        if (count is not null && x + countWidth <= left + width)
        {
            var badge = new Rect(x, Math.Round(y + (rowHeight - 18) / 2), countWidth, 18);
            context.DrawRectangle(palette.BadgeBrush, null, badge, 9, 9);
            count.Draw(context, new Point(badge.X + 6, Math.Round(badge.Y + (badge.Height - count.Height) / 2)));
        }
    }

    private void RenderPlaceholderCells(DrawingContext context, int row, double y, double rowHeight, DataGridLayout layout, int firstColumn,
                                        int lastColumn, double shift, DataGridPalette palette)
    {
        var padding = _grid.CellInlinePadding;
        for (var c = firstColumn; c <= lastColumn; c++)
        {
            var left = layout.Lefts[c] + shift;
            var width = layout.Widths[c];
            var share = 0.4 + (7 * row + (int)width) % 45 / 100.0;
            var bar = Math.Max(8, (width - padding * 2) * share);
            var x = _grid.ColumnOf(layout.Accessors[c]).IsNumeric ? left + width - padding - bar : left + padding;
            context.DrawRectangle(palette.SkeletonBrush, null, new Rect(x, Math.Round(y + (rowHeight - 10) / 2), bar, 10), 5, 5);
        }
    }

    private void RenderSkeleton(DrawingContext context, DataGridLayout layout, double rowHeight, Size size, DataGridPalette palette)
    {
        var (firstColumn, lastColumn, shift) = ColumnRange(layout, size.Width);
        var pulse = 1.0;
        if (_grid.IsMotionFull)
        {
            _pulse ??= new LiveLoop(this, dt =>
            {
                _pulseTime += dt;
                InvalidateVisual();
                return true;
            });
            _pulse.Start();
            var phase = _pulseTime % 2.8 / 1.4;
            var t = phase <= 1 ? phase : 2 - phase;
            pulse = 1 - 0.55 * ArcEasings.InOut.Ease(t);
        }
        else
        {
            StopPulse();
            pulse = 0.7;
        }
        using var opacity = context.PushOpacity(pulse);
        for (var i = 0; i < 6; i++)
        {
            var y = i * rowHeight;
            if (_pinned && layout.Gutter > 0)
            {
                context.FillRectangle(palette.SurfaceBrush, new Rect(0, y, layout.Gutter, rowHeight));
                context.DrawLine(palette.BorderPen, new Point(layout.Gutter - 0.5, y), new Point(layout.Gutter - 0.5, y + rowHeight));
            }
            if (_pinned && layout.PinEdge > 0)
            {
                context.DrawLine(palette.BorderPen, new Point(size.Width - 0.5, y), new Point(size.Width - 0.5, y + rowHeight));
            }
            if (firstColumn > lastColumn)
            {
                continue;
            }
            var left = layout.Lefts[firstColumn] + shift;
            var right = layout.Right(lastColumn) + shift;
            context.FillRectangle(palette.CellBrush, new Rect(left, y, right - left, rowHeight));
            context.DrawLine(palette.GridPen, new Point(left, y + rowHeight - 0.5), new Point(right, y + rowHeight - 0.5));
            RenderPlaceholderCells(context, i, y, rowHeight, layout, firstColumn, lastColumn, shift, palette);
        }
    }

    private void StopPulse()
    {
        if (_pulse is { IsActive: true })
        {
            _pulse.Stop();
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopPulse();
    }
}

/// <summary>Shapes the grid draws in several places: the row checkbox and the group chevron.</summary>
internal static class DataGridDrawing
{
    private static readonly Point[] s_check = [new(4, 8.25), new(6.75, 11), new(12, 5.5)];

    /// <summary>A 16px checkbox: checked (true), mixed (null) or unchecked (false).</summary>
    public static void Checkbox(DrawingContext context, Rect box, bool? state, DataGridPalette palette)
    {
        if (state == false)
        {
            context.DrawRectangle(palette.SurfaceBrush, palette.CheckboxPen, box.Deflate(0.5), 4, 4);
            return;
        }
        context.DrawRectangle(palette.ControlOnBrush, null, box, 4, 4);
        if (state is null)
        {
            context.DrawLine(palette.GlyphPen, new Point(box.X + 4.5, box.Center.Y), new Point(box.Right - 4.5, box.Center.Y));
            return;
        }
        var origin = box.TopLeft;
        context.DrawLine(palette.GlyphPen, s_check[0] + origin, s_check[1] + origin);
        context.DrawLine(palette.GlyphPen, s_check[1] + origin, s_check[2] + origin);
    }

    /// <summary>A small chevron pointing right at 0°, rotated by <paramref name="angle"/> degrees around <paramref name="center"/>.</summary>
    public static void Chevron(DrawingContext context, Point center, double angle, IPen pen)
    {
        var radians = angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        Point Rotate(double x, double y) => new(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);
        var a = Rotate(-1.75, -3.5);
        var b = Rotate(1.75, 0);
        var c = Rotate(-1.75, 3.5);
        context.DrawLine(pen, a, b);
        context.DrawLine(pen, b, c);
    }
}
