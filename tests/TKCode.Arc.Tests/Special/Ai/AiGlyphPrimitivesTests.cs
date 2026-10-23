using Avalonia;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The shared stroke and spinner primitives of the status marks.</summary>
public class AiGlyphPrimitivesTests
{
    [Fact]
    public void Stroke_Lengths_Add_Up_Along_The_Path()
    {
        var stroke = new AiStroke(new Point(0, 0), new Point(3, 4), new Point(3, 10));
        stroke.Count.ShouldBe(3);
        stroke.LengthTo(0).ShouldBe(0);
        stroke.LengthTo(1).ShouldBe(5, 1e-9);
        stroke.Length.ShouldBe(11, 1e-9);
    }

    [Fact]
    public void Stroke_Cut_Point_Moves_Along_The_Path()
    {
        var stroke = new AiStroke(new Point(0, 0), new Point(3, 4), new Point(3, 10));
        stroke.PointAt(0).ShouldBe(new Point(0, 0));
        stroke.PointAt(1).ShouldBe(new Point(3, 10));
        stroke.PointAt(5.0 / 11).ShouldBe(new Point(3, 4));
        var half = stroke.PointAt(0.5);
        half.X.ShouldBe(3, 1e-9);
        half.Y.ShouldBe(4 + 0.5, 1e-9);
        stroke.PointAt(-1).ShouldBe(new Point(0, 0));
        stroke.PointAt(7).ShouldBe(new Point(3, 10));
    }

    [Fact]
    public void Stroke_Needs_Two_Points() =>
        Should.Throw<ArgumentException>(() => new AiStroke(new Point(0, 0)));

    [Fact]
    public void Spin_Turns_Once_Per_Period_And_Wraps()
    {
        var spin = new AiSpin(0.8);
        spin.Advance(0.2);
        spin.Angle.ShouldBe(90, 1e-9);
        spin.Advance(0.6);
        spin.Angle.ShouldBe(0, 1e-9);
        spin.Advance(0.9);
        spin.Angle.ShouldBe(45, 1e-9);
        spin.Reset();
        spin.Angle.ShouldBe(0);
        var slow = new AiSpin(0.9);
        slow.Advance(0.45);
        slow.Angle.ShouldBe(180, 1e-9);
        slow.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Spin_Turn_Rotates_About_The_Center()
    {
        var spin = new AiSpin(1);
        spin.Advance(0.25);
        var p = spin.Turn(new Point(8, 8)).Transform(new Point(8, 2));
        p.X.ShouldBe(14, 1e-9);
        p.Y.ShouldBe(8, 1e-9);
    }

    [Fact]
    public void Quarter_Arc_Runs_From_Twelve_O_Clock_To_Three()
    {
        var arc = AiSpin.QuarterArc(new Point(8, 8), 6);
        arc.Bounds.Left.ShouldBe(8, 0.01);
        arc.Bounds.Right.ShouldBe(14, 0.01);
        arc.Bounds.Top.ShouldBe(2, 0.01);
        arc.Bounds.Bottom.ShouldBe(8, 0.01);
    }
}
