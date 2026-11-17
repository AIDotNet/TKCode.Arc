using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Choropleth explorer: the binning, camera and format arithmetic (pure) and the block's behaviour (headless).</summary>
public class ChoroplethExplorerTests
{
    // Pure arithmetic -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Quantile_Interpolates_Linearly_Like_D3()
    {
        double[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        ChoroplethMath.Quantile(sorted, 0.2).ShouldBe(2.8, 1e-12);
        ChoroplethMath.Quantile(sorted, 0.5).ShouldBe(5.5, 1e-12);
        ChoroplethMath.Quantile(sorted, 0).ShouldBe(1);
        ChoroplethMath.Quantile(sorted, 1).ShouldBe(10);
        ChoroplethMath.Quantile([7], 0.4).ShouldBe(7);
    }

    [Theory]
    [InlineData(10.3, 10)]
    [InlineData(13.8, 14)]
    [InlineData(20.975, 21)]
    [InlineData(0.0456, 0.046)]
    [InlineData(118_400, 120_000)]
    [InlineData(-4.83, -4.8)]
    public void Edges_Round_To_Two_Significant_Digits(double value, double expected) =>
        ChoroplethMath.RoundSignificant(value).ShouldBe(expected, 1e-9);

    [Fact]
    public void Sequential_Bins_Cut_At_Quintiles_And_An_Edge_Value_Goes_Up()
    {
        var values = Enumerable.Range(1, 51).Select(v => (double?)v).ToList();
        var binning = ChoroplethMath.Bin(values, ChoroplethScale.Sequential);
        binning.BinCount.ShouldBe(5);
        binning.Edges.ShouldBe([11, 21, 31, 41]);
        binning.Counts.ShouldBe([10, 10, 10, 10, 11]);
        binning.BinOf(11).ShouldBe(1);
        binning.BinOf(10.99).ShouldBe(0);
        binning.BinOf(null).ShouldBe(-1);
        binning.Alphas.ShouldBe([0.16, 0.34, 0.54, 0.74, 0.94]);
    }

    [Fact]
    public void Diverging_Bins_Split_At_Zero_In_Proportion_To_Each_Side()
    {
        // 13 negative and 38 positive values (Arc's growth map): 2 negative steps and 4 positive ones.
        var values = new List<double?>();
        for (var i = 1; i <= 13; i++)
        {
            values.Add(-i);
        }
        for (var i = 0; i < 38; i++)
        {
            values.Add(i + 0.5);
        }
        values.Add(null);
        var binning = ChoroplethMath.Bin(values, ChoroplethScale.Diverging);
        binning.BinCount.ShouldBe(6);
        binning.NegativeBins.ShouldBe(2);
        binning.Edges.Length.ShouldBe(5);
        binning.Edges[0].ShouldBe(-7);
        binning.Edges[1].ShouldBe(0);
        binning.Alphas.ShouldBe([0.86, 0.46, 0.2, 0.42, 0.66, 0.9]);
        binning.BinOf(0).ShouldBe(2);
        binning.BinOf(-0.1).ShouldBe(1);
        binning.IsNegative(1).ShouldBeTrue();
        binning.IsNegative(2).ShouldBeFalse();
        binning.NoDataCount.ShouldBe(1);
        binning.Counts.Sum().ShouldBe(51);
    }

    [Fact]
    public void Diverging_Without_Negatives_Uses_Only_The_Positive_Hue()
    {
        var binning = ChoroplethMath.Bin(Enumerable.Range(1, 20).Select(v => (double?)v).ToList(), ChoroplethScale.Diverging);
        binning.NegativeBins.ShouldBe(0);
        binning.BinCount.ShouldBe(6);
        binning.Edges.ShouldNotContain(0);
    }

    [Theory]
    [InlineData(0.16, false, 0.2104)]
    [InlineData(0.94, false, 0.9436)]
    [InlineData(0.16, true, 0.2944)]
    [InlineData(0.94, true, 0.9496)]
    [InlineData(0.86, false, 0.8684)]
    public void Bin_Opacity_Composites_Over_The_Surface(double alpha, bool dark, double expected) =>
        ChoroplethMath.ComposeOpacity(alpha, dark).ShouldBe(expected, 1e-9);

    [Fact]
    public void Camera_Fills_80_Percent_And_Caps_At_12()
    {
        // California in the 975 × 610 Albers USA box: scale 1.9757 (0.8 × 610 / 247).
        var box = new Rect(18.7, 160, 143.9, 247);
        var (s, x, y) = ChoroplethMath.CameraFor(box, new Size(975, 610));
        s.ShouldBe(0.8 * 610 / 247, 1e-9);
        (s * box.Center.X + x).ShouldBe(487.5, 1e-9);
        (s * box.Center.Y + y).ShouldBe(305, 1e-9);
        ChoroplethMath.CameraFor(new Rect(825.9, 265.3, 3.8, 4.6), new Size(975, 610)).Scale.ShouldBe(12);
    }

    [Fact]
    public void Sweep_Delay_Is_The_Centre_Over_The_Width_Times_260ms()
    {
        // Alaska's measured wave .1169 → 30.4 ms.
        var wave = ChoroplethMath.Wave(new Rect(25, 465, 178, 138), 975);
        wave.ShouldBe(114 / 975.0, 1e-9);
        ChoroplethMath.SweepDelay(wave).TotalMilliseconds.ShouldBe(30.4, 0.05);
        ChoroplethMath.SweepDelay(1).TotalMilliseconds.ShouldBe(260);
    }

    [Theory]
    [InlineData(9_800_000, ChoroplethValueFormat.Currency, "$9.8M")]
    [InlineData(903_000, ChoroplethValueFormat.Currency, "$903K")]
    [InlineData(46_000, ChoroplethValueFormat.Currency, "$46K")]
    [InlineData(4_400, ChoroplethValueFormat.Currency, "$4.4K")]
    [InlineData(950, ChoroplethValueFormat.Currency, "$950")]
    [InlineData(41.3, ChoroplethValueFormat.Change, "+41.3%")]
    [InlineData(-13.6, ChoroplethValueFormat.Change, "−13.6%")]
    [InlineData(17.5, ChoroplethValueFormat.Percent, "17.5%")]
    public void Values_Are_Written_Like_The_Reference(double value, ChoroplethValueFormat format, string expected) =>
        ChoroplethMath.FormatValue(value, format, "$").ShouldBe(expected);

    [Theory]
    [InlineData(120_000, ChoroplethValueFormat.Currency, "$120K")]
    [InlineData(1_600_000, ChoroplethValueFormat.Currency, "$1.6M")]
    [InlineData(7_000, ChoroplethValueFormat.Currency, "$7K")]
    [InlineData(4.3, ChoroplethValueFormat.Change, "+4.3%")]
    [InlineData(0, ChoroplethValueFormat.Change, "0%")]
    [InlineData(21, ChoroplethValueFormat.Change, "+21%")]
    [InlineData(-4.8, ChoroplethValueFormat.Change, "−4.8%")]
    [InlineData(4.6, ChoroplethValueFormat.Percent, "4.6%")]
    [InlineData(10, ChoroplethValueFormat.Percent, "10%")]
    public void Ticks_Are_Written_With_Two_Significant_Digits(double value, ChoroplethValueFormat format, string expected) =>
        ChoroplethMath.FormatTick(value, format, "$").ShouldBe(expected);

    [Fact]
    public void Ranking_Puts_Missing_Values_Last_And_Weighted_Average_Uses_The_First_Metric()
    {
        double?[] values = [3, null, 7, 7];
        string[] names = ["c", "a", "b", "a2"];
        ChoroplethMath.RankOrder(values, names).ShouldBe([3, 2, 0, 1]);
        ChoroplethMath.Aggregate([10, 20, null], null, ChoroplethAggregate.Sum).ShouldBe(30);
        ChoroplethMath.Aggregate([10, -20], [3, 1], ChoroplethAggregate.WeightedAverage).ShouldBe(2.5);
        ChoroplethMath.Aggregate([null], null, ChoroplethAggregate.Sum).ShouldBeNull();
        ChoroplethMath.Spoken("−4.8%", "minus").ShouldBe("minus 4.8%");
    }

    // Headless ---------------------------------------------------------------------------------------------------------------

    /// <summary>A 4 × 3 grid of squares over the 975 × 610 box; R0 (west) has four quarter subregions, one without data.</summary>
    private static ChoroplethArea GridArea(string key = "grid", string name = "Gridland", int columns = 4, int rows = 3)
    {
        var area = new ChoroplethArea(key, name, "states", "state") { SubregionNoun = "counties", SubregionNounSingular = "county" };
        double w = 975.0 / columns, h = 610.0 / rows;
        var i = 0;
        for (var c = 0; c < columns; c++)
        {
            for (var r = 0; r < rows; r++)
            {
                var x = c * w;
                var y = r * h;
                var region = new ChoroplethRegion($"R{i}", $"Region {i:00}", Square(x, y, w, h), new Rect(x, y, w, h));
                region.SetMetricValue("revenue", (i + 1) * 100_000);
                region.SetMetricValue("growth", i % 4 == 0 ? -(i + 1) : i + 1);
                area.Regions.Add(region);
                i++;
            }
        }
        var first = area.Regions[0];
        for (var q = 0; q < 4; q++)
        {
            var sx = q % 2 * w / 2;
            var sy = q / 2 * h / 2;
            var sub = new ChoroplethRegion($"S{q}", $"Sub {q}", Square(sx, sy, w / 2, h / 2), new Rect(sx, sy, w / 2, h / 2));
            sub.SetMetricValue("revenue", q == 3 ? null : (q + 1) * 10_000);
            sub.SetMetricValue("growth", q == 3 ? null : q - 1);
            first.Subregions.Add(sub);
        }
        return area;
    }

    private static string Square(double x, double y, double w, double h) =>
        string.Create(CultureInfo.InvariantCulture, $"M{x},{y} h{w} v{h} h{-w} z");

    private static ChoroplethExplorer Explorer(params ChoroplethArea[] areas)
    {
        var explorer = new ChoroplethExplorer { Width = 1152, Description = "Fiscal year" };
        explorer.Metrics.Add(new ChoroplethMetric("revenue", "Revenue") { HeadlineLabel = "Annual recurring revenue", LegendCaption = "Shaded by revenue" });
        explorer.Metrics.Add(new ChoroplethMetric("growth", "Growth", ChoroplethValueFormat.Change, ChoroplethScale.Diverging)
        {
            Aggregate = ChoroplethAggregate.WeightedAverage,
        });
        foreach (var area in areas.Length > 0 ? areas : [GridArea()])
        {
            explorer.Areas.Add(area);
        }
        return explorer;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = 1200;
        window.Height = 900;
        Pump();
        return window;
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Shows_The_Area_Ranked_And_Binned()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            explorer.CurrentArea!.Key.ShouldBe("grid");
            explorer.CurrentMetric!.Key.ShouldBe("revenue");
            explorer.TitleText!.Text.ShouldBe("Revenue by state");
            explorer.SideCountText!.Text.ShouldBe("12 states");
            explorer.AreaSwitch!.IsVisible.ShouldBeFalse();
            explorer.MetricSwitch!.IsVisible.ShouldBeTrue();
            explorer.Headline!.FinalText.ShouldBe("$7.8M");
            var rows = explorer.List!.Rows;
            rows.Count.ShouldBe(12);
            rows[0].Item!.Name.ShouldBe("Region 11");
            rows[0].Item!.ValueText.ShouldBe("$1.2M");
            rows[^1].Item!.Name.ShouldBe("Region 00");
            rows[^1].Item!.HasChevron.ShouldBeTrue();
            AutomationProperties.GetName(rows[^1]).ShouldBe("12th, Region 00, $100K. Open 4 counties");
            AutomationProperties.GetName(rows[0]).ShouldBe("1st, Region 11, $1.2M. Zoom in");
            explorer.Legend!.Steps.Count.ShouldBe(5);
            explorer.Legend.NoDataStep.IsVisible.ShouldBeFalse();
            explorer.Legend.Ticks.Count.ShouldBe(4);
            explorer.LegendCaptionText!.Text.ShouldBe("Shaded by revenue");
            AutomationProperties.GetName(explorer.Map!).ShouldBe("Map of Gridland shaded by revenue.");

            // Light theme ramp: the first step is the positive hue at .2104, the last at .9436.
            var (_, fill, opacity, enter) = explorer.Map!.StateOf(explorer.CurrentArea!.Regions[0]);
            fill.ShouldBe(0.2104, 1e-6);
            opacity.ShouldBe(1);
            enter.ShouldBe(1);
            explorer.Map.StateOf(explorer.CurrentArea.Regions[11]).Fill.ShouldBe(0.9436, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wide_Layout_Puts_A_780_By_488_Map_Beside_A_300_List_And_Narrow_Stacks()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            explorer.Map!.Bounds.Width.ShouldBe(780, 0.5);
            explorer.Map.Bounds.Height.ShouldBe(488, 0.5);
            explorer.List!.Bounds.Width.ShouldBe(300, 0.5);
            explorer.Classes.Contains(":wide").ShouldBeTrue();

            explorer.Width = 700;
            Pump();
            explorer.Classes.Contains(":wide").ShouldBeFalse();
            explorer.Map.Bounds.Width.ShouldBe(660, 0.5);
            TestHelpers.Descendant<ScrollViewer>(explorer, "PART_Scroller").Bounds.Height.ShouldBe(400, 0.5);

            explorer.Width = 500;
            Pump();
            explorer.Classes.Contains(":compact").ShouldBeTrue();
            explorer.Map.Bounds.Width.ShouldBe(468, 0.5);
            TestHelpers.Descendant<ScrollViewer>(explorer, "PART_Scroller").Bounds.Height.ShouldBe(360, 0.5);
            explorer.Legend!.Bounds.Width.ShouldBe(468, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Metric_Switch_Sweeps_West_To_East_And_Slides_The_Rows()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var explorer = Explorer();
        var changes = new List<ChoroplethSelectionChangedEventArgs>();
        explorer.MetricChanged += (_, e) => changes.Add(e);
        var window = Show(explorer, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var area = explorer.CurrentArea!;
            var west = area.Regions[0];
            var east = area.Regions[11];
            var westBefore = explorer.Map!.StateOf(west).Color;
            var eastBefore = explorer.Map.StateOf(east).Fill;
            explorer.Map.SweepDelayOf(west).TotalMilliseconds.ShouldBe(260 * (975 / 8.0) / 975, 0.01);
            explorer.Map.SweepDelayOf(east).TotalMilliseconds.ShouldBe(260 * (975 * 7 / 8.0) / 975, 0.01);

            explorer.SelectedMetricKey = "growth";
            Pump();
            changes.Count.ShouldBe(1);
            changes[0].NewKey.ShouldBe("growth");
            explorer.TopBinning!.Scale.ShouldBe(ChoroplethScale.Diverging);
            explorer.Headline!.FinalText.ShouldStartWith("+");

            // 120 ms in: the west (delay 32.5 ms) is on its way, the east (delay 227.5 ms) has not started.
            clock.Advance(TimeSpan.FromMilliseconds(120));
            explorer.Map.StateOf(west).Color.ShouldNotBe(westBefore);
            explorer.Map.StateOf(east).Fill.ShouldBe(eastBefore, 1e-9);
            explorer.List!.Rows.Any(r => Math.Abs(r.Offset) > 1).ShouldBeTrue();

            clock.Advance(TimeSpan.FromMilliseconds(1500));
            explorer.Map.StateOf(west).Color.ShouldBe(explorer.NegativeRamp);
            explorer.List.Rows.ShouldAllBe(r => Math.Abs(r.Offset) < 0.1);
            explorer.List.Rows[0].Item!.ValueText.ShouldBe("+12.0%");
            explorer.Announcer!.Announcement.ShouldBe("Ranked by growth. Region 11 leads at +12.0%.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Region_Flies_In_Shows_Its_Subregions_And_Escape_Zooms_Out()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var explorer = Explorer();
        var opened = new List<ChoroplethRegionOpenedEventArgs>();
        explorer.RegionOpened += (_, e) => opened.Add(e);
        var window = Show(explorer, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            var area = explorer.CurrentArea!;
            var r0 = area.Regions[0];
            explorer.Legend!.Steps[0].Focus();
            explorer.ZoomTo("R0");
            Pump();
            opened.Count.ShouldBe(1);
            opened[0].RegionKey.ShouldBe("R0");
            explorer.OpenRegion.ShouldBe(r0);
            var target = ChoroplethMath.CameraFor(r0.Bounds, new Size(975, 610));
            explorer.Map!.CameraTarget.Scale.ShouldBe(target.Scale, 1e-9);

            // About 200 ms in: the camera is on its way, the subregions are fading in (140 ms delay).
            clock.Advance(TimeSpan.FromMilliseconds(200));
            explorer.Map.Camera.Scale.ShouldBeInRange(1.05, target.Scale - 0.01);
            explorer.Map.SubLayerOpacity.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            explorer.Map.Camera.Scale.ShouldBe(target.Scale, 1e-3);
            explorer.Map.StateOf(r0).Opacity.ShouldBe(0);
            explorer.Map.StateOf(area.Regions[5]).Opacity.ShouldBe(ChoroplethExplorer.ContextOpacity, 1e-9);
            explorer.Map.SubLayerOpacity.ShouldBe(1);

            explorer.List!.Rows.Count.ShouldBe(4);
            explorer.List.Rows[0].Item!.Name.ShouldBe("Sub 2");
            explorer.List.Rows[^1].Item!.ValueText.ShouldBe("—");
            explorer.SideCountText!.Text.ShouldBe("4 counties");
            explorer.Legend!.NoDataStep.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(explorer.Legend.NoDataStep).ShouldBe("1 county with no data");
            explorer.Headline!.FinalText.ShouldBe("$100K");
            explorer.BackButton!.Content.ShouldBe("All states");
            explorer.Announcer!.History.ShouldContain("Region 00, 4 counties ranked by revenue. Press Escape to zoom out.");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            explorer.OpenRegion.ShouldBeNull();
            opened.Count.ShouldBe(2);
            opened[1].Region.ShouldBeNull();
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            explorer.Map.Camera.Scale.ShouldBe(1, 1e-3);
            explorer.Map.StateOf(r0).Opacity.ShouldBe(1);
            explorer.Announcer.Announcement.ShouldBe("Showing all states, ranked by revenue.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Region_Without_Subregions_Shows_The_Empty_Panel()
    {
        var explorer = Explorer();
        explorer.Areas[0].EmptyDetailText = "Counties exist for one state only.";
        var window = Show(explorer);
        try
        {
            explorer.ZoomTo("R11");
            Pump();
            explorer.EmptyPanel!.IsVisible.ShouldBeTrue();
            explorer.EmptyTitle!.Text.ShouldBe("No county detail for Region 11");
            TestHelpers.Descendant<TextBlock>(explorer, "PART_EmptyText").Text
                .ShouldBe("Counties exist for one state only. Region 11 ranks 1st of 12 states by revenue, at $1.2M.");
            explorer.SideCountText!.Text.ShouldBe("Region 11");
            explorer.Map!.StateOf(explorer.CurrentArea!.Regions[11]).Opacity.ShouldBe(1);
            explorer.Map.StateOf(explorer.CurrentArea.Regions[3]).Opacity.ShouldBe(ChoroplethExplorer.ContextOpacity, 1e-9);
            explorer.Announcer!.Announcement.ShouldBe("Region 11. No county detail. Press Escape to zoom out.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pinned_Legend_Step_Dims_Other_Regions_And_Rows_And_Escape_Releases_It_First()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            explorer.ZoomTo(null);
            var area = explorer.CurrentArea!;
            var binning = explorer.TopBinning!;
            explorer.PinnedBinIndex = 1;
            Pump();
            for (var i = 0; i < area.Regions.Count; i++)
            {
                var inBin = binning.BinOf(area.Regions[i].GetMetricValue("revenue")) == 1;
                explorer.Map!.StateOf(area.Regions[i]).Opacity.ShouldBe(inBin ? 1 : ChoroplethExplorer.DimmedOpacity, 1e-9);
            }
            explorer.List!.Rows.Where(r => r.Item!.Bin != 1).ShouldAllBe(r => Math.Abs(r.DimValue - 0.32) < 1e-9);
            explorer.List.Rows.Where(r => r.Item!.Bin == 1).ShouldAllBe(r => r.DimValue == 1);
            explorer.Legend!.Steps[1].IsChecked.ShouldBe(true);
            explorer.Legend.Steps[1].ScaleY.ShouldBe(1.6);
            explorer.LegendCaptionText!.Text.ShouldBe($"{binning.Counts[1]} states $320K to $540K");
            AutomationProperties.GetName(explorer.Legend.Steps[0]).ShouldBe($"{binning.Counts[0]} states below $320K");

            explorer.ZoomTo("R0");
            Pump();
            explorer.PinnedBinIndex.ShouldBeNull();
            explorer.PinnedBinIndex = 0;
            Pump();
            explorer.Legend.Steps[0].Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            explorer.PinnedBinIndex.ShouldBeNull();
            explorer.OpenRegion.ShouldNotBeNull();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            explorer.OpenRegion.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Legend_Step_Toggles_The_Pin()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            var step = explorer.Legend!.Steps[2];
            step.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            explorer.PinnedBinIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            explorer.PinnedBinIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Through_The_List_Opens_A_Region_And_Pins_A_Subregion()
    {
        var explorer = Explorer();
        var pins = new List<ChoroplethSubregionPinnedEventArgs>();
        explorer.SubregionPinned += (_, e) => pins.Add(e);
        var window = Show(explorer);
        try
        {
            var list = explorer.List!;
            list.FocusRow(null).ShouldBeTrue();
            list.Rows[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            list.Rows[0].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            list.Rows[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            var last = list.Rows[^1];
            last.IsFocused.ShouldBeTrue();
            last.Item!.Name.ShouldBe("Region 00");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            last.IsFocused.ShouldBeTrue();

            // The focused row outlines its region (no card).
            explorer.Map!.Tip.ShouldBeNull();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            explorer.OpenRegion!.Key.ShouldBe("R0");
            TestHelpers.PumpUntil(() => list.Rows.Count == 4 && list.Rows[0].IsFocused, 3000).ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            explorer.PinnedSubregionKey.ShouldBe("S2");
            pins.Count.ShouldBe(1);
            pins[0].Subregion!.Name.ShouldBe("Sub 2");
            list.Rows[0].IsChecked.ShouldBe(true);
            explorer.Announcer!.Announcement.ShouldBe("Sub 2: $30K, 1st of 4.");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            explorer.OpenRegion.ShouldBeNull();
            explorer.PinnedSubregionKey.ShouldBeNull();
            TestHelpers.PumpUntil(() => list.Rows.Count == 12 && list.Rows.Any(r => r.IsFocused), 3000).ShouldBeTrue();
            list.Rows.First(r => r.IsFocused).Item!.Name.ShouldBe("Region 00");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointing_At_The_Map_Finds_The_Row_And_Shows_The_Card()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            var map = explorer.Map!;
            var point = map.TranslatePoint(new Point(map.Bounds.Width * 0.9, map.Bounds.Height * 0.9), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            Pump();
            map.HoveredRegion!.Key.ShouldBe("R11");
            map.Tip!.Title.ShouldBe("Region 11");
            map.Tip.Rank.ShouldBe("1st of 12");
            map.Tip.Rows.Count.ShouldBe(2);
            map.Tip.Rows[0].IsCurrent.ShouldBeTrue();
            map.Tip.Hint.ShouldBe("Select to zoom in");
            explorer.List!.Rows[0].OnValue.ShouldBe(1);

            var west = map.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseMove(west, RawInputModifiers.None);
            Pump();
            map.Tip!.Hint.ShouldBe("Select to see 4 counties");
            window.MouseDown(west, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(west, MouseButton.Left, RawInputModifiers.None);
            Pump();
            explorer.OpenRegionKey.ShouldBe("R0");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Area_Switch_Lists_Areas_With_Counts_And_A_Change_Resets_The_Zoom()
    {
        var other = GridArea("other", "Otherland", 2, 2);
        var explorer = Explorer(GridArea(), other);
        var changes = new List<ChoroplethSelectionChangedEventArgs>();
        explorer.AreaChanged += (_, e) => changes.Add(e);
        var window = Show(explorer);
        try
        {
            var select = explorer.AreaSwitch!;
            select.IsVisible.ShouldBeTrue();
            var items = select.Items.OfType<SelectItem>().ToList();
            items.Select(i => i.Meta).ShouldBe(["12", "4"]);
            explorer.ZoomTo("R0");
            Pump();
            explorer.SelectedAreaKey = "other";
            Pump();
            changes.Count.ShouldBe(1);
            changes[0].NewKey.ShouldBe("other");
            explorer.OpenRegion.ShouldBeNull();
            explorer.OpenRegionKey.ShouldBeNull();
            explorer.SideCountText!.Text.ShouldBe("4 states");
            explorer.List!.Rows.Count.ShouldBe(4);
            select.SelectedIndex.ShouldBe(1);
            explorer.Announcer!.Announcement.ShouldBe("Otherland, 4 states ranked by revenue.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Changes_Colours_And_Camera_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var explorer = Explorer();
        var window = Show(explorer, ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            explorer.SelectedMetricKey = "growth";
            Pump();
            explorer.Map!.StateOf(explorer.CurrentArea!.Regions[0]).Color.ShouldBe(explorer.NegativeRamp);
            explorer.ZoomTo("R0");
            Pump();
            explorer.Map.Camera.Scale.ShouldBe(ChoroplethMath.CameraFor(explorer.CurrentArea.Regions[0].Bounds, new Size(975, 610)).Scale, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Values_Set_Later_Rebin_On_The_Next_Frame()
    {
        var explorer = Explorer();
        var window = Show(explorer);
        try
        {
            var region = explorer.CurrentArea!.Regions[0];
            region.SetMetricValue("revenue", 99_000_000);
            Pump();
            explorer.List!.Rows[0].Item!.Region.ShouldBe(region);
            explorer.List.Rows[0].Item!.ValueText.ShouldBe("$99.0M");
        }
        finally
        {
            window.Close();
        }
    }
}
