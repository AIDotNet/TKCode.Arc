using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

// Pointer and keyboard. The three hits take the input; the stage solves the cover's hinge angle and the bat's tilt from
// the pointer (one rectangle read per press, as Arc does) and leans the panel towards the pointer.
internal sealed partial class ArmingSwitchStage
{
    private ArmingSwitchPart? _dragPart;
    private IPointer? _dragPointer;
    private Point _pressPoint;
    private bool _dragMoved;
    private double _grabOffset;
    private double _lastAngle;
    private double _lastTime;
    private double _velocity;
    private Key? _holdKey;

    // Arc Arming switch (design reference): the cover's lip is 0.4 W from the hinge line at y 0.114 W; the bat is 0.135 W
    // long from its pivot at y 0.34 W.
    private const double HingeY = 0.114;
    private const double LidLength = 0.4;
    private const double PivotY = 0.34;
    private const double BatLength = 0.135;
    private const double DragSlop = 4;

    /// <summary>A pointer position in plate widths, ignoring the small lean.</summary>
    private Point ToPlate(Point p) => new((p.X - _plateOffset.X) / _w, (p.Y - _plateOffset.Y) / _w);

    private static double Degrees(double radians) => radians * 180 / Math.PI;

    private double CoverAngleAt(Point plate)
    {
        var rel = Math.Clamp((plate.Y - HingeY) / LidLength, -1, 1);
        var angle = Degrees(Math.Acos(rel));
        if (angle > CoverOpenAngle)
        {
            // Rubber band past the open stop.
            angle = CoverOpenAngle + ((angle - CoverOpenAngle) * 0.3);
        }
        return angle;
    }

    private double BatAngleAt(Point plate)
    {
        var rel = Math.Clamp((plate.Y - PivotY) / BatLength, -1, 1);
        return Degrees(Math.Asin(rel));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (e.Pointer.Type != PointerType.Touch || _dragPart is not null)
        {
            LeanTowards(p);
        }
        if (_dragPart is { } part && e.Pointer == _dragPointer)
        {
            if (!_dragMoved && (Math.Abs(p.Y - _pressPoint.Y) > DragSlop || Math.Abs(p.X - _pressPoint.X) > DragSlop))
            {
                _dragMoved = true;
            }
            if (_dragMoved)
            {
                DragTo(part, p, e.Timestamp / 1000.0);
            }
            e.Handled = true;
        }
    }

    private void LeanTowards(Point p)
    {
        var nx = Math.Clamp(p.X / Math.Max(1, Bounds.Width), 0, 1);
        var ny = Math.Clamp(p.Y / Math.Max(1, Bounds.Height), 0, 1);
        _tiltXGoal = RestTiltX + (6 * (0.5 - ny));
        _tiltYGoal = RestTiltY + (6 * (nx - 0.5));
        Kick();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragPart is null)
        {
            _tiltXGoal = RestTiltX;
            _tiltYGoal = RestTiltY;
        }
        Kick();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Kick();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var owner = Owner;
        if (owner is null || e.Source is not ArmingSwitchHit hit || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        hit.Focus(NavigationMethod.Pointer);
        var p = e.GetPosition(this);
        _pressPoint = p;
        _dragMoved = false;
        _velocity = 0;
        _lastTime = e.Timestamp / 1000.0;
        switch (hit.Part)
        {
            case ArmingSwitchPart.Lamp:
                if (owner.CanHold(refuse: true))
                {
                    StartHold();
                    _dragPart = ArmingSwitchPart.Lamp;
                    _dragPointer = e.Pointer;
                    e.Pointer.Capture(hit);
                }
                return;
            case ArmingSwitchPart.Cover:
                if (owner.IsRunning)
                {
                    return;
                }
                if (owner.IsLocked)
                {
                    owner.Refuse(ArmingSwitch.LockedText, rattle: true);
                    return;
                }
                _grabOffset = 0;
                _lastAngle = _cov;
                break;
            case ArmingSwitchPart.Switch:
                if (owner.IsRunning)
                {
                    return;
                }
                if (owner.IsLocked)
                {
                    owner.Refuse(ArmingSwitch.LockedText, rattle: true);
                    return;
                }
                _grabOffset = _bat - BatAngleAt(ToPlate(p));
                _lastAngle = _bat;
                break;
        }
        _dragPart = hit.Part;
        _dragPointer = e.Pointer;
        e.Pointer.Capture(hit);
    }

