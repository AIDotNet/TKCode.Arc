using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A container whose label brightens while the selection lens is over it.</summary>
internal interface IGlassLensItem
{
    void SetUnderLens(bool value);
}

/// <summary>
/// The selection model of a row of glass options (<see cref="GlassSegmented"/>, <see cref="GlassTabBar"/>): keeps the
/// <see cref="GlassLens"/> on the selected container, lets the pointer press, tap or drag the lens to another option, and
/// moves the selection with the arrow keys, Home and End (wrapping, skipping disabled options) with only the selected
/// option in the Tab order.
/// </summary>
internal sealed class GlassLensTrack
{
    /// <summary>A press that moves farther than this (px) along the row drags the lens.</summary>
    public const double DragSlop = 4;

    private readonly SelectingItemsControl _owner;
    private GlassLens? _lens;
    private IPointer? _pointer;
    private int _pressIndex = -1;
    private Point _pressPoint;
    private bool _dragging;
    private int _underLens = -1;

    public GlassLensTrack(SelectingItemsControl owner)
    {
        _owner = owner;
    }

    /// <summary>True while a pointer drags the lens.</summary>
    public bool IsDragging => _dragging;

    /// <summary>The option the lens is over (the selected one, or the one under a dragged lens).</summary>
    public int UnderLens => _underLens;

    public void Attach(GlassLens? lens)
    {
        _lens = lens;
        Sync(false);
    }

    /// <summary>Whether the option at <paramref name="index"/> can take focus and selection.</summary>
    public bool IsUsable(int index) =>
        _owner.IsEffectivelyEnabled && index >= 0 && index < _owner.ItemCount &&
        _owner.ContainerFromIndex(index) is not { IsEnabled: false };

    /// <summary>The bounds of the option at <paramref name="index"/> in the lens's coordinates.</summary>
    public Rect? RectOf(int index)
    {
        if (_lens is null || index < 0 || _owner.ContainerFromIndex(index) is not { IsVisible: true } container)
        {
            return null;
        }
        return container.TranslatePoint(default, _lens) is { } at ? new Rect(at, container.Bounds.Size) : null;
    }

    /// <summary>Puts the lens on the selected option; animated for a selection change, at once for a resize.</summary>
    public void Sync(bool animate)
    {
        if (_lens is null || _dragging)
        {
            return;
        }
        var index = _owner.SelectedIndex;
        if (RectOf(index) is not { } rect || rect.Width <= 0)
        {
            SetUnderLens(index);
            return;
        }
        SetUnderLens(index);
        if (_lens.HasTarget && _lens.TargetRect == rect)
        {
            return;
        }
        _lens.MoveTo(rect, animate && _owner.IsLoaded);
    }

    /// <summary>Called after every layout pass: a resize puts the lens straight onto its option.</summary>
    public void OnLayoutUpdated()
    {
        if (_lens is not null && !_dragging && !_lens.IsMoving)
        {
            Sync(false);
        }
    }

    public void UpdateTabStop()
    {
        var index = _owner.SelectedIndex;
        if (!IsUsable(index))
        {
            index = Step(-1, 1);
        }
        var container = index >= 0 ? _owner.ContainerFromIndex(index) : null;
        KeyboardNavigation.SetTabOnceActiveElement(_owner, container);
        if (_owner.ItemsPanelRoot is { } panel)
        {
            KeyboardNavigation.SetTabOnceActiveElement(panel, container);
        }
    }

