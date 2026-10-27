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

/// <summary>Metrics dashboard: the engine's measured numbers (pure) and the block's behaviour (headless).</summary>
public class MetricsDashboardTests
{
    private static readonly DateOnly End = new(2026, 10, 18);

    // Engine ----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1374, 1500, 500)]
    [InlineData(1448, 1500, 500)]
    [InlineData(63, 75, 25)]
    [InlineData(67, 75, 25)]
    [InlineData(431, 600, 200)]
    [InlineData(8855, 15000, 5000)]
    public void Count_Axis_Has_Three_Rows_From_Zero(double max, double top, double step)
    {
        var ticks = MetricsDashboardEngine.Ticks(MetricsDashboardMetricKind.Count, [max * 0.6, max], []);
        ticks.Min.ShouldBe(0);
        ticks.Max.ShouldBe(top);
        ticks.Step.ShouldBe(step);
        ticks.Ticks.Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(4.21, 4.86)]
    [InlineData(37.4, 42.7)]
    [InlineData(40.1, 40.2)]
    public void Ratio_Axis_Has_Three_Rows_And_Always_Covers_The_Data(double min, double max)
    {
        var ticks = MetricsDashboardEngine.Ticks(MetricsDashboardMetricKind.Ratio, [min, max], []);
        ticks.Ticks.Length.ShouldBe(4);
        ticks.Min.ShouldBeLessThanOrEqualTo(min);
        ticks.Max.ShouldBeGreaterThanOrEqualTo(max);
        AnalyticsTicks.Ladder.ShouldContain(ticks.Step / Math.Pow(10, Math.Floor(Math.Log10(ticks.Step))), 1e-9);
    }

    [Fact]
    public void Axis_Covers_The_Previous_Period_So_Compare_Never_Changes_It()
    {
        var ticks = MetricsDashboardEngine.Ticks(MetricsDashboardMetricKind.Ratio, [4.2, 4.6], [3.1, 4.0]);
        ticks.Min.ShouldBeLessThanOrEqualTo(3.1);
        ticks.Max.ShouldBeGreaterThanOrEqualTo(4.6);
    }

    [Theory]
    [InlineData(30, 6, 5)]
    [InlineData(30, 5, 6)]
    [InlineData(30, 4, 8)]
    [InlineData(13, 6, 3)]
    [InlineData(13, 5, 3)]
    [InlineData(13, 4, 4)]
    [InlineData(7, 6, 2)]
    public void X_Labels_Thin_By_Ceil_Of_Count_Over_Max(int count, int max, int step)
    {
        MetricsDashboardEngine.LabelStep(count, max).ShouldBe(step);
    }

    [Theory]
    [InlineData(MetricsDashboardMetricKind.Count, 0, 500, "0")]
    [InlineData(MetricsDashboardMetricKind.Count, 500, 500, "500")]
    [InlineData(MetricsDashboardMetricKind.Count, 1500, 500, "1.5K")]
    [InlineData(MetricsDashboardMetricKind.Count, 1000, 500, "1K")]
    [InlineData(MetricsDashboardMetricKind.Count, 15000, 5000, "15K")]
    [InlineData(MetricsDashboardMetricKind.Ratio, 42.5, 2.5, "42.5%")]
    [InlineData(MetricsDashboardMetricKind.Ratio, 4.25, 0.25, "4.25%")]
    [InlineData(MetricsDashboardMetricKind.Ratio, 4, 0.5, "4.0%")]
    public void Axis_Labels(MetricsDashboardMetricKind kind, double value, double step, string text)
    {
        MetricsDashboardEngine.FormatAxis(kind, value, step).ShouldBe(text);
    }

