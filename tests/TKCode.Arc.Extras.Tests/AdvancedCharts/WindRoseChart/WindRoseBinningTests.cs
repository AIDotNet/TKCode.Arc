namespace TKCode.Arc.Tests.AdvancedCharts;

/// <summary>The pure parts of the wind rose: sorting raw observations, the displayed values, the axis and direction names.</summary>
public class WindRoseBinningTests
{
    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(11.249, 16, 0)]
    [InlineData(11.25, 16, 1)]
    [InlineData(348.74, 16, 15)]
    [InlineData(348.75, 16, 0)]
    [InlineData(359.99, 16, 0)]
    [InlineData(360, 16, 0)]
    [InlineData(-10, 16, 0)]
    [InlineData(-11.26, 16, 15)]
    [InlineData(810, 8, 2)]
    [InlineData(22.49, 8, 0)]
    [InlineData(22.5, 8, 1)]
    [InlineData(337.49, 8, 7)]
    [InlineData(337.5, 8, 0)]
    [InlineData(5.62, 32, 0)]
    [InlineData(5.625, 32, 1)]
    [InlineData(354.375, 32, 0)]
    [InlineData(185, 36, 19)]
    public void Sector_Boundaries_Start_Half_A_Sector_Before_The_Centre_And_Wrap_At_North(double degrees, int count, int sector)
        => WindRoseBinning.SectorOf(degrees, count).ShouldBe(sector);

    [Fact]
    public void A_Direction_That_Is_Not_A_Number_Has_No_Sector()
    {
        WindRoseBinning.SectorOf(double.NaN, 16).ShouldBe(-1);
        WindRoseBinning.SectorOf(double.PositiveInfinity, 16).ShouldBe(-1);
    }

    [Fact]
    public void A_Speed_Goes_To_The_Last_Bin_Whose_Minimum_Is_Not_Above_It()
    {
        double[] minimums = [0.5, 2, 4];
        WindRoseBinning.BinOf(0.2, minimums).ShouldBe(0);
        WindRoseBinning.BinOf(0.5, minimums).ShouldBe(0);
        WindRoseBinning.BinOf(1.99, minimums).ShouldBe(0);
        WindRoseBinning.BinOf(2, minimums).ShouldBe(1);
        WindRoseBinning.BinOf(3.99, minimums).ShouldBe(1);
        WindRoseBinning.BinOf(4, minimums).ShouldBe(2);
        WindRoseBinning.BinOf(100, minimums).ShouldBe(2);
        WindRoseBinning.BinOf(double.NaN, minimums).ShouldBe(-1);
        WindRoseBinning.BinOf(-1, minimums).ShouldBe(-1);
    }

    [Fact]
    public void Binning_Counts_Calm_Strictly_Below_The_Threshold_And_Skips_Invalid_Readings()
    {
        WindObservation[] observations =
        [
            new(0, 0.4),            // calm
            new(double.NaN, 0.1),   // calm: a calm reading needs no direction
            new(0, 0.5),            // north, first bin (the threshold itself is not calm)
            new(359, 3),            // north, second bin
            new(90, 10),            // east, third bin
            new(double.NaN, 3),     // skipped: no direction
            new(45, double.NaN),    // skipped: no speed
            new(45, -1)             // skipped: negative speed
        ];
        var data = WindRoseBinning.Bin(observations, 4, [0.5, 2, 4]);
        data.Calm.ShouldBe(2);
        data.Total.ShouldBe(5);
        data.Skipped.ShouldBe(3);
        data.Rows.Count.ShouldBe(4);
        data.Rows.Select(r => (double)r.Direction!).ShouldBe([0.0, 90.0, 180.0, 270.0]);
        data.Rows[0].Values.ShouldBe([1, 1, 0]);
        data.Rows[1].Values.ShouldBe([0, 0, 1]);
        data.Rows[2].Values.ShouldBe([0, 0, 0]);
    }

    [Fact]
    public void Binning_Reads_Any_Items_And_Defaults_To_The_Chart_Bins()
    {
        var readings = new[] { (Heading: 180.0, Knots: 12.0), (Heading: 175.0, Knots: 1.0), (Heading: 10.0, Knots: 0.0) };
        var data = WindRoseBinning.Bin(readings, r => r.Heading, r => r.Knots, 8);
        data.Rows.Count.ShouldBe(8);
        data.Rows[4].Values.Count.ShouldBe(WindRoseBinning.DefaultBinMinimums.Count);
        data.Rows[4].Values[0].ShouldBe(1);
        data.Rows[4].Values[^1].ShouldBe(1);
        data.Calm.ShouldBe(1);
        Should.Throw<ArgumentOutOfRangeException>(() => WindRoseBinning.Bin(readings, r => r.Heading, r => r.Knots, 0));
    }

    [Fact]
    public void Percent_Divides_By_All_Observations_Including_Calm_And_Count_Keeps_The_Amounts()
    {
        // North 2 + 1, east 1, calm 1: five in all.
        double[] raw = [2, 1, 1, 0, 0, 0, 0, 0];
        var percent = WindRoseModel.Build(4, 2, raw, 1, true, double.NaN, double.NaN);
        percent.Total.ShouldBe(5);
        percent.Display.ShouldBe([40, 20, 20, 0, 0, 0, 0, 0], 1e-9);
        percent.Top.ShouldBe([40, 60, 20, 20, 0, 0, 0, 0], 1e-9);
        percent.CalmDisplay.ShouldBe(20, 1e-9);
        percent.SectorTotals.ShouldBe([3, 1, 0, 0]);
        percent.BinTotals.ShouldBe([3, 1]);
        percent.MaxStack.ShouldBe(60, 1e-9);
        percent.Maximum.ShouldBe(60);
        percent.Step.ShouldBe(20);

        var count = WindRoseModel.Build(4, 2, raw, 1, false, double.NaN, double.NaN);
        count.Display.ShouldBe(raw);
        count.Top.ShouldBe([2, 3, 1, 1, 0, 0, 0, 0]);
        count.CalmDisplay.ShouldBe(1);
        count.Maximum.ShouldBe(3);
        count.Step.ShouldBe(1);
    }

    [Fact]
    public void Negative_And_Missing_Amounts_Count_As_Zero_And_Calm_Alone_Is_Not_Empty()
    {
        var model = WindRoseModel.Build(4, 1, [double.NaN, -3, double.PositiveInfinity, 2], -1, true, double.NaN, double.NaN);
        model.Raw.ShouldBe([0, 0, 0, 2]);
        model.Calm.ShouldBe(0);
        model.IsEmpty.ShouldBeFalse();
        WindRoseModel.Build(4, 1, [0, 0, 0, 0], 0, true, double.NaN, double.NaN).IsEmpty.ShouldBeTrue();
        var calm = WindRoseModel.Build(4, 1, [0, 0, 0, 0], 3, true, double.NaN, double.NaN);
        calm.IsEmpty.ShouldBeFalse();
        calm.CalmDisplay.ShouldBe(100);
    }

    [Theory]
    [InlineData(12.4, double.NaN, double.NaN, false, 12.5, 2.5)]
    [InlineData(13, double.NaN, double.NaN, false, 15, 5)]
    [InlineData(4.2, double.NaN, double.NaN, false, 5, 1)]
    [InlineData(0.8, double.NaN, double.NaN, false, 0.8, 0.2)]
    [InlineData(0.9, double.NaN, double.NaN, false, 1, 0.2)]
    [InlineData(12.4, 20, double.NaN, false, 20, 5)]
    [InlineData(12.4, 12, double.NaN, false, 12, 2.5)]
    [InlineData(12.4, double.NaN, 3, false, 15, 3)]
    [InlineData(12.4, 20, 4, false, 20, 4)]
    [InlineData(7, double.NaN, double.NaN, true, 8, 2)]
    [InlineData(3, double.NaN, double.NaN, true, 3, 1)]
    [InlineData(1234, double.NaN, double.NaN, true, 1250, 250)]
    public void The_Axis_Picks_A_Round_Maximum_And_At_Most_Five_Rings_Unless_Fixed(double longest, double maximum, double step, bool counts,
                                                                                  double expectedMaximum, double expectedStep)
    {
        var (max, ring) = WindRoseModel.Axis(longest, maximum, step, counts);
        max.ShouldBe(expectedMaximum, 1e-9);
        ring.ShouldBe(expectedStep, 1e-9);
    }

    [Fact]
    public void Directions_Resolve_From_Degrees_Compass_Names_And_Custom_Labels()
    {
        string[] none = [];
        WindRoseModel.SectorOf("NNE", 16, none).ShouldBe(1);
        WindRoseModel.SectorOf("nne", 16, none).ShouldBe(1);
        WindRoseModel.SectorOf("NE", 16, none).ShouldBe(2);
        WindRoseModel.SectorOf("NbE", 32, none).ShouldBe(1);
        WindRoseModel.SectorOf("NNE", 8, none).ShouldBe(1);
        WindRoseModel.SectorOf("22.5", 16, none).ShouldBe(1);
        WindRoseModel.SectorOf(" 22.5° ", 16, none).ShouldBe(1);
        WindRoseModel.SectorOf(22.5, 16, none).ShouldBe(1);
        WindRoseModel.SectorOf(90, 8, none).ShouldBe(2);
        WindRoseModel.SectorOf(270f, 4, none).ShouldBe(3);
        WindRoseModel.SectorOf(180m, 4, none).ShouldBe(2);
        WindRoseModel.SectorOf("Ost", 4, ["Nord", "Ost", "Süd", "West"]).ShouldBe(1);
        WindRoseModel.SectorOf("bogus", 16, none).ShouldBe(-1);
        WindRoseModel.SectorOf(null, 16, none).ShouldBe(-1);
        WindRoseModel.BinOf("2").ShouldBe(2);
        WindRoseModel.BinOf(1.0).ShouldBe(1);
        WindRoseModel.BinOf(1.5).ShouldBe(-1);
        WindRoseModel.BinOf(null).ShouldBe(-1);
    }

    [Fact]
    public void Xaml_Values_Accept_Spaces_And_Commas()
    {
        WindRoseValuesConverter.Parse("1.5 2, 3;4").ShouldBe([1.5, 2.0, 3.0, 4.0]);
        WindRoseValuesConverter.Parse("  ").ShouldBeEmpty();
        Should.Throw<FormatException>(() => WindRoseValuesConverter.Parse("1 two"));
    }

    [Fact]
    public void Default_Labels_Are_Compass_Points_For_Powers_Of_Two_And_Degrees_Otherwise()
    {
        WindRoseModel.DefaultLabel(0, 4).ShouldBe("N");
        WindRoseModel.DefaultLabel(1, 4).ShouldBe("E");
        WindRoseModel.DefaultLabel(3, 8).ShouldBe("SE");
        WindRoseModel.DefaultLabel(1, 16).ShouldBe("NNE");
        WindRoseModel.DefaultLabel(2, 16).ShouldBe("NE");
        WindRoseModel.DefaultLabel(15, 16).ShouldBe("NNW");
        WindRoseModel.DefaultLabel(31, 32).ShouldBe("NbW");
        WindRoseModel.DefaultLabel(1, 36).ShouldBe("10°");
        WindRoseModel.DefaultLabel(1, 12).ShouldBe("30°");
    }

    [Theory]
    [InlineData(32, 160, 30, 1)]
    [InlineData(32, 60, 30, 4)]
    [InlineData(32, 10, 30, 8)]
    [InlineData(16, 60, 30, 2)]
    [InlineData(8, 60, 30, 1)]
    [InlineData(36, 60, 30, 3)]
    [InlineData(10, 20, 30, 5)]
    public void Labels_Thin_Out_In_Steps_That_Keep_The_Cardinal_Points(int count, double ring, double width, int step)
        => WindRoseChart.LabelStepFor(count, ring, width).ShouldBe(step);
}
