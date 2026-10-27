using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Uptime status: the arithmetic (pure, against the measured numbers) and the block's behaviour (headless).</summary>
public class UptimeStatusTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 14, 14, 30, 0, TimeSpan.Zero);

    // Bars ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1112, 10)]
    [InlineData(881.6, 8)]
    [InlineData(770, 7)]
    [InlineData(734, 6)]
    [InlineData(698, 6)]
    [InlineData(608, 5)]
    [InlineData(536, 4)]
    [InlineData(472, 3)]
    [InlineData(300, 3)]
    public void Bar_Width_Aims_At_A_Two_Pixel_Gap_And_Never_Goes_Under_Three(double width, double expected) =>
        UptimeEngine.BarWidth(width, 90).ShouldBe(expected);

    [Fact]
    public void Bar_Positions_Are_Rounded_Pitch_Steps_Between_The_Edges()
    {
        const double width = 1112;
        var w = UptimeEngine.BarWidth(width, 90);
        UptimeEngine.BarX(0, width, w, 90).ShouldBe(0);
        UptimeEngine.BarX(1, width, w, 90).ShouldBe(12);
        UptimeEngine.BarX(2, width, w, 90).ShouldBe(25);
        UptimeEngine.BarX(89, width, w, 90).ShouldBe(width - w);
        // The pitch is 12.38, so the gaps are 2 or 3 pixels.
        for (var i = 1; i < 90; i++)
        {
            var gap = UptimeEngine.BarX(i, width, w, 90) - UptimeEngine.BarX(i - 1, width, w, 90) - w;
            gap.ShouldBeInRange(2, 3);
        }
    }

    [Fact]
    public void The_Bar_Under_The_Pointer_Is_The_One_With_The_Nearest_Centre()
    {
        const double width = 1112;
        var w = UptimeEngine.BarWidth(width, 90);
        UptimeEngine.IndexAt(0, width, w, 90).ShouldBe(0);
        UptimeEngine.IndexAt(width, width, w, 90).ShouldBe(89);
        UptimeEngine.IndexAt(-30, width, w, 90).ShouldBe(0);
        for (var i = 0; i < 90; i++)
        {
            UptimeEngine.IndexAt(UptimeEngine.BarX(i, width, w, 90) + w / 2, width, w, 90).ShouldBe(i);
        }
    }

    [Fact]
    public void Bars_Are_34_22_And_12_High()
    {
        UptimeEngine.BarHeight(ServiceStatus.Operational).ShouldBe(34);
        UptimeEngine.BarHeight(ServiceStatus.Degraded).ShouldBe(22);
        UptimeEngine.BarHeight(ServiceStatus.Outage).ShouldBe(12);
    }

    // Days ---------------------------------------------------------------------------------------------------------------------

    private static UptimeInterval Interval(DateTimeOffset start, int minutes, ServiceStatus severity, params string[] services) =>
        new(start, TimeSpan.FromMinutes(minutes), severity, services);

    [Fact]
    public void A_Day_Is_One_Minus_Its_Downtime_Over_1440_And_Shows_Two_Decimals()
    {
        var end = DateOnly.FromDateTime(Now.UtcDateTime);
        var days = UptimeEngine.Bucket(end, 90, "api", [Interval(Now.AddDays(-22).Date.AddHours(7), 23, ServiceStatus.Outage, "api")]);
        var day = days[89 - 22];
        day.Minutes.ShouldBe(23);
        UptimeEngine.PercentText(day.Uptime).ShouldBe("98.40%");
        day.Status.ShouldBe(ServiceStatus.Outage);
        days[89].Status.ShouldBe(ServiceStatus.Operational);
        UptimeEngine.PercentText(days[89].Uptime).ShouldBe("100.00%");
    }

    [Fact]
    public void An_Incident_Across_Midnight_Is_Split_Between_The_Two_Days()
    {
        var start = new DateTimeOffset(2027, 3, 1, 23, 40, 0, TimeSpan.Zero);
        UptimeEngine.MinutesOnDay(start, TimeSpan.FromMinutes(50), new DateOnly(2027, 3, 1)).ShouldBe(20);
        UptimeEngine.MinutesOnDay(start, TimeSpan.FromMinutes(50), new DateOnly(2027, 3, 2)).ShouldBe(30);
        UptimeEngine.MinutesOnDay(start, TimeSpan.FromMinutes(50), new DateOnly(2027, 3, 3)).ShouldBe(0);
        var days = UptimeEngine.Bucket(new DateOnly(2027, 3, 14), 14, "a", [Interval(start, 50, ServiceStatus.Degraded, "a")]);
        days[0].Minutes.ShouldBe(20);
        days[1].Minutes.ShouldBe(30);
        days[0].Status.ShouldBe(ServiceStatus.Degraded);
        days[1].Status.ShouldBe(ServiceStatus.Degraded);
        UptimeEngine.WindowUptime(days).ShouldBe(1 - 50.0 / (14 * 1440), 1e-12);
    }

    [Theory]
    [InlineData(69, "99.95%")]
    [InlineData(72, "99.94%")]
    [InlineData(84, "99.94%")]
    [InlineData(17, "99.99%")]
    [InlineData(118, "99.91%")]
    [InlineData(137, "99.89%")]
    public void The_Window_Uptime_Is_One_Minus_All_Minutes_Over_Days_Times_1440(int minutes, string expected)
    {
        var days = UptimeEngine.Bucket(new DateOnly(2027, 3, 14), 90, "a", [Interval(Now.AddDays(-10).Date.AddHours(3), minutes, ServiceStatus.Degraded, "a")]);
        UptimeEngine.PercentText(UptimeEngine.WindowUptime(days)).ShouldBe(expected);
    }

    [Fact]
    public void The_Most_Severe_Incident_Sets_The_Day_And_Other_Services_Are_Left_Alone()
    {
        var start = Now.Date.AddHours(2);
        List<UptimeInterval> intervals =
        [
            Interval(start, 30, ServiceStatus.Degraded, "a", "b"),
            Interval(start.AddHours(5), 10, ServiceStatus.Outage, "a"),
            Interval(start.AddHours(8), 10, ServiceStatus.Outage, "c")
        ];
        var a = UptimeEngine.Bucket(new DateOnly(2027, 3, 14), 1, "a", intervals)[0];
        a.Status.ShouldBe(ServiceStatus.Outage);
        a.Minutes.ShouldBe(40);
        a.Intervals.ShouldBe([0, 1]);
        var b = UptimeEngine.Bucket(new DateOnly(2027, 3, 14), 1, "b", intervals)[0];
        b.Status.ShouldBe(ServiceStatus.Degraded);
        b.Minutes.ShouldBe(30);
        UptimeEngine.Bucket(new DateOnly(2027, 3, 14), 1, "d", intervals)[0].Minutes.ShouldBe(0);
    }

    // Chart --------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(400, 266, 500, 100)]
    [InlineData(1000, 612, 1250, 250)]
    [InlineData(750, 441, 1000, 200)]
    [InlineData(300, 166, 400, 100)]
    [InlineData(400, 2040, 2500, 500)]
    public void The_Chart_Domain_Is_The_Nice_Number_Above_One_Point_Two_Times_The_Larger(double target, double max, double top, double step)
    {
        var ticks = UptimeEngine.ChartTicks(target, max);
        ticks.Max.ShouldBe(top);
        ticks.Step.ShouldBe(step);
        ticks.Min.ShouldBe(0);
        ticks.Ticks[0].ShouldBe(0);
        ticks.Ticks[^1].ShouldBe(top);
    }

    [Fact]
    public void The_Plot_Maps_Values_Between_186_And_12_And_Samples_Between_44_And_Width_Minus_36()
    {
        UptimeEngine.ChartY(0, 500).ShouldBe(186);
        UptimeEngine.ChartY(500, 500).ShouldBe(12);
        UptimeEngine.ChartY(250, 500).ShouldBe(99);
        UptimeEngine.ChartX(0, 48, 588).ShouldBe(44);
        UptimeEngine.ChartX(47, 48, 588).ShouldBe(552);
        UptimeEngine.ChartX(6, 48, 588).ShouldBe(44 + 6 * 508.0 / 47, 1e-9);
        UptimeEngine.LabelIndices(48).ShouldBe([6, 18, 30, 42]);
        UptimeEngine.NearestSample(44 + 508.0 / 47 * 10.49, 48, 588).ShouldBe(10);
        UptimeEngine.NearestSample(44 + 508.0 / 47 * 10.51, 48, 588).ShouldBe(11);
        UptimeEngine.NearestSample(-5, 48, 588).ShouldBe(0);
        UptimeEngine.NearestSample(900, 48, 588).ShouldBe(47);
        UptimeEngine.Crisp(99.2).ShouldBe(99.5);
    }

    [Fact]
    public void The_Hover_Card_Is_Centred_On_The_Bar_And_Kept_Eight_From_The_Edges()
    {
        UptimeEngine.TipLeft(500, 1152).ShouldBe(372);
        UptimeEngine.TipLeft(20, 1152).ShouldBe(8);
        UptimeEngine.TipLeft(1140, 1152).ShouldBe(1152 - 8 - 256);
    }

    [Fact]
    public void Paging_Moves_The_Cursor_By_A_Week_And_Stops_At_The_Ends()
    {
        AnalyticsKeyboard.Step(Key.PageUp, 80, 90, page: 7, vertical: false).ShouldBe(73);
        AnalyticsKeyboard.Step(Key.PageDown, 80, 90, page: 7, vertical: false).ShouldBe(87);
        AnalyticsKeyboard.Step(Key.PageDown, 88, 90, page: 7, vertical: false).ShouldBe(89);
        AnalyticsKeyboard.Step(Key.PageUp, 3, 90, page: 7, vertical: false).ShouldBe(0);
        AnalyticsKeyboard.Step(Key.Up, 3, 90, page: 7, vertical: false).ShouldBeNull();
    }

    // Behaviour ------------------------------------------------------------------------------------------------------------------

    private static UptimeService MakeService(string key, string name, double target, double p50, double p95, int seed)
    {
        var samples = new List<LatencySample>();
        var first = Now.AddMinutes(-30 * 47);
        for (var i = 0; i < 48; i++)
        {
            var wave = Math.Sin((i + seed) * 0.7);
            samples.Add(new LatencySample(first.AddMinutes(30 * i), Math.Round(p50 * (1 + 0.1 * wave)), Math.Round(p95 * (1 + 0.2 * wave))));
        }
        return new UptimeService(key, name, key + ".test", target, samples);
    }

    private static UptimeIncident MakeIncident(string key, int daysAgo, int minutes, ServiceStatus severity, bool resolved, params string[] services)
    {
        var start = Now.Date.AddDays(-daysAgo).AddHours(7);
        var incident = new UptimeIncident { Key = key, Title = "Incident " + key, Severity = severity, Start = start, Duration = TimeSpan.FromMinutes(minutes), IsResolved = resolved };
        incident.ServiceKeys.AddRange(services);
        incident.Updates.Add(new IncidentUpdate("Investigating", start, "Looking into it.", IncidentTone.Warning));
        if (resolved)
        {
            incident.Updates.Add(new IncidentUpdate("Resolved", start.AddMinutes(minutes), "Fixed.", IncidentTone.Success));
        }
        incident.Lead = new UptimePerson("Ada Lovelace");
        return incident;
    }

    private static UptimeStatus Block(bool withIncidents = true)
    {
        var block = new UptimeStatus { Width = 1152, PageName = "Test status", Today = Now, UpdatedAt = Now.AddMinutes(2) };
        block.Services.Add(MakeService("a", "Alpha", 350, 80, 210, 0));
        block.Services.Add(MakeService("b", "Beta", 900, 200, 520, 1));
        block.Services.Add(MakeService("c", "Gamma", 700, 120, 380, 2));
        if (withIncidents)
        {
            block.Incidents.Add(MakeIncident("i1", 22, 19, ServiceStatus.Outage, true, "a"));
            block.Incidents.Add(MakeIncident("i2", 35, 61, ServiceStatus.Degraded, true, "b"));
            block.Incidents.Add(MakeIncident("i3", 48, 29, ServiceStatus.Degraded, true, "c", "a"));
        }
        return block;
    }

    private static Avalonia.Controls.Window Show(UptimeStatus block, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = 1300;
        window.Height = 1100;
        Pump();
        return window;
    }

    private static void Pump(int rounds = 4)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void The_Block_Derives_Days_Uptime_And_Status_From_The_Incidents()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.Rows.Count.ShouldBe(3);
            var a = block.Services[0];
            a.Days.Count.ShouldBe(90);
            // Two incidents hit Alpha: 19 + 29 minutes.
            UptimeEngine.PercentText(a.Uptime).ShouldBe("99.96%");
            a.Days[89 - 22].Status.ShouldBe(ServiceStatus.Outage);
            a.Days[89 - 48].Status.ShouldBe(ServiceStatus.Degraded);
            a.Days[89].Status.ShouldBe(ServiceStatus.Operational);
            a.CurrentStatus.ShouldBe(ServiceStatus.Operational);
            block.OverallStatus.ShouldBe(ServiceStatus.Operational);
            block.Classes.Contains(":operational").ShouldBeTrue();
            block.SelectedService.ShouldBe(a);
            block.Rows[0].Strip.Count.ShouldBe(90);
            block.Rows[0].Strip.Statuses[89 - 22].ShouldBe(ServiceStatus.Outage);
            block.TitleSwap!.Text.ShouldBe("All systems operational");
            block.SummarySwap!.Text.ShouldBe("Updated Mar 14 at 14:32 UTC");
            block.CountNumber!.Amount.ShouldBe(3);
            block.IncidentList!.LiveItems.Count.ShouldBe(3);
            // Newest first.
            block.IncidentList.LiveItems[0].Incident.Key.ShouldBe("i1");
            block.IncidentList.LiveItems[0].HasRule.ShouldBeFalse();
            block.IncidentList.LiveItems[1].HasRule.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bar_Band_Uses_The_Measured_Width_And_Heights()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var strip = block.Rows[0].Strip;
            var width = strip.Bounds.Width;
            width.ShouldBe(1152 - 40, 1);
            strip.Bounds.Height.ShouldBe(34);
            strip.BarRect(0).Width.ShouldBe(UptimeEngine.BarWidth(width, 90));
            strip.BarRect(0).Height.ShouldBe(34);
            strip.BarRect(89 - 22).Height.ShouldBe(12);
            strip.BarRect(89 - 48).Height.ShouldBe(22);
            strip.BarRect(89).Right.ShouldBe(width, 0.5);
            block.Rows[0].Bounds.Height.ShouldBe(77.8, 1.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Focused_Band_Inspects_The_Newest_Day_And_Pages_By_Seven()
    {
        var block = Block();
        var days = new List<UptimeDayEventArgs>();
        block.DayInspected += (_, e) => days.Add(e);
        var window = Show(block);
        try
        {
            var strip = block.Rows[1].Strip;
            strip.Focus(NavigationMethod.Tab);
            Pump();
            strip.InspectedIndex.ShouldBe(89);
            block.InspectedDay.ShouldBe(89);
            block.Tip!.IsShown.ShouldBeTrue();
            block.Tip.Content!.Title.ShouldStartWith("Beta · ");
            days[^1].Day!.Date.ShouldBe(new DateOnly(2027, 3, 14));
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            Pump();
            strip.InspectedIndex.ShouldBe(82);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(81);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Home, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(89);
            block.InspectAnnouncer!.Announcement!.ShouldContain("100.00% uptime");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(-1);
            block.InspectedDay.ShouldBeNull();
            // After Escape ← starts from the day before the newest.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            strip.InspectedIndex.ShouldBe(88);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Day_Card_Lists_The_Incidents_Of_That_Day_Or_Says_There_Were_None()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var strip = block.Rows[0].Strip;
            strip.Inspect(89 - 22, false);
            Pump();
            var content = block.Tip!.Content!;
            content.Percent.ShouldBe("98.68%");
            content.Status.ShouldBe(ServiceStatus.Outage);
            content.StatusName.ShouldBe("Major outage");
            content.Rows.Count.ShouldBe(1);
            content.Rows[0].Duration.ShouldBe("19 min");
            strip.Inspect(10, false);
            Pump();
            block.Tip.Content!.Rows.Count.ShouldBe(0);
            block.Tip.Content.EmptyText.ShouldBe("No incidents recorded");
            // The card sits 8 above the band and is centred on the bar.
            var stripTop = strip.TranslatePoint(default, block.Tip)!.Value;
            block.Tip.Target.Y.ShouldBe(stripTop.Y - 8 - block.Tip.Measure(block.Tip.Content).Height, 0.5);
            block.Tip.Target.X.ShouldBe(UptimeEngine.TipLeft(strip.TranslatePoint(new Point(strip.BarCenter(10), 0), block.Tip)!.Value.X, block.Tip.Bounds.Width), 0.5);
            strip.ClearInspection();
            Pump();
            block.Tip.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Service_Charts_It_And_Moves_The_Highlight()
    {
        var block = Block();
        var changes = new List<UptimeServiceEventArgs>();
        block.SelectedServiceChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            block.ServiceList!.HighlightTarget.Y.ShouldBe(block.Rows[0].Bounds.Y, 0.5);
            var press = block.Rows[1].Button.TranslatePoint(new Point(40, 10), window)!.Value;
            window.MouseMove(press, RawInputModifiers.None);
            window.MouseDown(press, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(press, MouseButton.Left, RawInputModifiers.None);
            Pump();
            block.SelectedService.ShouldBe(block.Services[1]);
            changes.Count.ShouldBe(1);
            changes[0].Service.ShouldBe(block.Services[1]);
            block.ServiceList.HighlightTarget.Y.ShouldBe(block.Rows[1].Bounds.Y, 0.5);
            block.Rows[1].Button.IsChecked.ShouldBe(true);
            block.Rows[0].Button.IsChecked.ShouldBe(false);
            block.Chart!.TargetGoal.ShouldBe(900);
            block.Chart.Axis.TargetMax.ShouldBe(1250);
            block.SubtitleSwap!.Text.ShouldBe("Beta · last 24 hours");
            block.TargetNumber!.Amount.ShouldBe(900);
            // The strip's Enter key also chooses.
            block.Rows[2].Strip.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            block.SelectedService.ShouldBe(block.Services[2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Chart_Is_Not_Inspected_On_Focus_And_Its_Keys_Step_By_Half_Hour()
    {
        var block = Block();
        var points = new List<UptimePointEventArgs>();
        block.PointInspected += (_, e) => points.Add(e);
        var window = Show(block);
        try
        {
            var chart = block.Chart!;
            chart.Focus(NavigationMethod.Tab);
            Pump();
            chart.CursorIndex.ShouldBe(-1);
            block.LatestText!.Text.ShouldBe("Latest, 14:30 UTC");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(47);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(46);
            block.InspectedPoint.ShouldBe(46);
            var sample = block.Services[0].Latency[46];
            block.P95Number!.Amount.ShouldBe(sample.P95);
            block.P50Number!.Amount.ShouldBe(sample.P50);
            block.LatestText.Text.ShouldBe("Latest, Mar 14, 14:00 UTC");
            points[^1].Sample.ShouldBe(sample);
            block.InspectAnnouncer!.Announcement!.ShouldContain("p95");
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(46);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(-1);
            block.InspectedPoint.ShouldBeNull();
            block.LatestText.Text.ShouldBe("Latest, 14:30 UTC");
            block.P95Number.Amount.ShouldBe(block.Services[0].Latency[^1].P95);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Incident_Changes_The_Header_The_Row_The_Counts_And_Grows_A_List_Item_And_Resolving_Reverses_It()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var original = block.Services[2].Uptime;
            var incident = MakeIncident("i4", 0, 34, ServiceStatus.Degraded, false, "c");
            incident.Start = Now.AddMinutes(-34);
            incident.IsExpanded = true;
            block.Incidents.Add(incident);
            block.StatusSummary = "Gamma slow since 13:56 UTC";
            Pump();
            block.OverallStatus.ShouldBe(ServiceStatus.Degraded);
            block.Classes.Contains(":degraded").ShouldBeTrue();
            block.Icon!.Status.ShouldBe(ServiceStatus.Degraded);
            block.TitleSwap!.Text.ShouldBe("Degraded performance");
            block.SummarySwap!.Text.ShouldBe("Gamma slow since 13:56 UTC");
            block.Services[2].CurrentStatus.ShouldBe(ServiceStatus.Degraded);
            block.Rows[2].Button.Badge!.IsVisible.ShouldBeTrue();
            block.Rows[2].Button.Badge!.Label.ShouldBe("Degraded");
            block.Rows[2].Strip.Statuses[89].ShouldBe(ServiceStatus.Degraded);
            block.CountNumber!.Amount.ShouldBe(4);
            block.IncidentList!.LiveItems.Count.ShouldBe(4);
            block.IncidentList.LiveItems[0].Incident.ShouldBe(incident);
            block.IncidentList.LiveItems[0].Header.Badge!.IsVisible.ShouldBeTrue();
            block.IncidentList.LiveItems[0].Header.MetaText.ShouldContain("34 min so far");
            block.StatusAnnouncer!.Announcement.ShouldBe("Incident: Incident i4");
            var before = block.Services[2].Uptime;

            incident.Updates.Add(new IncidentUpdate("Resolved", Now.AddMinutes(19), "Fixed.", IncidentTone.Success));
            incident.Duration = TimeSpan.FromMinutes(53);
            incident.IsResolved = true;
            block.StatusSummary = null;
            Pump();
            block.OverallStatus.ShouldBe(ServiceStatus.Operational);
            block.TitleSwap.Text.ShouldBe("All systems operational");
            block.Rows[2].Button.Badge!.IsVisible.ShouldBeFalse();
            // Today stays degraded: the day had an incident.
            block.Rows[2].Strip.Statuses[89].ShouldBe(ServiceStatus.Degraded);
            block.Services[2].Uptime.ShouldBeLessThan(before);
            block.IncidentList.LiveItems[0].Header.ResolvedBlock!.IsVisible.ShouldBeTrue();
            block.IncidentList.LiveItems[0].Timeline.EntryCount.ShouldBe(2);
            block.StatusAnnouncer.Announcement!.ShouldBe("Resolved: Incident i4");

            block.Incidents.Remove(incident);
            Pump();
            block.IncidentList.LiveItems.Count.ShouldBe(3);
            block.CountNumber.Amount.ShouldBe(3);
            block.Rows[2].Strip.Statuses[89].ShouldBe(ServiceStatus.Operational);
            block.Services[2].Uptime.ShouldBe(original, 1e-12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Incident_Opens_And_Closes_And_The_Event_Reports_It()
    {
        var block = Block();
        var events = new List<UptimeIncidentEventArgs>();
        block.IncidentExpandedChanged += (_, e) => events.Add(e);
        var window = Show(block);
        try
        {
            var item = block.IncidentList!.LiveItems[1];
            item.Incident.IsExpanded.ShouldBeFalse();
            item.Slot.IsExpanded.ShouldBeFalse();
            item.Header.IsChecked = true;
            Pump();
            item.Incident.IsExpanded.ShouldBeTrue();
            item.Slot.IsExpanded.ShouldBeTrue();
            events.Count.ShouldBe(1);
            events[0].Incident.ShouldBe(item.Incident);
            events[0].IsExpanded.ShouldBeTrue();
            item.Timeline.Phases.ShouldBe(["Resolved", "Investigating"]);
            AutomationProperties.GetName(item.Header)!.ShouldContain("expanded");
            item.Incident.IsExpanded = false;
            Pump();
            item.Slot.IsExpanded.ShouldBeFalse();
            item.Header.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Blocks_Own_Width()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.IsStacked.ShouldBeFalse();
            block.LowerGrid!.ColumnDefinitions.Count.ShouldBe(2);
            block.Rows[0].Strip.Bounds.Width.ShouldBe(1112, 1);
            block.Width = 700;
            Pump();
            block.IsStacked.ShouldBeTrue();
            block.Classes.Contains(":stacked").ShouldBeTrue();
            block.LowerGrid.ColumnDefinitions.Count.ShouldBe(1);
            Avalonia.Controls.Grid.GetRow(block.IncidentsFrame!).ShouldBe(1);
            block.IncidentsFrame!.BorderThickness.Left.ShouldBe(0);
            block.Rows[0].Strip.BarRect(0).Width.ShouldBe(UptimeEngine.BarWidth(block.Rows[0].Strip.Bounds.Width, 90));
            block.Width = 400;
            Pump();
            block.IsCompactLayout.ShouldBeTrue();
            block.Classes.Contains(":compact").ShouldBeTrue();
            block.Rows[0].Strip.BarRect(0).Width.ShouldBe(3);
            block.Rows[0].Button.Classes.Contains(":compact").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Strip_And_Chart_Are_Named_And_Described_For_Assistive_Technology()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            AutomationProperties.GetName(block.Rows[0].Strip).ShouldBe("Alpha, daily status for the last 90 days");
            AutomationProperties.GetHelpText(block.Rows[0].Strip)!.ShouldContain("99.96% uptime");
            AutomationProperties.GetHelpText(block.Rows[0].Strip)!.ShouldContain("Major outage, 19 minutes");
            AutomationProperties.GetName(block.Rows[0].Button).ShouldBe("Alpha, 99.96% uptime, Operational");
            AutomationProperties.GetName(block.Chart!)!.ShouldContain("Alpha response time chart");
            AutomationProperties.GetHelpText(block.Chart!)!.ShouldContain("Target 350 ms");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_Another_Service_Springs_The_Curves_The_Domain_And_The_Highlight_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Chart!.Axis.TargetMax.ShouldBe(500);
            var firstP95 = block.Chart.P95Rows[10];
            block.SelectedService = block.Services[1];
            Pump();
            clock.Advance(TimeSpan.FromMilliseconds(30));
            // Mid-flight: the domain, a curve value, the target and the highlight are between old and new.
            block.Chart.Axis.DomainMax.ShouldBeInRange(500.5, 1249.5);
            block.Chart.P95Rows[10].ShouldNotBe(firstP95);
            block.Chart.P95Rows[10].ShouldNotBe(block.Chart.P95Rows.Targets[10]);
            block.Chart.TargetNow.ShouldBeInRange(350.5, 899.5);
            var highlight = block.ServiceList!.Highlight.Y;
            highlight.ShouldBeGreaterThan(block.Rows[0].Bounds.Y);
            highlight.ShouldBeLessThan(block.Rows[1].Bounds.Y);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            block.Chart.Axis.DomainMax.ShouldBe(1250, 0.5);
            block.Chart.TargetNow.ShouldBe(900, 0.5);
            block.ServiceList.Highlight.Y.ShouldBe(block.Rows[1].Bounds.Y, 0.5);
            // The curve values land on the chosen service's samples.
            block.Chart.P95Rows[10].ShouldBe(block.Services[1].Latency[10].P95, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Day_Card_Fades_In_Follows_On_A_Spring_And_Fades_Out()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var strip = block.Rows[0].Strip;
            strip.Inspect(20, false);
            Pump();
            block.Tip!.CardOpacity.ShouldBe(0, 0.05);
            clock.Advance(TimeSpan.FromMilliseconds(80));
            block.Tip.CardOpacity.ShouldBeInRange(0.2, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            block.Tip.CardOpacity.ShouldBe(1, 0.01);
            strip.BarOpacity.ShouldBe(0.38, 0.01);
            var start = block.Tip.Position.X;
            strip.Inspect(60, false);
            Pump();
            clock.Advance(TimeSpan.FromMilliseconds(40));
            block.Tip.Position.X.ShouldBeGreaterThan(start);
            block.Tip.Position.X.ShouldBeLessThan(block.Tip.Target.X);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            block.Tip.Position.X.ShouldBe(block.Tip.Target.X, 0.5);
            strip.ClearInspection();
            Pump();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            block.Tip.CardOpacity.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            block.Tip.CardOpacity.ShouldBe(0, 0.01);
            strip.BarOpacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Status_Change_Springs_The_Icon_Lines_And_Fades_The_Bar_Colour_And_Reduced_Motion_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var incident = MakeIncident("i9", 0, 20, ServiceStatus.Degraded, false, "c");
            incident.Start = Now.AddMinutes(-20);
            block.Incidents.Add(incident);
            Pump();
            clock.Advance(TimeSpan.FromMilliseconds(30));
            // The bar height of today (34 → 22) and the icon's line ends are between their shapes.
            block.Rows[2].Strip.Heights[89].ShouldBeInRange(22.1, 33.9);
            var lines = block.Icon!.Lines.Values.ToArray();
            lines[0].ShouldNotBe(6.1);
            lines[0].ShouldNotBe(10);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            block.Rows[2].Strip.Heights[89].ShouldBe(22, 0.2);
            block.Icon.Lines.Values[0].ShouldBe(10, 0.05);
            block.Icon.Lines.Values[7].ShouldBe(10.9, 0.05);
        }
        finally
        {
            window.Close();
        }

        var reduced = Block();
        var window2 = Show(reduced, ArcMotionLevel.Reduced);
        try
        {
            var incident = MakeIncident("i9", 0, 20, ServiceStatus.Degraded, false, "c");
            incident.Start = Now.AddMinutes(-20);
            reduced.Incidents.Add(incident);
            Pump();
            reduced.Rows[2].Strip.Heights[89].ShouldBe(22, 0.01);
            reduced.Icon!.Lines.Values[0].ShouldBe(10, 0.01);
        }
        finally
        {
            window2.Close();
        }
    }

    [AvaloniaFact]
    public void An_All_Green_Page_Shows_The_Empty_Incident_Text_And_No_Services_Shows_The_Empty_State()
    {
        var block = Block(withIncidents: false);
        var window = Show(block);
        try
        {
            block.CountNumber!.Amount.ShouldBe(0);
            block.IncidentList!.LiveItems.Count.ShouldBe(0);
            block.Rows[0].Service.Days.ShouldAllBe(d => d.Status == ServiceStatus.Operational);
            UptimeEngine.PercentText(block.Services[0].Uptime).ShouldBe("100.00%");
            block.Services.Clear();
            Pump();
            block.Classes.Contains(":empty").ShouldBeTrue();
            block.Rows.Count.ShouldBe(0);
            block.SelectedService.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
