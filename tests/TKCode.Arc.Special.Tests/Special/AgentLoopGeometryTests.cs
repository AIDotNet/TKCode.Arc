using Avalonia;

namespace TKCode.Arc.Tests.Special;

/// <summary>The agent loop's ring geometry and breakpoints, checked against the widths measured on the reference.</summary>
public class AgentLoopGeometryTests
{
    // Measured label widths: Take action 94.3 with its icon (14px), 63.5 without (12px); Verify 58.1 / 32.6.
    private const double Act = 94.3;
    private const double Verify = 58.1;
    private const double ActCompact = 63.5;
    private const double VerifyCompact = 32.6;

    [Fact]
    public void A_Wide_Column_Gets_The_Full_94_Radius_At_120_Degrees()
    {
        var geometry = AgentLoopGeometry.Solve(406, Act, Verify);
        geometry.IsCompact.ShouldBeFalse();
        geometry.Radius.ShouldBe(94);
        geometry.LowerAngle.ShouldBe(120);
        geometry.Center.ShouldBe(new Point(203, 128));
        var act = geometry.NodeOf(AgentLoopStation.Act);
        act.X.ShouldBe(284.41, 0.01);
        act.Y.ShouldBe(175, 0.01);
        var verify = geometry.NodeOf(AgentLoopStation.Verify);
        verify.X.ShouldBe(121.59, 0.01);
        geometry.NodeOf(AgentLoopStation.Gather).ShouldBe(new Point(203, 34));
        geometry.RingHeight.ShouldBe(228);
        geometry.LaneTop.ShouldBe(242);
        geometry.ColumnHeight.ShouldBe(298);
    }

    [Theory]
    [InlineData(394, 89)]
    [InlineData(375, 78)]
    [InlineData(430, 94)]
    [InlineData(467, 94)]
    public void The_Radius_Shrinks_To_Keep_The_Lower_Labels_Inside(double width, double radius)
    {
        var geometry = AgentLoopGeometry.Solve(width, Act, Verify);
        geometry.IsCompact.ShouldBeFalse();
        geometry.Radius.ShouldBe(radius);
        geometry.LowerAngle.ShouldBe(120);
    }

    [Theory]
    [InlineData(357, 94, 120)]
    [InlineData(320, 94, 120)]
    [InlineData(310, 90, 120)]
    [InlineData(295, 81, 120)]
    [InlineData(280, 73, 120)]
    [InlineData(270, 67, 120)]
    [InlineData(268, 89, 140)]
    [InlineData(240, 67, 140)]
    public void Compact_Columns_Use_12px_Labels_And_Move_The_Lower_Stations_Down_Below_270(double width, double radius, double angle)
    {
        var geometry = AgentLoopGeometry.Solve(width, ActCompact, VerifyCompact);
        geometry.IsCompact.ShouldBeTrue();
        geometry.Top.ShouldBe(30);
        geometry.LabelGap.ShouldBe(13);
        geometry.Radius.ShouldBe(radius);
        geometry.LowerAngle.ShouldBe(angle);
    }

    [Theory]
    [InlineData(406, false, 58)]
    [InlineData(394, false, 55.5)]
    [InlineData(375, false, 50)]
    [InlineData(310, true, 56)]
    [InlineData(240, true, 26.7)]
    public void Pins_Run_From_Under_The_Node_To_The_Lanes(double width, bool compact, double length)
    {
        var geometry = compact ? AgentLoopGeometry.Solve(width, ActCompact, VerifyCompact) : AgentLoopGeometry.Solve(width, Act, Verify);
        foreach (var station in new[] { AgentLoopStation.Act, AgentLoopStation.Verify })
        {
            var (from, to) = geometry.PinOf(station);
            // R (1 + cos θ) + 11: from 9 under the node to the lanes' top.
            (to.Y - from.Y).ShouldBe(length, 0.1);
            from.X.ShouldBe(to.X);
            from.Y.ShouldBe(geometry.NodeOf(station).Y + 9, 1e-9);
            to.Y.ShouldBe(geometry.LaneTop);
        }
    }

    [Fact]
    public void Chevrons_Sit_Halfway_Along_Each_Arc()
    {
        AgentLoopGeometry.Solve(406, Act, Verify).ChevronAngles.ShouldBe((60, 180, 300));
        AgentLoopGeometry.Solve(250, ActCompact, VerifyCompact).ChevronAngles.ShouldBe((70, 180, 290));
    }

    [Fact]
    public void Lanes_Split_The_Column_Or_Merge_When_Compact()
    {
        var (checks, calls) = AgentLoopGeometry.Solve(406, Act, Verify).Lanes;
        checks.ShouldBe(new Rect(0, 242, 197, 56));
        calls.ShouldBe(new Rect(209, 242, 197, 56));
        var compact = AgentLoopGeometry.Solve(320, ActCompact, VerifyCompact).Lanes;
        compact.Checks.Width.ShouldBe(320);
        compact.Calls.ShouldBe(compact.Checks);
    }

    [Theory]
    [InlineData(694, (int)AgentLoopArrangement.Wide, 406)]
    [InlineData(640, (int)AgentLoopArrangement.Wide, 352)]
    [InlineData(639, (int)AgentLoopArrangement.StackedTwoColumns, 639)]
    [InlineData(460, (int)AgentLoopArrangement.StackedTwoColumns, 460)]
    [InlineData(459, (int)AgentLoopArrangement.StackedOneColumn, 459)]
    [InlineData(320, (int)AgentLoopArrangement.StackedOneColumn, 320)]
    public void The_Context_Stack_Moves_Under_The_Ring_Below_640(double width, int arrangement, double ringWidth)
    {
        AgentLoopGeometry.ArrangementFor(width).ShouldBe((AgentLoopArrangement)arrangement);
        AgentLoopGeometry.RingColumnWidth(width).ShouldBe(ringWidth);
    }

    [Fact]
    public void Smaller_Breakpoints_Hide_The_Speed_Words_And_Compact_The_Ring()
    {
        AgentLoopGeometry.ShowsSpeedLabel(400).ShouldBeTrue();
        AgentLoopGeometry.ShowsSpeedLabel(399).ShouldBeFalse();
        AgentLoopGeometry.IsCompactColumn(360).ShouldBeFalse();
        AgentLoopGeometry.IsCompactColumn(359).ShouldBeTrue();
    }

    [Theory]
    [InlineData(744, 24)]
    [InlineData(512, 22.4)]
    [InlineData(472, 20.8)]
    [InlineData(432, 19.2)]
    [InlineData(352, 16)]
    [InlineData(300, 16)]
    public void The_Card_Padding_Eases_From_24_To_16(double cardWidth, double padding)
        => AgentLoopGeometry.CardPadding(cardWidth).ShouldBe(padding, 0.001);

    [Theory]
    [InlineData(3, (int)AgentLoopArrangement.Wide, 3, 215.8)]
    [InlineData(3, (int)AgentLoopArrangement.StackedTwoColumns, 2, 151.8)]
    [InlineData(3, (int)AgentLoopArrangement.StackedOneColumn, 3, 215.8)]
    [InlineData(4, (int)AgentLoopArrangement.Wide, 4, 279.8)]
    [InlineData(0, (int)AgentLoopArrangement.Wide, 1, 87.8)]
    public void Every_Loop_Reserves_A_Context_Slot(int loops, int arrangement, int rows, double height)
    {
        var actual = AgentLoopGeometry.ContextRows(loops, (AgentLoopArrangement)arrangement);
        actual.ShouldBe(rows);
        AgentLoopGeometry.ContextHeight(actual).ShouldBe(height, 0.001);
    }
}