    private void SetUnderLens(int index)
    {
        _underLens = index;
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (_owner.ContainerFromIndex(i) is IGlassLensItem item)
            {
                item.SetUnderLens(i == index);
            }
        }
    }

    /// <summary>The usable option whose box holds <paramref name="x"/> (lens coordinates), or -1.</summary>
    public int HitIndex(double x)
    {
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (RectOf(i) is { } rect && x >= rect.Left && x < rect.Right)
            {
                return IsUsable(i) ? i : -1;
            }
        }
        return -1;
    }

    /// <summary>The usable option whose center is nearest to <paramref name="x"/>, or -1.</summary>
    public int NearestIndex(double x)
    {
        var best = -1;
        var distance = double.MaxValue;
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (IsUsable(i) && RectOf(i) is { } rect)
            {
                var d = Math.Abs(rect.Center.X - x);
                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The lens rectangle for a drag at <paramref name="x"/>: centered on the pointer between the first and the last usable
    /// option, as wide as the options it passes between (blended), as tall as the one it is nearest to.
    /// </summary>
    public Rect? DragRect(double x)
    {
        Rect? first = null;
        Rect? last = null;
        Rect? before = null;
        Rect? after = null;
        for (var i = 0; i < _owner.ItemCount; i++)
        {
            if (!IsUsable(i) || RectOf(i) is not { } rect)
            {
                continue;
            }
            first ??= rect;
            last = rect;
            if (rect.Center.X <= x)
            {
                before = rect;
            }
            else
            {
                after ??= rect;
            }
        }
        if (first is not { } start || last is not { } end)
        {
            return null;
        }
        var center = Math.Clamp(x, start.Center.X, end.Center.X);
        var from = before ?? start;
        var to = after ?? end;
        var span = to.Center.X - from.Center.X;
        var t = span > 0.5 ? Math.Clamp((center - from.Center.X) / span, 0, 1) : 0;
        var width = from.Width + (to.Width - from.Width) * t;
        var near = t < 0.5 ? from : to;
        return new Rect(center - width / 2, near.Y, width, near.Height);
    }

    public bool OnPointerPressed(PointerPressedEventArgs e)
    {
        if (_lens is null || _pointer is not null || !e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed)
        {
            return false;
        }
        var position = e.GetPosition(_lens);
        var index = HitIndex(position.X);
        if (index < 0)
        {
            return false;
        }
        _pointer = e.Pointer;
        _pressPoint = position;
        _pressIndex = index;
        _dragging = false;
        e.Pointer.Capture(_owner);
        if (index == _owner.SelectedIndex)
        {
            _lens.SetLifted(true);
        }
        if (_owner.ContainerFromIndex(index) is { } container)
        {
            container.Focus(NavigationMethod.Pointer);
        }
        return true;
    }

    public bool OnPointerMoved(PointerEventArgs e)
    {
        if (_lens is null || !ReferenceEquals(e.Pointer, _pointer))
        {
            return false;
        }
        var position = e.GetPosition(_lens);
        if (!_dragging)
        {
            // Only the selected option carries the lens: a press elsewhere is a tap that selects on release.
            if (_pressIndex != _owner.SelectedIndex || Math.Abs(position.X - _pressPoint.X) < DragSlop)
            {
                return true;
            }
            _dragging = true;
            _lens.SetLifted(true);
        }
        if (DragRect(position.X) is { } rect)
        {
            _lens.DragTo(rect);
            SetUnderLens(NearestIndex(rect.Center.X));
        }
        return true;
    }

    public bool OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_lens is null || !ReferenceEquals(e.Pointer, _pointer))
        {
            return false;
        }
        var position = e.GetPosition(_lens);
        int target;
        if (_dragging)
        {
            target = NearestIndex(_lens.CurrentRect.Center.X);
        }
        else
        {
            target = HitIndex(position.X) == _pressIndex ? _pressIndex : -1;
        }
        Finish();
        e.Pointer.Capture(null);
        if (target >= 0 && target != _owner.SelectedIndex)
        {
            _owner.SelectedIndex = target;
        }
        else
        {
            Sync(true);
        }
        return true;
    }

    public void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        Finish();
        Sync(true);
    }

    private void Finish()
    {
        _pointer = null;
        _pressIndex = -1;
        _dragging = false;
        _lens?.SetLifted(false);
    }

    /// <summary>
    /// Handles an arrow key, Home or End from an option: selects and focuses the next usable option (wrapping). Returns true
    /// when the key was used.
    /// </summary>
    public bool HandleKey(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _owner.ItemCount == 0)
        {
            return false;
        }
        var rtl = _owner.FlowDirection == FlowDirection.RightToLeft;
        var current = _owner.SelectedIndex;
        var next = e.Key switch
        {
            Key.Right => Step(current, rtl ? -1 : 1),
            Key.Left => Step(current, rtl ? 1 : -1),
            Key.Down => Step(current, 1),
            Key.Up => Step(current, -1),
            Key.Home => Step(-1, 1),
            Key.End => Step(_owner.ItemCount, -1),
            _ => -2
        };
        if (next == -2)
        {
            return false;
        }
        e.Handled = true;
        if (next < 0)
        {
            return true;
        }
        _owner.SelectedIndex = next;
        if (_owner.ContainerFromIndex(next) is { } container)
        {
            container.Focus(NavigationMethod.Directional);
        }
        return true;
    }

    /// <summary>The next usable index from <paramref name="from"/> in <paramref name="direction"/>, wrapping; -1 if none.</summary>
    private int Step(int from, int direction)
    {
        var count = _owner.ItemCount;
        if (count == 0)
        {
            return -1;
        }
        var wraps = from >= 0 && from < count;
        var index = from;
        for (var n = 0; n < count; n++)
        {
            index += direction;
            if (index < 0 || index >= count)
            {
                if (!wraps)
                {
                    return -1;
                }
                index = index < 0 ? count - 1 : 0;
            }
            if (IsUsable(index))
            {
                return index;
            }
        }
        return -1;
    }
}
