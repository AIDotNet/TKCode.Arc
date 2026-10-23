using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Projects;

/// <summary>The Project board's measured column rules, slot heights and proximity snapping (pure, no UI).</summary>
public class ProjectBoardLayoutTests
{
    [Theory]
    [InlineData(1440, 342.5, false)]
    [InlineData(1100, 257.5, false)]
    [InlineData(900, 207.5, false)]
    [InlineData(870, 200, false)]
    [InlineData(869, 200, true)]
    [InlineData(820, 200, true)]
    [InlineData(701, 200, true)]
    public void Wide_Columns_Share_The_Width_Down_To_200_Then_Scroll(double width, double column, bool scrolls)
    {
        var grid = ProjectBoardLayout.Columns(width, 4, narrow: false, minColumnWidth: 200);
        grid.ColumnWidth.ShouldBe(column, 0.01);
        grid.Padding.ShouldBe(20);
        grid.Scrolls.ShouldBe(scrolls);
        grid.ColumnX(0).ShouldBe(20);
        grid.ColumnX(1).ShouldBe(20 + column + 10, 0.01);
        if (scrolls)
        {
            grid.GridWidth.ShouldBe(830, 0.01);
        }
    }

    [Theory]
    [InlineData(700, 624)]
    [InlineData(640, 564)]
    [InlineData(520, 444)]
    [InlineData(390, 314)]
    [InlineData(320, 244)]
    public void Narrow_Columns_Leave_The_Next_One_Peeking_In(double width, double column)
    {
        var grid = ProjectBoardLayout.Columns(width, 4, narrow: true, minColumnWidth: 200);
        grid.ColumnWidth.ShouldBe(column, 0.01);
        grid.Padding.ShouldBe(16);
        grid.Scrolls.ShouldBeTrue();
        // The second column peeks in: 34px before the right padding, 50px in all.
        (width - grid.ColumnX(1)).ShouldBe(50, 0.01);
    }

    [Fact]
    public void Other_Stage_Counts_Use_The_Same_Rules()
    {
        ProjectBoardLayout.Columns(1440, 5, false, 200).ColumnWidth.ShouldBe((1400 - 40) / 5.0, 0.01);
        ProjectBoardLayout.Columns(1440, 6, false, 200).ColumnWidth.ShouldBe(225, 0.01);
        ProjectBoardLayout.Columns(1000, 6, false, 200).Scrolls.ShouldBeTrue();
        ProjectBoardLayout.Columns(700, 2, true, 200).ColumnWidth.ShouldBe(624, 0.01);
        ProjectBoardLayout.Columns(160, 4, true, 200).ColumnWidth.ShouldBe(ProjectBoardLayout.MinNarrowColumnWidth);
        ProjectBoardLayout.Columns(double.PositiveInfinity, 4, false, 200).ColumnWidth.ShouldBe(200);
    }

    [Fact]
    public void Slots_Are_At_Least_320_And_Grow_With_Their_Cards()
    {
        ProjectBoardLayout.SlotHeight([]).ShouldBe(320);
        ProjectBoardLayout.SlotHeight([102.2, 102.2]).ShouldBe(320);
        ProjectBoardLayout.SlotHeight([102.2, 102.2, 102.2]).ShouldBe(6 + 3 * 102.2 + 2 * 6 + 6, 0.001);
        ProjectBoardLayout.SlotHeight(Enumerable.Repeat(102.2, 9).ToList()).ShouldBe(6 + 9 * 102.2 + 8 * 6 + 6, 0.001);
    }

    [Fact]
    public void Snap_Points_Are_Column_Starts_Clamped_To_The_Scroll_Range()
    {
        var grid = ProjectBoardLayout.Columns(700, 4, narrow: true, minColumnWidth: 200);
        var points = ProjectBoardLayout.SnapPoints(grid);
        points.Count.ShouldBe(4);
        points[0].ShouldBe(0);
        points[1].ShouldBe(634, 0.01);
        points[2].ShouldBe(1268, 0.01);
        points[3].ShouldBe(grid.MaxOffset, 0.01);
        grid.MaxOffset.ShouldBe(16 + 4 * 624 + 30 + 16 - 700, 0.01);
    }

    [Theory]
    [InlineData(100, 0.0)]
    [InlineData(200, 0.0)]
    [InlineData(250, null)]
    [InlineData(400, null)]
    [InlineData(500, 634.0)]
    [InlineData(600, 634.0)]
    [InlineData(634, null)]
    [InlineData(0, null)]
    public void An_Offset_Snaps_Only_When_It_Is_Within_A_Third_Of_A_Column(double offset, double? expected)
    {
        // The measured 700px board: columns 624 wide, 634 apart; Arc snapped 100 and 200 back to 0, kept 250 and 400,
        // and pulled 500, 600 and 620 to 634.
        var points = new List<double> { 0, 634, 1268 };
        ProjectsSnapScroll.Choose(offset, points, 624 / 3.0).ShouldBe(expected);
        ProjectsSnapScroll.Choose(offset, points, 0).ShouldBeNull();
        ProjectsSnapScroll.Choose(offset, [], 200).ShouldBeNull();
    }
}
