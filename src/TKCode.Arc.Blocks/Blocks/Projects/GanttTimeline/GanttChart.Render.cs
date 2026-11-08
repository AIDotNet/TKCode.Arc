using System.Globalization;
using Avalonia;
using Avalonia.Media;

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
    private const double TierOneY = 10;
    private const double TierTwoY = 30;

    private double RowY(int index, GanttRow row)
    {
        var target = index * GanttModel.RowHeight;
        if (_before.TryGetValue(RowId(row), out var s))
        {
            return s.Y + (target - s.Y) * _layoutT.Value;
        }
        if (row.Task is { } task)
        {
            var g = _model.Rows.ToList().FindIndex(r => r.Group?.Id == task.GroupId);
            var from = g >= 0 ? g * GanttModel.RowHeight : target;
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
        var line = new Pen(GridBrush, 1);
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
            var label = big ? cursor.Year.ToString(CultureInfo.InvariantCulture) : cursor.ToString("MMMM", CultureInfo.InvariantCulture);
            var ft = Text(label, 12, SecondaryBrush);
            var lx = Math.Max(x0, _scrollX) + 8;
            lx = Math.Min(lx, x1 - ft.Width - 8);
            // A label shows whole or not at all, and never under the today pill.
            var whole = lx >= _scrollX && lx + ft.Width <= _scrollX + ViewWidth;
            if (whole && lx >= x0 - 1 && !pill.Intersects(new Rect(lx, TierOneY, ft.Width, ft.Height)))
            {
                ctx.DrawText(ft, new Point(lx, TierOneY));
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
                label = d.Day.ToString(CultureInfo.InvariantCulture);
            }
            else if (_zoom == GanttZoom.Week && d.DayOfWeek == DayOfWeek.Monday)
            {
                label = d.Day.ToString(CultureInfo.InvariantCulture);
            }
            else if (big && d.Day == 1)
            {
                label = d.ToString("MMM", CultureInfo.InvariantCulture);
            }
            if (label is null)
            {
                continue;
            }
            var ft = Text(label, 13, muted);
            var tx = _zoom == GanttZoom.Day ? x + (dw - ft.Width) / 2 : x + 6;
            ctx.DrawText(ft, new Point(tx, TierTwoY));
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
        var gridPen = new Pen(GridBrush, 1);
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
            ctx.DrawLine(gridPen, new Point(a * dw, y + GanttModel.RowHeight - 0.5), new Point((b + 1) * dw, y + GanttModel.RowHeight - 0.5));
        }
        // The today line runs behind the arrows and the bars, so it never cuts through a bar's label.
        var todayX = TodayContentX();
        if (todayX >= 0)
        {
            ctx.DrawLine(new Pen(TodayBrush, 1), new Point(todayX, 0), new Point(todayX, bodyH));
        }
        DrawArrows(ctx);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var y = RowY(i, row);
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

    private void DrawSummary(DrawingContext ctx, GanttGroup g, double y)
    {
        if (_model.Summary(g) is not { } s)
        {
            return;
        }
        var x0 = XOf(s.Start);
        var x1 = XOf(s.End.AddDays(1));
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
        var cy = y + BarTop + BarHeight / 2;
        var conflict = _model.HasConflict(t);
        var hot = IsHot(t.Id);
        var pen = new Pen(conflict ? ConflictBrush : hot ? TextBrush : BarEdgeBrush, hot || conflict ? 1.5 : 1);
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
            var percent = ((int)Math.Round(p * 100)).ToString(CultureInfo.InvariantCulture) + "%";
            if (rect.Width >= 96)
            {
                var pct = Text(percent, 13, SecondaryBrush);
                var room = rect.Width - BarTextInset * 2 - pct.Width - 8;
                var ft = Text(t.Name, 13, TextBrush, FontWeight.Medium, room);
                ctx.DrawText(ft, new Point(rect.X + BarTextInset, cy - ft.Height / 2));
                ctx.DrawText(pct, new Point(rect.Right - BarTextInset - pct.Width, cy - pct.Height / 2));
            }
            else if (rect.Width >= 36)
            {
                var pct = Text(percent, 12, SecondaryBrush, FontWeight.Normal, rect.Width - 8);
                ctx.DrawText(pct, new Point(rect.X + (rect.Width - pct.Width) / 2, cy - pct.Height / 2));
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
            ctx.DrawRectangle(null, new Pen(FocusBrush, 2), new RoundedRect(rect, radius));
        }
    }

    private void DrawDateTag(DrawingContext ctx, DragState d, Rect bar)
    {
        var (s, e) = Snapped(d);
        var label = d.Task.IsMilestone || s == e
            ? s.ToString("MMM d", CultureInfo.InvariantCulture)
            : s.ToString("MMM d", CultureInfo.InvariantCulture) + " – " + e.ToString("MMM d", CultureInfo.InvariantCulture);
        var ft = Text(label, 11, TodayTextBrush, FontWeight.Medium);
        var pill = new Rect(bar.X, bar.Y - 22, ft.Width + 14, 18);
        ctx.DrawRectangle(TodayBrush, null, new RoundedRect(pill, 9));
        ctx.DrawText(ft, new Point(pill.X + 7, pill.Y + (pill.Height - ft.Height) / 2));
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
        var pen = new Pen(TextBrush, 1, new DashStyle([3, 3], 0));
        ctx.DrawRectangle(null, pen, new RoundedRect(rect, BarRadius));
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
        var ft = Text("Today", 12, TodayTextBrush, FontWeight.Medium);
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
        ctx.DrawLine(new Pen(TodayBrush, 1), new Point(x, 22), new Point(x, hh));
        var ft = Text("Today", 12, TodayTextBrush, FontWeight.Medium);
        var pill = TodayPill(x);
        ctx.DrawRectangle(TodayBrush, null, new RoundedRect(pill, 9));
        ctx.DrawText(ft, new Point(pill.X + 8, pill.Y + (pill.Height - ft.Height) / 2));
    }

    private void DrawArrows(DrawingContext ctx)
    {
        var rows = _model.Rows;
        var index = new Dictionary<string, int>();
        for (var i = 0; i < rows.Count; i++)
        {
            index[RowId(rows[i])] = i;
        }
        foreach (var link in _model.Links)
        {
            if (!TryArrowEnd(link.Dependency, link.FoldedDependency, index, out _, out var fromX, out var fromY)
                || !TryArrowEnd(link.Task, link.FoldedTask, index, out var toX, out _, out var toY))
            {
                continue;
            }
            var dep = link.Dependency;
            var conflict = link.Task.Start <= (dep.IsMilestone ? dep.Start : dep.End);
            var hot = IsHot(link.Task.Id) || IsHot(dep.Id);
            var brush = conflict ? ConflictBrush : hot ? TextBrush : ArrowBrush;
            DrawArrow(ctx, new Pen(brush, hot || conflict ? 1.5 : 1), fromX, fromY, toX, toY);
        }
    }

    /// <summary>The left and right tips an arrow can attach to: the task's bar or diamond, or the summary bar of its folded phase.</summary>
    private bool TryArrowEnd(GanttTask task, GanttGroup? folded, Dictionary<string, int> index, out double x0, out double x1, out double y)
    {
        x0 = x1 = y = 0;
        var rows = _model.Rows;
        if (folded is not null)
        {
            if (!index.TryGetValue("g:" + folded.Id, out var g) || _model.Summary(folded) is not { } s)
            {
                return false;
            }
            x0 = XOf(s.Start);
            x1 = XOf(s.End.AddDays(1));
            y = RowY(g, rows[g]) + GanttModel.RowHeight / 2;
            return true;
        }
        if (!index.TryGetValue("t:" + task.Id, out var i))
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

    private static void DrawDiamond(DrawingContext ctx, IBrush? brush, IPen? pen, Point c, double r)
    {
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(new Point(c.X, c.Y - r), true);
            gc.LineTo(new Point(c.X + r, c.Y));
            gc.LineTo(new Point(c.X, c.Y + r));
            gc.LineTo(new Point(c.X - r, c.Y));
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(brush, pen, g);
    }

    private static void DrawArrow(DrawingContext ctx, Pen pen, double x1, double y1, double x2, double y2)
    {
        var pts = new List<Point> { new(x1, y1) };
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
        }
        ctx.DrawGeometry(null, pen, g);
        var head = new StreamGeometry();
        using (var hc = head.Open())
        {
            hc.BeginFigure(new Point(x2 - ArrowHead, y2 - ArrowHeadHalf), false);
            hc.LineTo(new Point(x2, y2));
            hc.LineTo(new Point(x2 - ArrowHead, y2 + ArrowHeadHalf));
            hc.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(pen.Brush, pen.Thickness, null, PenLineCap.Round, PenLineJoin.Round), head);
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
        var tasks = Text("Tasks", 13, MutedBrush);
        using (ctx.PushClip(new Rect(0, hh, colW - 1, Math.Max(0, h - hh))))
        {
            var gridPen = new Pen(GridBrush, 1);
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
                        var count = Text(row.GroupTaskCount.ToString(CultureInfo.InvariantCulture), 12, MutedBrush);
                        var name = Text(g.Name, 14, TextBrush, FontWeight.Medium, colW - 36 - count.Width - 30);
                        ctx.DrawText(name, new Point(36, cy - name.Height / 2));
                        ctx.DrawText(count, new Point(colW - 16 - count.Width, cy - count.Height / 2));
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
                        var ft = Text(t.Name, 14, IsHot(t.Id) ? TextBrush : SecondaryBrush, FontWeight.Normal, colW - left - 12);
                        ctx.DrawText(ft, new Point(left, cy - ft.Height / 2));
                    }
                }
                ctx.DrawLine(gridPen, new Point(0, y + GanttModel.RowHeight - 0.5), new Point(colW, y + GanttModel.RowHeight - 0.5));
            }
        }
        ctx.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, colW - 1, hh - 1));
        ctx.DrawText(tasks, new Point(16, TierTwoY));
    }

    private void DrawChevron(DrawingContext ctx, double cx, double cy, bool collapsed)
    {
        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            if (collapsed)
            {
                gc.BeginFigure(new Point(cx - 2, cy - 4), false);
                gc.LineTo(new Point(cx + 2, cy));
                gc.LineTo(new Point(cx - 2, cy + 4));
            }
            else
            {
                gc.BeginFigure(new Point(cx - 4, cy - 2), false);
                gc.LineTo(new Point(cx, cy + 2));
                gc.LineTo(new Point(cx + 4, cy - 2));
            }
            gc.EndFigure(false);
        }
        ctx.DrawGeometry(null, new Pen(MutedBrush, 1.5, null, PenLineCap.Round, PenLineJoin.Round), g);
    }
}
