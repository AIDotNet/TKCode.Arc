using TKCode.Arc.Gestures;
using Avalonia;

namespace TKCode.Arc.Core.Tests.Gestures;

public class DragRecognizerTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Starts_Only_After_The_Threshold()
    {
        var drag = new DragRecognizer(new DragOptions { Threshold = 4 });
        drag.Begin(new Point(10, 10), Ms(0));
        drag.Move(new Point(12, 11), Ms(8)).ShouldBe(DragTransition.None);
        drag.Phase.ShouldBe(DragPhase.Pending);
        drag.Move(new Point(15, 10), Ms(16)).ShouldBe(DragTransition.Started);
        drag.Phase.ShouldBe(DragPhase.Dragging);
        drag.Offset.ShouldBe(new Vector(5, 0));
        drag.Move(new Point(20, 13), Ms(24)).ShouldBe(DragTransition.Moved);
        drag.Delta.ShouldBe(new Vector(5, 3));
    }

    [Fact]
    public void A_Press_Without_Movement_Is_Not_A_Drag()
    {
        var drag = new DragRecognizer();
        drag.Begin(new Point(0, 0), Ms(0));
        drag.Move(new Point(1, 1), Ms(10));
        drag.End(new Point(1, 1), Ms(20)).ShouldBeFalse();
        drag.Phase.ShouldBe(DragPhase.Idle);
        drag.ReleaseVelocity.ShouldBe(default);
    }

    [Fact]
    public void Axis_Lock_Masks_The_Cross_Axis()
    {
        var drag = new DragRecognizer(new DragOptions { Axis = DragAxis.Vertical });
        drag.Begin(new Point(0, 0), Ms(0));
        drag.Move(new Point(3, 10), Ms(10)).ShouldBe(DragTransition.Started);
        drag.Offset.ShouldBe(new Vector(0, 10));
        drag.Move(new Point(30, 20), Ms(20));
        drag.Offset.ShouldBe(new Vector(0, 20));
        drag.Delta.ShouldBe(new Vector(0, 10));
    }

    [Fact]
    public void A_Gesture_Along_The_Other_Axis_Is_Rejected_So_Scrolling_Passes_Through()
    {
        var drag = new DragRecognizer(new DragOptions { Axis = DragAxis.Horizontal });
        drag.Begin(new Point(0, 0), Ms(0));
        drag.Move(new Point(2, 8), Ms(10)).ShouldBe(DragTransition.Rejected);
        drag.Phase.ShouldBe(DragPhase.Rejected);
        drag.Move(new Point(60, 8), Ms(20)).ShouldBe(DragTransition.None);
        drag.End(new Point(60, 8), Ms(30)).ShouldBeFalse();
    }

    [Fact]
    public void Cross_Axis_Rejection_Can_Be_Turned_Off()
    {
        var drag = new DragRecognizer(new DragOptions { Axis = DragAxis.Horizontal, RejectCrossAxis = false });
        drag.Begin(new Point(0, 0), Ms(0));
        drag.Move(new Point(2, 8), Ms(10)).ShouldBe(DragTransition.Started);
        drag.Offset.ShouldBe(new Vector(2, 0));
    }

    [Fact]
    public void Release_Reports_The_Velocity_And_A_Pause_Zeroes_It()
    {
        var drag = new DragRecognizer(new DragOptions { Axis = DragAxis.Vertical });
        drag.Begin(new Point(0, 0), Ms(0));
        for (var i = 1; i <= 8; i++)
        {
            drag.Move(new Point(0, i * 8), Ms(i * 8));
        }
        drag.End(new Point(0, 64), Ms(70)).ShouldBeTrue();
        drag.ReleaseVelocity.Y.ShouldBe(1000, 1);

        drag.Begin(new Point(0, 0), Ms(0));
        for (var i = 1; i <= 8; i++)
        {
            drag.Move(new Point(0, i * 8), Ms(i * 8));
        }
        drag.End(new Point(0, 64), Ms(400)).ShouldBeTrue();
        drag.ReleaseVelocity.ShouldBe(default);
    }

    [Fact]
    public void Reject_And_Cancel_Return_To_Idle_On_Release()
    {
        var drag = new DragRecognizer();
        drag.Begin(new Point(0, 0), Ms(0));
        drag.Move(new Point(10, 0), Ms(5)).ShouldBe(DragTransition.Started);
        drag.Reject();
        drag.Move(new Point(20, 0), Ms(10)).ShouldBe(DragTransition.None);
        drag.Cancel();
        drag.Phase.ShouldBe(DragPhase.Idle);
    }
}
