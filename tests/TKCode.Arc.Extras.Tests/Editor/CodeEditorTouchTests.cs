using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TextSelection = TKCode.Arc.Editor.TextSelection;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Touch (phase 8; upstream's pointerHandler.ts and touch.ts): tap, double tap, long press with selection handles,
/// drag scrolling with a flick, pinch ignored; the gesture recognizer on its own with exact timestamps.
/// </summary>
public class CodeEditorTouchTests
{
    private static (CodeEditor Editor, Window Window, CodeEditorView View, Button Other) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 200 };
        var other = new Button { Content = "other" };
        var window = TestHelpers.Show(new StackPanel { Children = { other, editor } });
        other.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<CodeEditorView>(editor), other);
    }

    private static Point At(Window window, CodeEditorView view, int line, int column)
    {
        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;
        var x = origin.X + view.GutterWidth + (view.CharWidth * (column - 1)) + 1 - view.ScrollLeft;
        var y = origin.Y + (view.LineHeight * (line - 0.5)) - view.ScrollTop;
        return new Point(x, y);
    }

    private static void Tap(Window window, Point point)
    {
        var finger = window.TouchBegin(point, RawInputModifiers.None);
        window.TouchEnd(finger, point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static string Many(int lines) => string.Join("\n", Enumerable.Range(1, lines).Select(i => "line " + i));

    [AvaloniaFact]
    public void Tap_Focuses_The_Editor_And_Moves_The_Cursor()
    {
        var (editor, window, view, _) = Show("hello world\nsecond line");
        editor.IsKeyboardFocusWithin.ShouldBeFalse();

        Tap(window, At(window, view, 2, 4));
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
        editor.GetSelection().ShouldBe(new TextSelection(2, 4, 2, 4));
        view.TouchSelection?.IsVisible.ShouldNotBe(true);
    }

    [AvaloniaFact]
    public void A_Touch_Press_Alone_Does_Not_Focus_Or_Move()
    {
        var (editor, window, view, _) = Show("hello");
        var finger = window.TouchBegin(At(window, view, 1, 3), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        editor.IsKeyboardFocusWithin.ShouldBeFalse();
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 1));
        window.TouchEnd(finger, At(window, view, 1, 3), RawInputModifiers.None);
    }

    [AvaloniaFact]
    public void Double_Tap_Selects_The_Word_And_Shows_The_Handles()
    {
        var (editor, window, view, _) = Show("hello world foo");
        Tap(window, At(window, view, 1, 8));
        Tap(window, At(window, view, 1, 8));

        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 7, 1, 12));
        var touch = view.TouchSelection.ShouldNotBeNull();
        touch.IsVisible.ShouldBeTrue();
        touch.StartHandle.Classes.ShouldContain(":start");
        touch.EndHandle.Classes.ShouldContain(":end");
        touch.StartHandle.Classes.ShouldNotContain(":caret");

        // the handles' tips sit under the selection's ends, at the bottom of the line
        window.CaptureRenderedFrame()?.Dispose();
        var startTip = touch.GetHandleTip(startHandle: true).ShouldNotBeNull();
        var endTip = touch.GetHandleTip(startHandle: false).ShouldNotBeNull();
        startTip.X.ShouldBe(view.GetX(1, 7), 0.5);
        endTip.X.ShouldBe(view.GetX(1, 12), 0.5);
        startTip.Y.ShouldBe(view.GetY(1) + view.LineHeight, 0.5);
    }

    [AvaloniaFact]
    public void Long_Press_Selects_The_Word_And_Release_Changes_Nothing()
    {
        var (editor, window, view, _) = Show("alpha beta gamma");
        var point = At(window, view, 1, 9);
        var finger = window.TouchBegin(point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        view.TouchGesture.IsActive.ShouldBeTrue();

        // the hold timer fires (the dispatcher's timers do not run in the headless test)
        view.HoldElapsed(view.TouchGesture.PressTime + CodeEditorTouchGesture.HoldDelay);
        editor.IsKeyboardFocusWithin.ShouldBeTrue();
        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 7, 1, 11));
        view.TouchSelection.ShouldNotBeNull().IsVisible.ShouldBeTrue();

        window.TouchEnd(finger, point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 7, 1, 11));
        view.TouchSelection!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Dragging_A_Handle_Moves_Its_End_Of_The_Selection()
    {
        var (editor, window, view, _) = Show("hello world foo bar");
        Tap(window, At(window, view, 1, 2));
        Tap(window, At(window, view, 1, 2));
        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 1, 1, 6));
        var touch = view.TouchSelection!;

        // the end handle to the end of "foo": the tip is at the bottom of the line
        touch.DragHandleTo(startHandle: false, new Point(view.GetX(1, 16), view.GetY(1) + view.LineHeight));
        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 1, 1, 16));
        touch.IsVisible.ShouldBeTrue();

        // the start handle past the end: the selection turns around the end it keeps
        touch.DragHandleTo(startHandle: true, new Point(view.GetX(1, 20), view.GetY(1) + view.LineHeight));
        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 16, 1, 20));
    }

    [AvaloniaFact]
    public void A_Finger_Drags_A_Handle()
    {
        var (editor, window, view, _) = Show("hello world foo bar");
        Tap(window, At(window, view, 1, 2));
        Tap(window, At(window, view, 1, 2));
        window.CaptureRenderedFrame()?.Dispose();
        var touch = view.TouchSelection!;
        var origin = view.TranslatePoint(default, window)!.Value;
        var tip = touch.GetHandleTip(startHandle: false)!.Value;

        // grab the end handle a little below and right of its tip, move by eight columns
        var grab = origin + tip + new Point(6, 8);
        var finger = window.TouchBegin(grab, RawInputModifiers.None);
        window.TouchMove(finger, grab + new Point(view.CharWidth * 8, 0), RawInputModifiers.None);
        window.TouchEnd(finger, grab + new Point(view.CharWidth * 8, 0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        editor.GetSelection()!.Value.Range.ShouldBe(new TextRange(1, 1, 1, 14));
        touch.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Keys_And_A_Tap_Hide_The_Handles()
    {
        var (editor, window, view, _) = Show("hello world");
        Tap(window, At(window, view, 1, 2));
        Tap(window, At(window, view, 1, 2));
        view.TouchSelection!.IsVisible.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        view.TouchSelection.IsVisible.ShouldBeFalse();

        // a word selection again, then a single tap somewhere else (well after the double tap)
        view.HandleTouchResult(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Hold, At(window, view, 1, 9) - view.TranslatePoint(default, window)!.Value));
        view.TouchSelection.IsVisible.ShouldBeTrue();
        view.HandleTouchResult(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Tap, new Point(view.GetX(1, 3), 5), TapCount: 1));
        view.TouchSelection.IsVisible.ShouldBeFalse();
        editor.GetSelection().ShouldBe(new TextSelection(1, 3, 1, 3));
    }

    [AvaloniaFact]
    public void Dragging_Scrolls_Without_Moving_The_Cursor()
    {
        var (editor, window, view, _) = Show(Many(200));
        var start = At(window, view, 8, 3);
        var finger = window.TouchBegin(start, RawInputModifiers.None);
        window.TouchMove(finger, start - new Point(0, 40), RawInputModifiers.None);
        window.TouchMove(finger, start - new Point(0, 100), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        view.ScrollTop.ShouldBe(100, 0.5);

        window.TouchEnd(finger, start - new Point(0, 100), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 1));
    }

    [AvaloniaFact]
    public void A_Flick_Coasts_And_Slows_Down()
    {
        var (_, _, view, _) = Show(Many(500));
        view.HandleTouchResult(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Inertia, Velocity: new Vector(0, -2)));
        view.IsCoasting.ShouldBeTrue();

        view.InertiaTick(16);
        var first = view.ScrollTop;
        first.ShouldBeGreaterThan(25);
        view.InertiaTick(16);
        (view.ScrollTop - first).ShouldBeLessThan(first);

        // 2 px/ms stops after 400 ms of friction
        for (var i = 0; i < 30 && view.IsCoasting; i++)
        {
            view.InertiaTick(16);
        }

        view.IsCoasting.ShouldBeFalse();
        var rest = view.ScrollTop;
        rest.ShouldBeInRange(300, 420);
    }

    [AvaloniaFact]
    public void Pinch_Does_Nothing()
    {
        var (editor, window, view, _) = Show(Many(200));
        var a = At(window, view, 3, 2);
        var b = At(window, view, 6, 2);
        var one = window.TouchBegin(a, RawInputModifiers.None);
        var two = window.TouchBegin(b, RawInputModifiers.None);
        window.TouchMove(one, a - new Point(0, 30), RawInputModifiers.None);
        window.TouchMove(two, b + new Point(0, 30), RawInputModifiers.None);
        window.TouchEnd(two, b + new Point(0, 30), RawInputModifiers.None);
        window.TouchEnd(one, a - new Point(0, 30), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        view.ScrollTop.ShouldBe(0);
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 1));
    }

    [AvaloniaFact]
    public void The_Mouse_Still_Selects_And_Stops_A_Flick()
    {
        var (editor, window, view, _) = Show(Many(500));
        view.HandleTouchResult(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Inertia, Velocity: new Vector(0, -1)));
        view.IsCoasting.ShouldBeTrue();
        window.MouseDown(At(window, view, 2, 3), MouseButton.Left);
        window.MouseUp(At(window, view, 2, 3), MouseButton.Left);
        view.IsCoasting.ShouldBeFalse();
        editor.GetSelection().ShouldBe(new TextSelection(2, 3, 2, 3));
    }

    // ---- the recognizer on its own (touch.ts)

    [AvaloniaFact]
    public void Gesture_Counts_Taps_Within_400_Ms()
    {
        var gesture = new CodeEditorTouchGesture();
        gesture.Press(1, new Point(10, 10), 1000);
        gesture.Release(1, new Point(10, 10), 1050).ShouldBe(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Tap, new Point(10, 10), TapCount: 1));
        gesture.Press(2, new Point(12, 11), 1200);
        gesture.Release(2, new Point(12, 11), 1260).TapCount.ShouldBe(2);
        gesture.Press(3, new Point(12, 11), 1800);
        gesture.Release(3, new Point(12, 11), 1850).TapCount.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Gesture_Holds_After_700_Ms_Once()
    {
        var gesture = new CodeEditorTouchGesture();
        gesture.Press(1, new Point(10, 10), 0);
        gesture.HoldElapsed(600).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        gesture.HoldElapsed(700).Kind.ShouldBe(CodeEditorTouchGestureKind.Hold);
        gesture.Move(1, new Point(60, 10), 750).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        gesture.Release(1, new Point(60, 10), 800).Kind.ShouldBe(CodeEditorTouchGestureKind.None);

        // without a timer, a long touch is a hold on release (upstream)
        gesture.Press(2, new Point(10, 10), 2000);
        gesture.Release(2, new Point(10, 10), 2800).Kind.ShouldBe(CodeEditorTouchGestureKind.Hold);
    }

    [AvaloniaFact]
    public void Gesture_Drags_Past_The_Threshold_And_Flicks()
    {
        var gesture = new CodeEditorTouchGesture();
        gesture.Press(1, new Point(100, 300), 0);
        gesture.Move(1, new Point(100, 291), 5).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        var started = gesture.Move(1, new Point(100, 280), 10);
        started.Kind.ShouldBe(CodeEditorTouchGestureKind.Change);
        started.Translation.ShouldBe(new Vector(0, -20));
        for (var t = 20; t <= 80; t += 10)
        {
            gesture.Move(1, new Point(100, 280 - (2 * (t - 10))), t).Translation.Y.ShouldBe(-20, 0.001);
        }

        var release = gesture.Release(1, new Point(100, 140), 80);
        release.Kind.ShouldBe(CodeEditorTouchGestureKind.Inertia);
        release.Velocity.Y.ShouldBe(-2, 0.1);

        // the friction: 0.005 px/ms per ms
        var (translation, velocity) = CodeEditorTouchGesture.InertiaStep(new Vector(0, -2), 100);
        velocity.Y.ShouldBe(-1.5, 1e-9);
        translation.Y.ShouldBe(-150, 1e-9);
        CodeEditorTouchGesture.InertiaStep(new Vector(0, -0.2), 100).Velocity.ShouldBe(default);
    }

    [AvaloniaFact]
    public void Gesture_A_Pause_Before_Release_Does_Not_Flick()
    {
        var gesture = new CodeEditorTouchGesture();
        gesture.Press(1, new Point(0, 100), 0);
        gesture.Move(1, new Point(0, 50), 20);
        gesture.Release(1, new Point(0, 50), 400).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
    }

    [AvaloniaFact]
    public void Gesture_A_Second_Finger_Cancels_Everything_Until_All_Are_Up()
    {
        var gesture = new CodeEditorTouchGesture();
        gesture.Press(1, new Point(0, 100), 0);
        gesture.Press(2, new Point(0, 200), 10);
        gesture.IsActive.ShouldBeFalse();
        gesture.Move(1, new Point(0, 40), 20).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        gesture.HoldElapsed(800).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        gesture.Release(2, new Point(0, 200), 30).Kind.ShouldBe(CodeEditorTouchGestureKind.None);
        gesture.Release(1, new Point(0, 40), 40).Kind.ShouldBe(CodeEditorTouchGestureKind.None);

        // a new touch works again, and the cancelled one did not count as a first tap
        gesture.Press(3, new Point(5, 5), 100);
        gesture.Release(3, new Point(5, 5), 120).ShouldBe(new CodeEditorTouchGestureResult(CodeEditorTouchGestureKind.Tap, new Point(5, 5), TapCount: 1));
    }
}
