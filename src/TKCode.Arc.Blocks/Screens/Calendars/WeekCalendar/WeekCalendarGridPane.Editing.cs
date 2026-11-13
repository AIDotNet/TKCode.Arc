using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The editing parts of a grid pane: the draft block of a new event, a block lifted by a drag (its place, its dashed origin
/// and its time pill), and the hit test of the resize handles.
/// </summary>
internal sealed partial class WeekCalendarGridPane
{
    private WeekCalendarDraftPresenter? _draft;
    private WeekCalendarDragOrigin? _origin;
    private WeekCalendarDragLabel? _label;
    private WeekCalendarEventPresenter? _lifted;
    private (DateOnly Day, double Start, double End)? _liftedAt;
    private Rect? _originBox;
    private bool _labelBelow;

    /// <summary>The draft block while a new event is drawn or waits in the quick create popover, or null.</summary>
    public WeekCalendarDraftPresenter? Draft => _draft is { IsVisible: true } d ? d : null;

    /// <summary>The block a drag lifted, or null.</summary>
    public WeekCalendarEventPresenter? Lifted => _lifted;

    /// <summary>Where the lifted block is being dragged to.</summary>
    public (DateOnly Day, double Start, double End)? LiftedAt => _liftedAt;

    /// <summary>The time pill of a drag, while it shows.</summary>
    public WeekCalendarDragLabel? DragLabel => _label is { IsVisible: true } l ? l : null;

    /// <summary>The dashed place a dragged block came from, while it shows.</summary>
    public WeekCalendarDragOrigin? DragOrigin => _origin is { IsVisible: true } o ? o : null;

    /// <summary>True when the time pill sits under the block (the top of the visible hours would hide it above).</summary>
    public bool IsLabelBelow => _labelBelow;

    /// <summary>The z order of a block: a dragged one floats over everything, the selected one over its neighbours.</summary>
    private static int ZIndexOf(WeekCalendarEventPresenter presenter) => presenter.IsDragging ? 12 : presenter.IsSelected ? 7 : 5;

    /// <summary>Updates the selection of every block.</summary>
    public void SetSelected(WeekCalendarEvent? selected)
    {
        foreach (var presenter in _presenters)
        {
            presenter.IsSelected = ReferenceEquals(presenter.Event, selected);
            presenter.ZIndex = ZIndexOf(presenter);
        }
    }

    // Draft --------------------------------------------------------------------------------------------------------------

    /// <summary>Shows (or moves) the draft block of a new event.</summary>
    public WeekCalendarDraftPresenter ShowDraft(DateOnly day, int start, int end, string? calendarId)
    {
        if (_draft is null)
        {
            _draft = new WeekCalendarDraftPresenter(_owner);
            AddChild(_draft);
        }
        _draft.IsVisible = true;
        _draft.Set(day, start, end, calendarId);
        InvalidateMeasure();
        InvalidateArrange();
        return _draft;
    }

    /// <summary>Removes the draft block (in the frame the popover starts to leave, as Arc).</summary>
    public void HideDraft()
    {
        if (_draft is { IsVisible: true })
        {
            _draft.IsVisible = false;
        }
    }

    /// <summary>The rect of the draft block: its top on the start line (no 1px offset, unlike events), at least 18 high.</summary>
    public Rect DraftBox(DateOnly day, double start, double end, Size size)
    {
        var column = WeekCalendarColumns.Column(Days.ColumnOf(day), Days.Count, size);
        var grid = _owner.TimeGrid;
        var top = grid.YOf(start);
        var height = Math.Max(WeekCalendarTimeGrid.MinEventHeight, (end - start) / 60.0 * grid.HourHeight - 2);
        return new Rect(column.X, top, column.Width, height);
    }

    // Lift ---------------------------------------------------------------------------------------------------------------

    /// <summary>Lifts <paramref name="presenter"/> for a drag: it floats in place, its origin shows dashed and its time in a pill.</summary>
    public void Lift(WeekCalendarEventPresenter presenter)
    {
        if (_lifted is not null && !ReferenceEquals(_lifted, presenter))
        {
            EndLift();
        }
        _lifted = presenter;
        presenter.IsDragging = true;
        presenter.ZIndex = ZIndexOf(presenter);
        _originBox = presenter.Bounds;
        _liftedAt = (presenter.Day, presenter.StartMinute, presenter.EndMinute);
        _origin ??= NewChild(new WeekCalendarDragOrigin(_owner));
        _origin.CalendarId = presenter.Event?.CalendarId;
        _origin.IsVisible = true;
        _origin.InvalidateVisual();
        _label ??= NewChild(new WeekCalendarDragLabel(_owner));
        _label.IsVisible = true;
        UpdateLabel();
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>
    /// Moves (or resizes) the lifted block to <paramref name="start"/>–<paramref name="end"/> on <paramref name="day"/>. A
    /// change of place hops on the hop spring from where the block is drawn; a change of length is applied at once.
    /// </summary>
    public void MoveLifted(DateOnly day, double start, double end)
    {
        if (_lifted is null || _liftedAt is { } at && at.Day == day && at.Start == start && at.End == end)
        {
            return;
        }
        var moves = _liftedAt is not { } was || was.Day != day || was.Start != start;
        _liftedAt = (day, start, end);
        _lifted.SetPreview(start, end);
        if (moves)
        {
            // Code-driven: the hop follows the pointer slot by slot and restarts from where the block is drawn.
            Flip.Arm(MotionSpec.Spring(WeekCalendarMotion.Hop));
        }
        UpdateLabel();
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <summary>
    /// Puts the lifted block down where its data says (after a commit, or back at its origin when cancelled):
    /// <paramref name="animate"/> lets it settle on the relayout spring from where it is drawn.
    /// </summary>
    public void EndLift(bool animate = false)
    {
        if (_lifted is { } presenter)
        {
            presenter.IsDragging = false;
            presenter.SetPreview(null, null);
            presenter.ZIndex = ZIndexOf(presenter);
        }
        _lifted = null;
        _liftedAt = null;
        _originBox = null;
        if (_origin is not null)
        {
            _origin.IsVisible = false;
        }
        if (_label is not null)
        {
            _label.IsVisible = false;
        }
        if (animate)
        {
            Flip.Arm(MotionSpec.Spring(WeekCalendarMotion.Relayout));
        }
        InvalidateMeasure();
        InvalidateArrange();
    }

    private T NewChild<T>(T child) where T : Control
    {
        AddChild(child);
        return child;
    }

    private void UpdateLabel()
    {
        if (_label is null || _liftedAt is not { } at)
        {
            return;
        }
        _label.Text = WeekCalendarTime.TimeRange(at.Start, at.End, _owner.EffectiveCulture);
    }

    // Hit testing ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The block whose resize handle is under <paramref name="point"/> (pane coordinates): 7px inside its bottom edge or 3px
    /// under it. A block under the point wins over the handle of the one above it.
    /// </summary>
    public WeekCalendarEventPresenter? ResizeHandleAt(Point point, WeekCalendarEventPresenter? pressed)
    {
        if (pressed is not null)
        {
            var local = point - pressed.Bounds.TopLeft;
            return pressed.CanResize && pressed.IsOnResizeHandle(local.Y) && local.X >= 0 && local.X <= pressed.Bounds.Width ? pressed : null;
        }
        foreach (var presenter in _presenters)
        {
            var b = presenter.Bounds;
            if (presenter.CanResize && point.X >= b.X && point.X <= b.Right && point.Y >= b.Bottom &&
                point.Y <= b.Bottom + WeekCalendarEditMetrics.ResizeHandleOutside)
            {
                return presenter;
            }
        }
        return null;
    }

    // Layout -----------------------------------------------------------------------------------------------------------------

    private void MeasureEditing(Size size)
    {
        if (_draft is { IsVisible: true } draft)
        {
            draft.Measure(DraftBox(draft.Day, draft.Start, draft.End, size).Size);
        }
        if (_origin is { IsVisible: true } origin && _originBox is { } box)
        {
            origin.Measure(box.Size);
        }
        if (_label is { IsVisible: true } label)
        {
            label.Measure(new Size(label.PillWidth, WeekCalendarEditMetrics.DragLabelHeight));
        }
    }

    private void ArrangeEditing(Size size)
    {
        if (_draft is { IsVisible: true } draft)
        {
            draft.Arrange(DraftBox(draft.Day, draft.Start, draft.End, size));
        }
        if (_origin is { IsVisible: true } origin && _originBox is { } box)
        {
            origin.Arrange(box);
        }
        if (_label is { IsVisible: true } label && _lifted is { } lifted)
        {
            var rect = RectOf(lifted, size);
            var width = label.PillWidth;
            var top = rect.Top - WeekCalendarEditMetrics.DragLabelOffset;
            // Arc's pill hides under the sticky header near the top of the visible hours; ours moves under the block.
            var visibleTop = _owner.GridView?.Scroller.Offset.Y ?? 0;
            _labelBelow = top < visibleTop + 2;
            if (_labelBelow)
            {
                top = rect.Bottom + WeekCalendarEditMetrics.DragLabelOffset - WeekCalendarEditMetrics.DragLabelHeight;
            }
            label.Arrange(new Rect(rect.X + (rect.Width - width) / 2, top, width, WeekCalendarEditMetrics.DragLabelHeight));
        }
    }
}
