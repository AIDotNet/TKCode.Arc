// Ported from vscode (src/vs/base/browser/touch.ts: Gesture's onTouchStart / onTouchMove / onTouchEnd, dispatchEvent's
// tap count and inertia; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - one recognizer per editor view, fed with Avalonia's touch pointer events and their timestamps, instead of a
//    process-wide listener on the document; its results are returned to the caller instead of dispatched as DOM events;
//  - a long press is recognized when HoldDelay has passed with the finger still (HoldElapsed, which the view calls from
//    a timer), not only on release as upstream's contextmenu gesture; the view selects a word there;
//  - scrolling starts once the finger moved by the drag threshold (Arc's DragRecognizer, 10 px) instead of on the
//    first move; a press that scrolled is never a tap (upstream: a tap when the finger ended within 30 px);
//  - the release velocity is Arc's VelocityTracker estimate over the last 100 ms (inside DragRecognizer), not the
//    speed between the first and last of upstream's rolling samples; the deceleration is upstream's
//    SCROLL_FRICTION, -0.005 px/ms², per axis;
//  - a second finger cancels the gesture until every finger is up: pinching does nothing (upstream scrolls by every
//    finger's movement).

using Avalonia;
using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>What a touch did (upstream's gesture event types).</summary>
internal enum CodeEditorTouchGestureKind
{
    /// <summary>Nothing to act on.</summary>
    None,

    /// <summary>A short touch without moving (<see cref="CodeEditorTouchGestureResult.TapCount"/>: 1 or 2).</summary>
    Tap,

    /// <summary>A long touch without moving (upstream's contextmenu gesture).</summary>
    Hold,

    /// <summary>The finger moved: scroll by <see cref="CodeEditorTouchGestureResult.Translation"/>.</summary>
    Change,

    /// <summary>The finger left while moving: coast with <see cref="CodeEditorTouchGestureResult.Velocity"/> (px/ms).</summary>
    Inertia,
}

/// <summary>A recognized gesture and its data.</summary>
internal readonly record struct CodeEditorTouchGestureResult(
    CodeEditorTouchGestureKind Kind,
    Point Point = default,
    int TapCount = 0,
    Vector Translation = default,
    Vector Velocity = default)
{
    public static CodeEditorTouchGestureResult None => default;
}

/// <summary>
/// Recognizes taps (with a tap count), long presses, drags and flicks from one finger (upstream's Gesture of touch.ts).
/// Times are in milliseconds.
/// </summary>
internal sealed class CodeEditorTouchGesture
{
    /// <summary>A touch held this long without moving is a hold (upstream's HOLD_DELAY).</summary>
    public const double HoldDelay = 700;

    /// <summary>A second tap within this time is a double tap (upstream's CLEAR_TAP_COUNT_TIME).</summary>
    public const double ClearTapCountTime = 400;

    /// <summary>The deceleration of a flick, in px/ms² (upstream's SCROLL_FRICTION).</summary>
    public const double ScrollFriction = -0.005;

    /// <summary>How far the finger moves before it scrolls (Arc's touch slop).</summary>
    public const double DragThreshold = 10;

    private readonly DragRecognizer _drag = new(new DragOptions { Threshold = DragThreshold, CapturePointer = false, LeftButtonOnly = false });
    private long? _pointerId;
    private int _otherPointers;
    private bool _cancelled;
    private bool _holdDone;
    private double _pressTime;
    // no tap yet, or the tap count was cleared (upstream: 0, an epoch time long past)
    private double _lastSetTapCountTime = double.NegativeInfinity;

    /// <summary>Whether a finger is down (and the gesture not cancelled by a second one).</summary>
    public bool IsActive => _pointerId is not null && !_cancelled;

    /// <summary>When the finger touched down (the pointer event's timestamp).</summary>
    public double PressTime => _pressTime;

    /// <summary>Whether the finger is scrolling.</summary>
    public bool IsDragging => _drag.Phase == DragPhase.Dragging;

    /// <summary>A finger touched down at <paramref name="point"/>.</summary>
    public void Press(long pointerId, Point point, double time)
    {
        if (_pointerId is not null)
        {
            if (pointerId != _pointerId)
            {
                // a second finger (a pinch): nothing until all are up
                _otherPointers++;
                _cancelled = true;
                _drag.Cancel();
                _lastSetTapCountTime = double.NegativeInfinity;
            }

            return;
        }

        _pointerId = pointerId;
        _otherPointers = 0;
        _cancelled = false;
        _holdDone = false;
        _pressTime = time;
        _drag.Begin(point, TimeSpan.FromMilliseconds(time));
    }

    /// <summary>A finger moved; returns a <see cref="CodeEditorTouchGestureKind.Change"/> when the view should scroll.</summary>
    public CodeEditorTouchGestureResult Move(long pointerId, Point point, double time)
    {
        if (pointerId != _pointerId || _cancelled || _holdDone)
        {
            return CodeEditorTouchGestureResult.None;
        }

        var transition = _drag.Move(point, TimeSpan.FromMilliseconds(time));
        if (transition is DragTransition.Started or DragTransition.Moved)
        {
            // a scroll cancels the tap count (upstream's dispatchEvent for Change)
            _lastSetTapCountTime = double.NegativeInfinity;
            return new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Change, point, Translation: _drag.Delta);
        }

        return CodeEditorTouchGestureResult.None;
    }

    /// <summary>
    /// Called by the view's timer: returns a <see cref="CodeEditorTouchGestureKind.Hold"/> once the finger has been down
    /// and still for <see cref="HoldDelay"/>.
    /// </summary>
    public CodeEditorTouchGestureResult HoldElapsed(double time)
    {
        if (!IsActive || _holdDone || IsDragging || time - _pressTime < HoldDelay)
        {
            return CodeEditorTouchGestureResult.None;
        }

        _holdDone = true;
        _lastSetTapCountTime = double.NegativeInfinity;
        return new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Hold, _drag.Origin);
    }

    /// <summary>A finger left: a tap, a hold (when no timer saw it), a flick, or nothing.</summary>
    public CodeEditorTouchGestureResult Release(long pointerId, Point point, double time)
    {
        if (pointerId != _pointerId)
        {
            if (_otherPointers > 0)
            {
                _otherPointers--;
            }

            return CodeEditorTouchGestureResult.None;
        }

        var cancelled = _cancelled;
        var holdDone = _holdDone;
        var holdTime = time - _pressTime;
        var wasDragging = _drag.End(point, TimeSpan.FromMilliseconds(time));
        var velocity = _drag.ReleaseVelocity;
        _pointerId = null;
        if (cancelled || holdDone)
        {
            return CodeEditorTouchGestureResult.None;
        }

        if (wasDragging)
        {
            var perMillisecond = velocity / 1000;
            return (perMillisecond.X == 0 && perMillisecond.Y == 0) || !double.IsFinite(perMillisecond.X) || !double.IsFinite(perMillisecond.Y)
                ? CodeEditorTouchGestureResult.None
                : new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Inertia, point, Velocity: perMillisecond);
        }

        if (holdTime >= HoldDelay)
        {
            _lastSetTapCountTime = double.NegativeInfinity;
            return new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Hold, _drag.Origin);
        }

        // upstream's dispatchEvent: a second tap soon after the first is a double tap
        var tapCount = time - _lastSetTapCountTime > ClearTapCountTime ? 1 : 2;
        _lastSetTapCountTime = time;
        return new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Tap, _drag.Origin, TapCount: tapCount);
    }

    /// <summary>The pointer was lost (capture taken away): the gesture ends without a result.</summary>
    public void Cancel()
    {
        _pointerId = null;
        _otherPointers = 0;
        _cancelled = false;
        _drag.Cancel();
    }

    /// <summary>
    /// One step of a flick (upstream's inertia): the velocity slows by <see cref="ScrollFriction"/> per millisecond on each
    /// axis; returns the translation of this step and the new velocity, which is zero once it stopped.
    /// </summary>
    public static (Vector Translation, Vector Velocity) InertiaStep(Vector velocity, double elapsedMilliseconds)
    {
        var speedX = Math.Abs(velocity.X) + (ScrollFriction * elapsedMilliseconds);
        var speedY = Math.Abs(velocity.Y) + (ScrollFriction * elapsedMilliseconds);
        var deltaX = speedX > 0 ? Math.Sign(velocity.X) * speedX * elapsedMilliseconds : 0;
        var deltaY = speedY > 0 ? Math.Sign(velocity.Y) * speedY * elapsedMilliseconds : 0;
        return (new Vector(deltaX, deltaY), new Vector(speedX > 0 ? Math.Sign(velocity.X) * speedX : 0, speedY > 0 ? Math.Sign(velocity.Y) * speedY : 0));
    }
}