    private void DragTo(ArmingSwitchPart part, Point p, double time)
    {
        var owner = Owner;
        if (owner is null || !Full)
        {
            return;
        }
        var plate = ToPlate(p);
        if (part == ArmingSwitchPart.Cover)
        {
            var angle = CoverAngleAt(plate);
            _covMode = CoverMode.Drag;
            _covDragTarget = angle;
            Track(angle, time);
            var open = angle > Halfway;
            if (open != owner.IsCoverOpen)
            {
                owner.SetCover(open, fromDrag: true);
                _covMode = CoverMode.Drag;
            }
        }
        else if (part == ArmingSwitchPart.Switch)
        {
            if (!owner.IsCoverOpen)
            {
                return;
            }
            var angle = Math.Clamp(BatAngleAt(plate) + _grabOffset, -BatThrow, BatThrow);
            Track(angle, time);
            var armed = owner.IsArmed;
            // Past centre the over-centre spring takes the bat out of the hand and slams it into the far stop.
            if ((!armed && angle < 0) || (armed && angle > 0))
            {
                _dragPart = null;
                _dragPointer?.Capture(null);
                _dragPointer = null;
                _batVel = (angle - _bat) / 0.016;
                owner.SetArmed(!armed, fromUser: true);
                _batMode = BatMode.Drive;
                Kick();
                return;
            }
            _batMode = BatMode.Drag;
            _batDragTarget = angle;
        }
        Kick();
    }

    private void Track(double angle, double time)
    {
        var dt = time - _lastTime;
        if (dt > 0.001)
        {
            _velocity = (0.6 * ((angle - _lastAngle) / dt)) + (0.4 * _velocity);
        }
        _lastAngle = angle;
        _lastTime = time;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer != _dragPointer || _dragPart is not { } part)
        {
            return;
        }
        e.Handled = true;
        EndDrag(part, click: !_dragMoved);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (e.Pointer == _dragPointer && _dragPart is { } part)
        {
            EndDrag(part, click: false);
        }
    }

    private void EndDrag(ArmingSwitchPart part, bool click)
    {
        var owner = Owner;
        _dragPart = null;
        _dragPointer = null;
        if (owner is null)
        {
            return;
        }
        switch (part)
        {
            case ArmingSwitchPart.Lamp:
                EndHold();
                break;
            case ArmingSwitchPart.Cover:
                if (click)
                {
                    Activate(ArmingSwitchPart.Cover);
                    break;
                }
                // Keep the release speed: a flick up opens, a flick down closes.
                var projected = _cov + (_velocity * 0.12);
                var open = projected > Halfway;
                _covVel = _velocity;
                if (open != owner.IsCoverOpen)
                {
                    owner.SetCover(open, fromDrag: true);
                }
                _covMode = owner.IsCoverOpen ? CoverMode.Open : CoverMode.Close;
                break;
            case ArmingSwitchPart.Switch:
                if (click)
                {
                    Activate(ArmingSwitchPart.Switch);
                    break;
                }
                // Let go before centre: it snaps back with the speed of the hand.
                _batVel = _velocity;
                _batGoal = owner.IsArmed ? -BatThrow : BatThrow;
                _batMode = BatMode.Drive;
                break;
        }
        Kick();
    }

    /// <summary>Toggles a part as a click, Enter or Space does.</summary>
    internal void Activate(ArmingSwitchPart part)
    {
        var owner = Owner;
        if (owner is null)
        {
            return;
        }
        if (part == ArmingSwitchPart.Cover)
        {
            if (owner.IsRunning)
            {
                return;
            }
            owner.SetCover(!owner.IsCoverOpen);
        }
        else if (part == ArmingSwitchPart.Switch)
        {
            owner.SetArmed(!owner.IsArmed, fromUser: true);
        }
    }

    private void StartHold()
    {
        _holding = true;
        _raw = Math.Clamp(_hold, 0, 1);
        Kick();
    }

    private void EndHold()
    {
        if (!_holding)
        {
            return;
        }
        _holding = false;
        Kick();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var owner = Owner;
        if (owner is null || e.Source is not ArmingSwitchHit hit || e.Handled)
        {
            return;
        }
        var activate = e.Key is Key.Enter or Key.Space;
        switch (hit.Part)
        {
            case ArmingSwitchPart.Cover:
                if (activate)
                {
                    Activate(ArmingSwitchPart.Cover);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    owner.OpenCover();
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    owner.CloseCover();
                    e.Handled = true;
                }
                break;
            case ArmingSwitchPart.Switch:
                if (activate)
                {
                    Activate(ArmingSwitchPart.Switch);
                    e.Handled = true;
                }
                else if (e.Key is Key.Up or Key.Down)
                {
                    var arm = e.Key == Key.Up;
                    if (arm != owner.IsArmed)
                    {
                        owner.SetArmed(arm, fromUser: true);
                    }
                    e.Handled = true;
                }
                break;
            case ArmingSwitchPart.Lamp:
                if (activate)
                {
                    e.Handled = true;
                    if (_holdKey is null && !_holding && owner.CanHold(refuse: true))
                    {
                        _holdKey = e.Key;
                        StartHold();
                    }
                }
                break;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_holdKey is { } key && e.Key == key)
        {
            _holdKey = null;
            EndHold();
            e.Handled = true;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_holdKey is not null && !_lampHit.IsFocused)
        {
            _holdKey = null;
            EndHold();
        }
    }
}
