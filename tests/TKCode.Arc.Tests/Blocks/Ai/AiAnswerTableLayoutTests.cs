namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiAnswerTableLayout"/>: automatic column widths as a browser lays out the answer's table.</summary>
public class AiAnswerTableLayoutTests
{
    private static readonly double[] Mins = [100, 90, 113];
    private static readonly double[] Maxes = [98, 283, 203];

    [Fact]
    public void A_Wide_Box_Shares_The_Extra_Width_In_Proportion_To_The_Longest_Lines()
    {
        var (widths, table) = AiAnswerTableLayout.Columns(679, 448, Mins, Maxes);
        table.ShouldBe(679);
        widths.Sum().ShouldBe(679, 0.001);
        // Column 0's widest word (100) is wider than its longest line (98): the line counts as 100.
        widths[0].ShouldBe(100 * 679 / 586.0, 0.001);
        widths[1].ShouldBe(283 * 679 / 586.0, 0.001);
    }

    [Fact]
    public void A_Narrow_Box_Keeps_The_Table_At_Its_Minimum_And_Scrolls()
    {
        var (widths, table) = AiAnswerTableLayout.Columns(288, 448, Mins, Maxes);
        table.ShouldBe(448);
        widths.Sum().ShouldBe(448, 0.001);
        for (var i = 0; i < 3; i++)
        {
            widths[i].ShouldBeGreaterThanOrEqualTo(Mins[i] - 0.001);
            widths[i].ShouldBeLessThanOrEqualTo(Math.Max(Mins[i], Maxes[i]) + 0.001);
        }
    }

    [Fact]
    public void A_Small_Table_Is_Not_Stretched_To_The_Minimum_Width()
    {
        var (_, table) = AiAnswerTableLayout.Columns(200, 448, [40, 40], [60, 70]);
        table.ShouldBe(200);
        // Never narrower than the widest words.
        AiAnswerTableLayout.Columns(50, 448, [40, 40], [60, 70]).Table.ShouldBe(130);
        AiAnswerTableLayout.Columns(50, 0, [40, 40], [60, 70]).Table.ShouldBe(80);
    }

    [Fact]
    public void An_Unbounded_Box_Takes_The_Longest_Lines()
    {
        var (widths, table) = AiAnswerTableLayout.Columns(double.PositiveInfinity, 448, Mins, Maxes);
        table.ShouldBe(586);
        widths.ShouldBe([100, 283, 203]);
        AiAnswerTableLayout.Columns(500, 448, [], []).Widths.ShouldBeEmpty();
    }
}
