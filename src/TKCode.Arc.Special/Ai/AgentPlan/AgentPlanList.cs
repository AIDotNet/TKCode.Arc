using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The column of step rows. Rows have their own (springing) heights and are stacked without gaps; the list keeps a row's
/// control instance when the step moves, so a title field in it keeps its focus, caret and selection. A move (keyboard,
/// drag or the host) is a FLIP: rows jump back to where they were drawn and spring to their new slot on the plan's layout
/// spring, as a render transform only. A dragged row follows the pointer 1:1 (no spring) and swaps with a neighbour once its
/// middle passes the neighbour's middle; on release it springs into its slot. The panel's children are kept in step order, so
/// Tab follows the plan.
/// </summary>
internal sealed class AgentPlanList : Panel
{
    private readonly List<AgentPlanRow> _ordered = new();
    private bool _armed;
    private bool _disarmPosted;
    private AgentPlanRow? _dragged;
    private double _grab;
    private int _dragStartIndex;
    private double _pointerY;

    /// <summary>The row being dragged, or null.</summary>
    public AgentPlanRow? Dragged => _dragged;

    /// <summary>The index the dragged row started from (among the live rows).</summary>
    public int DragStartIndex => _dragStartIndex;

    /// <summary>Every row in order, the ones folding away included.</summary>
    public IReadOnlyList<AgentPlanRow> Ordered => _ordered;

    /// <summary>The rows of the plan's steps, in order (rows folding away left out).</summary>
    public IEnumerable<AgentPlanRow> Live => _ordered.Where(r => r.Presence != AgentPlanRowPresence.Leaving);

    /// <summary>Raised when a drag swaps the dragged row with a neighbour: the row and its new live index.</summary>
    public event Action<AgentPlanRow, int>? DragSwapped;

    /// <summary>Raised when a drag ends: the row and the live index it started from.</summary>
    public event Action<AgentPlanRow, int>? DragEnded;

