using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

internal enum DragMode
{
    None,
    Move,
    ResizeStart,
    ResizeEnd,
}

internal sealed partial class GanttChart
{
    private const double EdgeGrab = 7;
    private const double AutoScrollZone = 36;

    private sealed class DragState
    {
        public required GanttTask Task { get; init; }

        public DragMode Mode { get; init; }

        public double PressX { get; init; }

        public double Delta { get; set; }

        public double LastX { get; set; }
    }

    private DragState? _drag;
    private DragMode _releaseMode;
    private string? _releaseId;
    private bool _keyboardFocus;
    private LiveLoop? _autoScroll;
    private double _pointerX;

    private int RowAt(double y)
    {
        var i = (int)Math.Floor((y - HeaderHeight + _scrollY) / GanttModel.RowHeight);
        return i >= 0 && i < _model.Rows.Count ? i : -1;
    }

    private (DateOnly Start, DateOnly End) Snapped(DragState d)
    {
        var days = (int)Math.Round(d.Delta / Math.Max(1, DayPx));
        var t = d.Task;
        var s = t.Start;
        var e = t.IsMilestone ? t.Start : t.End;
        switch (d.Mode)
        {
            case DragMode.Move:
                s = s.AddDays(days);
                e = e.AddDays(days);
                break;
            case DragMode.ResizeStart:
                s = s.AddDays(days);
                if (s > e)
                {
                    s = e;
                }
                break;
            case DragMode.ResizeEnd:
                e = e.AddDays(days);
                if (e < s)
                {
                    e = s;
                }
                break;
        }
        return (s, e);
    }

    private (double X0, double X1) DragBar(DragState d)
    {
        var t = d.Task;
        var x0 = XOf(t.Start);
        var x1 = XOf(t.IsMilestone ? t.Start.AddDays(1) : t.End.AddDays(1));
        var dw = Math.Max(1, DayPx);
        switch (d.Mode)
        {
            case DragMode.Move:
                return (x0 + d.Delta, x1 + d.Delta);
            case DragMode.ResizeStart:
            {
                var nx = x0 + d.Delta;
                var limit = x1 - dw;
                if (nx > limit)
                {
                    nx = limit + GanttModel.Resist(nx - limit);
                    return (Math.Min(nx, x1 - 2), x1);
                }
                return (nx, x1);
            }
            default:
            {
                var nx = x1 + d.Delta;
                var limit = x0 + dw;
                if (nx < limit)
                {
                    nx = limit - GanttModel.Resist(limit - nx);
                    return (x0, Math.Max(nx, x0 + 2));
                }
                return (x0, nx);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed)
        {
            return;
        }
        Focus();
        _keyboardFocus = false;
        var pos = p.Position;
        var colW = ColumnWidth;
        var i = RowAt(pos.Y);
        if (pos.Y < HeaderHeight || i < 0)
        {
            return;
        }
        var row = _model.Rows[i];
        if (pos.X < colW)
        {
            if (row.Group is { } g)
            {
                GroupToggled?.Invoke(g.Id);
            }
            else if (row.Task is { } t)
            {
                Select(t.Id);
            }
            e.Handled = true;
            return;
        }
        if (row.Task is not { } task)
        {
            return;
        }
        var cx = pos.X - colW + _scrollX;
        var (x0, x1) = BarX(task, row);
        if (task.IsMilestone)
        {
            x0 -= 6;
            x1 += 6;
        }
        if (cx < x0 - 2 || cx > x1 + 2)
        {
            return;
        }
        var mode = DragMode.Move;
        if (!task.IsMilestone && x1 - x0 > EdgeGrab * 3)
        {
            mode = cx <= x0 + EdgeGrab ? DragMode.ResizeStart : cx >= x1 - EdgeGrab ? DragMode.ResizeEnd : DragMode.Move;
        }
        _focusId = task.Id;
        _scrollMotion.Stop();
        _drag = new DragState { Task = task, Mode = mode, PressX = pos.X, LastX = pos.X };
        _pointerX = pos.X;
        _release.Jump(0);
        _releaseId = null;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        if (_drag is null)
        {
            UpdateCursor(pos);
            return;
        }
        _pointerX = pos.X;
        _drag.Delta += pos.X - _drag.LastX;
        _drag.LastX = pos.X;
        if (_drag.Mode == DragMode.Move && _drag.Task.IsMilestone)
        {
            _drag.Delta = Math.Round(_drag.Delta);
        }
        EnsureAutoScroll();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is null)
        {
            return;
        }
        var d = _drag;
        _drag = null;
        e.Pointer.Capture(null);
        StopAutoScroll();
        var (s, en) = Snapped(d);
        var t = d.Task;
        var changed = s != t.Start || en != (t.IsMilestone ? t.Start : t.End);
        if (!changed)
        {
            if (Math.Abs(d.Delta) < 3)
            {
                Select(_selectedId == t.Id ? null : t.Id);
            }
            AnimateRelease(d, 0, d.Mode);
            return;
        }
        var kind = d.Mode == DragMode.Move ? GanttChangeKind.Move : GanttChangeKind.Resize;
        var free = DragBarEdge(d);
        Edited?.Invoke(t, s, en, kind);
        var snapped = d.Mode == DragMode.ResizeEnd ? XOf(en.AddDays(1)) : XOf(s);
        AnimateRelease(d, free - snapped, d.Mode);
    }

