using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

// Pointer and keyboard input of the ring. Every pointer event is mapped to an item by angle and radius here (the sectors
// take no input of their own), so the click mode and the marking gesture share one geometry.
public partial class RadialMenu
{
    // A gesture counts as a move toward an item once the pointer is this far from its origin while over a sector, even
    // inside the dead zone (only possible when the ring was shifted away from the press point).
    private const double GestureSlop = 8;

    private IPointer? _gesturePointer;
    private Point _gestureOrigin;
    private bool _leftDeadZone;
    private RadialMenuItem? _pressed;
    private IPointer? _pressPointer;
    private bool _centerPressed;
    private bool _keyboardModality;

    /// <summary>True while a press-drag-release gesture is in progress.</summary>
    internal bool IsGestureActive => _gesturePointer is not null;

    /// <summary>The gesture's origin in ring coordinates (tests).</summary>
    internal Point GestureOrigin => _gestureOrigin;

    private Point RingCenter => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>
    /// The enabled item under <paramref name="point"/> on the ring band (between the centre and the plate's rim, inside
    /// the sweep), or null. The gaps belong to their nearest neighbour.
    /// </summary>
    internal RadialMenuItem? ItemAtRing(Point point, bool includeDisabled = false)
    {
        var center = RingCenter;
        var distance = RadialMenuLayout.Distance(center, point);
        if (distance <= EffectiveInnerRadius || distance > EffectiveOuterRadius + PlatePadding)
        {
            return null;
        }
        var items = VisibleItems();
        var angle = RadialMenuLayout.AngleOf(center, point);
        if (!RadialMenuLayout.InSweep(angle, StartAngle, SweepAngle, 0))
        {
            return null;
        }
        var index = RadialMenuLayout.IndexAt(angle, items.Count, StartAngle, SweepAngle);
        if (index < 0)
        {
            return null;
        }
        var item = items[index];
        return includeDisabled || item.IsEffectivelyEnabled ? item : null;
    }

    /// <summary>
    /// The item a gesture points at from <see cref="GestureOrigin"/>: a sector under the pointer wins; otherwise, past
    /// the dead zone, the item in the pointer's direction (however far out). Null in the dead zone or toward a disabled item.
    /// </summary>
    internal RadialMenuItem? GestureTarget(Point point)
    {
        if (ItemAtRing(point, includeDisabled: true) is { } under)
        {
            return under.IsEffectivelyEnabled ? under : null;
        }
        if (RadialMenuLayout.Distance(_gestureOrigin, point) <= EffectiveInnerRadius)
        {
            return null;
        }
        var items = VisibleItems();
        if (items.Count == 0)
        {
            return null;
        }
        var angle = RadialMenuLayout.AngleOf(_gestureOrigin, point);
        var step = RadialMenuLayout.ClampSweep(SweepAngle) / items.Count;
        var index = RadialMenuLayout.IndexAt(angle, items.Count, StartAngle, SweepAngle, tolerance: step / 2);
        if (index < 0)
        {
            return null;
        }
        var item = items[index];
        return item.IsEffectivelyEnabled ? item : null;
    }

    /// <summary>Starts a press-drag-release gesture with <paramref name="pointer"/>, which is still pressed at <paramref name="origin"/> (ring coordinates).</summary>
    internal void BeginGesture(IPointer pointer, Point origin)
    {
        _gesturePointer = pointer;
        _gestureOrigin = origin;
        _leftDeadZone = false;
        PseudoClasses.Set(GesturePseudoClass, true);
        pointer.Capture(this);
    }