    private int OrderedIndexOfLive(int live)
    {
        var seen = 0;
        for (var i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].Presence == AgentPlanRowPresence.Leaving)
            {
                continue;
            }
            if (seen == live)
            {
                return i;
            }
            seen++;
        }
        return _ordered.Count;
    }

    /// <summary>The live index of <paramref name="row"/>, or -1.</summary>
    public int LiveIndexOf(AgentPlanRow row)
    {
        var seen = 0;
        foreach (var r in _ordered)
        {
            if (r.Presence == AgentPlanRowPresence.Leaving)
            {
                continue;
            }
            if (ReferenceEquals(r, row))
            {
                return seen;
            }
            seen++;
        }
        return -1;
    }

    /// <summary>Adds <paramref name="row"/> at the live index <paramref name="liveIndex"/>.</summary>
    public void Insert(AgentPlanRow row, int liveIndex)
    {
        var at = OrderedIndexOfLive(liveIndex);
        _ordered.Insert(at, row);
        Children.Insert(at, row);
        InvalidateMeasure();
    }

    /// <summary>Drops a row that has finished leaving.</summary>
    public void Drop(AgentPlanRow row)
    {
        if (_dragged == row)
        {
            EndDrag();
        }
        _ordered.Remove(row);
        Children.Remove(row);
        InvalidateMeasure();
    }

    /// <summary>Removes every row at once.</summary>
    public void Clear()
    {
        _dragged = null;
        _ordered.Clear();
        Children.Clear();
        InvalidateMeasure();
    }

    /// <summary>Moves a row to another live index with a FLIP of the rows between.</summary>
    public void Move(AgentPlanRow row, int newLiveIndex)
    {
        var from = _ordered.IndexOf(row);
        if (from < 0)
        {
            return;
        }
        var live = LiveIndexOf(row);
        if (live == newLiveIndex)
        {
            return;
        }
        _ordered.RemoveAt(from);
        var to = OrderedIndexOfLive(newLiveIndex);
        _ordered.Insert(to, row);
        Children.Move(from, to);
        Arm();
    }

    /// <summary>The next arrange animates the rows that change place.</summary>
    public void Arm()
    {
        _armed = true;
        InvalidateArrange();
        if (!_disarmPosted)
        {
            _disarmPosted = true;
            // Layout runs before background jobs; an arm that moved nothing must not leak into a later resize.
            Dispatcher.UIThread.Post(() =>
            {
                _disarmPosted = false;
                _armed = false;
            }, DispatcherPriority.Background);
        }
    }

    // Drag ---------------------------------------------------------------------------------------------------------------

    /// <summary>Lifts <paramref name="row"/>; <paramref name="pointerY"/> is in this list's coordinates.</summary>
    public void BeginDrag(AgentPlanRow row, double pointerY)
    {
        if (_dragged is not null)
        {
            return;
        }
        _dragged = row;
        _dragStartIndex = LiveIndexOf(row);
        _grab = pointerY - (double.IsNaN(row.LayoutTop) ? 0 : row.LayoutTop) - row.Offset.Value;
        _pointerY = pointerY;
        row.Offset.Stop();
        row.SetLifted(true);
    }

    /// <summary>Moves the dragged row with the pointer and swaps it with a neighbour once its middle passes theirs.</summary>
    public void DragTo(double pointerY)
    {
        if (_dragged is not { } row)
        {
            return;
        }
        _pointerY = pointerY;
        var live = _ordered.Where(r => r.Presence != AgentPlanRowPresence.Leaving).ToList();
        var total = 0.0;
        foreach (var r in live)
        {
            total += r.DesiredSize.Height;
        }
        var height = row.DesiredSize.Height;
        var top = Math.Clamp(pointerY - _grab, 0, Math.Max(0, total - height));
        // A row swaps with a neighbour once it is dragged more than half that neighbour's height past its slot.
        var current = live.IndexOf(row);
        var target = current;
        var slotTop = 0.0;
        for (var i = 0; i < current; i++)
        {
            slotTop += live[i].DesiredSize.Height;
        }
        while (true)
        {
            var delta = top - slotTop;
            if (delta > 0 && target + 1 < live.Count && delta > live[target + 1].DesiredSize.Height / 2)
            {
                slotTop += live[target + 1].DesiredSize.Height;
                target++;
            }
            else if (delta < 0 && target > 0 && -delta > live[target - 1].DesiredSize.Height / 2)
            {
                slotTop -= live[target - 1].DesiredSize.Height;
                target--;
            }
            else
            {
                break;
            }
        }

        if (target != current)
        {
            Move(row, target);
            live = _ordered.Where(r => r.Presence != AgentPlanRowPresence.Leaving).ToList();
            DragSwapped?.Invoke(row, target);
        }
        var slot = 0.0;
        foreach (var r in live)
        {
            if (ReferenceEquals(r, row))
            {
                break;
            }
            slot += r.DesiredSize.Height;
        }
        row.LayoutTop = slot;
        row.Offset.Jump(top - slot);
    }

    /// <summary>Drops the dragged row: it springs into its slot and loses its lift.</summary>
    public void EndDrag()
    {
        if (_dragged is not { } row)
        {
            return;
        }
        _dragged = null;
        row.SetLifted(false);
        if (IsLoaded && AgentPlanMotion.IsFull(this))
        {
            _ = row.Offset.AnimateTo(0, MotionSpec.Spring(AgentPlanMotion.Drop));
        }
        else
        {
            row.Offset.Jump(0);
        }
        DragEnded?.Invoke(row, _dragStartIndex);
    }

    // Layout -------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var total = 0.0;
        foreach (var row in _ordered)
        {
            row.Measure(new Size(width, double.PositiveInfinity));
            total += row.DesiredSize.Height;
        }
        return new Size(double.IsFinite(width) ? width : 0, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var row in _ordered)
        {
            var h = row.DesiredSize.Height;
            row.Arrange(new Rect(0, y, finalSize.Width, h));
            if (_armed && !ReferenceEquals(row, _dragged) && !double.IsNaN(row.LayoutTop) && Math.Abs(row.LayoutTop - y) > 0.5 &&
                row.Presence != AgentPlanRowPresence.Leaving && IsLoaded && AgentPlanMotion.IsFull(this))
            {
                row.Offset.Jump(row.Offset.Value + row.LayoutTop - y);
                _ = row.Offset.AnimateTo(0, MotionSpec.Spring(AgentPlanMotion.Layout));
            }
            if (!ReferenceEquals(row, _dragged) || double.IsNaN(row.LayoutTop))
            {
                row.LayoutTop = y;
            }
            y += h;
        }
        return finalSize;
    }
}
