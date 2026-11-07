using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Pointer input: hover fan, lift and tilt (mouse only), the vertical drag that cycles cards, pulling the open card.</summary>
public partial class WalletStack
{
    private WalletStackCardView? _pressed;
    private WalletStackCardView? _candidate;
    private WalletStackCardView? _hovered;
    private bool _pointerInside;
    private bool _hoverIsMouse;
    private DragMode _dragMode;
    private double _dragStart;
    private int _dragStartIndex;
    private double _dragDistance;
    private double _dragZero;

    private enum DragMode
    {
        None,
        Stack,
        Open,
    }

    private void AttachInput(WalletStackStage stage)
    {
        stage.AddHandler(PointerEnteredEvent, OnStagePointerEntered, handledEventsToo: true);
        stage.AddHandler(PointerMovedEvent, OnStagePointerMoved, handledEventsToo: true);
        stage.AddHandler(PointerExitedEvent, OnStagePointerExited, handledEventsToo: true);
        _drag = new DragTracker(stage, new DragOptions
        {
            Threshold = WalletStackGesture.Threshold,
            Axis = DragAxis.Vertical,
            RejectCrossAxis = true,
            HandledEventsToo = true,
        })
        {
            CanBegin = e =>
            {
                _candidate = FindView(e.Source as Visual);
                return _candidate is not null;
            },
        };
        _drag.Pressed += OnDragPressed;
        _drag.Starting += OnDragStarting;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += OnDragCancelled;
        _drag.Released += OnDragReleased;
    }

    private void DetachInput(WalletStackStage stage)
    {
        stage.RemoveHandler(PointerEnteredEvent, OnStagePointerEntered);
        stage.RemoveHandler(PointerMovedEvent, OnStagePointerMoved);
        stage.RemoveHandler(PointerExitedEvent, OnStagePointerExited);
        _drag?.Dispose();
        _drag = null;
    }

    private static WalletStackCardView? FindView(Visual? source)
    {
        for (var v = source; v is not null; v = v.GetVisualParent())
        {
            if (v is WalletStackCardView view)
            {
                return view;
            }
            if (v is WalletStackStage)
            {
                return null;
            }
        }
        return null;
    }

