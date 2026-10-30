using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// LiveVisitors: the simulated stream it plays with no data, data passed by the app (sorting, bars, sentences, times),
/// pushed arrivals and conversions, pause (button, keyboard, held snapshot, event, announcements), the connection states,
/// the rows' enter and exit, the map's pings and label, the narrow layout, labels and the pure parts (simulation, land).
/// </summary>
public class LiveVisitorsTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The tests' own invented site (the stream the library used to play built in; the Gallery now passes its own): about
    /// 128 people, 21 cities, eight pages and three kinds of conversion.
    /// </summary>
    private static LiveVisitorsSimulation Sample(int seed = 7) => new(seed, () => s_now)
    {
        TypicalCount = 128,
        Cities =
        [
            new("San Francisco", "US", 37.77, -122.42, 5), new("New York", "US", 40.71, -74.01, 5), new("Austin", "US", 30.27, -97.74, 3),
            new("Toronto", "CA", 43.65, -79.38, 3), new("Mexico City", "MX", 19.43, -99.13, 2), new("São Paulo", "BR", -23.55, -46.63, 3),
            new("London", "GB", 51.51, -0.13, 5), new("Dublin", "IE", 53.35, -6.26, 2), new("Paris", "FR", 48.86, 2.35, 3),
            new("Amsterdam", "NL", 52.37, 4.90, 3), new("Berlin", "DE", 52.52, 13.40, 4), new("Zurich", "CH", 47.38, 8.54, 2),
            new("Stockholm", "SE", 59.33, 18.07, 2), new("Madrid", "ES", 40.42, -3.70, 2), new("Lagos", "NG", 6.52, 3.38, 2),
            new("Nairobi", "KE", -1.29, 36.82, 1), new("Bengaluru", "IN", 12.97, 77.59, 4), new("Singapore", "SG", 1.35, 103.82, 3),
            new("Seoul", "KR", 37.57, 126.98, 2), new("Tokyo", "JP", 35.68, 139.69, 4), new("Sydney", "AU", -33.87, 151.21, 3),
        ],
        SitePages =
        [
            new("/", 34), new("/pricing", 22), new("/docs/quickstart", 17), new("/blog/launch-week", 14), new("/changelog", 9),
            new("/signup", 8), new("/customers", 6), new("/docs/api/webhooks", 5),
        ],
        Conversions = [new("Signed up", 62), new("Upgraded to Pro", 28), new("Upgraded to Team", 10)],
    };

    private static (LiveVisitors Card, Window Window) Open(Action<LiveVisitors>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off,
        double width = 640)
    {
        var card = new LiveVisitors { Now = () => s_now, Simulation = Sample(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static LiveVisitorsPage[] SamplePages() =>
    [
        new("/docs", 12), new("/pricing", 30), new("/", 18), new("/blog", 3), new("/changelog", 7),
    ];

    private static void UseData(LiveVisitors card)
    {
        card.Count = 70;
        card.Pages = SamplePages();
        card.Visitors = [new LiveVisitorsPing("a", 52.5, 13.4), new LiveVisitorsPing("b", 40.7, -74)];
        card.Events =
        [
            new LiveVisitorsEvent("e1", "Signed up", "Berlin", "DE", s_now.AddSeconds(-12)) { Latitude = 52.52, Longitude = 13.4 },
            new LiveVisitorsEvent("e2", "Upgraded to Pro", "London", "GB", s_now.AddMinutes(-3)),
        ];
    }

    [AvaloniaFact]
    public void Plays_The_Simulated_Stream_Without_Data()
    {
        var (card, window) = Open();
        try
        {
            card.IsSimulated.ShouldBeTrue();
            card.Classes.Contains(":simulated").ShouldBeTrue();
            card.DisplayedCount.ShouldBe(128);
            card.Digits!.Value.ShouldBe(128);
            AutomationProperties.GetName(card.CountBox!).ShouldBe("128 people on the site now");
            card.CountLabel!.Text.ShouldBe("people on the site now");
            card.PageList!.Keys.Count.ShouldBe(4);
            card.EventList!.Keys.Count.ShouldBe(4);
            card.Map!.PingIds.Count.ShouldBeGreaterThan(0);
            card.StatusText.ShouldBe("Live");
            card.Announcer!.History.ShouldContain("Showing a simulated stream of visitors");
            AutomationProperties.GetName(card).ShouldBe("Live visitors");
            card.IsClockActive.ShouldBeTrue();
            card.StatusDot!.IsPulsing.ShouldBeTrue();

            // About six seconds of traffic: arrivals every 600ms and a conversion every 2.4s reach the card.
            var before = card.EventList.Keys[0];
            card.Advance(6);
            Pump();
            card.Simulation!.Time.ShouldBeGreaterThanOrEqualTo(6);
            card.EventList.Keys[0].ShouldNotBe(before);
            card.Map.PingIds.Count.ShouldBeGreaterThan(5);

            // Reconnecting keeps the last data: the simulated stream holds until the card is live again.
            card.Connection = LiveVisitorsConnection.Reconnecting;
            Pump();
            var held = card.Simulation!.Time;
            card.Advance(3);
            card.Simulation!.Time.ShouldBe(held);
            card.Connection = LiveVisitorsConnection.Live;
            Pump();
            card.Advance(1);
            card.Simulation!.Time.ShouldBeGreaterThan(held);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shows_The_App_Data_Sorted_With_Bars_And_Sentences()
    {
        var (card, window) = Open(UseData);
        try
        {
            card.IsSimulated.ShouldBeFalse();
            card.Digits!.Value.ShouldBe(70);
            AutomationProperties.GetName(card.CountBox!).ShouldBe("70 people on the site now");
            card.PageList!.Keys.ShouldBe(["/pricing", "/", "/docs", "/changelog"]);
            var top = (LiveVisitorsPageRow)card.PageList.Rows[0];
            top.Path.ShouldBe("/pricing");
            top.CountText.ShouldBe("30");
            top.BarFraction.ShouldBe(1, 0.001);
            ((LiveVisitorsPageRow)card.PageList.Rows[1]).BarFraction.ShouldBe(18.0 / 30, 0.001);
            AutomationProperties.GetName(top).ShouldBe("/pricing, 30 people");

            card.EventList!.Keys.ShouldBe(["e1", "e2"]);
            var row = (LiveVisitorsEventRow)card.EventList.Rows[0];
            row.Label.ShouldBe("Signed up");
            row.City.ShouldBe("Berlin");
            row.Code.ShouldBe("DE");
            row.TimeText.ShouldBe("12s");
            AutomationProperties.GetName(row).ShouldBe("Signed up, Berlin, Germany, 12 seconds ago");
            ((LiveVisitorsEventRow)card.EventList.Rows[1]).TimeText.ShouldBe("3m");

            card.Map!.PingIds.ShouldBe(["a", "b"]);

            card.MaxPages = 2;
            Pump();
            card.PageList.Keys.ShouldBe(["/pricing", "/"]);

            card.Count = 1;
            Pump();
            card.CountLabel!.Text.ShouldBe("person on the site now");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Snapshots_Reorder_The_Rows_And_Update_In_Place()
    {
        var (card, window) = Open(UseData);
        try
        {
            var pricing = card.PageList!.RowOf("/pricing");
            card.Pages = [new("/docs", 40), new("/pricing", 30), new("/", 18), new("/changelog", 7)];
            Pump();
            card.PageList.Keys.ShouldBe(["/docs", "/pricing", "/", "/changelog"]);
            card.PageList.RowOf("/pricing").ShouldBeSameAs(pricing);
            card.PageList.OffsetOf("/pricing").ShouldBe(32);
            card.PageList.OffsetOf("/docs").ShouldBe(0);

            card.Events = [new LiveVisitorsEvent("e3", "Upgraded to Team", "Tokyo", "JP", s_now), .. card.Events!];
            Pump();
            card.EventList!.Keys.ShouldBe(["e3", "e1", "e2"]);
            ((LiveVisitorsEventRow)card.EventList.Rows[0]).TimeText.ShouldBe("now");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Slides_Rows_And_Fades_The_Leaving_One()
    {
        var (card, window) = Open(UseData, ArcMotionLevel.Full);
        try
        {
            card.MaxEvents = 2;
            Pump();
            card.Events = [new LiveVisitorsEvent("e3", "Signed up", "Paris", "FR", s_now), .. card.Events!];
            Dispatcher.UIThread.RunJobs();
            card.EventList!.Keys.ShouldBe(["e3", "e1"]);
            // The last row slides one row down and fades out below the clip; the new one starts transparent at the top.
            card.EventList.LeavingKeys.ShouldBe(["e2"]);
            card.EventList.RowOf("e3")!.Opacity.ShouldBeLessThan(1);
            card.EventList.OffsetOf("e3").ShouldBe(0);
            TestHelpers.PumpUntil(() => card.EventList.LeavingKeys.Count == 0 && Math.Abs(card.EventList.OffsetOf("e1") - 32) < 0.5).ShouldBeTrue();
            card.EventList.RowOf("e3")!.Opacity.ShouldBe(1, 0.001);
            card.EventList.OffsetOf("e1").ShouldBe(32, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Button_Holds_The_Snapshot_From_The_Keyboard()
    {
        var (card, window) = Open(UseData);
        try
        {
            var raised = new List<bool>();
            card.PausedChanged += (_, e) => raised.Add(e.IsPaused);
            var pause = card.PauseButton!;
            AutomationProperties.GetName(pause).ShouldBe("Pause live updates");
            Tooltip.GetTip(pause).ShouldBe("Pause live updates");

            pause.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Pump();

            card.IsPaused.ShouldBeTrue();
            pause.IsChecked.ShouldBe(true);
            raised.ShouldBe([true]);
            card.StatusText.ShouldBe("Paused");
            card.Classes.Contains(":paused").ShouldBeTrue();
            AutomationProperties.GetName(pause).ShouldBe("Resume live updates");
            card.Announcer!.Announcement.ShouldBe("Live updates paused");
            card.StatusDot!.IsPulsing.ShouldBeFalse();
            card.IsClockActive.ShouldBeFalse();

            // Whatever arrives while paused waits.
            card.Count = 99;
            card.Pages = [new("/new", 99)];
            Pump();
            card.Digits!.Value.ShouldBe(70);
            card.PageList!.Keys[0].ShouldBe("/pricing");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.IsPaused.ShouldBeFalse();
            raised.ShouldBe([true, false]);
            card.Digits.Value.ShouldBe(99);
            card.PageList.Keys.ShouldBe(["/new"]);
            card.Announcer.Announcement.ShouldBe("Live updates resumed");

            // Set from code: no PausedChanged, the button follows.
            card.IsPaused = true;
            Pump();
            pause.IsChecked.ShouldBe(true);
            raised.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pause_Button_Can_Be_Hidden()
    {
        var (card, window) = Open(c =>
        {
            UseData(c);
            c.IsPauseVisible = false;
        });
        try
        {
            card.PauseButton!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connecting_Shows_Placeholders_And_Reconnecting_Keeps_The_Data()
    {
        var (card, window) = Open(c =>
        {
            UseData(c);
            c.Connection = LiveVisitorsConnection.Connecting;
        });
        try
        {
            card.StatusText.ShouldBe("Connecting");
            card.Digits!.IsVisible.ShouldBeFalse();
            card.PageList!.IsPlaceholder.ShouldBeTrue();
            card.EventList!.IsPlaceholder.ShouldBeTrue();
            card.Map!.PingIds.ShouldBeEmpty();
            card.StatusDot!.IsPulsing.ShouldBeFalse();
            card.PageList.Bounds.Height.ShouldBe(4 * 32);

            card.Connection = LiveVisitorsConnection.Live;
            Pump();
            card.StatusText.ShouldBe("Live");
            card.Digits.IsVisible.ShouldBeTrue();
            card.PageList.IsPlaceholder.ShouldBeFalse();
            card.Map.PingIds.Count.ShouldBe(2);

            card.Connection = LiveVisitorsConnection.Reconnecting;
            Pump();
            card.StatusText.ShouldBe("Reconnecting");
            card.StatusDot.IsPulsing.ShouldBeFalse();
            card.PageList.Keys.Count.ShouldBe(4);
            card.Digits.Value.ShouldBe(70);

            // Paused while reconnecting: the word reads Paused and the dot matches it, not the warning color.
            var warning = (card.StatusDot.Fill as Avalonia.Media.ISolidColorBrush)!.Color;
            card.IsPaused = true;
            Pump();
            card.StatusText.ShouldBe("Paused");
            (card.StatusDot.Fill as Avalonia.Media.ISolidColorBrush)!.Color.ShouldNotBe(warning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Map_Pings_In_Fades_Out_And_Labels_The_Newest_Conversion()
    {
        var (card, window) = Open(UseData, ArcMotionLevel.Full);
        try
        {
            var map = card.Map!;
            map.LabelText.ShouldBeNull();
            card.Visitors = [new LiveVisitorsPing("b", 40.7, -74), new LiveVisitorsPing("c", 35.7, 139.7)];
            card.Events = [new LiveVisitorsEvent("e9", "Signed up", "Tokyo", "JP", s_now) { Latitude = 35.7, Longitude = 139.7 }];
            Dispatcher.UIThread.RunJobs();
            map.PingIds.ShouldContain("c");
            map.LeavingIds.ShouldBe(["a"]);
            map.LabelText.ShouldBe("Tokyo");
            map.Advance(LiveVisitorsMap.ExitDuration + 0.05);
            map.PingIds.ShouldBe(["b", "c"]);
            map.LabelText.ShouldBe("Tokyo");
            map.Advance(LiveVisitorsMap.LabelDuration);
            map.LabelText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pushed_Arrivals_And_Conversions_Join_The_Data()
    {
        var (card, window) = Open(UseData);
        try
        {
            card.PushVisitor(48.86, 2.35);
            Pump();
            card.Map!.PingIds.Count.ShouldBe(3);
            card.PushEvent(new LiveVisitorsEvent("p1", "Signed up", "Paris", "FR", s_now) { Latitude = 48.86, Longitude = 2.35 });
            Pump();
            card.EventList!.Keys[0].ShouldBe("p1");
            card.Map.PingIds.ShouldContain("p1");
            card.Map.LabelText.ShouldBe("Paris");

            card.Advance(LiveVisitors.PushedVisitorLifetime + 0.5);
            Pump();
            card.Map.PingIds.ShouldBe(["a", "b"]);
            card.EventList.Keys[0].ShouldBe("p1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ticker_Times_Age_Once_A_Second()
    {
        var now = s_now;
        var (card, window) = Open(c =>
        {
            UseData(c);
            c.Now = () => now;
        });
        try
        {
            var row = (LiveVisitorsEventRow)card.EventList!.Rows[0];
            row.TimeText.ShouldBe("12s");
            now = now.AddSeconds(50);
            card.Advance(1.1);
            row.TimeText.ShouldBe("1m");
            AutomationProperties.GetName(row).ShouldBe("Signed up, Berlin, Germany, 1 minute ago");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lists_Stack_Below_520()
    {
        var (card, window) = Open(UseData, width: 480);
        try
        {
            card.Classes.Contains(":narrow").ShouldBeTrue();
            card.Padding.ShouldBe(new Thickness(16));
            TestHelpers.Descendant<LiveVisitorsColumns>(card)!.IsStacked.ShouldBeTrue();
            card.Bounds.Width.ShouldBe(480);

            window.Width = 900;
            Pump();
            card.Bounds.Width.ShouldBe(640);
            card.Padding.ShouldBe(new Thickness(20));
            TestHelpers.Descendant<LiveVisitorsColumns>(card)!.IsStacked.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (card, window) = Open(c =>
        {
            UseData(c);
            c.Labels = new LiveVisitorsLabels
            {
                Title = "Im Moment",
                Live = "Live dabei",
                People = n => "Personen in der App",
                Pause = "Anhalten",
            };
        });
        try
        {
            AutomationProperties.GetName(card).ShouldBe("Im Moment");
            card.StatusText.ShouldBe("Live dabei");
            card.CountLabel!.Text.ShouldBe("Personen in der App");
            AutomationProperties.GetName(card.PauseButton!).ShouldBe("Anhalten");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Default_Labels_Format_Times_And_Countries()
    {
        var labels = LiveVisitorsLabels.Default;
        labels.Ago(TimeSpan.FromSeconds(2)).ShouldBe("now");
        labels.Ago(TimeSpan.FromSeconds(12)).ShouldBe("12s");
        labels.Ago(TimeSpan.FromMinutes(3.5)).ShouldBe("3m");
        labels.Ago(TimeSpan.FromHours(2)).ShouldBe("2h");
        labels.Ago(TimeSpan.FromDays(4)).ShouldBe("4d");
        labels.AgoSpoken(TimeSpan.FromSeconds(1)).ShouldBe("just now");
        labels.AgoSpoken(TimeSpan.FromSeconds(12)).ShouldBe("12 seconds ago");
        labels.AgoSpoken(TimeSpan.FromMinutes(1)).ShouldBe("1 minute ago");
        labels.CountryName("de").ShouldBe("Germany");
        labels.CountryName("GB").ShouldBe("United Kingdom");
        labels.CountryName("XX").ShouldBe("XX");
        labels.People(1).ShouldBe("person on the site now");
        labels.People(2).ShouldBe("people on the site now");
        labels.EventSentence("Signed up", "", "Germany", "just now").ShouldBe("Signed up, Germany, just now");
    }

    [AvaloniaFact]
    public void Without_Data_Or_A_Simulation_The_Card_Holds_Placeholders_And_Says_No_Data()
    {
        var (card, window) = Open(c => c.Simulation = null);
        try
        {
            card.IsSimulated.ShouldBeFalse();
            card.IsEmpty.ShouldBeTrue();
            card.Classes.Contains(":nodata").ShouldBeTrue();
            card.Classes.Contains(":simulated").ShouldBeFalse();
            card.StatusText.ShouldBe("No data");
            card.StatusDot!.IsPulsing.ShouldBeFalse();
            card.IsClockActive.ShouldBeFalse();
            card.PageList!.IsPlaceholder.ShouldBeTrue();
            card.EventList!.IsPlaceholder.ShouldBeTrue();
            card.Map!.PingIds.ShouldBeEmpty();
            AutomationProperties.GetName(card.CountBox!).ShouldBe("No data");
            card.Announcer!.History.ShouldNotContain("Showing a simulated stream of visitors");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Data_Wins_Over_The_Simulation_And_Clearing_The_Simulation_Stops_It()
    {
        var (card, window) = Open();
        try
        {
            card.IsSimulated.ShouldBeTrue();
            card.Advance(2);
            var played = card.Simulation!.Time;
            played.ShouldBeGreaterThan(0);

            // Clearing the simulation stops it and empties the card.
            card.Simulation = null;
            Pump();
            card.IsEmpty.ShouldBeTrue();
            card.IsClockActive.ShouldBeFalse();
            card.StatusText.ShouldBe("No data");
            card.PageList!.IsPlaceholder.ShouldBeTrue();

            // The app's own data wins over a simulation set again.
            var simulation = Sample();
            card.Simulation = simulation;
            UseData(card);
            Pump();
            card.IsSimulated.ShouldBeFalse();
            card.DisplayedCount.ShouldBe(70);
            card.PageList.Keys.ShouldBe(["/pricing", "/", "/docs", "/changelog"]);
            card.Advance(3);
            simulation.Time.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Simulation_Plays_The_Same_Traffic_From_A_Seed()
    {
        var a = Sample(3);
        var b = Sample(3);
        a.Count.ShouldBe(128);
        a.Events.Count.ShouldBe(4);
        a.Pages.Sum(p => p.Visitors).ShouldBe(a.Count);
        var changes = 0;
        a.Changed += (_, _) => changes++;
        for (var i = 0; i < 60; i++)
        {
            a.Advance(TimeSpan.FromSeconds(0.1));
            b.Advance(TimeSpan.FromSeconds(0.1));
        }
        changes.ShouldBeGreaterThan(0);
        a.Count.ShouldBe(b.Count);
        a.Events.Select(e => e.Id).ShouldBe(b.Events.Select(e => e.Id));
        // Two conversions in six seconds (one every 2.4s), about ten arrivals on the map, pages adding up to the count.
        a.Events.Count.ShouldBe(6);
        a.Visitors.Count(v => v.Kind == LiveVisitorsPingKind.Visit).ShouldBeInRange(8, 12);
        a.Pages.Sum(p => p.Visitors).ShouldBe(a.Count);
        a.Count.ShouldBeInRange(100, 160);
        a.Advance(TimeSpan.Zero).ShouldBeFalse();

        // With no data the stream is empty and stays so.
        var empty = new LiveVisitorsSimulation();
        empty.Count.ShouldBe(0);
        empty.Pages.ShouldBeEmpty();
        empty.Events.ShouldBeEmpty();
        empty.Visitors.ShouldBeEmpty();
        empty.Advance(TimeSpan.FromSeconds(5)).ShouldBeFalse();
    }

    [Fact]
    public void Land_Is_A_Coarse_Dot_Grid_With_A_Projection()
    {
        LiveVisitorsLand.Columns.ShouldBe(90);
        LiveVisitorsLand.RowCount.ShouldBe(34);
        LiveVisitorsLand.DotCount.ShouldBeInRange(900, 1200);
        LiveVisitorsLand.IsLand(52.5, 13.4).ShouldBeTrue();
        LiveVisitorsLand.IsLand(-23.5, -46.6).ShouldBeTrue();
        LiveVisitorsLand.IsLand(30, -40).ShouldBeFalse();
        var p = LiveVisitorsLand.Project(80, -180);
        p.X.ShouldBe(0, 0.001);
        p.Y.ShouldBe(0, 0.001);
        LiveVisitorsLand.Project(-90, 180).Y.ShouldBe(34, 0.001);
    }

    [AvaloniaFact]
    public void The_count_rolls_each_digit_in_280ms_on_ease_out()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window) = Open(c => c.Count = 70, ArcMotionLevel.Full);
        try
        {
            var digits = card.Digits!;
            digits.WheelMotion.ShouldBe(MotionSpec.Tween(TimeSpan.FromMilliseconds(280), AiHeightBox.EaseOut));
            card.Count = 72;
            Pump();
            digits.WheelTargets[^1].ShouldBe(2);
            // Frame by frame the ones wheel is where CSS ease-out puts it; after 280ms (16.8 frames) it has landed.
            for (var n = 1; n <= 16; n++)
            {
                clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
                digits.WheelPositions[^1].ShouldBe(2 * AiHeightBox.EaseOut.Ease(n / 60.0 / 0.28), 1e-3, $"frame {n}");
            }
            clock.Advance(TimeSpan.FromSeconds(1.0 / 60.0));
            digits.WheelPositions.ShouldBe(digits.WheelTargets);
        }
        finally
        {
            window.Close();
        }
    }
}