    private void EndGesture()
    {
        if (_gesturePointer is not { } pointer)
        {
            return;
        }
        _gesturePointer = null;
        PseudoClasses.Set(GesturePseudoClass, false);
        if (ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
    }

    private void ClearPress()
    {
        _pressed?.SetPressed(false);
        _pressed = null;
        if (_pressPointer is { } pointer && ReferenceEquals(pointer.Captured, this))
        {
            pointer.Capture(null);
        }
        _pressPointer = null;
        _centerPressed = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_gesturePointer is not null && ReferenceEquals(e.Pointer, _gesturePointer))
        {
            e.Handled = true;
            var distance = RadialMenuLayout.Distance(_gestureOrigin, point);
            var target = GestureTarget(point);
            if (distance > EffectiveInnerRadius || (distance > GestureSlop && ItemAtRing(point) is not null))
            {
                _leftDeadZone = true;
            }
            _keyboardModality = false;
            SetHighlight(target);
            return;
        }
        if (_pressed is { } pressed && ReferenceEquals(e.Pointer, _pressPointer))
        {
            // Pressed in click mode: the sector stays highlighted and looks pressed only while the pointer is on it.
            pressed.SetPressed(ReferenceEquals(ItemAtRing(point), pressed));
            return;
        }
        if (e.Pointer.Type == PointerType.Touch || _centerPressed)
        {
            return;
        }
        _keyboardModality = false;
        SetHighlight(ItemAtRing(point));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        // A closing ring leaving the pointer keeps the chosen item as SelectedItem.
        if (!IsInline && _state is PopupState.Closing or PopupState.Closed)
        {
            return;
        }
        if (_gesturePointer is null && _pressed is null && !_keyboardModality)
        {
            SetHighlight(null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled)
        {
            return;
        }
        e.Handled = true;
        _keyboardModality = false;
        if (_gesturePointer is not null)
        {
            return;
        }
        var point = e.GetPosition(this);
        if (RadialMenuLayout.Distance(RingCenter, point) <= EffectiveInnerRadius)
        {
            // A press in the centre cancels (on release, so the click does not fall through to what is behind).
            _centerPressed = true;
            _pressPointer = e.Pointer;
            e.Pointer.Capture(this);
            return;
        }
        if (ItemAtRing(point) is not { } item)
        {
            return;
        }
        _pressed = item;
        _pressPointer = e.Pointer;
        item.SetPressed(true);
        SetHighlight(item);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (_gesturePointer is not null && ReferenceEquals(e.Pointer, _gesturePointer))
        {
            e.Handled = true;
            var moved = _leftDeadZone;
            var target = moved ? GestureTarget(point) : null;
            EndGesture();
            if (!moved)
            {
                // Released without heading anywhere (a click): the menu stays open for a click on an item.
                if (e.Pointer.Type != PointerType.Touch)
                {
                    SetHighlight(ItemAtRing(point));
                }
                return;
            }
            if (target is not null)
            {
                Invoke(target);
            }
            else
            {
                CloseCore(returnFocus: true);
            }
            return;
        }
        if (!ReferenceEquals(e.Pointer, _pressPointer))
        {
            return;
        }
        e.Handled = true;
        if (_centerPressed)
        {
            var inCenter = RadialMenuLayout.Distance(RingCenter, point) <= EffectiveInnerRadius;
            ClearPress();
            if (inCenter && !IsInline)
            {
                CloseCore(returnFocus: true);
            }
            return;
        }
        var pressed = _pressed;
        ClearPress();
        if (pressed is not null && ReferenceEquals(ItemAtRing(point), pressed))
        {
            Invoke(pressed);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_gesturePointer is not null && ReferenceEquals(e.Pointer, _gesturePointer))
        {
            // Capture taken away mid-gesture (the window lost focus): keep the menu open for a click.
            _gesturePointer = null;
            PseudoClasses.Set(GesturePseudoClass, false);
        }
        if (ReferenceEquals(e.Pointer, _pressPointer))
        {
            _pressed?.SetPressed(false);
            _pressed = null;
            _pressPointer = null;
            _centerPressed = false;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var floating = !IsInline;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                Move(1);
                break;
            case Key.Left:
            case Key.Up:
                Move(-1);
                break;
            case Key.Tab when floating:
                Move(shift ? -1 : 1);
                break;
            case Key.Home:
                MoveToEnd(first: true);
                break;
            case Key.End:
                MoveToEnd(first: false);
                break;
            case Key.Enter:
            case Key.Space:
                if (_highlighted is { } highlighted)
                {
                    _keyboardModality = true;
                    Invoke(highlighted, fromKeyboard: true);
                }
                break;
            case Key.Escape when floating && _state != PopupState.Closed:
                _keyboardModality = true;
                CloseCore(returnFocus: true);
                break;
            default:
                if (!TypeAhead(e))
                {
                    return;
                }
                break;
        }
        e.Handled = true;
    }

    private void Move(int delta)
    {
        _keyboardModality = true;
        var items = VisibleItems();
        if (items.Count == 0)
        {
            return;
        }
        var start = _highlighted is { } current ? items.IndexOf(current) : -1;
        if (start < 0)
        {
            // Nothing highlighted yet: the first step lands on the first (or, going back, the last) item.
            start = delta > 0 ? -1 : items.Count;
        }
        for (var step = 1; step <= items.Count; step++)
        {
            var index = ((start + delta * step) % items.Count + items.Count) % items.Count;
            if (items[index].IsEffectivelyEnabled)
            {
                SetHighlight(items[index], moveFocus: true);
                return;
            }
        }
    }

    private void MoveToEnd(bool first)
    {
        _keyboardModality = true;
        var items = VisibleItems();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[first ? i : items.Count - 1 - i];
            if (item.IsEffectivelyEnabled)
            {
                SetHighlight(item, moveFocus: true);
                return;
            }
        }
    }

    private bool TypeAhead(KeyEventArgs e)
    {
        if (e.KeySymbol is not { Length: 1 } symbol || !char.IsLetterOrDigit(symbol[0]) ||
            (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return false;
        }
        var items = VisibleItems();
        var start = _highlighted is { } current ? items.IndexOf(current) : -1;
        for (var step = 1; step <= items.Count; step++)
        {
            var item = items[(start + step + items.Count) % items.Count];
            if (item.IsEffectivelyEnabled && item.GetText(TextSelector) is { Length: > 0 } text &&
                text.StartsWith(symbol, StringComparison.CurrentCultureIgnoreCase))
            {
                _keyboardModality = true;
                SetHighlight(item, moveFocus: true);
                return true;
            }
        }
        return false;
    }

    /// <summary>Highlights the first enabled item and moves keyboard focus to it (keyboard opens).</summary>
    private void HighlightFirstForKeyboard()
    {
        _keyboardModality = true;
        if (_highlighted is { } current && current.IsEffectivelyEnabled && current.IsVisible)
        {
            current.Focus(NavigationMethod.Tab);
            return;
        }
        MoveToEnd(first: true);
        if (_highlighted is null)
        {
            Focus(NavigationMethod.Tab);
        }
    }
}