    private void OnStagePointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        _pointerInside = true;
        _hoverIsMouse = true;
        if (_dragMode == DragMode.None && _open is null)
        {
            SetFan(1);
        }
    }

    private void OnStagePointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            return;
        }
        _pointerInside = false;
        if (_dragMode != DragMode.None)
        {
            return;
        }
        SetFan(0);
        ClearHover();
    }

    private void OnStagePointerMoved(object? sender, PointerEventArgs e)
    {
        // Touch never fans or tilts (Arc: hover effects are for a fine pointer only).
        if (e.Pointer.Type != PointerType.Mouse || _stage is null || _dragMode != DragMode.None)
        {
            return;
        }
        _hoverIsMouse = true;
        if (!_pointerInside)
        {
            _pointerInside = true;
            if (_open is null)
            {
                SetFan(1);
            }
        }
        var view = FindView(e.Source as Visual);
        Hover(view);
        if (view is not null && IsTiltEnabled)
        {
            var slot = _stage.Slots[view.Index];
            var point = e.GetPosition(slot);
            var size = slot.Bounds.Size;
            if (size.Width > 0 && size.Height > 0)
            {
                view.Tilt.PointAt(point.X / size.Width * 2 - 1, point.Y / size.Height * 2 - 1);
            }
        }
    }

    // The hovered card lifts (8px unless it is in front; its shadow deepens) on Smooth; the others settle back.
    private void Hover(WalletStackCardView? view)
    {
        if (ReferenceEquals(view, _hovered))
        {
            return;
        }
        _hovered?.Tilt.Leave();
        _hovered = view;
        for (var i = 0; i < _lifts.Count; i++)
        {
            var target = _open is null && view is not null && view.Index == i ? 1 : 0;
            if (_lifts[i].Target != target)
            {
                _ = _lifts[i].AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
        }
    }

    private void ClearHover()
    {
        Hover(null);
        if (_stage is null)
        {
            return;
        }
        foreach (var view in _stage.Views)
        {
            view.Tilt.Leave();
        }
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        _pressed = _candidate;
        _candidate = null;
        _pressed?.Press(true);
    }

    private void OnDragStarting(object? sender, DragGestureEventArgs e)
    {
        if (_stage is null || _cards.Count == 0)
        {
            e.Cancel = true;
            return;
        }
        if (_open is not null)
        {
            // Only the open card can be pulled; a drag elsewhere stays a click.
            if (_pressed is null || _pressed.Index != _raised)
            {
                e.Cancel = true;
                return;
            }
            _dragMode = DragMode.Open;
            _dragStart = _raises[_raised].Value;
            _dragDistance = WalletStackLayout.OpenDistance(_stage.Geometry, _cards.Count);
        }
        else
        {
            _dragMode = DragMode.Stack;
            _dragStart = _position.Value;
            _dragStartIndex = Math.Clamp((int)Math.Round(_dragStart), 0, _cards.Count - 1);
            // The fan freezes during the drag; one card is the front card's way to the pocket.
            _dragDistance = WalletStackLayout.CardDistance(_stage.Geometry, _cards.Count, _fan.Value);
            _fan.Jump(_fan.Value);
        }
        // Follow from the point the threshold was crossed, without a jump.
        _dragZero = e.Offset.Y;
        _pressed?.Press(false);
        PseudoClasses.Set(DraggingPseudoClass, true);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (_stage is null)
        {
            return;
        }
        var offset = e.Offset.Y - _dragZero;
        if (_dragMode == DragMode.Stack)
        {
            _position.Jump(WalletStackGesture.DragPosition(_dragStart, offset, _dragDistance, _cards.Count));
        }
        else if (_dragMode == DragMode.Open && _raised >= 0)
        {
            _raises[_raised].Jump(WalletStackGesture.OpenAmount(_dragStart, offset, _dragDistance, _stage.Geometry.CardWidth));
        }
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e) => EndDrag(e.Velocity.Y);

    private void OnDragCancelled(object? sender, DragGestureEventArgs e) => EndDrag(0);

    private void EndDrag(double velocity)
    {
        var mode = _dragMode;
        _dragMode = DragMode.None;
        PseudoClasses.Set(DraggingPseudoClass, false);
        _pressed?.Press(false);
        _pressed = null;
        if (mode == DragMode.Stack)
        {
            var target = WalletStackGesture.ReleaseTarget(_position.Value, velocity, _dragDistance, _dragStartIndex, _cards.Count);
            SettlePosition(target, _dragDistance > 0 ? velocity / _dragDistance : 0);
        }
        else if (mode == DragMode.Open && _raised >= 0)
        {
            var rate = _dragDistance > 0 ? -velocity / _dragDistance : 0;
            if (WalletStackGesture.ReleaseCloses(_raises[_raised].Value, velocity, _dragDistance))
            {
                CloseCard(rate, focus: false);
            }
            else
            {
                _ = _raises[_raised].AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth), rate);
            }
        }
        if (_stage is not null && !_stage.IsPointerOver)
        {
            _pointerInside = false;
            SetFan(0);
            ClearHover();
        }
        else if (_hoverIsMouse && _open is null)
        {
            SetFan(1);
        }
    }

    // A press that never became a drag: open the card (or switch / close while one is open).
    private void OnDragReleased(object? sender, DragGestureEventArgs e)
    {
        var view = _pressed;
        _pressed = null;
        view?.Press(false);
        if (view is null)
        {
            return;
        }
        view.Focus(NavigationMethod.Pointer);
        ActivateCard(view.Index, keyboard: false);
    }
}