    private double DragBarEdge(DragState d)
    {
        var (x0, x1) = DragBar(d);
        return d.Mode == DragMode.ResizeEnd ? x1 : x0;
    }

    private void AnimateRelease(DragState d, double offset, DragMode mode)
    {
        _releaseId = d.Task.Id;
        _releaseMode = mode;
        _before = [];
        if (IsReduced || Math.Abs(offset) < 0.5)
        {
            _release.Jump(0);
            _releaseId = null;
            InvalidateVisual();
            return;
        }
        _release.Jump(offset);
        _ = _release.AnimateTo(0, MotionSpec.Spring(ProjectsSprings.Card)).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            _releaseId = null;
            InvalidateVisual();
        }));
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag is not null)
        {
            _drag = null;
            StopAutoScroll();
            InvalidateVisual();
        }
    }

    private void UpdateCursor(Point pos)
    {
        var cursor = StandardCursorType.Arrow;
        string? hover = null;
        var i = pos.Y >= HeaderHeight ? RowAt(pos.Y) : -1;
        if (i >= 0 && pos.X < ColumnWidth)
        {
            cursor = StandardCursorType.Hand;
            hover = _model.Rows[i].Task?.Id;
        }
        else if (i >= 0 && _model.Rows[i].Task is { } t)
        {
            var cx = pos.X - ColumnWidth + _scrollX;
            var (x0, x1) = BarX(t, _model.Rows[i]);
            if (t.IsMilestone)
            {
                var c = (x0 + x1) / 2;
                x0 = c - MilestoneRadius;
                x1 = c + MilestoneRadius;
            }
            if (cx >= x0 - 2 && cx <= x1 + 2)
            {
                hover = t.Id;
                cursor = !t.IsMilestone && (cx <= x0 + EdgeGrab || cx >= x1 - EdgeGrab) && x1 - x0 > EdgeGrab * 3 ? StandardCursorType.SizeWestEast : StandardCursorType.Hand;
            }
        }
        Cursor = new Cursor(cursor);
        SetHover(hover);
    }

    private void SetHover(string? id)
    {
        if (_hoverId != id)
        {
            _hoverId = id;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null);
    }

    /// <summary>The interval of the timer the auto-scroll used to run on; a frame now scrolls by the same amount per elapsed time.</summary>
    private const double AutoScrollTick = 0.016;

    private void EnsureAutoScroll()
    {
        // A frame loop rather than a 16 ms timer: it is aligned with rendering and stops when the chart leaves the tree.
        // It is a function of the drag, not decoration, so it runs at every motion level and whatever the viewport says.
        _autoScroll ??= new LiveLoop(this, AutoScrollStep) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        _autoScroll.Start();
    }

    private bool AutoScrollStep(double dt)
    {
        if (_drag is null)
        {
            return false;
        }
        var left = ColumnWidth + AutoScrollZone;
        var right = Bounds.Width - AutoScrollZone;
        var dx = _pointerX < left ? -(left - _pointerX) / 4 : _pointerX > right ? (_pointerX - right) / 4 : 0;
        dx = Math.Clamp(dx, -14, 14);
        if (Math.Abs(dx) < 0.1)
        {
            return true;
        }
        var before = _scrollX;
        _scrollX += dx * (dt / AutoScrollTick);
        ClampScroll();
        _drag.Delta += _scrollX - before;
        InvalidateVisual();
        return true;
    }

    private void StopAutoScroll() => _autoScroll?.Stop();

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var dx = e.Delta.X * 48;
        var dy = e.Delta.Y * 48;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && dx == 0)
        {
            dx = dy;
            dy = 0;
        }
        _scrollMotion.Stop();
        _scrollX += -dx;
        _scrollY += -dy;
        ClampScroll();
        InvalidateVisual();
        e.Handled = true;
    }

    private void Select(string? id)
    {
        _selectedId = id;
        if (id is not null)
        {
            _focusId = id;
            Announce?.Invoke(_model.ById.TryGetValue(id, out var t) ? Describe(t) : string.Empty);
        }
        InvalidateVisual();
    }

    internal string Describe(GanttTask t)
    {
        var dates = t.IsMilestone ? t.Start.ToString("MMM d", CultureInfo.InvariantCulture)
            : t.Start.ToString("MMM d", CultureInfo.InvariantCulture) + " – " + t.End.ToString("MMM d", CultureInfo.InvariantCulture);
        var s = t.Name + ", " + dates;
        if (!t.IsMilestone)
        {
            s += ", " + GanttModel.Span(t).ToString(CultureInfo.InvariantCulture) + " days, " + ((int)Math.Round(t.Progress * 100)).ToString(CultureInfo.InvariantCulture) + "% done";
        }
        if (_model.HasConflict(t))
        {
            s += ", starts before a task it depends on ends";
        }
        return s;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var visible = _model.VisibleTasks().ToList();
        if (visible.Count == 0)
        {
            return;
        }
        var idx = Math.Max(0, visible.FindIndex(t => t.Id == _focusId));
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _keyboardFocus = true;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            {
                idx = Math.Clamp(idx + (e.Key == Key.Down ? 1 : -1), 0, visible.Count - 1);
                _focusId = visible[idx].Id;
                RevealRow(visible[idx]);
                Announce?.Invoke(Describe(visible[idx]));
                e.Handled = true;
                break;
            }
            case Key.Left or Key.Right when alt:
            {
                var t = visible[idx];
                var step = e.Key == Key.Left ? -1 : 1;
                var end = t.IsMilestone ? t.Start : t.End;
                var (s, en) = shift && !t.IsMilestone
                    ? (t.Start, end.AddDays(step) < t.Start ? t.Start : end.AddDays(step))
                    : (t.Start.AddDays(step), end.AddDays(step));
                if (s != t.Start || en != end)
                {
                    Edited?.Invoke(t, s, en, shift ? GanttChangeKind.Resize : GanttChangeKind.Move);
                }
                e.Handled = true;
                break;
            }
            case Key.Enter or Key.Space:
                Select(_selectedId == visible[idx].Id ? null : visible[idx].Id);
                e.Handled = true;
                break;
            case Key.Escape:
                Select(null);
                e.Handled = true;
                break;
        }
        InvalidateVisual();
    }

    private void RevealRow(GanttTask task)
    {
        var rows = _model.Rows;
        var i = -1;
        for (var k = 0; k < rows.Count; k++)
        {
            if (rows[k].Task?.Id == task.Id)
            {
                i = k;
                break;
            }
        }
        if (i < 0)
        {
            return;
        }
        _scrollMotion.Stop();
        var top = i * GanttModel.RowHeight;
        var viewH = Bounds.Height - HeaderHeight;
        if (top < _scrollY)
        {
            _scrollY = top;
        }
        else if (top + GanttModel.RowHeight > _scrollY + viewH)
        {
            _scrollY = top + GanttModel.RowHeight - viewH;
        }
        var x0 = XOf(task.Start);
        var x1 = XOf(task.IsMilestone ? task.Start.AddDays(1) : task.End.AddDays(1));
        if (x0 < _scrollX)
        {
            _scrollX = x0 - 12;
        }
        else if (x1 > _scrollX + ViewWidth)
        {
            _scrollX = x1 - ViewWidth + 12;
        }
        ClampScroll();
    }

    internal string? FocusedId => _focusId;

    internal void ClearSelection() => Select(null);
}

internal sealed class GanttChartPeer(GanttChart owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;

    protected override string? GetNameCore() => "Timeline";
}
