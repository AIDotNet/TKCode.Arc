namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// SeatManagerMath against the numbers observed on Arc's preview (notes 2b.2: $16 a seat, Oct 18 – Nov 18, today Oct 27,
/// 22 of 31 days left) and the invented sample (notes 11: $18 a seat).
/// </summary>
public class SeatManagerMathTests
{
    private static readonly DateTime Start = new(2026, 10, 18);
    private static readonly DateTime End = new(2026, 11, 18);
    private static readonly DateTime Today = new(2026, 10, 27);

    private static SeatManagerQuote Quote(int current, int target, decimal price) =>
        SeatManagerMath.BuildQuote(current, target, price, Start, End, Today);

    [Theory]
    [InlineData(1, "11.35")]
    [InlineData(2, "22.71")]
    [InlineData(3, "34.06")]
    [InlineData(4, "45.42")]
    [InlineData(5, "56.77")]
    [InlineData(6, "68.13")]
    [InlineData(7, "79.48")]
    [InlineData(8, "90.84")]
    [InlineData(9, "102.19")]
    [InlineData(10, "113.55")]
    [InlineData(13, "147.61")]
    public void Proration_Matches_The_Observed_Charges_And_Rounds_The_Total(int delta, string due)
    {
        var quote = Quote(12, 12 + delta, 16m);
        quote.DueToday.ShouldBe(decimal.Parse(due, System.Globalization.CultureInfo.InvariantCulture));
        quote.DaysLeft.ShouldBe(22);
        quote.PeriodDays.ShouldBe(31);
        quote.Delta.ShouldBe(delta);
    }

    [Fact]
    public void Three_Seats_Round_The_Total_Not_Each_Seat()
    {
        // Per seat 11.35 × 3 would be 34.05; Arc shows 34.06.
        Quote(12, 15, 16m).DueToday.ShouldBe(34.06m);
    }

    [Theory]
    [InlineData(12, "192")]
    [InlineData(13, "208")]
    [InlineData(14, "224")]
    [InlineData(15, "240")]
    [InlineData(25, "400")]
    public void The_New_Total_Is_Target_Times_Price(int target, string total)
    {
        Quote(12, target, 16m).NextTotal.ShouldBe(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_Sample_Plan_Prices_Match_The_Notes()
    {
        Quote(12, 13, 18m).DueToday.ShouldBe(12.77m);
        Quote(12, 14, 18m).DueToday.ShouldBe(25.55m);
        Quote(12, 15, 18m).DueToday.ShouldBe(38.32m);
        Quote(12, 25, 18m).DueToday.ShouldBe(166.06m);
        Quote(12, 12, 18m).NextTotal.ShouldBe(216m);
        Quote(12, 15, 18m).NextTotal.ShouldBe(270m);
        Quote(12, 25, 18m).NextTotal.ShouldBe(450m);
    }

    [Fact]
    public void Removing_Seats_Costs_Nothing_Today_And_Renews_At_The_End()
    {
        var quote = Quote(15, 12, 16m);
        quote.DueToday.ShouldBe(0m);
        quote.NextTotal.ShouldBe(192m);
        quote.Delta.ShouldBe(-3);
        quote.RenewsOn.ShouldBe(End);
    }

    [Fact]
    public void Currencies_Without_Minor_Units_Round_To_Whole_Amounts()
    {
        SeatManagerMath.MinorDigits("JPY").ShouldBe(0);
        SeatManagerMath.MinorDigits("eur").ShouldBe(2);
        SeatManagerMath.BuildQuote(12, 13, 2400m, Start, End, Today, 0).DueToday.ShouldBe(1703m);
    }

    [Fact]
    public void The_Public_Build_Uses_The_Plan()
    {
        var plan = new SeatManagerPlan { Name = "Team", PricePerSeat = 18m, MaxSeats = 25 };
        var quote = SeatManagerQuote.Build(12, 15, plan, Start, End, Today);
        quote.DueToday.ShouldBe(38.32m);
        quote.NextTotal.ShouldBe(270m);
        quote.Current.ShouldBe(12);
        quote.Target.ShouldBe(15);
    }

    [Fact]
    public void Cells_Are_Members_Invited_Open_Then_Added()
    {
        var cells = new List<SeatManagerCellKind>();
        SeatManagerMath.FillCells(cells, 10, 2, 12, 15);
        cells.Count.ShouldBe(15);
        cells.Take(10).ShouldAllBe(k => k == SeatManagerCellKind.Member);
        cells.Skip(10).Take(2).ShouldAllBe(k => k == SeatManagerCellKind.Invited);
        cells.Skip(12).ShouldAllBe(k => k == SeatManagerCellKind.Adding);

        SeatManagerMath.FillCells(cells, 10, 1, 12, 13);
        cells.ShouldBe(Enumerable.Repeat(SeatManagerCellKind.Member, 10)
            .Append(SeatManagerCellKind.Invited).Append(SeatManagerCellKind.Open).Append(SeatManagerCellKind.Adding).ToList());
    }

    [Fact]
    public void Removed_Seats_Turn_The_Last_Open_Cells_To_Removing()
    {
        var cells = new List<SeatManagerCellKind>();
        SeatManagerMath.FillCells(cells, 10, 2, 15, 12);
        cells.Count.ShouldBe(15);
        cells.Skip(12).ShouldAllBe(k => k == SeatManagerCellKind.Removing);
        cells.Count(k => k == SeatManagerCellKind.Open).ShouldBe(0);

        SeatManagerMath.FillCells(cells, 10, 0, 15, 13);
        cells.Count(k => k == SeatManagerCellKind.Open).ShouldBe(3);
        cells.Count(k => k == SeatManagerCellKind.Removing).ShouldBe(2);
        cells[^1].ShouldBe(SeatManagerCellKind.Removing);
    }

    [Theory]
    [InlineData(3, 0, (int)SeatManagerNoticeKind.Adding)]
    [InlineData(3, 4, (int)SeatManagerNoticeKind.Adding)]
    [InlineData(-2, 3, (int)SeatManagerNoticeKind.Removing)]
    [InlineData(0, 3, (int)SeatManagerNoticeKind.Open)]
    [InlineData(0, 0, (int)SeatManagerNoticeKind.Full)]
    public void The_Notice_Follows_The_Priority(int delta, int open, int kind)
    {
        ((int)SeatManagerMath.PickNotice(delta, open)).ShouldBe(kind);
    }

    [Fact]
    public void Steps_Clamp_And_Report_Pressing_Past_A_Limit()
    {
        SeatManagerMath.Step(23, -5, 12, 25, out var edge).ShouldBe(18);
        edge.ShouldBe(0);
        SeatManagerMath.Step(18, -5, 12, 25, out edge).ShouldBe(13);
        SeatManagerMath.Step(13, 5, 12, 25, out edge).ShouldBe(18);
        SeatManagerMath.Step(23, 5, 12, 25, out edge).ShouldBe(25);
        edge.ShouldBe(0);
        SeatManagerMath.Step(25, 5, 12, 25, out edge).ShouldBe(25);
        edge.ShouldBe(1);
        SeatManagerMath.Step(25, 1, 12, 25, out edge).ShouldBe(25);
        edge.ShouldBe(1);
        SeatManagerMath.Step(12, -1, 12, 25, out edge).ShouldBe(12);
        edge.ShouldBe(-1);
    }
}
