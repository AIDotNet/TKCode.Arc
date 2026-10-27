using Avalonia.Input;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>MRR waterfall: the accumulation, axis, bar, label and ratio rules (pure), asserting the notes' measured numbers.</summary>
public class MrrWaterfallEngineTests
{
    // A quarter: start 286,400, then +39,800 +21,670 +4,820 −9,470 −18,460 (the demo data's Q2).
    private static readonly double[] s_amounts = [39_800, 21_670, 4_820, -9_470, -18_460];

    private static double[] Levels()
    {
        var levels = new double[6];
        MrrWaterfallEngine.Levels(286_400, s_amounts, levels);
        return levels;
    }

    [Fact]
    public void Levels_Accumulate_Every_Step_And_The_Last_Is_The_Ending_Level()
    {
        var levels = Levels();
        levels.ShouldBe([286_400, 326_200, 347_870, 352_690, 343_220, 324_760]);
    }

    [Fact]
    public void Axis_Reaches_Six_Tenths_Of_The_Span_Below_And_A_Twentieth_Above_And_Steps_By_A_Ladder_Value()
    {
        // min 286,400, max 352,690, span 66,290: raw 246,626 … 356,004.5, a sixth of that is 18,230 → the 20,000 step.
        var ticks = MrrWaterfallEngine.Ticks(Levels());
        ticks.Step.ShouldBe(20_000);
        ticks.Min.ShouldBe(240_000);
        ticks.Max.ShouldBe(360_000);
        ticks.Ticks.ShouldBe([240_000, 260_000, 280_000, 300_000, 320_000, 340_000, 360_000]);
    }

    [Fact]
    public void Axis_Takes_A_25k_Step_For_A_Wider_Bridge_And_Always_Covers_Every_Level()
    {
        double[] levels = [400_000, 460_000, 490_000, 497_000, 483_000, 457_000];
        var ticks = MrrWaterfallEngine.Ticks(levels);
        // raw 341,800 … 501,850: a sixth is 26,675, nearest ladder step 25,000.
        ticks.Step.ShouldBe(25_000);
        ticks.Min.ShouldBe(325_000);
        ticks.Max.ShouldBe(525_000);
        ticks.Ticks.Length.ShouldBe(9);
        foreach (var level in levels)
        {
            level.ShouldBeGreaterThanOrEqualTo(ticks.Min);
            level.ShouldBeLessThanOrEqualTo(ticks.Max);
        }
    }

    [Fact]
    public void Axis_In_Percent_Of_Start_Runs_85_To_125_By_Five()
    {
        var percent = Levels().Select(v => v / 286_400 * 100).ToArray();
        percent[0].ShouldBe(100, 1e-9);
        var ticks = MrrWaterfallEngine.Ticks(percent);
        ticks.Step.ShouldBe(5);
        ticks.Min.ShouldBe(85);
        ticks.Max.ShouldBe(125);
        ticks.Ticks.Length.ShouldBe(9);
    }

    [Theory]
    [InlineData(822, 300)]
    [InlineData(678, 298)]
    [InlineData(660, 290)]
    [InlineData(606, 267)]
    [InlineData(592, 260)]
    [InlineData(534, 235)]
    [InlineData(527, 232)]
    [InlineData(300, 232)]
    [InlineData(1400, 300)]
    public void Chart_Height_Is_044_Of_The_Width_Clamped_To_232_300(double width, double height)
    {
        MrrWaterfallEngine.ChartHeight(width).ShouldBe(height);
    }

    [Fact]
    public void Band_Is_The_Plot_Width_Over_The_Bars_And_Bars_Are_56_Percent_Of_It_Up_To_56()
    {
        MrrWaterfallEngine.Band(822, 7).ShouldBe(110.571428, 1e-5);
        MrrWaterfallEngine.BarWidth(110.571).ShouldBe(56);
        // Measured bar widths 56 / 52 / 50 / 49 / 45 / 44 / 39 / 36 / 33 / 32 / 27 / 21 / 19 over bands of the rule 0.56 x band.
        (double Band, double Width)[] cases =
            [(100, 56), (93, 52), (89, 50), (87.5, 49), (80, 45), (78.6, 44), (69.6, 39), (64, 36), (58.9, 33), (57, 32), (48.2, 27), (37.5, 21), (34, 19)];
        foreach (var (band, width) in cases)
        {
            MrrWaterfallEngine.BarWidth(band).ShouldBe(width);
        }
    }

