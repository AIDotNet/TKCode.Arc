using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Pull input. The sheet's position is a <see cref="MotionValue{T}"/> rather than a ControlTheme transition: a pull writes
/// it on every move and a spring that is still returning can be caught mid-flight. A press on the feed starts watching
/// moves at the window (so a first move that leaves the feed still counts, unlike Arc, whose pull then sticks); the
/// pointer is captured only once the pull starts, so a press that does not pull still clicks what is under it.
/// </summary>
public partial class StretchRefresh
{
    private TopLevel? _root;
    private IPointer? _pointer;
    private static readonly TimerCallback s_wheelTimerCallback = static state => ((StretchRefresh)state!).OnWheelTimer();
    private ITimer? _wheelTimer;
    private Action? _onWheelTimer;

    private bool AtTop => _scroller is null || _scroller.Offset.Y <= 0.5;

    private TimeSpan Now => TimeSpan.FromTicks(Time.GetUtcNow().UtcTicks);

    /// <summary>The raw distance a new pull starts from: a sheet still springing back to the top is caught where it is.</summary>
    private double CatchFrom => _machine.State == StretchRefreshState.Idle && _y is { Value: > 0.5 } y
        ? StretchRefreshMath.Unresist(y.Value)
        : 0;

    private void OnSheetPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_machine.CanPull || _gesture.IsPressed || _gesture.IsWheelPulling || !IsEffectivelyEnabled)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _root = TopLevel.GetTopLevel(this);
        if (_root is null)
        {
            return;
        }
        _pointer = e.Pointer;
        _gesture.Press(e.GetPosition(_root), e.Pointer.Type == PointerType.Touch, AtTop, CatchFrom);
        _root.AddHandler(PointerMovedEvent, OnRootMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _root.AddHandler(PointerReleasedEvent, OnRootReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnRootMoved(object? sender, PointerEventArgs e)
    {
        if (_root is null || !ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        // A mouse released outside the window sends no release: the first move without the button ends the pull.
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            FinishPointer(e);
            return;
        }
        switch (_gesture.Move(e.GetPosition(_root), () => AtTop))
        {
            case StretchRefreshPullStep.Started:
                if (_sheet is not null)
                {
                    e.Pointer.Capture(_sheet);
                }
                e.PreventGestureRecognition();
                BeginPull();
                UpdatePull();
                e.Handled = true;
                break;
            case StretchRefreshPullStep.Moved:
                UpdatePull();
                e.Handled = true;
                break;
        }
    }

    private void OnRootReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(e.Pointer, _pointer))
        {
            return;
        }
        if (_gesture.IsPointerPulling)
        {
            e.Handled = true;
        }
        FinishPointer(e);
    }

    private void FinishPointer(PointerEventArgs e)
    {
        var pointer = _pointer;
        EndPointerTracking();
        var pulled = _gesture.Release();
        if (pointer is not null && _sheet is not null && ReferenceEquals(pointer.Captured, _sheet))
        {
            pointer.Capture(null);
        }
        if (pulled)
        {
            ReleasePull(StretchRefreshTrigger.Pull);
        }
    }

    private void EndPointerTracking()
    {
        if (_root is not null)
        {
            _root.RemoveHandler(PointerMovedEvent, OnRootMoved);
            _root.RemoveHandler(PointerReleasedEvent, OnRootReleased);
        }
        _root = null;
        _pointer = null;
    }

    private void OnSheetCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(e.Source, _sheet) || !ReferenceEquals(e.Pointer, _pointer) || !_gesture.IsPointerPulling)
        {
            return;
        }
        // Something else took the pointer mid-pull: abandon it without refreshing.
        EndPointerTracking();
        _gesture.Release();
        _machine.Cancel();
        UpdateState();
        SpringBack();
    }

    private void OnSheetWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_machine.CanPull || _gesture.IsPointerPulling || !IsEffectivelyEnabled)
        {
            return;
        }
        var wasPulling = _gesture.IsWheelPulling;
        if (!wasPulling && (_well is null || !new Rect(_well.Bounds.Size).Contains(e.GetPosition(_well))))
        {
            return;
        }
        var pixels = e.Delta.Y * StretchRefreshMath.WheelPixelsPerUnit;
        if (!_gesture.Wheel(pixels, AtTop, Now, CatchFrom))
        {
            return;
        }
        e.Handled = true;
        if (!wasPulling)
        {
            BeginPull();
        }
        UpdatePull();
        // One timer per control, re-armed by every wheel event.
        _wheelTimer ??= Time.CreateTimer(s_wheelTimerCallback, this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _wheelTimer.Change(StretchRefreshMath.WheelIdle, Timeout.InfiniteTimeSpan);
    }

    private void OnWheelTimer()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(_onWheelTimer ??= OnWheelTimer);
            return;
        }
        if (!_gesture.IsWheelPulling || !_gesture.IsWheelIdle(Now))
        {
            return;
        }
        StopWheelTimer();
        _gesture.EndWheel();
        ReleasePull(StretchRefreshTrigger.Wheel);
    }

    private void StopWheelTimer() => _wheelTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private void BeginPull()
    {
        _y?.Stop();
        _tension?.Stop();
    }

    /// <summary>Follows the gesture 1:1 (no smoothing) at every motion level.</summary>
    private void UpdatePull()
    {
        var y = StretchRefreshMath.Resist(_gesture.Distance);
        _y?.Jump(y);
        _tension?.Jump(y);
        var before = _machine.State;
        if (_machine.Pull(y))
        {
            UpdateState();
            if ((before == StretchRefreshState.Armed) != (_machine.State == StretchRefreshState.Armed))
            {
                ShowArmed(_machine.State == StretchRefreshState.Armed);
            }
        }
    }

    private void ReleasePull(StretchRefreshTrigger trigger)
    {
        if (_machine.Release())
        {
            UpdateState();
            _ = RunCycle(trigger);
            return;
        }
        UpdateState();
        SpringBack();
    }

    /// <summary>A pull let go before the threshold: the sheet and the rows spring back to rest (no release velocity, as measured).</summary>
    private void SpringBack()
    {
        _ = _y?.AnimateTo(0, MotionSpec.Spring(ReturnSpring));
        _ = _tension?.AnimateTo(0, MotionSpec.Spring(RestSpring));
        ShowArmed(false);
    }
}
