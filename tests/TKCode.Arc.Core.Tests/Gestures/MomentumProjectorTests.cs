using TKCode.Arc.Gestures;

namespace TKCode.Arc.Core.Tests.Gestures;

public class MomentumProjectorTests
{
    [Fact]
    public void Decay_Projection_Matches_The_Arc_Formula()
    {
        // v/1000 × rate/(1 − rate): 0.99 ≈ 0.1s of travel, 0.995 ≈ 0.2s.
        MomentumProjector.Project(1000, 0.99).ShouldBe(99, 1e-9);
        MomentumProjector.Project(-500, 0.995).ShouldBe(-99.5, 1e-9);
        Should.Throw<ArgumentOutOfRangeException>(() => MomentumProjector.Project(100, 1));
    }

    [Fact]
    public void Linear_Projection_Coasts_For_The_Horizon()
    {
        MomentumProjector.Project(800, TimeSpan.FromSeconds(0.2)).ShouldBe(160, 1e-9);
    }

    [Fact]
    public void Nearest_Snap_Point_Wins_Without_A_Flick()
    {
        // Bottom sheet offsets: tallest detent 0, smaller detent 300, closed 640.
        double[] points = [0, 300, 640];
        var options = SnapProjection.Linear(TimeSpan.FromSeconds(0.2), fromIndex: 1, flickVelocity: 320);
        MomentumProjector.SelectSnapPoint(points, 280, 0, options).ShouldBe(1);
        MomentumProjector.SelectSnapPoint(points, 160, 100, options).ShouldBe(1);
        MomentumProjector.SelectSnapPoint(points, 100, -200, options).ShouldBe(0);
    }

    [Fact]
    public void A_Fast_Flick_Moves_At_Least_One_Point_In_Its_Direction()
    {
        double[] points = [0, 300, 640];
        var options = SnapProjection.Linear(TimeSpan.FromSeconds(0.2), fromIndex: 1, flickVelocity: 320);
        // Projection 300 + 400·0.2 = 380 still lands on 300, but 400 px/s down is a flick: next point down.
        MomentumProjector.SelectSnapPoint(points, 300, 400, options).ShouldBe(2);
        MomentumProjector.SelectSnapPoint(points, 300, -400, options).ShouldBe(0);
        // Below the flick speed it stays.
        MomentumProjector.SelectSnapPoint(points, 300, 300, options).ShouldBe(1);
    }

    [Fact]
    public void Flick_Works_With_Unsorted_Points_And_Stops_At_The_End()
    {
        double[] points = [640, 0, 300];
        var options = SnapProjection.Decay(0.99, fromIndex: 0, flickVelocity: 320);
        MomentumProjector.SelectSnapPoint(points, 640, 500, options).ShouldBe(0);
        MomentumProjector.SelectSnapPoint(points, 640, -500, options).ShouldBe(2);
    }

    [Fact]
    public void Empty_Points_Return_Minus_One()
    {
        MomentumProjector.SelectSnapPoint([], 10, 0, default).ShouldBe(-1);
        MomentumProjector.NearestIndex([], 10).ShouldBe(-1);
    }
}