    [Fact]
    public void Bar_Left_Is_Centred_In_Its_Band_And_Rounded()
    {
        // Band 0 of 822: from 48 to 158.57, centre 103.29, width 56 → 75.29 → 75.
        MrrWaterfallEngine.BarLeft(48, MrrWaterfallEngine.Band(822, 7)).ShouldBe(75);
        MrrWaterfallEngine.BarLeft(48 + 110.5714, 110.5714).ShouldBe(186);
    }

    [Fact]
    public void Band_Under_72_Uses_Short_Names_And_Under_48_Small_Type()
    {
        MrrWaterfallEngine.IsCompactBand(71.9).ShouldBeTrue();
        MrrWaterfallEngine.IsCompactBand(72).ShouldBeFalse();
        MrrWaterfallEngine.IsTinyBand(47.9).ShouldBeTrue();
        MrrWaterfallEngine.IsTinyBand(48).ShouldBeFalse();
    }

    [Fact]
    public void Index_At_Finds_The_Band_Under_The_Pointer_And_Clamps()
    {
        MrrWaterfallEngine.IndexAt(10, 822, 7).ShouldBe(0);
        MrrWaterfallEngine.IndexAt(48, 822, 7).ShouldBe(0);
        MrrWaterfallEngine.IndexAt(158.5, 822, 7).ShouldBe(0);
        MrrWaterfallEngine.IndexAt(158.7, 822, 7).ShouldBe(1);
        MrrWaterfallEngine.IndexAt(5000, 822, 7).ShouldBe(6);
        MrrWaterfallEngine.IndexAt(100, 822, 0).ShouldBe(-1);
    }

    [Fact]
    public void Bars_Stand_On_The_Baseline_Or_Float_Between_Levels_In_Whole_Pixels()
    {
        var levels = Levels();
        var ticks = MrrWaterfallEngine.Ticks(levels);
        const double height = 300;
        var bottom = MrrWaterfallEngine.PlotBottom(height);
        bottom.ShouldBe(266);
        var scale = new AnalyticsLinearScale(ticks.Min, ticks.Max, bottom, MrrWaterfallEngine.PlotTop);
        var band = MrrWaterfallEngine.Band(822, 7);

        var start = MrrWaterfallEngine.Bar(0, 7, levels, scale, bottom, 48, band);
        start.Bottom.ShouldBe(266);
        start.Top.ShouldBe(Math.Round(scale.Map(286_400)));
        start.Width.ShouldBe(56);

        var growth = MrrWaterfallEngine.Bar(1, 7, levels, scale, bottom, 48 + band, band);
        growth.Top.ShouldBe(Math.Round(scale.Map(326_200)));
        growth.Bottom.ShouldBe(Math.Round(scale.Map(286_400)));
        growth.Top.ShouldBe(Math.Floor(growth.Top));
        growth.Height.ShouldBe(growth.Bottom - growth.Top);

        var churn = MrrWaterfallEngine.Bar(5, 7, levels, scale, bottom, 48 + 5 * band, band);
        churn.Top.ShouldBe(Math.Round(scale.Map(343_220)));
        churn.Bottom.ShouldBe(Math.Round(scale.Map(324_760)));

        var end = MrrWaterfallEngine.Bar(6, 7, levels, scale, bottom, 48 + 6 * band, band);
        end.Top.ShouldBe(Math.Round(scale.Map(324_760)));
        end.Bottom.ShouldBe(266);
    }

    [Fact]
    public void Connectors_Sit_At_The_Running_Level_Minus_Half_A_Pixel_And_Labels_Six_Pixels_Off_The_Bar()
    {
        var levels = Levels();
        var ticks = MrrWaterfallEngine.Ticks(levels);
        var scale = new AnalyticsLinearScale(ticks.Min, ticks.Max, 266, 30);
        MrrWaterfallEngine.ConnectorY(0, levels, scale).ShouldBe(Math.Round(scale.Map(286_400)) - 0.5);
        MrrWaterfallEngine.ConnectorY(3, levels, scale).ShouldBe(Math.Round(scale.Map(352_690)) - 0.5);
        MrrWaterfallEngine.ConnectorY(5, levels, scale).ShouldBe(Math.Round(scale.Map(324_760)) - 0.5);

        var box = new MrrBarBox(100, 56, 80, 40);
        MrrWaterfallEngine.LabelAnchor(box, below: false).ShouldBe(74);
        MrrWaterfallEngine.LabelAnchor(box, below: true).ShouldBe(126);
        MrrWaterfallEngine.IsBelow(4, 7, levels).ShouldBeTrue();
        MrrWaterfallEngine.IsBelow(5, 7, levels).ShouldBeTrue();
        MrrWaterfallEngine.IsBelow(1, 7, levels).ShouldBeFalse();
        MrrWaterfallEngine.IsBelow(0, 7, levels).ShouldBeFalse();
        MrrWaterfallEngine.IsBelow(6, 7, levels).ShouldBeFalse();
    }

