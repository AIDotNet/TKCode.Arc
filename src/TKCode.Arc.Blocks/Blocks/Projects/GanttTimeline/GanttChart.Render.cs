using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

internal sealed partial class GanttChart
{
    private const double BarTop = 8;
    private const double ArrowCorner = 5;
    private const double ArrowOut = 11;
    private const double ArrowMinOut = 4;
    private const double ArrowHead = 6;
    private const double ArrowHeadHalf = 4.5;
    private const double MilestoneRadius = 8;
    private const double BarTextInset = 10;
    private const double BarFillOpacity = 0.7;
    private const double TextFitStep = 4;
    private const double ViewMargin = 24;
    private const double TierOneY = 10;
    private const double TierTwoY = 30;

    private static readonly string[] s_dayText = Enumerable.Range(0, 32).Select(d => d.ToString(CultureInfo.InvariantCulture)).ToArray();
    private static readonly string[] s_percentText = Enumerable.Range(0, 101).Select(d => d.ToString(CultureInfo.InvariantCulture) + "%").ToArray();
    private static readonly string[] s_monthText = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;
    private static readonly string[] s_monthShortText = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;

    // Everything below is asked for many times in every frame of a zoom or a drag, so none of it is made twice.
    private readonly Dictionary<(IBrush? Brush, double Thickness, bool Round), IPen> _pens = [];
    private readonly Dictionary<int, string> _numberText = [];
    private readonly Dictionary<double, StreamGeometry> _diamonds = [];
    private readonly Dictionary<(string Task, string Dependency), ArrowShape> _arrows = [];
    private StreamGeometry? _chevronOpen;
    private StreamGeometry? _chevronClosed;
    private static readonly ImmutableDashStyle s_snapDash = new([3, 3], 0);
    private IPen? _snapPen;
    private IBrush? _snapPenBrush;

    private sealed record ArrowShape(double X1, double Y1, double X2, double Y2, StreamGeometry Geometry);

    private IPen PenOf(IBrush? brush, double thickness, bool round = false)
    {
        var key = (brush, thickness, round);
        if (!_pens.TryGetValue(key, out var pen))
        {
            if (_pens.Count > 32)
            {
                _pens.Clear();
            }
            pen = round ? new Pen(brush, thickness, null, PenLineCap.Round, PenLineJoin.Round) : new Pen(brush, thickness);
            _pens[key] = pen;
        }
        return pen;
    }

    /// <summary>The dashed outline of the snap target, remade only when the text brush changes.</summary>
    private IPen SnapPen()
    {
        if (_snapPen is null || !ReferenceEquals(_snapPenBrush, TextBrush))
        {
            _snapPenBrush = TextBrush;
            _snapPen = new Pen(_snapPenBrush, 1, s_snapDash);
        }
        return _snapPen;
    }

    private string NumberText(int value)
    {
        if (!_numberText.TryGetValue(value, out var text))
        {
            text = value.ToString(CultureInfo.InvariantCulture);
            _numberText[value] = text;
        }
        return text;
    }

    /// <summary>The text whole when it fits; otherwise trimmed to a width on a coarse step, so a bar that changes width every frame does not lay its name out every frame.</summary>
    private AnalyticsText TextFit(string text, double size, FontWeight weight, double room)
    {
        var whole = Text(text, size, weight);
        return whole.Width <= room ? whole : Text(text, size, weight, Math.Max(1, Math.Floor(room / TextFitStep) * TextFitStep));
    }

    private double RowY(int index, GanttRow row)
    {
        var target = index * GanttModel.RowHeight;
        if (_layoutT.Value >= 1)
        {
            return target;
        }
        if (_before.TryGetValue(RowId(row), out var s))
        {
            return s.Y + (target - s.Y) * _layoutT.Value;
        }
        if (row.Task is { } task)
        {
            var from = _model.GroupRows.TryGetValue(task.GroupId, out var g) ? g * GanttModel.RowHeight : target;
            return from + (target - from) * _layoutT.Value;
        }
        return target;
    }

