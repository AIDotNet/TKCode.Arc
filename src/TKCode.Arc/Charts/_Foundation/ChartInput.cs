using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>The readout keyboard model shared by the charts (Arc: arrows, PageUp/PageDown, Home/End, Escape).</summary>
internal static class ChartKeyboard
{
    /// <summary>
    /// Next active index for <paramref name="key"/>. ←/↓ previous, →/↑ next (from rest the first press lands on
    /// the latest point), PageUp/PageDown jump <paramref name="page"/>, Home/End go to the ends. Returns false
    /// for keys the model does not use.
    /// </summary>
    public static bool TryStep(Key key, int? index, int first, int last, int page, bool verticalSteps, out int next)
    {
        next = 0;
        if (last < first)
        {
            return false;
        }
        var from = index ?? last + 1;
        int? target = key switch
        {
            Key.Left => from - 1,
            Key.Down when verticalSteps => from - 1,
            Key.Right => index is null ? last : from + 1,
            Key.Up when verticalSteps => index is null ? last : from + 1,
            Key.PageDown => from - page,
            Key.PageUp => index is null ? last : from + page,
            Key.Home => first,
            Key.End => last,
            _ => null
        };
        if (target is not { } value)
        {
            return false;
        }
        next = Math.Clamp(value, first, last);
        return true;
    }
}

/// <summary>
/// Local pointer drag tracking for chart gestures (BrushChart's window and handles): captures the pointer on
/// press, waits for a 3px slop before reporting movement, and reports the offset from the press point.
/// </summary>
internal sealed class ChartDragTracker
{
    private IPointer? _pointer;
    private Point _start;

    public bool IsActive => _pointer is not null;

    public bool HasMoved { get; private set; }

    public int Mode { get; private set; }

    public Point Start => _start;

    public void Begin(PointerPressedEventArgs e, Visual relativeTo, int mode)
    {
        _pointer = e.Pointer;
        _start = e.GetPosition(relativeTo);
        HasMoved = false;
        Mode = mode;
        e.Pointer.Capture(relativeTo as IInputElement);
    }

    /// <summary>Returns true when the drag has passed the slop; <paramref name="offset"/> is the movement since the press.</summary>
    public bool Move(PointerEventArgs e, Visual relativeTo, out Vector offset)
    {
        offset = default;
        if (_pointer is null || e.Pointer != _pointer)
        {
            return false;
        }
        var position = e.GetPosition(relativeTo);
        offset = position - _start;
        if (!HasMoved && Math.Abs(offset.X) < 3 && Math.Abs(offset.Y) < 3)
        {
            return false;
        }
        HasMoved = true;
        return true;
    }

    public void End()
    {
        _pointer?.Capture(null);
        _pointer = null;
    }

    public void Cancel() => _pointer = null;
}