    [Fact]
    public void Break_Mark_Is_A_Slanted_Band_And_The_Marker_Sits_Under_The_Baseline()
    {
        MrrWaterfallEngine.BreakMark(56).ShouldBe([(0, 5), (56, 0), (56, 3), (0, 8)]);
        MrrWaterfallEngine.Marker(266).ShouldBe((268, 2));
    }

    [Fact]
    public void Headline_Ratios_Follow_The_Measured_Formulas()
    {
        // NRR leaves out new business and reactivation; quick ratio is added over lost; logo churn is lost accounts over start.
        MrrWaterfallEngine.NetRevenueRetention(286_400, 21_670, 9_470, 18_460)!.Value.ShouldBe(280_140.0 / 286_400, 1e-12);
        MrrWaterfallEngine.QuickRatio(39_800 + 21_670 + 4_820, 9_470 + 18_460)!.Value.ShouldBe(66_290.0 / 27_930, 1e-12);
        MrrWaterfallEngine.LogoChurn(79, 892)!.Value.ShouldBe(79.0 / 892, 1e-12);
        MrrWaterfallEngine.QuickRatio(100, 0).ShouldBeNull();
        MrrWaterfallEngine.NetRevenueRetention(0, 1, 1, 1).ShouldBeNull();
        MrrWaterfallEngine.LogoChurn(1, 0).ShouldBeNull();
    }

    [Fact]
    public void Keys_Move_The_Bar_With_Wrap_Around()
    {
        MrrWaterfallEngine.Navigate(Key.Right, 6, 7).ShouldBe(0);
        MrrWaterfallEngine.Navigate(Key.Down, 6, 7).ShouldBe(0);
        MrrWaterfallEngine.Navigate(Key.Left, 0, 7).ShouldBe(6);
        MrrWaterfallEngine.Navigate(Key.Up, 0, 7).ShouldBe(6);
        MrrWaterfallEngine.Navigate(Key.Right, 2, 7).ShouldBe(3);
        MrrWaterfallEngine.Navigate(Key.Home, 4, 7).ShouldBe(0);
        MrrWaterfallEngine.Navigate(Key.End, 4, 7).ShouldBe(6);
        MrrWaterfallEngine.Navigate(Key.A, 4, 7).ShouldBeNull();
    }

    [Fact]
    public void Rows_List_The_Largest_Accounts_And_Group_The_Rest()
    {
        var accounts = new List<MrrAccount>
        {
            new("a", 1_000, "d", 1), new("b", 5_000, null, 1), new("c", 3_000, null, 1), new("d", 2_000, null, 1), new("e", 4_000, null, 1), new("f", 500, null, 1)
        };
        var rows = MrrWaterfallEngine.Rows(accounts, 20_000, 40, 5, true, "rest", "Rest", n => $"{n} accounts");
        rows.Select(r => r.Name).ShouldBe(["b", "e", "c", "d", "a", "Rest"]);
        var rest = rows[^1];
        rest.IsRest.ShouldBeTrue();
        rest.Amount.ShouldBe(20_000 - 15_000);
        rest.AccountCount.ShouldBe(40 - 5);
        rest.Detail.ShouldBe("35 accounts");
        rows.Sum(r => r.Share).ShouldBe(1, 1e-9);
        rows[0].Share.ShouldBe(0.25, 1e-12);
    }

    [Fact]
    public void Rows_Of_A_Total_Have_No_Remainder_And_Shares_Add_To_One()
    {
        var tiers = new List<MrrAccount> { new("Business", 100, null, 2), new("Team", 300, null, 8) };
        var rows = MrrWaterfallEngine.Rows(tiers, 400, 10, int.MaxValue, false, "rest", "Rest", n => n.ToString());
        rows.Count.ShouldBe(2);
        rows.Sum(r => r.Share).ShouldBe(1, 1e-9);
        rows.Any(r => r.IsRest).ShouldBeFalse();
    }

    [Fact]
    public void Rows_Skip_The_Remainder_When_The_Named_Accounts_Explain_The_Whole_Step()
    {
        var accounts = new List<MrrAccount> { new("a", 600, null, 1), new("b", 400, null, 1) };
        MrrWaterfallEngine.Rows(accounts, 1_000, 2, 5, true, "rest", "Rest", n => n.ToString()).Count.ShouldBe(2);
    }
}