    private double RowOpacity(GanttRow row) => row.Task is not null && !_before.ContainsKey(RowId(row)) ? Math.Clamp(_layoutT.Value, 0, 1) : 1;

    private (double X0, double X1) BarX(GanttTask task, GanttRow row)
    {
        var x0 = XOf(task.Start);
        var x1 = XOf(task.IsMilestone ? task.Start.AddDays(1) : task.End.AddDays(1));
        if (_before.TryGetValue(RowId(row), out var s) && _layoutT.Value < 1 && s.Present && s.X1 > s.X0 - 0.5)
        {
            x0 = s.X0 + (x0 - s.X0) * _layoutT.Value;
            x1 = s.X1 + (x1 - s.X1) * _layoutT.Value;
        }
        if (_drag is { } d && d.Task.Id == task.Id)
        {
            (x0, x1) = DragBar(d);
        }
        else if (_releaseId == task.Id)
        {
            var off = _release.Value;
            switch (_releaseMode)
            {
                case DragMode.Move:
                    x0 += off;
                    x1 += off;
                    break;
                case DragMode.ResizeStart:
                    x0 += off;
                    break;
                case DragMode.ResizeEnd:
                    x1 += off;
                    break;
            }
        }
        return (x0, x1);
    }

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
        {
            return;
        }
        ctx.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, w, h));
        var colW = ColumnWidth;
        var hh = HeaderHeight;
        var cal = new Rect(colW, 0, Math.Max(0, w - colW), h);
        using (ctx.PushClip(cal))
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(colW - _scrollX, 0)))
            {
                DrawHeader(ctx);
            }
            using (ctx.PushClip(new Rect(colW, hh, cal.Width, Math.Max(0, h - hh))))
            {
                using (ctx.PushTransform(Matrix.CreateTranslation(colW - _scrollX, hh - _scrollY)))
                {
                    DrawBody(ctx);
                }
            }
            DrawToday(ctx, colW, hh, h);
        }
        DrawColumn(ctx, colW, hh, h);
        var line = PenOf(GridBrush, 1);
        ctx.DrawLine(line, new Point(0, hh - 0.5), new Point(w, hh - 0.5));
        ctx.DrawLine(line, new Point(colW - 0.5, 0), new Point(colW - 0.5, h));
    }

    private (int FirstDay, int LastDay) VisibleDays()
    {
        var dw = Math.Max(1, DayPx);
        var a = (int)Math.Floor(_scrollX / dw) - 1;
        var b = (int)Math.Ceiling((_scrollX + ViewWidth) / dw) + 1;
        // The grid runs to the edge of the view even when the plan is shorter than the view (month zoom on a short plan).
        var total = Math.Max(_last.DayNumber - _first.DayNumber, (int)Math.Ceiling(ViewWidth / dw) + 1);
        return (Math.Clamp(a, 0, total), Math.Clamp(b, 0, total));
    }

    private void DrawHeader(DrawingContext ctx)
    {
        var (a, b) = VisibleDays();
        var dw = DayPx;
        var big = _zoom == GanttZoom.Month;
        var muted = MutedBrush;
        var pill = TodayPill(TodayContentX());
        // Tier 1: months (years at month zoom), sticky at the left edge; the tier 2 labels crossfade with the scale.
        var firstDate = _first.AddDays(a);
        var lastDate = _first.AddDays(b);
        var cursor = new DateOnly(firstDate.Year, big ? 1 : firstDate.Month, 1);
        while (cursor <= lastDate)
        {
            var next = big ? cursor.AddYears(1) : cursor.AddMonths(1);
            var x0 = XOf(cursor);
            var x1 = XOf(next);
            var label = big ? NumberText(cursor.Year) : s_monthText[cursor.Month - 1];
            var ft = Text(label, 12);
            var lx = Math.Max(x0, _scrollX) + 8;
            lx = Math.Min(lx, x1 - ft.Width - 8);
            // A label shows whole or not at all, and never under the today pill.
            var whole = lx >= _scrollX && lx + ft.Width <= _scrollX + ViewWidth;
            if (whole && lx >= x0 - 1 && !pill.Intersects(new Rect(lx, TierOneY, ft.Width, ft.Height)))
            {
                DrawText(ctx, ft, SecondaryBrush, new Point(lx, TierOneY));
            }
            cursor = next;
        }
        // Tier 2: day numbers, Monday numbers, or month names.
        for (var i = a; i <= b; i++)
        {
            var d = _first.AddDays(i);
            var x = i * dw;
            string? label = null;
            if (_zoom == GanttZoom.Day)
            {
                label = s_dayText[d.Day];
            }
            else if (_zoom == GanttZoom.Week && d.DayOfWeek == DayOfWeek.Monday)
            {
                label = s_dayText[d.Day];
            }
            else if (big && d.Day == 1)
            {
                label = s_monthShortText[d.Month - 1];
            }
            if (label is null)
            {
                continue;
            }
            var ft = Text(label, 13);
            var tx = _zoom == GanttZoom.Day ? x + (dw - ft.Width) / 2 : x + 6;
            DrawText(ctx, ft, muted, new Point(tx, TierTwoY));
        }
    }

    private void DrawBody(DrawingContext ctx)
    {
        var dw = DayPx;
        var (a, b) = VisibleDays();
        var bodyH = Math.Max(ContentHeight, Bounds.Height - HeaderHeight + _scrollY);
        // Weekend shading and week lines.
        if (_zoom != GanttZoom.Month)
        {
            for (var i = a; i <= b; i++)
            {
                var d = _first.AddDays(i);
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    ctx.DrawRectangle(WeekendBrush, null, new Rect(i * dw, 0, dw, bodyH));
                }
            }
        }
        var gridPen = PenOf(GridBrush, 1);
        for (var i = a; i <= b; i++)
        {
            var d = _first.AddDays(i);
            var line = _zoom == GanttZoom.Day || (_zoom == GanttZoom.Week && d.DayOfWeek == DayOfWeek.Monday) || (_zoom == GanttZoom.Month && d.Day == 1);
            if (line)
            {
                ctx.DrawLine(gridPen, new Point(i * dw + 0.5, 0), new Point(i * dw + 0.5, bodyH));
            }
        }
        var rows = _model.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var y = RowY(i, rows[i]);
            if (!RowInView(y))
            {
                continue;
            }
            ctx.DrawLine(gridPen, new Point(a * dw, y + GanttModel.RowHeight - 0.5), new Point((b + 1) * dw, y + GanttModel.RowHeight - 0.5));
        }
        // The today line runs behind the arrows and the bars, so it never cuts through a bar's label.
        var todayX = TodayContentX();
        if (todayX >= 0)
        {
            ctx.DrawLine(PenOf(TodayBrush, 1), new Point(todayX, 0), new Point(todayX, bodyH));
        }
        DrawArrows(ctx);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = RowY(i, row);
            if (!RowInView(y))
            {
                continue;
            }
            var opacity = RowOpacity(row);
            using (ctx.PushOpacity(opacity))
            {
                if (row.Group is { } g)
                {
                    DrawSummary(ctx, g, y);
                }
                else if (row.Task is { } t)
                {
                    DrawBar(ctx, t, row, y);
                }
            }
        }
        if (_drag is { } d2)
        {
            DrawSnapTarget(ctx, d2);
        }
    }

    /// <summary>Whether a row at <paramref name="y"/> shows, with room for the date tag a dragged bar raises above itself.</summary>
    private bool RowInView(double y) => y + GanttModel.RowHeight + ViewMargin >= _scrollY && y - ViewMargin <= _scrollY + ViewHeight;

    private bool ColumnInView(double x0, double x1) => x1 + ViewMargin >= _scrollX && x0 - ViewMargin <= _scrollX + ViewWidth;

    private void DrawSummary(DrawingContext ctx, GanttGroup g, double y)
    {
        if (_model.Summary(g) is not { } s)
        {
            return;
        }
        var x0 = XOf(s.Start);
        var x1 = XOf(s.End.AddDays(1));
        if (!ColumnInView(x0, x1))
        {
            return;
        }
        var rect = new Rect(x0, y + (GanttModel.RowHeight - 6) / 2, Math.Max(2, x1 - x0), 6);
        ctx.DrawRectangle(SummaryTrackBrush, null, new RoundedRect(rect, 3));
        var fill = rect.Width * Math.Clamp(s.Progress, 0, 1);
        if (fill > 0.5)
        {
            ctx.DrawRectangle(SummaryBrush, null, new RoundedRect(new Rect(rect.X, rect.Y, fill, rect.Height), 3));
        }
    }

    private void DrawBar(DrawingContext ctx, GanttTask t, GanttRow row, double y)
    {
        var (x0, x1) = BarX(t, row);
        if (!ColumnInView(x0 - MilestoneRadius, x1 + MilestoneRadius))
        {
            return;
        }
        var cy = y + BarTop + BarHeight / 2;
        var conflict = _model.HasConflict(t);
        var hot = IsHot(t.Id);
        var pen = PenOf(conflict ? ConflictBrush : hot ? TextBrush : BarEdgeBrush, hot || conflict ? 1.5 : 1);
        if (t.IsMilestone)
        {
            var c = new Point((x0 + x1) / 2, cy);
            var r = MilestoneRadius;
            DrawDiamond(ctx, conflict ? ConflictBrush : SummaryBrush, hot ? pen : null, c, r);
            DrawFocus(ctx, t, new Rect(c.X - r - 3, c.Y - r - 3, r * 2 + 6, r * 2 + 6), r + 3);
            return;
        }
        var rect = new Rect(x0, y + BarTop, Math.Max(2, x1 - x0), BarHeight);
        var rr = new RoundedRect(rect, BarRadius);
        ctx.DrawRectangle(BarBrush, null, rr);
        var p = Math.Clamp(t.Progress, 0, 1);
        using (ctx.PushClip(rr))
        {
            if (p > 0)
            {
                using (ctx.PushOpacity(BarFillOpacity))
                {
                    ctx.DrawRectangle(BarFillBrush, null, new Rect(rect.X, rect.Y, rect.Width * p, rect.Height));
                }
            }
            var percent = s_percentText[(int)Math.Round(p * 100)];
            if (rect.Width >= 96)
            {
                var pct = Text(percent, 13);
                var room = rect.Width - BarTextInset * 2 - pct.Width - 8;
                var ft = TextFit(t.Name, 13, FontWeight.Medium, room);
                DrawText(ctx, ft, TextBrush, new Point(rect.X + BarTextInset, cy - ft.Height / 2));
                DrawText(ctx, pct, SecondaryBrush, new Point(rect.Right - BarTextInset - pct.Width, cy - pct.Height / 2));
            }
            else if (rect.Width >= 36)
            {
                var pct = TextFit(percent, 12, FontWeight.Normal, rect.Width - 8);
                DrawText(ctx, pct, SecondaryBrush, new Point(rect.X + (rect.Width - pct.Width) / 2, cy - pct.Height / 2));
            }
        }
        // The edge goes over the fill, so a filled bar keeps its outline.
        ctx.DrawRectangle(null, pen, rr);
        DrawFocus(ctx, t, rect.Inflate(3), BarRadius + 3);
        if (_drag is { } d && d.Task.Id == t.Id)
        {
            DrawDateTag(ctx, d, rect);
        }
    }

    private void DrawFocus(DrawingContext ctx, GanttTask t, Rect rect, double radius)
    {
        if (_keyboardFocus && IsFocused && _focusId == t.Id)
        {
            ctx.DrawRectangle(null, PenOf(FocusBrush, 2), new RoundedRect(rect, radius));
        }
    }

    private void DrawDateTag(DrawingContext ctx, DragState d, Rect bar)
    {
        var (s, e) = Snapped(d);
        var label = d.Task.IsMilestone || s == e
            ? s.ToString("MMM d", CultureInfo.InvariantCulture)
            : s.ToString("MMM d", CultureInfo.InvariantCulture) + " – " + e.ToString("MMM d", CultureInfo.InvariantCulture);
        var ft = Text(label, 11, FontWeight.Medium);
        var pill = new Rect(bar.X, bar.Y - 22, ft.Width + 14, 18);
        ctx.DrawRectangle(TodayBrush, null, new RoundedRect(pill, 9));
        DrawText(ctx, ft, TodayTextBrush, new Point(pill.X + 7, pill.Y + (pill.Height - ft.Height) / 2));
    }

    private void DrawSnapTarget(DrawingContext ctx, DragState d)
    {
        var (s, e) = Snapped(d);
        var rows = _model.Rows;
        var idx = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Task?.Id == d.Task.Id)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0)
        {
            return;
        }
        var x0 = XOf(s);
        var x1 = XOf(d.Task.IsMilestone ? s.AddDays(1) : e.AddDays(1));
        var rect = new Rect(x0, idx * GanttModel.RowHeight + BarTop, Math.Max(2, x1 - x0), BarHeight);
        ctx.DrawRectangle(null, SnapPen(), new RoundedRect(rect, BarRadius));
    }

    private bool IsHot(string id) => _selectedId == id || _hoverId == id;

    /// <summary>The today line in content coordinates, on a whole pixel of the screen so it stays one crisp pixel wide.</summary>
    private double TodayContentX()
    {
        var screen = ColumnWidth + XOf(_today) + DayPx / 2 - _scrollX;
        return Math.Round(screen) + 0.5 - ColumnWidth + _scrollX;
    }

    private Rect TodayPill(double x)
    {
        var ft = Text("Today", 12, FontWeight.Medium);
        return new Rect(x - (ft.Width + 16) / 2, 6, ft.Width + 16, 18);
    }

    /// <summary>The pill and the stretch of the line inside the header; the body draws the rest under the bars.</summary>
    private void DrawToday(DrawingContext ctx, double colW, double hh, double h)
    {
        var x = colW + TodayContentX() - _scrollX;
        if (x < colW || x > Bounds.Width)
        {
            return;
        }
        ctx.DrawLine(PenOf(TodayBrush, 1), new Point(x, 22), new Point(x, hh));
        var ft = Text("Today", 12, FontWeight.Medium);
        var pill = TodayPill(x);
        ctx.DrawRectangle(TodayBrush, null, new RoundedRect(pill, 9));
        DrawText(ctx, ft, TodayTextBrush, new Point(pill.X + 8, pill.Y + (pill.Height - ft.Height) / 2));
    }

    private void DrawArrows(DrawingContext ctx)
    {
        if (_arrows.Count > _model.Links.Count * 2 + 16)
        {
            _arrows.Clear();
        }
        foreach (var link in _model.Links)
        {
            if (!TryArrowEnd(link.Dependency, link.FoldedDependency, out _, out var fromX, out var fromY)
                || !TryArrowEnd(link.Task, link.FoldedTask, out var toX, out _, out var toY))
            {
                continue;
            }
            // An arrow's box: its trunk stays between its ends, and a tight one steps back by a little more than the margin covers.
            if (!ColumnInView(Math.Min(fromX, toX), Math.Max(fromX, toX) + ArrowOut)
                || Math.Max(fromY, toY) + ViewMargin < _scrollY
                || Math.Min(fromY, toY) - ViewMargin > _scrollY + ViewHeight)
            {
                continue;
            }
            var dep = link.Dependency;
            var conflict = link.Task.Start <= (dep.IsMilestone ? dep.Start : dep.End);
            var hot = IsHot(link.Task.Id) || IsHot(dep.Id);
            var brush = conflict ? ConflictBrush : hot ? TextBrush : ArrowBrush;
            ctx.DrawGeometry(null, PenOf(brush, hot || conflict ? 1.5 : 1, round: true), ArrowGeometry(link, fromX, fromY, toX, toY));
        }
    }

    /// <summary>
    /// The arrow as one figure pair, kept until an end moves: hovering, scrolling or dragging another bar leaves it alone.
    /// </summary>
    private StreamGeometry ArrowGeometry(in GanttLink link, double x1, double y1, double x2, double y2)
    {
        var key = (link.Task.Id, link.Dependency.Id);
        if (_arrows.TryGetValue(key, out var shape) && shape.X1 == x1 && shape.Y1 == y1 && shape.X2 == x2 && shape.Y2 == y2)
        {
            return shape.Geometry;
        }
        var geometry = BuildArrow(x1, y1, x2, y2);
        _arrows[key] = new ArrowShape(x1, y1, x2, y2, geometry);
        return geometry;
    }

    /// <summary>The left and right tips an arrow can attach to: the task's bar or diamond, or the summary bar of its folded phase.</summary>
    private bool TryArrowEnd(GanttTask task, GanttGroup? folded, out double x0, out double x1, out double y)
    {
        x0 = x1 = y = 0;
        var rows = _model.Rows;
        if (folded is not null)
        {
            if (!_model.GroupRows.TryGetValue(folded.Id, out var g) || _model.Summary(folded) is not { } s)
            {
                return false;
            }
            x0 = XOf(s.Start);
            x1 = XOf(s.End.AddDays(1));
            y = RowY(g, rows[g]) + GanttModel.RowHeight / 2;
            return true;
        }
        if (!_model.TaskRows.TryGetValue(task.Id, out var i))
        {
            return false;
        }
        (x0, x1) = BarX(task, rows[i]);
        y = RowY(i, rows[i]) + BarTop + BarHeight / 2;
        if (task.IsMilestone)
        {
            var c = (x0 + x1) / 2;
            x0 = c - MilestoneRadius;
            x1 = c + MilestoneRadius;
        }
        return true;
    }

    /// <summary>A diamond around the origin, drawn in place by a transform, so each size is built once.</summary>
    private void DrawDiamond(DrawingContext ctx, IBrush? brush, IPen? pen, Point c, double r)
    {
        if (!_diamonds.TryGetValue(r, out var g))
        {
            g = new StreamGeometry();
            using (var gc = g.Open())
            {
                gc.BeginFigure(new Point(0, -r), true);
                gc.LineTo(new Point(r, 0));
                gc.LineTo(new Point(0, r));
                gc.LineTo(new Point(-r, 0));
                gc.EndFigure(true);
            }
            _diamonds[r] = g;
        }
        using (ctx.PushTransform(Matrix.CreateTranslation(c.X, c.Y)))
        {
            ctx.DrawGeometry(brush, pen, g);
        }
    }

    private static StreamGeometry BuildArrow(double x1, double y1, double x2, double y2)
    {
        var pts = new List<Point>(6) { new(x1, y1) };
        var gap = x2 - x1;
        if (gap >= ArrowHead + ArrowMinOut + 2)
        {
            // Out of the source, down a shared trunk, into the target: tasks fed by one source branch off one line.
            // A one-day gap at week zoom still fits this shape, with the trunk nearer the source.
            var trunk = x1 + Math.Min(ArrowOut, gap - ArrowHead - 2);
            pts.Add(new Point(trunk, y1));
            pts.Add(new Point(trunk, y2));
        }
        else
        {
            // No room between the bars: step back along the row boundary and come in from the left.
            var out1 = x1 + 8;
            var mid = y1 + Math.Sign(y2 - y1 == 0 ? 1 : y2 - y1) * GanttModel.RowHeight / 2;
            pts.Add(new Point(out1, y1));
            pts.Add(new Point(out1, mid));
            pts.Add(new Point(x2 - ArrowHead - 4, mid));
            pts.Add(new Point(x2 - ArrowHead - 4, y2));
        }
        pts.Add(new Point(x2, y2));
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(pts[0], false);
            for (var i = 1; i < pts.Count - 1; i++)
            {
                var prev = pts[i - 1];
                var cur = pts[i];
                var next = pts[i + 1];
                var r = Math.Min(ArrowCorner, Math.Min(Dist(prev, cur), Dist(cur, next)) / 2);
                if (r < 0.5)
                {
                    gc.LineTo(cur);
                    continue;
                }
                var a = Toward(cur, prev, r);
                var b = Toward(cur, next, r);
                gc.LineTo(a);
                gc.QuadraticBezierTo(cur, b);
            }
            gc.LineTo(pts[^1]);
            gc.EndFigure(false);
            gc.BeginFigure(new Point(x2 - ArrowHead, y2 - ArrowHeadHalf), false);
            gc.LineTo(new Point(x2, y2));
            gc.LineTo(new Point(x2 - ArrowHead, y2 + ArrowHeadHalf));
            gc.EndFigure(false);
        }
        return g;
    }

    private static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static Point Toward(Point from, Point to, double r)
    {
        var d = Dist(from, to);
        return d < 0.001 ? from : new Point(from.X + (to.X - from.X) * r / d, from.Y + (to.Y - from.Y) * r / d);
    }

    private void DrawColumn(DrawingContext ctx, double colW, double hh, double h)
    {
        ctx.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, colW - 1, h));
        var tasks = Text("Tasks", 13);
        using (ctx.PushClip(new Rect(0, hh, colW - 1, Math.Max(0, h - hh))))
        {
            var gridPen = PenOf(GridBrush, 1);
            var rows = _model.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var y = hh + RowY(i, row) - _scrollY;
                if (y > h || y + GanttModel.RowHeight < hh)
                {
                    continue;
                }
                using (ctx.PushOpacity(RowOpacity(row)))
                {
                    var cy = y + GanttModel.RowHeight / 2;
                    if (row.Task is { } hotTask && IsHot(hotTask.Id))
                    {
                        ctx.DrawRectangle(RowHotBrush, null, new Rect(0, y, colW - 1, GanttModel.RowHeight));
                    }
                    if (row.Group is { } g)
                    {
                        DrawChevron(ctx, 19, cy, row.IsCollapsed);
                        var count = Text(NumberText(row.GroupTaskCount), 12);
                        var name = Text(g.Name, 14, FontWeight.Medium, colW - 36 - count.Width - 30);
                        DrawText(ctx, name, TextBrush, new Point(36, cy - name.Height / 2));
                        DrawText(ctx, count, MutedBrush, new Point(colW - 16 - count.Width, cy - count.Height / 2));
                    }
                    else if (row.Task is { } t)
                    {
                        var left = 36.0;
                        if (t.IsMilestone)
                        {
                            DrawDiamond(ctx, MutedBrush, null, new Point(left + 11, cy), 5);
                            left += 32;
                        }
                        else if (t.Assignee is { } p)
                        {
                            ctx.DrawEllipse(AvatarBrush(p), null, new Point(left + 11, cy), 11, 11);
                            left += 32;
                        }
                        var ft = Text(t.Name, 14, FontWeight.Normal, colW - left - 12);
                        DrawText(ctx, ft, IsHot(t.Id) ? TextBrush : SecondaryBrush, new Point(left, cy - ft.Height / 2));
                    }
                }
                ctx.DrawLine(gridPen, new Point(0, y + GanttModel.RowHeight - 0.5), new Point(colW, y + GanttModel.RowHeight - 0.5));
            }
        }
        ctx.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, colW - 1, hh - 1));
        DrawText(ctx, tasks, MutedBrush, new Point(16, TierTwoY));
    }

    private void DrawChevron(DrawingContext ctx, double cx, double cy, bool collapsed)
    {
        var g = collapsed ? _chevronClosed : _chevronOpen;
        if (g is null)
        {
            g = new StreamGeometry();
            using (var gc = g.Open())
            {
                if (collapsed)
                {
                    gc.BeginFigure(new Point(-2, -4), false);
                    gc.LineTo(new Point(2, 0));
                    gc.LineTo(new Point(-2, 4));
                }
                else
                {
                    gc.BeginFigure(new Point(-4, -2), false);
                    gc.LineTo(new Point(0, 2));
                    gc.LineTo(new Point(4, -2));
                }
                gc.EndFigure(false);
            }
            if (collapsed)
            {
                _chevronClosed = g;
            }
            else
            {
                _chevronOpen = g;
            }
        }
        using (ctx.PushTransform(Matrix.CreateTranslation(cx, cy)))
        {
            ctx.DrawGeometry(null, PenOf(MutedBrush, 1.5, round: true), g);
        }
    }
}