    [Fact]
    public void Ninety_Days_Are_Thirteen_Weeks_With_A_Short_Last_One()
    {
        var daily = Enumerable.Repeat(10.0, 180).ToArray();
        var series = MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Count, daily, null, null, 90, End, 60);
        series.IsWeekly.ShouldBeTrue();
        series.Count.ShouldBe(13);
        series.Spans[0].ShouldBe(7);
        series.Spans[12].ShouldBe(6);
        series.Values[0].ShouldBe(70);
        series.Values[12].ShouldBe(60);
        series.Starts[0].ShouldBe(End.AddDays(-89));
        series.Starts[12].ShouldBe(End.AddDays(-5));
        series.Total.ShouldBe(900);
        series.Previous.ShouldNotBeNull();
        series.Change.ShouldBe(0);
    }

    [Fact]
    public void Thirty_Days_Are_Daily_With_A_Relative_Change()
    {
        var daily = Enumerable.Range(0, 60).Select(i => i < 30 ? 10.0 : 11.7).ToArray();
        var series = MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Count, daily, null, null, 30, End, 60);
        series.IsWeekly.ShouldBeFalse();
        series.Count.ShouldBe(30);
        series.Starts[^1].ShouldBe(End);
        series.Total.ShouldBe(351, 1e-9);
        series.PreviousTotal.ShouldBe(300);
        series.Change!.Value.ShouldBe(17, 1e-9);
        series.Previous![0].ShouldBe(10);
    }

    [Fact]
    public void Ratios_Use_Numerators_And_Denominators_And_Change_In_Points()
    {
        var den = Enumerable.Repeat(100.0, 14).ToArray();
        var num = Enumerable.Range(0, 14).Select(i => i < 7 ? 4.0 : i == 13 ? 12.0 : 4.0).ToArray();
        var rates = num.Select((n, i) => n / den[i] * 100).ToArray();
        var series = MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Ratio, rates, num, den, 7, End, 60);
        series.Total.ShouldBe(36.0 / 700 * 100, 1e-9);
        series.PreviousTotal!.Value.ShouldBe(4, 1e-9);
        series.Change!.Value.ShouldBe(36.0 / 7 - 4, 1e-9);

        var mean = MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Ratio, rates, null, null, 7, End, 60);
        mean.Total.ShouldBe(rates.Skip(7).Average(), 1e-9);
    }

    [Fact]
    public void Short_Data_Has_No_Previous_Period_Or_Change()
    {
        var series = MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Count, [1, 2, 3, 4, 5], null, null, 7, End, 60);
        series.Count.ShouldBe(5);
        series.Previous.ShouldBeNull();
        series.Change.ShouldBeNull();
        series.Total.ShouldBe(15);
        MetricsDashboardEngine.Build(MetricsDashboardMetricKind.Count, null, null, null, 30, End, 60).Count.ShouldBe(0);
    }

    [Fact]
    public void Default_Texts()
    {
        MetricsDashboardEngine.FormatValue(MetricsDashboardMetricKind.Count, 36686).ShouldBe("36,686");
        MetricsDashboardEngine.FormatValue(MetricsDashboardMetricKind.Ratio, 4.83).ShouldBe("4.8%");
        MetricsDashboardEngine.FormatChange(MetricsDashboardMetricKind.Count, -11.72).ShouldBe("11.7%");
        MetricsDashboardEngine.FormatChange(MetricsDashboardMetricKind.Ratio, 0.12).ShouldBe("0.1 pts");
    }

    // Block -----------------------------------------------------------------------------------------------------------

    private static double[] Series(double baseValue, double slope, int seed, int days = 180) =>
        Enumerable.Range(0, days).Select(d => baseValue * (1 + slope * d) * (1 + 0.1 * Math.Sin(d * 0.9 + seed))).ToArray();

    private static MetricsDashboard Create()
    {
        var block = new MetricsDashboard { Title = "Insights", Source = "northwind.dev", EndDate = End };
        block.Metrics.Add(new MetricsDashboardMetric { Label = "Sessions", Daily = Series(1200, 0.003, 1) });
        block.Metrics.Add(new MetricsDashboardMetric { Label = "Trials", Daily = Series(55, 0.003, 2) });
        block.Metrics.Add(new MetricsDashboardMetric { Label = "Activation", Kind = MetricsDashboardMetricKind.Ratio, Daily = Series(4.6, 0, 3) });
        block.Metrics.Add(new MetricsDashboardMetric
        {
            Label = "Churn risk", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = Series(40, -0.0005, 4)
        });
        foreach (var (label, value) in new[] { ("/", 10840.0), ("/features", 5912.0), ("/roadmap", 1705.0) })
        {
            block.PrimaryItems.Add(new MetricsDashboardItem { Label = label, Values = [value * 0.24, value, value * 2.7] });
        }
        block.SecondaryItems.Add(new MetricsDashboardItem { Label = "Direct", Value = 12960 });
        block.SecondaryItems.Add(new MetricsDashboardItem { Label = "Search", Value = 9850 });
        return block;
    }

    private static Window Show(Control control, double width = 1100, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Shows_Tabs_Chart_Lists_And_The_Default_Range()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            block.LayoutSize.ShouldBe(MetricsDashboardSize.Large);
            block.Tabs.Count.ShouldBe(4);
            block.Tabs[0].IsSelected.ShouldBeTrue();
            block.Tabs.Count(t => t.IsTabStop).ShouldBe(1);
            block.RangeSwitch!.SelectedIndex.ShouldBe(1);
            AutomationProperties.GetName(block.RangeSwitch).ShouldBe("Date range");
            AutomationProperties.GetName(block.RangeSwitch.Buttons[0]).ShouldBe("Last 7 days");
            block.Plot!.Scene!.Series.Count.ShouldBe(30);
            block.Plot.Bounds.Height.ShouldBe(224);
            block.Plot.MaxLabels.ShouldBe(6);
            AutomationProperties.GetName(block.Plot).ShouldBe("Sessions, last 30 days");
            block.PrimaryRows.Count.ShouldBe(3);
            block.PrimaryRows[0].Share.ShouldBe(1);
            block.PrimaryRows[1].Share.ShouldBe(5912.0 / 10840, 1e-9);
            block.PrimaryRows[0].Bounds.Height.ShouldBe(30);
            block.SecondaryRows[1].Value.ShouldBe(9850);
            var tab = block.Tabs[0];
            AutomationProperties.GetName(tab)!.ShouldStartWith("Sessions, ");
            AutomationProperties.GetName(tab)!.ShouldContain("versus the previous period");
            block.TabStrip!.Underline.CurrentRect.Width.ShouldBe(tab.Bounds.Width, 0.5);
            block.TabStrip.Underline.CurrentRect.Bottom.ShouldBe(tab.Bounds.Bottom + 1, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Switch_Changes_Every_Number_And_The_Subtitle()
    {
        var block = Create();
        var ranges = new List<MetricsDashboardRangeChangedEventArgs>();
        block.RangeChanged += (_, e) => ranges.Add(e);
        var window = Show(block);
        try
        {
            var subtitle = TestHelpers.Descendant<TextBlock>(block, MetricsDashboard.SubtitlePart);
            subtitle.Text.ShouldBe("northwind.dev · Last 30 days");
            block.RangeSwitch!.Buttons[2].Focus();
            block.RangeSwitch.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            block.SelectedRange.ShouldBe(90);
            ranges.Single().NewRange.ShouldBe(90);
            ranges[0].OldRange.ShouldBe(30);
            subtitle.Text.ShouldBe("northwind.dev · Last 90 days");
            block.Plot!.Scene!.Series.Count.ShouldBe(13);
            block.Plot.Scene.Title(0).ShouldStartWith("Week of ");
            block.Plot.Scene.Title(12).ShouldEndWith("(6 days)");
            block.PrimaryRows[0].Value.ShouldBe(10840 * 2.7, 1e-6);
            block.SecondaryRows[0].Value.ShouldBe(12960);

            block.SelectedRange = 7;
            Dispatcher.UIThread.RunJobs();
            block.RangeSwitch.SelectedIndex.ShouldBe(0);
            block.Plot.Scene.Series.Count.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_Select_By_Click_And_Roving_Arrows()
    {
        var block = Create();
        var changes = new List<MetricsDashboardMetricChangedEventArgs>();
        block.SelectedMetricChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            block.Tabs[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.SelectedMetricIndex.ShouldBe(1);
            block.Tabs[1].IsFocused.ShouldBeTrue();
            block.Tabs[1].IsSelected.ShouldBeTrue();
            block.Tabs.Count(t => t.IsTabStop).ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(0);
            changes.Count.ShouldBe(3);
            changes[1].Metric!.Label.ShouldBe("Churn risk");

            block.SelectedMetricIndex = 2;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(block.Plot!).ShouldBe("Activation, last 30 days");
            block.Plot!.Scene!.AxisLabels[3].ShouldEndWith("%");
            var ticks = block.Plot.Scene.Series.Ticks;
            block.Plot.Scene.Series.Values.Max().ShouldBeLessThanOrEqualTo(ticks.Max);
            block.Plot.Scene.Series.Values.Min().ShouldBeGreaterThanOrEqualTo(ticks.Min);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Keyboard_Inspection_Follows_The_Measured_Model()
    {
        var block = Create();
        var points = new List<MetricsDashboardPointEventArgs>();
        block.PointInspected += (_, e) => points.Add(e);
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            plot.Focus(NavigationMethod.Tab);
            block.ActiveIndex.ShouldBe(-1);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(28);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(29);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(0);
            points[^1].Date.ShouldBe(End.AddDays(-29));
            points[^1].PreviousValue.ShouldBeNull();
            block.Announcer!.Announcement.ShouldStartWith("Sep 19, ");
            plot.Readout.Content!.Title.ShouldBe("Sep 19");

            block.IsCompareEnabled = true;
            Dispatcher.UIThread.RunJobs();
            block.Announcer.Announcement.ShouldEndWith(" previous");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            points[^1].PreviousValue.ShouldNotBeNull();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(-1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(29);
            block.RangeSwitch!.FocusSelected();
            block.ActiveIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Inspects_The_Nearest_Point_Inside_The_Plot_Only()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var rect = plot.PlotRect;
            var origin = plot.TranslatePoint(rect.TopLeft, window)!.Value;
            var step = rect.Width / 29;
            window.MouseMove(new Point(origin.X + step * 10.4, origin.Y + 100), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.ActiveIndex.ShouldBe(10);
            window.MouseMove(new Point(origin.X + step * 10.6, origin.Y + 100), RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(11);
            // The x axis row is not part of the hit area.
            window.MouseMove(new Point(origin.X + step * 10.6, origin.Y + rect.Height + 12), RawInputModifiers.None);
            block.ActiveIndex.ShouldBe(-1);
            block.InspectPoint(4);
            block.ActiveIndex.ShouldBe(4);
            block.ClearInspection();
            block.ActiveIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compare_Box_Toggles_The_Previous_Period_And_The_Legend()
    {
        var block = Create();
        var changes = new List<bool>();
        block.CompareChanged += (_, e) => changes.Add(e.IsEnabled);
        var window = Show(block);
        try
        {
            var legend = TestHelpers.Descendant<Control>(block, MetricsDashboard.PreviousLegendPart);
            legend.IsVisible.ShouldBeFalse();
            block.Plot!.CompareOpacity.ShouldBe(0);
            block.CompareBox!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            block.IsCompareEnabled.ShouldBeTrue();
            legend.IsVisible.ShouldBeTrue();
            block.Plot.CompareOpacity.ShouldBe(1);
            block.IsCompareEnabled = false;
            block.CompareBox.IsChecked.ShouldBe(false);
            changes.ShouldBe([true, false]);
            AutomationProperties.GetName(block.CompareBox).ShouldBe("Compare");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Export_Runs_The_Command_Confirms_And_Resets()
    {
        var block = Create();
        MetricsDashboardExportRequest? executed = null;
        block.ExportCommand = new TestCommand(p => executed = (MetricsDashboardExportRequest)p!);
        block.ExportConfirmDuration = TimeSpan.FromMilliseconds(50);
        var events = 0;
        block.ExportRequested += (_, _) => events++;
        var window = Show(block);
        try
        {
            block.ExportButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            executed.ShouldNotBeNull();
            executed.RangeDays.ShouldBe(30);
            executed.StartDate.ShouldBe(End.AddDays(-29));
            executed.Metric!.Label.ShouldBe("Sessions");
            events.ShouldBe(1);
            block.IsExportConfirmed.ShouldBeTrue();
            block.ExportButton.IsConfirmed.ShouldBeTrue();
            AutomationProperties.GetName(block.ExportButton).ShouldBe("Exported");
            block.Announcer!.Announcement.ShouldBe("Exported");
            TestHelpers.PumpUntil(() => !block.IsExportConfirmed, 3000).ShouldBeTrue();
            AutomationProperties.GetName(block.ExportButton).ShouldBe("Export");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Blocks_Own_Width()
    {
        var block = Create();
        var host = new Border { Width = 600, Child = block, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = Show(host);
        try
        {
            block.LayoutSize.ShouldBe(MetricsDashboardSize.Medium);
            block.TabStrip!.Columns.ShouldBe(2);
            block.Tabs[2].Bounds.Y.ShouldBeGreaterThan(block.Tabs[0].Bounds.Bottom);
            block.Plot!.MaxLabels.ShouldBe(5);

            host.Width = 420;
            Dispatcher.UIThread.RunJobs();
            block.LayoutSize.ShouldBe(MetricsDashboardSize.Small);
            block.Plot.Bounds.Height.ShouldBe(168 + 24);
            block.RangeSwitch!.IsStretched.ShouldBeTrue();
            block.Header!.IsWrapped.ShouldBeTrue();
            block.Tabs[0].Tile.ValueFontSize.ShouldBe(18);

            host.Width = 900;
            Dispatcher.UIThread.RunJobs();
            block.LayoutSize.ShouldBe(MetricsDashboardSize.Large);
            block.TabStrip.Columns.ShouldBe(4);
            block.Header.IsWrapped.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Cross_Fades_The_Chart_In_240ms_And_Counts_480ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Create();
        var window = Show(block, level: ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            plot.IsCrossFading.ShouldBeFalse();
            var before = block.Tabs[0].Tile.CountingValue.DisplayText;
            block.SelectedRange = 90;
            Dispatcher.UIThread.RunJobs();
            plot.IsCrossFading.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            plot.FadeProgress.ShouldBeInRange(0.5, 0.99);
            block.Tabs[0].Tile.CountingValue.DisplayText.ShouldNotBe(before);
            block.Tabs[0].Tile.CountingValue.DisplayText.ShouldNotBe(block.Tabs[0].Tile.CountingValue.FinalText);
            clock.Advance(TimeSpan.FromMilliseconds(160));
            plot.IsCrossFading.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(300));
            block.Tabs[0].Tile.CountingValue.DisplayText.ShouldBe(block.Tabs[0].Tile.CountingValue.FinalText);

            block.IsCompareEnabled = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            plot.CompareOpacity.ShouldBeInRange(0.1, 0.9);
            clock.Advance(TimeSpan.FromMilliseconds(900));
            plot.CompareOpacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Replaces_The_Chart_At_Once()
    {
        var block = Create();
        var window = Show(block, level: ArcMotionLevel.Reduced);
        try
        {
            block.SelectedRange = 7;
            Dispatcher.UIThread.RunJobs();
            block.Plot!.IsCrossFading.ShouldBeFalse();
            block.IsCompareEnabled = true;
            block.Plot.CompareOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formatters_And_Sources_Are_Followed()
    {
        var block = Create();
        var metrics = new System.Collections.ObjectModel.ObservableCollection<MetricsDashboardMetric>
        {
            new() { Label = "Revenue", Daily = Series(900, 0.002, 5), ValueFormatter = v => $"${v:N0}", DeltaFormatter = v => $"{v:0.00}%" }
        };
        block.MetricsSource = metrics;
        block.ListValueFormatter = v => $"{v / 1000:0.0}k";
        var window = Show(block);
        try
        {
            block.Tabs.Count.ShouldBe(1);
            block.Tabs[0].Tile.CountingValue.FinalText.ShouldStartWith("$");
            block.Tabs[0].Tile.Delta.Text!.ShouldEndWith("%");
            block.Tabs[0].Tile.Delta.Text!.Split('.')[1].Length.ShouldBe(3);
            block.PrimaryRows[0].ValueText.FinalText.ShouldBe("10.8k");
            metrics.Add(new MetricsDashboardMetric { Label = "Orders", Daily = Series(30, 0.002, 6) });
            Dispatcher.UIThread.RunJobs();
            block.Tabs.Count.ShouldBe(2);
            block.Plot!.Scene!.Value(1234).ShouldBe("$1,234");
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class TestCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
