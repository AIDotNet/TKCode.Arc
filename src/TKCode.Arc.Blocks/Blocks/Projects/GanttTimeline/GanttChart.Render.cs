using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

internal sealed partial class GanttChart
{
    private const double BarTop = 8;
    private const double ArrowCorner = 4;

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
        var total = _last.DayNumber - _first.DayNumber;
        return (Math.Clamp(a, 0, total), Math.Clamp(b, 0, total));
    }

    private void DrawHeader(DrawingContext ctx)
    {
        var (a, b) = VisibleDays();
        var dw = DayPx;
        var big = _zoom == GanttZoom.Month;
        var muted = MutedBrush;
        var grid = new Pen(GridBrush, 1);
        var hh = HeaderHeight;
        // Tier 1: months (years at month zoom), sticky at the left edge; the tier 2 labels crossfade with the scale.
        var firstDate = _first.AddDays(a);
        var lastDate = _first.AddDays(b);
        var cursor = new DateOnly(firstDate.Year, big ? 1 : firstDate.Month, 1);
        while (cursor <= lastDate)
        {
            var next = big ? cursor.AddYears(1) : cursor.AddMonths(1);
            var x0 = XOf(cursor);
            var x1 = XOf(next);
            var label = big ? cursor.Year.ToString(CultureInfo.CurrentUICulture) : cursor.ToString("MMMM", CultureInfo.CurrentUICulture);
            var ft = Text(label, 12, TextBrush);
            var lx = Math.Max(x0, _scrollX) + 8;
            lx = Math.Min(lx, x1 - ft.Width - 8);
            if (lx >= x0 - 1)
            {
                ctx.DrawText(ft, new Point(lx, 14));
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
                label = d.Day.ToString(CultureInfo.CurrentUICulture);
            }
            else if (_zoom == GanttZoom.Week && d.DayOfWeek == DayOfWeek.Monday)
            {
                label = d.Day.ToString(CultureInfo.CurrentUICulture);
            }
            else if (big && d.Day == 1)
            {
                label = d.ToString("MMM", CultureInfo.CurrentUICulture);
            }
            if (label is null)
            {
                continue;
            }
            var ft = Text(label, 12, muted);
            var tx = _zoom == GanttZoom.Day ? x + (dw - ft.Width) / 2 : x + 6;
            ctx.DrawText(ft, new Point(tx, 36));
        }
        _ = grid;
        _ = hh;
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
        ctx.DrawRectangle(BarBrush, null, new RoundedRect(rect, 3));
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
        var selected = _selectedId == t.Id;
        var pen = new Pen(conflict ? ConflictBrush : selected ? TextBrush : GridBrush, selected || conflict ? 1.5 : 1);
        if (t.IsMilestone)
        {
            var c = new Point((x0 + x1) / 2, cy);
            var r = 8.0;
            var g = new StreamGeometry();
            using (var gc = g.Open())
            {
                gc.BeginFigure(new Point(c.X, c.Y - r), true);
                gc.LineTo(new Point(c.X + r, c.Y));
                gc.LineTo(new Point(c.X, c.Y + r));
                gc.LineTo(new Point(c.X - r, c.Y));
                gc.EndFigure(true);
            }
            ctx.DrawGeometry(conflict ? ConflictBrush : SummaryBrush, selected ? pen : null, g);
            DrawFocus(ctx, t, new Rect(c.X - r - 3, c.Y - r - 3, r * 2 + 6, r * 2 + 6), r + 3);
            return;
        }
        var rect = new Rect(x0, y + BarTop, Math.Max(2, x1 - x0), BarHeight);
        var rr = new RoundedRect(rect, BarRadius);
        ctx.DrawRectangle(BarBrush, pen, rr);
        var p = Math.Clamp(t.Progress, 0, 1);
        if (p > 0)
        {
            using (ctx.PushClip(rr))
            {
                ctx.DrawRectangle(BarFillBrush, null, new Rect(rect.X, rect.Y, rect.Width * p, rect.Height));
            }
        }
        using (ctx.PushClip(rr))
        {
            if (rect.Width >= 96)
            {
                var pct = rect.Width >= 36 ? Text(((int)Math.Round(p * 100)).ToString(CultureInfo.CurrentUICulture) + "%", 12, MutedBrush) : null;
                var room = rect.Width - 16 - (pct is null ? 0 : pct.Width + 8);
                var ft = Text(t.Name, 12, TextBrush, FontWeight.Medium, room);
                ctx.DrawText(ft, new Point(rect.X + 8, cy - ft.Height / 2));
                if (pct is not null)
                {
                    ctx.DrawText(pct, new Point(rect.Right - 8 - pct.Width, cy - pct.Height / 2));
                }
            }
            else if (rect.Width >= 36)
            {
                var pct = Text(((int)Math.Round(p * 100)).ToString(CultureInfo.CurrentUICulture) + "%", 12, MutedBrush, FontWeight.Normal, rect.Width - 8);
                ctx.DrawText(pct, new Point(rect.X + 4, cy - pct.Height / 2));
            }
        }
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
            ? s.ToString("MMM d", CultureInfo.CurrentUICulture)
            : s.ToString("MMM d", CultureInfo.CurrentUICulture) + " – " + e.ToString("MMM d", CultureInfo.CurrentUICulture);
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

    private void DrawToday(DrawingContext ctx, double colW, double hh, double h)
    {
        var x = colW + XOf(_today) + DayPx / 2 - _scrollX;
        if (x < colW || x > Bounds.Width)
        {
            return;
        }
        ctx.DrawLine(new Pen(TodayBrush, 1), new Point(Math.Round(x) + 0.5, 22), new Point(Math.Round(x) + 0.5, h));
        var ft = Text("Today", 11, TodayTextBrush, FontWeight.Medium);
        var pill = new Rect(x - (ft.Width + 16) / 2, 6, ft.Width + 16, 18);
        ctx.DrawRectangle(TodayBrush, null, new RoundedRect(pill, 9));
        ctx.DrawText(ft, new Point(pill.X + 8, pill.Y + (pill.Height - ft.Height) / 2));
    }

    private void DrawArrows(DrawingContext ctx)
    {
        var rows = _model.Rows;
        var index = new Dictionary<string, int>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Task is { } t)
            {
                index[t.Id] = i;
            }
        }
        foreach (var (task, i) in index.Select(kv => (_model.ById[kv.Key], kv.Value)))
        {
            foreach (var depId in task.DependsOn)
            {
                if (!index.TryGetValue(depId, out var j) || !_model.ById.TryGetValue(depId, out var dep))
                {
                    continue;
                }
                var (_, px1) = BarX(dep, rows[j]);
                var (sx0, _) = BarX(task, rows[i]);
                var py = RowY(j, rows[j]) + BarTop + BarHeight / 2;
                var sy = RowY(i, rows[i]) + BarTop + BarHeight / 2;
                var conflict = task.Start <= (dep.IsMilestone ? dep.Start : dep.End);
                var hot = _selectedId == task.Id || _selectedId == dep.Id;
                var brush = conflict ? ConflictBrush : hot ? TextBrush : ArrowBrush;
                if (dep.IsMilestone)
                {
                    px1 += 0;
                }
                DrawArrow(ctx, new Pen(brush, hot || conflict ? 1.5 : 1), px1, py, sx0, sy);
            }
        }
    }

    private static void DrawArrow(DrawingContext ctx, Pen pen, double x1, double y1, double x2, double y2)
    {
        var pts = new List<Point> { new(x1, y1) };
        var out1 = x1 + 8;
        if (x2 - x1 >= 16)
        {
            pts.Add(new Point(out1, y1));
            pts.Add(new Point(out1, y2));
        }
        else
        {
            var mid = y1 + Math.Sign(y2 - y1 == 0 ? 1 : y2 - y1) * GanttModel.RowHeight / 2;
            pts.Add(new Point(out1, y1));
            pts.Add(new Point(out1, mid));
            pts.Add(new Point(x2 - 8, mid));
            pts.Add(new Point(x2 - 8, y2));
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
            hc.BeginFigure(new Point(x2 - 5, y2 - 3.5), false);
            hc.LineTo(new Point(x2, y2));
            hc.LineTo(new Point(x2 - 5, y2 + 3.5));
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
        var tasks = Text("Tasks", 12, MutedBrush);
        ctx.DrawText(tasks, new Point(16, 36));
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
                    if (row.Group is { } g)
                    {
                        DrawChevron(ctx, 19, cy, row.IsCollapsed);
                        var count = Text(row.GroupTaskCount.ToString(CultureInfo.CurrentUICulture), 12, MutedBrush);
                        var name = Text(g.Name, 14, TextBrush, FontWeight.Medium, colW - 36 - count.Width - 30);
                        ctx.DrawText(name, new Point(36, cy - name.Height / 2));
                        ctx.DrawText(count, new Point(colW - 16 - count.Width, cy - count.Height / 2));
                    }
                    else if (row.Task is { } t)
                    {
                        var sel = _selectedId == t.Id;
                        var left = 36.0;
                        if (t.Assignee is { } p)
                        {
                            ctx.DrawEllipse(AvatarBrush(p), null, new Point(left + 11, cy), 11, 11);
                            left += 32;
                        }
                        var ft = Text(t.Name, 14, sel ? TextBrush : MutedBrush, sel ? FontWeight.Medium : FontWeight.Normal, colW - left - 12);
                        ctx.DrawText(ft, new Point(left, cy - ft.Height / 2));
                    }
                }
                ctx.DrawLine(gridPen, new Point(0, y + GanttModel.RowHeight - 0.5), new Point(colW, y + GanttModel.RowHeight - 0.5));
            }
        }
        ctx.DrawRectangle(SurfaceBrush, null, new Rect(0, 0, colW - 1, hh - 1));
        ctx.DrawText(tasks, new Point(16, 36));
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
