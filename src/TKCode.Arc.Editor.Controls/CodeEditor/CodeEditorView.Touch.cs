// Touch (upstream's TouchHandler and PointerEventHandler in src/vs/editor/browser/controller/pointerHandler.ts, commit
// b57d5c9e, MIT license): a tap focuses the editor and moves the cursor (revealType Minimal), a double tap selects the
// word (dispatchMouse with tapCount 2), a drag scrolls (onChange: deltaScrollNow by the translation) and keeps coasting
// after a flick; the gestures come from CodeEditorTouchGesture (touch.ts).
// Differences from upstream:
//  - a long press selects the word under the finger (upstream opens the context menu, which the control does not
//    have) and, like a double tap, shows the selection handles (CodeEditorTouchSelection.cs) that drag either end;
//  - only touch pointers come here; a pen and the mouse keep the mouse handling (CodeEditorView.Mouse.cs) unchanged;
//  - a press does not focus the editor (so scrolling does not bring up a soft keyboard); a tap, double tap or long
//    press does, as upstream's onTap focuses the textarea;
//  - the flick runs on a 16 ms dispatcher timer instead of animation frames.

using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    internal const string TouchSource = "touch";

    private readonly CodeEditorTouchGesture _touchGesture = new();
    private DispatcherTimer? _holdTimer;
    private DispatcherTimer? _inertiaTimer;
    private Vector _inertiaVelocity;
    private long _inertiaLastTimestamp;
    private CodeEditorTouchSelection? _touchSelection;

    /// <summary>The touch gesture recognizer (for tests).</summary>
    internal CodeEditorTouchGesture TouchGesture => _touchGesture;

    /// <summary>Whether a flick is still scrolling the view.</summary>
    internal bool IsCoasting => _inertiaTimer is { IsEnabled: true };

    /// <summary>The selection handles shown after a touch selected a word (null before the first one).</summary>
    internal CodeEditorTouchSelection? TouchSelection => _touchSelection;

    private static bool IsTouch(PointerEventArgs e) => e.Pointer.Type == PointerType.Touch;

    // ---- pointer events of touch pointers (called by the pointer overrides in CodeEditorView.Mouse.cs)

    private void OnTouchPressed(PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is null || _editor is null)
        {
            return;
        }

        _editor.OnBeforeInteractionEvent();
        StopInertia();
        var wasActive = _touchGesture.IsActive;
        _touchGesture.Press(e.Pointer.Id, e.GetPosition(this), e.Timestamp);
        if (!wasActive && _touchGesture.IsActive)
        {
            e.Pointer.Capture(this);
            StartHoldTimer();
        }
    }

    private void OnTouchMoved(PointerEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is null)
        {
            return;
        }

        var result = _touchGesture.Move(e.Pointer.Id, e.GetPosition(this), e.Timestamp);
        if (result.Kind == CodeEditorTouchGestureKind.Change)
        {
            _holdTimer?.Stop();
            _viewModel.ViewLayout.DeltaScrollNow(-result.Translation.X, -result.Translation.Y);
        }
    }

    private void OnTouchReleased(PointerReleasedEventArgs e)
    {
        e.Handled = true;
        _holdTimer?.Stop();
        var result = _touchGesture.Release(e.Pointer.Id, e.GetPosition(this), e.Timestamp);
        if (!_touchGesture.IsActive)
        {
            e.Pointer.Capture(null);
        }

        HandleTouchResult(result);
    }

    private void OnTouchCaptureLost()
    {
        _holdTimer?.Stop();
        _touchGesture.Cancel();
    }

    /// <summary>Acts on a recognized gesture (tests call it with results of their own).</summary>
    internal void HandleTouchResult(CodeEditorTouchGestureResult result)
    {
        if (_viewModel is null || _editor is null)
        {
            return;
        }

        switch (result.Kind)
        {
            case CodeEditorTouchGestureKind.Tap:
                _editor.Focus();
                CancelComposition();
                DispatchTouch(result.Point, result.TapCount);
                break;
            case CodeEditorTouchGestureKind.Hold:
                _editor.Focus();
                CancelComposition();
                DispatchTouch(result.Point, tapCount: 2);
                break;
            case CodeEditorTouchGestureKind.Inertia:
                StartInertia(result.Velocity);
                break;
        }
    }

    // upstream's _dispatchGesture / TouchHandler.onTap: one tap moves the cursor, two select the word; a word selection
    // brings the handles
    private void DispatchTouch(Point point, int tapCount)
    {
        var viewModel = _viewModel!;
        var viewPosition = GetViewPositionAt(new Point(Math.Max(point.X, GutterWidth), point.Y));
        var options = new CoreNavigationCommands.MoveCommandOptions
        {
            Source = TouchSource,
            Position = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(viewPosition),
            ViewPosition = viewPosition,
            RevealType = NavigationCommandRevealType.Minimal,
        };
        if (tapCount >= 2)
        {
            CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, options);
            ShowTouchSelection();
        }
        else
        {
            HideTouchSelection();
            CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, options);
        }

        RestartBlink();
    }

    // ---- long press

    private void StartHoldTimer()
    {
        _holdTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(CodeEditorTouchGesture.HoldDelay), DispatcherPriority.Input, (_, _) =>
        {
            // the dispatcher's clock is not the pointer events' one: the delay has passed since the press
            _holdTimer!.Stop();
            HoldElapsed(_touchGesture.PressTime + CodeEditorTouchGesture.HoldDelay);
        });
        _holdTimer.Stop();
        _holdTimer.Start();
    }

    /// <summary>The hold timer fired (tests pass the pointer timestamp the hold is measured against).</summary>
    internal void HoldElapsed(double timestampMilliseconds) =>
        HandleTouchResult(_touchGesture.HoldElapsed(timestampMilliseconds));

    // ---- flick

    private void StartInertia(Vector velocity)
    {
        _inertiaVelocity = velocity;
        _inertiaTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_inertiaLastTimestamp, now).TotalMilliseconds;
            _inertiaLastTimestamp = now;
            InertiaTick(elapsed);
        });
        _inertiaLastTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        _inertiaTimer.Start();
    }

    /// <summary>One step of a flick: scrolls by the time since the last step and slows down; stops at rest.</summary>
    internal void InertiaTick(double elapsedMilliseconds)
    {
        if (_viewModel is null)
        {
            StopInertia();
            return;
        }

        var (translation, velocity) = CodeEditorTouchGesture.InertiaStep(_inertiaVelocity, Math.Clamp(elapsedMilliseconds, 0, 100));
        _inertiaVelocity = velocity;
        if (translation.X != 0 || translation.Y != 0)
        {
            _viewModel.ViewLayout.DeltaScrollNow(-translation.X, -translation.Y);
        }

        if (velocity.X == 0 && velocity.Y == 0)
        {
            StopInertia();
        }
    }

    private void StopInertia()
    {
        _inertiaTimer?.Stop();
        _inertiaVelocity = default;
    }

    // ---- selection handles

    private void ShowTouchSelection()
    {
        if (_editor is null)
        {
            return;
        }

        _touchSelection ??= new CodeEditorTouchSelection(_editor, this);
        _touchSelection.Show();
    }

    private void HideTouchSelection() => _touchSelection?.Hide();
}
