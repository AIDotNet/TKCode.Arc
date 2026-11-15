using Avalonia;
using Avalonia.Input;

namespace TKCode.Arc;

// Pointer, keyboard, hover and focus.
public partial class SlideToConfirm
{
    private IPointer? _pointer;
    private bool _pointerPending;
    private Point _pressPoint;
    private double _grabOffset;

    private bool AcceptsInput => IsEffectivelyEnabled && !IsBusy;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hovered = true;
            UpdateTint();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateTint();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateTint();
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        EndGlide(springBack: true);
        UpdateTint();
        InvalidateVisual();
    }

    private bool IsFocusVisible => IsFocused && PseudoClasses.Contains(":focus-visible");

    private void UpdateTint() => SetTint((_hovered && AcceptsInput) || IsFocusVisible);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!AcceptsInput || _pointer is not null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var p = e.GetPosition(this);
        _touched = true;
        CancelHint();
        Focus(NavigationMethod.Pointer);
        var cx = (_h / 2) + _pos;
        if (Math.Abs(p.X - cx) > GrabReach * Scale)
        {
            // A press on the track away from the thumb does not move it; a small nudge shows what does.
            PlayNudge(0.38, 3.2);
            e.Handled = true;
            return;
        }
        _pointer = e.Pointer;
        _pressPoint = p;
        _grabOffset = p.X - cx;
        e.Handled = true;
        if (e.Pointer.Type == PointerType.Touch)
        {
            // Touch waits for a horizontal intent so a vertical swipe still scrolls the page.
            _pointerPending = true;
            return;
        }
        StartDrag(e.Pointer);
    }

    private void StartDrag(IPointer pointer)
    {
        _pointerPending = false;
        pointer.Capture(this);
        _glideKey = null;
        _keyValue = -1;
        _showError = false;
        _dragTarget = _pos;
        _pressTarget = 1;
        _mode = Mode.Drag;
        State = SlideToConfirmState.Dragging;
        InvalidateText();
        Kick();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer is null || e.Pointer != _pointer)
        {
            return;
        }
        var p = e.GetPosition(this);
        if (_pointerPending)
        {
            var dx = Math.Abs(p.X - _pressPoint.X);
            var dy = Math.Abs(p.Y - _pressPoint.Y);
            if (dy > 6 && dy > dx)
            {
                _pointer = null;
                _pointerPending = false;
                return;
            }
            if (dx < 6)
            {
                return;
            }
            StartDrag(e.Pointer);
        }
        var raw = p.X - _grabOffset - (_h / 2);
        _dragTarget = raw <= _travel ? Math.Max(0, raw) : _travel + SlideToConfirmMath.RubberThumb(raw - _travel, Scale);
        e.Handled = true;
        Kick();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer is null || e.Pointer != _pointer)
        {
            return;
        }
        e.Handled = true;
        EndDrag(release: true);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pointer is not null && e.Pointer == _pointer && !_pointerPending)
        {
            EndDrag(release: true);
        }
    }

    private void EndDrag(bool release)
    {
        var wasPending = _pointerPending;
        _pointer = null;
        _pointerPending = false;
        if (wasPending || _mode != Mode.Drag)
        {
            return;
        }
        _pressTarget = 0;
        if (release && _travel > 0 && _pos >= Threshold * _travel - 0.01)
        {
            BeginCommit();
            return;
        }
        SpringBack();
    }

    private void SpringBack()
    {
        _pressTarget = 0;
        SetMode(Mode.Spring, 0, SlideToConfirmMath.Release);
        if (_state == SlideToConfirmState.Dragging)
        {
            State = SlideToConfirmState.Idle;
        }
        Kick();
    }

    /// <summary>Ends a pointer drag or a key glide without confirming (disabled, detached, focus lost).</summary>
    private void CancelGesture()
    {
        if (_pointer is not null)
        {
            var pointer = _pointer;
            _pointer = null;
            _pointerPending = false;
            pointer.Capture(null);
            if (_mode == Mode.Drag)
            {
                SpringBack();
            }
        }
        EndGlide(springBack: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !AcceptsInput || _pointer is not null)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Right:
            case Key.Up:
                MoveByKey(SlideToConfirmMath.ArrowStep(KeyBase(), 1));
                break;
            case Key.Left:
            case Key.Down:
                MoveByKey(SlideToConfirmMath.ArrowStep(KeyBase(), -1));
                break;
            case Key.PageUp:
                MoveByKey(SlideToConfirmMath.PageStep(KeyBase(), 1));
                break;
            case Key.PageDown:
                MoveByKey(SlideToConfirmMath.PageStep(KeyBase(), -1));
                break;
            case Key.Home:
                _touched = true;
                CancelHint();
                _keyValue = -1;
                _showError = false;
                SpringBack();
                InvalidateText();
                break;
            case Key.End:
                _touched = true;
                BeginCommit();
                break;
            case Key.Space:
            case Key.Enter:
                if (_glideKey is null)
                {
                    StartGlide(e.Key);
                }
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_glideKey is { } key && e.Key == key)
        {
            e.Handled = true;
            EndGlide(springBack: true);
        }
    }

    private double KeyBase() => _keyValue >= 0 ? _keyValue : (_travel > 0 ? Math.Clamp(_pos / _travel, 0, 1) : 0);

    /// <summary>Moves the thumb to <paramref name="value"/> (0–1) on the key spring; keys never confirm on their own.</summary>
    internal void MoveByKey(double value)
    {
        if (!AcceptsInput)
        {
            return;
        }
        _touched = true;
        CancelHint();
        _showError = false;
        _keyValue = value;
        _pressTarget = 0;
        SetMode(Mode.Spring, value * _travel, SlideToConfirmMath.Key);
        if (_state is SlideToConfirmState.Dragging or SlideToConfirmState.Error)
        {
            State = SlideToConfirmState.Idle;
        }
        InvalidateText();
        Kick();
    }

    private void StartGlide(Key key)
    {
        _touched = true;
        CancelHint();
        if (!CanAnimate)
        {
            // No frames to glide with: holding the key confirms at once.
            BeginCommit();
            return;
        }
        _glideKey = key;
        _keyValue = -1;
        _showError = false;
        _glideTarget = Math.Max(0, _pos);
        _pressTarget = 1;
        _mode = Mode.Glide;
        State = SlideToConfirmState.Dragging;
        InvalidateText();
        Kick();
    }

    private void EndGlide(bool springBack)
    {
        if (_glideKey is null)
        {
            return;
        }
        _glideKey = null;
        if (_mode == Mode.Glide && springBack)
        {
            SpringBack();
        }
    }
}
