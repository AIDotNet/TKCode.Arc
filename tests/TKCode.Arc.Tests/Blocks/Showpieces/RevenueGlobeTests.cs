using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="RevenueGlobe"/>: data and totals, playback, selection and flights, keyboard, hit testing, layouts, motion levels, demos.</summary>
public class RevenueGlobeTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Four cities, three regions; Boston pays $10 every minute, Zurich $20 every two minutes, Osaka $5 every five minutes
    /// and Denver (HQ) $1 every ten minutes, all day. The clock is external and stands at 12:00.
    /// </summary>
    private static RevenueGlobe Globe()
    {
        var globe = new RevenueGlobe { ClockMode = RevenueGlobeClockMode.External, Title = "Payments", DateText = "Thursday" };
        globe.Regions.Add(new RevenueRegion { Key = "am", Name = "Americas", Forecast = 40_000 });
        globe.Regions.Add(new RevenueRegion { Key = "eu", Name = "EMEA", Forecast = 20_000 });
        globe.Regions.Add(new RevenueRegion { Key = "ap", Name = "APAC", Forecast = 10_000 });
        globe.Cities.Add(City("den", "Denver", "US", "am", 39.74, -104.99, -7));
        globe.Cities.Add(City("bos", "Boston", "US", "am", 42.36, -71.06, -5));
        globe.Cities.Add(City("zur", "Zurich", "CH", "eu", 47.38, 8.54, 1));
        globe.Cities.Add(City("osa", "Osaka", "JP", "ap", 34.69, 135.50, 9));
        globe.Headquarters = globe.Cities[0];
        globe.DayStart = Day;
        globe.Now = Day.AddHours(12);
        var payments = new List<RevenuePayment>();
        for (var minute = 0; minute < 1440; minute++)
        {
            var time = Day.AddMinutes(minute).AddSeconds(30);
            payments.Add(new RevenuePayment(time, "bos", 10));
            if (minute % 2 == 0)
            {
                payments.Add(new RevenuePayment(time, "zur", 20));
            }
            if (minute % 5 == 0)
            {
                payments.Add(new RevenuePayment(time, "osa", 5));
            }
            if (minute % 10 == 0)
            {
                payments.Add(new RevenuePayment(time, "den", 1));
            }
        }
        globe.Payments = payments;
        return globe;
    }

    private static RevenueCity City(string key, string name, string country, string region, double lat, double lon, double utc) => new()
    {
        Key = key, Name = name, CountryCode = country, RegionKey = region, Latitude = lat, Longitude = lon, UtcOffsetHours = utc
    };

    private static (RevenueGlobe Globe, Window Window) ShowGlobe(double width = 700, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var globe = Globe();
        var window = TestHelpers.Show(globe, level);
        window.Width = width;
        window.Height = 1400;
        Dispatcher.UIThread.RunJobs();
        return (globe, window);
    }

    // Expected totals of the dataset at a time of day (payments at :30 of every minute up to and including it).
    private static (double Revenue, int Count) Expected(double seconds)
    {
        var revenue = 0.0;
        var count = 0;
        for (var minute = 0; minute < 1440; minute++)
        {
            if (minute * 60 + 30 > seconds)
            {
                break;
            }
            revenue += 10;
            count++;
            if (minute % 2 == 0)
            {
                revenue += 20;
                count++;
            }
            if (minute % 5 == 0)
            {
                revenue += 5;
                count++;
            }
            if (minute % 10 == 0)
            {
                revenue += 1;
                count++;
            }
        }
        return (revenue, count);
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Arc_Preview()
    {
        var globe = new RevenueGlobe();
        globe.ReplaySpeed.ShouldBe(1500);
        globe.HomeTilt.ShouldBe(22);
        globe.DriftSpeed.ShouldBe(4);
        globe.DriftCenterOffset.ShouldBe(40);
        globe.DriftHalfRange.ShouldBe(43);
        globe.CityZoom.ShouldBe(1.30);
        globe.RegionZoom.ShouldBe(1.08);
        globe.CurrencySymbol.ShouldBe("$");
        globe.TimeFormat.ShouldBe("HH:mm:ss");
        globe.ClockMode.ShouldBe(RevenueGlobeClockMode.Internal);
        globe.IsLive.ShouldBeTrue();
        globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Live);
        globe.Labels.Replay.ShouldBe("Replay today");
        globe.Labels.OfForecast(62).ShouldBe("62% of forecast");
        // Now starts at the current time (whole seconds) and the day at its midnight UTC.
        (DateTimeOffset.UtcNow - globe.Now).TotalSeconds.ShouldBeInRange(-1, 5);
        globe.Time.ShouldBe(globe.Now);
    }

    [AvaloniaFact]
    public void Figures_Show_The_Totals_At_The_Playhead()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var noon = Expected(12 * 3600);
            globe.CurrentRevenue.ShouldBe(noon.Revenue);
            globe.CurrentPaymentCount.ShouldBe(noon.Count);
            globe.CurrentLastHour.ShouldBe(noon.Revenue - Expected(11 * 3600).Revenue);
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.RevenuePart).Text.ShouldBe(ShowpieceFormat.Currency(noon.Revenue));
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.PaymentsPart).Text.ShouldBe(ShowpieceFormat.Grouped(noon.Count));
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.ClockPart).Text.ShouldBe("12:00:00");

            // Regions: Americas = Boston + Denver against 40,000.
            var rows = globe.RegionRowControls;
            rows.Count.ShouldBe(3);
            var americas = globe.CityRevenueAt(0) + globe.CityRevenueAt(1);
            rows[0].Amount.ShouldBe(americas);
            rows[0].Progress.ShouldBe(americas / 40_000, 1e-9);
            rows[0].ShareText.ShouldBe($"{Math.Round(americas / 400):0}% of forecast");

            // Top cities, best first (four cities, so four rows).
            globe.CityRowControls.Count.ShouldBe(4);
            globe.CityRowControls.Select(r => r.CityName).ShouldBe(["Boston", "Zurich", "Osaka", "Denver"]);

            globe.SeekTo(Day.AddHours(6));
            globe.CurrentRevenue.ShouldBe(Expected(6 * 3600).Revenue);
            globe.Time.ShouldBe(Day.AddHours(6));
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.ClockPart).Text.ShouldBe("06:00:00");
            globe.Timeline!.PlayedFraction.ShouldBe(0.25, 1e-9);
            globe.Timeline.AvailableFraction.ShouldBe(0.5, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Histogram_Counts_Payments_Per_Quarter_Hour()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var bins = globe.Ledger.Bins;
            bins.Length.ShouldBe(96);
            // 15 Boston + 8 or 7 Zurich + 3 Osaka + 2 or 1 Denver payments per quarter hour.
            bins[0].ShouldBe(15 + 8 + 3 + 2);
            globe.Ledger.MaxBin.ShouldBe(28);
            globe.Timeline!.BarHeight(0).ShouldBe(36);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Time_Changes_Raise_TimeChanged_And_Follow_The_Two_Way_Property()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var events = new List<RevenueGlobeTimeChangedEventArgs>();
            globe.TimeChanged += (_, e) => events.Add(e);
            globe.Time = Day.AddHours(3);
            events.ShouldHaveSingleItem().Time.ShouldBe(Day.AddHours(3));
            // Without full motion a seek holds (nothing would replay it).
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Paused);
            globe.IsLive.ShouldBeFalse();
            globe.IsPaused.ShouldBeTrue();
            globe.Classes.Contains(":paused").ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.LiveTextPart).Text.ShouldBe("Go live");
            globe.GoLive();
            globe.IsLive.ShouldBeTrue();
            globe.Time.ShouldBe(Day.AddHours(12));
            // Under reduced or no motion the pill reads "Now".
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.LiveTextPart).Text.ShouldBe("Now");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Starts_At_Midnight_And_Goes_Live_At_The_End()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var announcer = TestHelpers.Descendant<ShowpieceLiveRegion>(globe);
            globe.Replay();
            globe.Time.ShouldBe(Day);
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Replaying);
            globe.CurrentRevenue.ShouldBe(0);
            announcer.LastAnnouncement.ShouldBe("Replaying today");
            globe.Classes.Contains(":replaying").ShouldBeTrue();
            // Twelve hours at 1500× take about 29 s.
            var seconds = 0.0;
            while (globe.PlaybackState == RevenueGlobePlaybackState.Replaying && seconds < 60)
            {
                globe.AdvanceClock(1 / 30.0);
                seconds += 1 / 30.0;
            }
            seconds.ShouldBe(12 * 2.4 + 1, 1.2);
            globe.IsLive.ShouldBeTrue();
            announcer.LastAnnouncement.ShouldBe("Back to live");
            globe.CurrentRevenue.ShouldBe(Expected(12 * 3600).Revenue);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Holds_Time_And_Play_Replays_On()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var pause = TestHelpers.Descendant<AvaloniaButton>(globe, RevenueGlobe.PausePart);
            AutomationProperties.GetName(pause).ShouldBe("Pause");
            globe.IsPaused = true;
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Paused);
            AutomationProperties.GetName(pause).ShouldBe("Play");
            globe.Now = Day.AddHours(12).AddMinutes(5);
            globe.Time.ShouldBe(Day.AddHours(12), "paused");
            globe.TogglePauseCommand.Execute(null);
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Replaying);
            globe.IsPaused.ShouldBeFalse();
            for (var i = 0; i < 10; i++)
            {
                globe.AdvanceClock(0.1);
            }
            globe.IsLive.ShouldBeTrue("five minutes replay in well under a second");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Payments_Fly_And_Count_When_They_Land()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            var canvas = globe.Canvas!;
            var before = globe.CurrentRevenue;
            // A minute passes: Boston, Zurich (12:00 is even) and Osaka (12:00 is a multiple of five) pay at 12:00:30.
            for (var i = 0; i < 60; i++)
            {
                globe.Now = globe.Now.AddSeconds(1);
            }
            canvas.ArcCount.ShouldBe(3, "Denver's own payment draws no arc");
            globe.PendingCount.ShouldBe(3);
            globe.CurrentRevenue.ShouldBe(before + 1, "only the HQ payment counts before the arcs land");
            for (var i = 0; i < 120; i++)
            {
                canvas.Step(1 / 60.0);
            }
            globe.PendingCount.ShouldBe(0);
            globe.CurrentRevenue.ShouldBe(before + 36);
            canvas.RingCount.ShouldBeGreaterThan(0);
            for (var i = 0; i < 120; i++)
            {
                canvas.Step(1 / 60.0);
            }
            canvas.ArcCount.ShouldBe(0);
            canvas.RingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Added_Payments_Count_And_Fly()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            var before = globe.CurrentRevenue;
            globe.AddPayment(new RevenuePayment(globe.Now.AddSeconds(1), "zur", 100));
            globe.CurrentRevenue.ShouldBe(before, "not due yet");
            globe.Now = globe.Now.AddSeconds(2);
            globe.Canvas!.ArcCount.ShouldBe(1);
            globe.Ledger.Count.ShouldBe(Expected(86400).Count + 1);
            // Out of order: the ledger is rebuilt and the payment still counts.
            globe.AddPayment(new RevenuePayment(Day.AddHours(1), "osa", 7));
            globe.Ledger.Count.ShouldBe(Expected(86400).Count + 2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Starts_At_Most_14_Arcs_A_Second()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            globe.Replay();
            var canvas = globe.Canvas!;
            var started = 0;
            for (var i = 0; i < 120; i++)
            {
                var arcs = canvas.ArcCount;
                globe.AdvanceClock(1 / 60.0);
                started += canvas.ArcCount - arcs;
            }
            // 14 a second; a pick that falls on an HQ payment flies no arc.
            started.ShouldBeInRange(20, 29);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_A_City_Flies_There_And_Clears_The_Region()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var changes = new List<RevenueGlobeSelectionChangedEventArgs>();
            globe.SelectionChanged += (_, e) => changes.Add(e);
            var announcer = TestHelpers.Descendant<ShowpieceLiveRegion>(globe);
            globe.SelectedRegion = globe.Regions[2];
            globe.Zoom.ShouldBe(1.08, 1e-6);
            globe.Canvas!.LabeledCities.ShouldBe([3]);
            globe.Classes.Contains(":region-selected").ShouldBeTrue();
            globe.RegionRowControls[2].IsChecked.ShouldBe(true);
            announcer.LastAnnouncement.ShouldBe("Camera on APAC");

            var zurich = globe.Cities[2];
            globe.SelectedCity = zurich;
            globe.SelectedRegion.ShouldBeNull();
            changes.Count.ShouldBe(2);
            changes[1].OldRegion.ShouldBe(globe.Regions[2]);
            changes[1].NewCity.ShouldBe(zurich);
            // Without motion the camera lands at once: Zurich in the middle, zoomed to 1.3.
            globe.CenterLongitude.ShouldBe(8.54, 1e-6);
            globe.CenterLatitude.ShouldBe(47.38, 1e-6);
            globe.Zoom.ShouldBe(1.3, 1e-6);
            globe.TipCity.ShouldBe(2, "the card stays on the selected city");
            globe.CityRowControls.Single(r => r.CityName == "Zurich").IsChecked.ShouldBe(true);

            // Escape on the globe lets go: zoom 1, tilt home, longitude kept.
            globe.Canvas.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            globe.SelectedCity.ShouldBeNull();
            globe.Zoom.ShouldBe(1, 1e-6);
            globe.CenterLatitude.ShouldBe(22, 1e-6);
            globe.CenterLongitude.ShouldBe(8.54, 1e-6);
            announcer.LastAnnouncement.ShouldBe("Selection cleared");
            globe.TipCity.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Toggle_The_Selection_And_Report_Clicked_Cities()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var invoked = new List<RevenueCity>();
            globe.CityInvoked += (_, e) => invoked.Add(e.City);
            var row = globe.CityRowControls[0];
            row.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            globe.SelectedCity.ShouldBe(globe.Cities[1]);
            invoked.ShouldBe([globe.Cities[1]]);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            globe.SelectedCity.ShouldBeNull("a second press clears");
            invoked.Count.ShouldBe(2);
            var region = globe.RegionRowControls[1];
            region.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            globe.SelectedRegion.ShouldBe(globe.Regions[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cities_Hit_Within_11px_Of_Their_Dot_On_The_Near_Side()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var canvas = globe.Canvas!;
            canvas.TryProjectCity(1, out var boston, out var depth).ShouldBeTrue();
            depth.ShouldBeGreaterThan(0);
            canvas.CityAt(boston).ShouldBe(1);
            canvas.CityAt(boston + new Vector(12, 0)).ShouldBe(1);
            canvas.CityAt(boston + new Vector(16, 0)).ShouldBe(-1);
            // Osaka is on the far side of the home view: never hit.
            canvas.TryProjectCity(3, out var osaka, out _).ShouldBeFalse();
            canvas.CityAt(osaka).ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timeline_Keys_Step_Jump_And_Go_Live()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var timeline = globe.Timeline!;
            timeline.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            globe.Time.ShouldBe(Day.AddHours(12).AddMinutes(-5));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            globe.Time.ShouldBe(Day.AddHours(11).AddMinutes(-5));
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            globe.Time.ShouldBe(Day.AddHours(10).AddMinutes(-5));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            globe.Time.ShouldBe(Day);
            globe.CurrentRevenue.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            globe.Time.ShouldBe(Day.AddHours(1));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            globe.IsLive.ShouldBeTrue();
            var peer = (RevenueGlobeTimelineAutomationPeer)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(timeline);
            peer.Maximum.ShouldBe(12 * 3600);
            peer.Value.ShouldBe(12 * 3600);
            peer.GetName().ShouldBe("Time of day, UTC");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timeline_Drag_Scrubs_And_Releases_Into_A_Replay()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            var timeline = globe.Timeline!;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var left = timeline.TranslatePoint(new Point(timeline.Bounds.Width * 0.25, 20), window)!.Value;
            window.MouseDown(left, MouseButton.Left, RawInputModifiers.None);
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Scrubbing);
            globe.Time.ShouldBe(Day.AddHours(6));
            var next = timeline.TranslatePoint(new Point(timeline.Bounds.Width * 0.375, 20), window)!.Value;
            window.MouseMove(next, RawInputModifiers.LeftMouseButton);
            globe.Time.ShouldBe(Day.AddHours(9));
            window.MouseUp(next, MouseButton.Left, RawInputModifiers.None);
            globe.PlaybackState.ShouldBe(RevenueGlobePlaybackState.Replaying);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Turn_The_Globe()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var canvas = globe.Canvas!;
            canvas.Focus();
            var before = globe.CenterLongitude;
            // Without motion an impulse is a quarter second of travel: 55°/s × 0.25 s, the surface moving left.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            ShowpieceProjection.Degrees(ShowpieceProjection.WrapAngle(ShowpieceProjection.Radians(globe.CenterLongitude - before))).ShouldBe(13.75, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Arrow_Keys_Glide_And_Drags_Follow_The_Pointer()
    {
        // A manual clock: frames that run while the machine is busy would decay the glide before it is read.
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            var canvas = globe.Canvas!;
            var camera = canvas.OrbitCamera;
            camera.IsDriftEnabled.ShouldBeTrue();
            ShowpieceProjection.Degrees(camera.DriftCenter).ShouldBe(-104.99 + 40, 1e-9);
            canvas.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            ShowpieceProjection.Degrees(camera.YawVelocity).ShouldBe(-55, 4.5, "a push west adds −55°/s to the drift");
            // The glide loses 1.3% a frame.
            var v0 = camera.YawVelocity;
            canvas.Step(1 / 60.0);
            (camera.YawVelocity / v0).ShouldBe(0.987, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Block_Width()
    {
        var (globe, window) = ShowGlobe(1400);
        try
        {
            globe.CurrentLayout.ShouldBe(RevenueGlobeLayout.Wide);
            globe.Classes.Contains(":wide").ShouldBeTrue();
            var canvas = globe.Canvas!;
            canvas.Bounds.Height.ShouldBe(580, 0.5);
            canvas.Bounds.Width.ShouldBe(1400 - 2 - 320, 0.5);
            canvas.BaseRadius.ShouldBe(0.41 * 580, 0.5);

            window.Width = 700;
            Dispatcher.UIThread.RunJobs();
            globe.CurrentLayout.ShouldBe(RevenueGlobeLayout.Stacked);
            canvas.Bounds.Height.ShouldBe(0.66 * 698, 0.5);
            TestHelpers.Descendant<RevenueGlobePanelsLayout>(globe).TwoColumns.ShouldBeTrue();

            window.Width = 450;
            Dispatcher.UIThread.RunJobs();
            globe.CurrentLayout.ShouldBe(RevenueGlobeLayout.Narrow);
            globe.Classes.Contains(":narrow").ShouldBeTrue();
            globe.Classes.Contains(":compact").ShouldBeFalse();
            canvas.Bounds.Height.ShouldBe(Math.Min(440, 0.96 * 448), 1);
            TestHelpers.Descendant<TextBlock>(globe, RevenueGlobe.RevenuePart).FontSize.ShouldBe(22);

            window.Width = 390;
            Dispatcher.UIThread.RunJobs();
            globe.Classes.Contains(":compact").ShouldBeTrue();
            TestHelpers.Descendant<RevenueGlobeControlsPanel>(globe).ClockOnOwnRow.ShouldBeTrue();
            canvas.BaseRadius.ShouldBe(0.41 * Math.Min(388, 0.96 * 388), 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_A_Still_Snapshot_And_Requests_No_Frames()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Reduced);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            globe.Classes.Contains(":reduced").ShouldBeTrue();
            TestHelpers.Descendant<AvaloniaButton>(globe, RevenueGlobe.ReplayPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<AvaloniaButton>(globe, RevenueGlobe.PausePart).IsVisible.ShouldBeFalse();
            // One still arc per city that paid lately (three cities fly arcs here).
            globe.Canvas!.StillArcCount.ShouldBe(3);
            globe.Canvas.ArcCount.ShouldBe(0);
            for (var i = 0; i < 5; i++)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            globe.Canvas.IsLoopRunning.ShouldBeFalse("an idle globe asks for no frames");
            // Moving the present does not move the picture.
            globe.Now = globe.Now.AddMinutes(1);
            globe.Canvas.ArcCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accessibility_Names_The_Globe_And_Summarizes_The_Moment()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            var canvas = globe.Canvas!;
            AutomationProperties.GetName(canvas).ShouldBe("Revenue globe. Arrow keys turn the globe, Escape clears the selection.");
            var summary = AutomationProperties.GetHelpText(canvas);
            summary.ShouldStartWith($"{ShowpieceFormat.Currency(Expected(12 * 3600).Revenue)} revenue today from");
            summary.ShouldContain("as of 12:00 UTC");
            summary.ShouldContain("Boston leads.");
            var table = TestHelpers.Descendant<StackPanel>(globe, RevenueGlobe.TablePart);
            table.Children.Count.ShouldBe(4);
            ((TextBlock)table.Children[1]).Text.ShouldStartWith("Boston, Americas: $");
            globe.Labels = new RevenueGlobeLabels { Globe = "Umsatz", GlobeHelp = "Pfeiltasten drehen.", Live = "Jetzt live" };
            AutomationProperties.GetName(canvas).ShouldBe("Umsatz. Pfeiltasten drehen.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Top_City_Row_Rings_The_City_And_Opens_Its_Card()
    {
        var (globe, window) = ShowGlobe();
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var row = globe.CityRowControls[0];
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            globe.Canvas!.ListHoverCity.ShouldBe(1);
            globe.TipCity.ShouldBe(1);
            var tip = TestHelpers.Descendant<RevenueGlobeTip>(globe);
            tip.CityName.ShouldBe("Boston");
            // Boston is UTC−5: noon UTC is 07:00 there.
            tip.Subtitle.ShouldBe("Americas · 07:00 local");
            tip.TodayText.ShouldBe("$7,200");
            tip.PaymentsText.ShouldBe("720");
            tip.ShareText.ShouldBe(ShowpieceFormat.Percent(7200 / globe.CurrentRevenue, 1));
            TestHelpers.Descendant<ShowpieceHoverCard>(globe).IsOpen.ShouldBeTrue();
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            globe.TipCity.ShouldBe(-1);
            TestHelpers.Descendant<ShowpieceHoverCard>(globe).IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Globe_Is_Collected_While_Its_Payments_And_Regions_Live_On()
    {
        var payments = new System.Collections.ObjectModel.ObservableCollection<RevenuePayment> { new(Day.AddHours(1), "bos", 10) };
        var regions = new Avalonia.Collections.AvaloniaList<RevenueRegion> { new() { Key = "am", Name = "Americas", Forecast = 1_000 } };
        var weak = TestHelpers.ShowAndClose(() =>
        {
            var globe = Globe();
            globe.Payments = payments;
            globe.Regions = regions;
            return globe;
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        payments.Add(new RevenuePayment(Day.AddHours(2), "bos", 10));
        regions.Add(new RevenueRegion { Key = "eu", Name = "EMEA" });
    }

    [AvaloniaFact]
    public void A_Globe_Put_Back_Counts_What_Arrived_While_It_Was_Away()
    {
        var globe = Globe();
        var payments = new System.Collections.ObjectModel.ObservableCollection<RevenuePayment>(globe.Payments!);
        globe.Payments = payments;
        var regions = new Avalonia.Collections.AvaloniaList<RevenueRegion>(globe.Regions);
        globe.Regions = regions;
        var host = new Border { Child = globe };
        var window = TestHelpers.Show(host);
        window.Width = 700;
        window.Height = 1400;
        Dispatcher.UIThread.RunJobs();
        try
        {
            var before = globe.CurrentRevenue;
            globe.RegionRowControls.Count.ShouldBe(3);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            payments.Add(new RevenuePayment(Day.AddHours(11), "bos", 1_000));
            regions.Add(new RevenueRegion { Key = "oc", Name = "Oceania", Forecast = 5_000 });

            host.Child = globe;
            Dispatcher.UIThread.RunJobs();
            globe.CurrentRevenue.ShouldBe(before + 1_000);
            globe.RegionRowControls.Count.ShouldBe(4);

            // Back in the window, it listens again.
            payments.Add(new RevenuePayment(Day.AddHours(11), "zur", 500));
            Dispatcher.UIThread.RunJobs();
            globe.CurrentRevenue.ShouldBe(before + 1_500);
            regions.Add(new RevenueRegion { Key = "af", Name = "Africa", Forecast = 2_000 });
            Dispatcher.UIThread.RunJobs();
            globe.RegionRowControls.Count.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Clock_Loop_Runs_Only_While_Time_Moves_By_Itself()
    {
        var (globe, window) = ShowGlobe(level: ArcMotionLevel.Full);
        try
        {
            globe.IsClockActive.ShouldBeFalse("live on an external clock: the app moves time");
            globe.Replay();
            globe.IsClockActive.ShouldBeTrue();
            globe.Pause();
            globe.IsClockActive.ShouldBeFalse();
            globe.GoLive();
            globe.IsClockActive.ShouldBeTrue("the fast-forward to the present");
            globe.ClockMode = RevenueGlobeClockMode.Internal;
            globe.IsClockActive.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Land_Replaces_The_Natural_Earth_Dots()
    {
        var land = RevenueGlobeLand.FromPoints([1, 0, 0, 0, 1, 0], [0, 0, 1]);
        land.LandCount.ShouldBe(2);
        land.OceanCount.ShouldBe(1);
        Should.Throw<ArgumentException>(() => RevenueGlobeLand.FromPoints([1, 0], []));
        var (globe, window) = ShowGlobe();
        try
        {
            globe.Land = land;
            globe.Canvas!.Land.ShouldBeSameAs(land);
            globe.Land = null;
            globe.Canvas.Land.ShouldBeSameAs(RevenueGlobeLand.Default);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Demos_Render_In_Light_And_Dark()
    {
        Func<Control>[] demos =
        [
            () => new RevenueGlobePreviewDemo(), () => new RevenueGlobeSelectionDemo(), () => new RevenueGlobePlaybackDemo(),
            () => new RevenueGlobeNarrowDemo(), () => new RevenueGlobeLiveFeedDemo(), () => new RevenueGlobeReducedMotionDemo()
        ];
        try
        {
            foreach (var dark in new[] { false, true })
            {
                await TestHelpers.ApplyAsync(dark ? TestHelpers.Dark : null);
                foreach (var create in demos)
                {
                    var demo = create();
                    var window = TestHelpers.Show(demo, ArcMotionLevel.Full);
                    try
                    {
                        window.Width = 900;
                        window.Height = 1300;
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                        var globe = TestHelpers.Descendant<RevenueGlobe>(demo);
                        globe.Canvas.ShouldNotBeNull();
                        globe.Canvas.Bounds.Width.ShouldBeGreaterThan(0);
                        globe.Ledger.Count.ShouldBeGreaterThan(0);
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        Dispatcher.UIThread.RunJobs();
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Sample_Day_Is_Deterministic_And_Shaped_Like_The_Spec()
    {
        var a = new RevenueGlobe();
        RevenueGlobeSampleDay.Apply(a);
        var b = new RevenueGlobe();
        RevenueGlobeSampleDay.Apply(b);
        a.Payments!.Count().ShouldBe(b.Payments!.Count());
        a.Cities.Count.ShouldBe(40);
        a.Headquarters!.Name.ShouldBe("Denver");
        a.Now.ShouldBe(RevenueGlobeSampleDay.Start);
        var ledger = a.Ledger;
        ledger.Count.ShouldBeInRange(180_000, 240_000);
        // Night bars about half the day's, one spike to the top.
        var heights = Enumerable.Range(0, 96).Select(i => RevenueGlobeLedger.BarHeight(ledger.Bins[i], ledger.MaxBin)).ToArray();
        heights.Max().ShouldBe(36);
        heights.Take(16).Average().ShouldBeLessThan(heights.Skip(40).Take(16).Average());
        heights.Count(h => h > 30).ShouldBe(1);
    }
}
