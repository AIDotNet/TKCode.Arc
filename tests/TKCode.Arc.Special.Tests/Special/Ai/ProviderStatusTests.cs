using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ProviderStatus: the sample story it opens on, simulated checks on the countdown (and the failing one), the app's own
/// providers and refresh handler, incident rows opening their updates, the uptime bar's keyboard model and shared day
/// tooltip, the narrow layout, loading, labels, events and the pure rules (window, health, formats, ordering).
/// </summary>
public class ProviderStatusTests
{
    /// <summary>The test's own invented story (made-up providers, models, numbers and incidents), as simulation steps.</summary>
    private static class Sample
    {
        /// <summary>How many checks the story takes before it starts over.</summary>
        public const int StoryLength = 11;

        /// <summary>The step of the story whose check fails.</summary>
        public const int FailingStep = 9;

        /// <summary>How often the sample checks again.</summary>
        public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(8);

        /// <summary>The six sample providers after <paramref name="check"/> simulated checks, with times relative to now.</summary>
        public static IReadOnlyList<ProviderStatusItem> Providers(int check) => Providers(check, DateTimeOffset.UtcNow);

        /// <summary>The six sample providers after <paramref name="check"/> simulated checks, with times relative to <paramref name="now"/>.</summary>
        public static IReadOnlyList<ProviderStatusItem> Providers(int check, DateTimeOffset now)
        {
            var step = Step(check);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            // Incident times are whole minutes before now, so they read like a status page.
            var minute = new DateTimeOffset(now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day, now.UtcDateTime.Hour,
                now.UtcDateTime.Minute, 0, TimeSpan.Zero);

            var northwind = new ProviderStatusItem
            {
                Id = "northwind",
                Name = "Northwind AI",
                Mark = ArcIcons.Zap,
                Detail = "Atlas 3 Pro · Atlas 3",
                Health = step switch
                {
                    <= 1 => ProviderStatusHealth.Outage,
                    <= 3 => ProviderStatusHealth.Degraded,
                    _ => ProviderStatusHealth.Operational
                },
                LatencyMs = Latency(412, check, 0, step <= 1 ? 4.6 : step <= 3 ? 1.6 : 1),
                Days = History(0, today, step <= 5 ? (98.62, "Elevated error rates on Atlas 3 Pro since this morning.") : (98.9, "Elevated error rates for 38 minutes."),
                    [(6, 99.82, "Slower responses in one region."), (17, 98.4, "API errors for 52 minutes.")]),
                Incident = step > 5 ? null : Incident("nw-errors", "Elevated error rates on Atlas 3 Pro", minute.AddMinutes(-14),
                [
                    (ProviderStatusStage.Investigating, 0, "We are seeing elevated error rates on Atlas 3 Pro and are looking into it."),
                    (ProviderStatusStage.Identified, 6, "A bad configuration change on our inference gateway is the cause. We are rolling it back."),
                    (ProviderStatusStage.Monitoring, 11, "The rollback is complete and error rates are back to normal. We are watching closely."),
                    (ProviderStatusStage.Resolved, 14, "This incident is resolved. Requests made during it can be retried safely.")
                ], step switch
                {
                    0 => 1,
                    1 => 2,
                    <= 3 => 3,
                    _ => 4
                })
            };

            var contoso = new ProviderStatusItem
            {
                Id = "contoso",
                Name = "Contoso Labs",
                Mark = ArcIcons.Sparkles,
                Detail = "Atlas Vision",
                Health = step <= 1 ? ProviderStatusHealth.Degraded : ProviderStatusHealth.Operational,
                LatencyMs = Latency(620, check, 1, step <= 1 ? 3.1 : step == 2 ? 1.3 : 1),
                Days = History(1, today, (99.74, "Slower responses on Atlas Vision for about 40 minutes."),
                    [(11, 99.86, "Image uploads slower than usual."), (23, 99.5, "Some requests timed out.")]),
                Incident = step > 3 ? null : Incident("cl-slow", "Slower responses on Atlas Vision", minute.AddMinutes(-9),
                [
                    (ProviderStatusStage.Investigating, 0, "Atlas Vision is answering more slowly than usual. We are investigating."),
                    (ProviderStatusStage.Identified, 4, "A capacity shortfall in one region is the cause. More capacity is coming online."),
                    (ProviderStatusStage.Monitoring, 7, "Response times are recovering. We are monitoring."),
                    (ProviderStatusStage.Resolved, 9, "Response times have been normal for 15 minutes. This incident is resolved.")
                ], step + 1)
            };

            var fabrikamOpen = step is >= 6 and <= 8;
            var fabrikam = new ProviderStatusItem
            {
                Id = "fabrikam",
                Name = "Fabrikam",
                Mark = ArcIcons.Layers3,
                Detail = "Atlas Mini",
                Health = step == 6 ? ProviderStatusHealth.Degraded : ProviderStatusHealth.Operational,
                LatencyMs = Latency(280, check, 2, step == 6 ? 2.2 : 1),
                Days = History(2, today, step >= 6 ? (99.88, "A short rise in errors on Atlas Mini.") : (100, null),
                    [(3, 99.93, null), (20, 98.7, "Streaming responses stopped early for an hour.")]),
                Incident = !fabrikamOpen ? null : Incident("fb-errors", "Errors on streaming responses", minute.AddMinutes(-5),
                [
                    (ProviderStatusStage.Investigating, 0, "Some streaming responses from Atlas Mini end early. We are investigating."),
                    (ProviderStatusStage.Monitoring, 3, "A fix is deployed and streams complete again. We are monitoring."),
                    (ProviderStatusStage.Resolved, 5, "This incident is resolved.")
                ], step - 5)
            };

            var northwindEu = new ProviderStatusItem
            {
                Id = "northwind-eu",
                Name = "Northwind AI EU",
                Mark = ArcIcons.Globe,
                Detail = "Atlas 3 · EU data residency",
                Health = ProviderStatusHealth.Operational,
                LatencyMs = Latency(455, check, 3, 1),
                Days = History(3, today, (100, null), [(9, 99.81, "Elevated latency after a network change.")])
            };

            var batch = new ProviderStatusItem
            {
                Id = "contoso-batch",
                Name = "Contoso Labs Batch",
                Mark = ArcIcons.Server,
                Detail = "Atlas 3 Pro · batch jobs",
                Health = ProviderStatusHealth.Operational,
                LatencyMs = Latency(1340, check, 4, 1),
                // Monitoring started 24 days ago: the bar pads the older days as No data.
                Days = History(4, today, (100, null), [(14, 99.4, "Jobs queued longer than usual.")], startDay: 6)
            };

            var edge = new ProviderStatusItem
            {
                Id = "fabrikam-edge",
                Name = "Fabrikam Edge",
                Detail = "Atlas Mini · regional fallback",
                Health = ProviderStatusHealth.Operational,
                LatencyMs = Latency(190, check, 5, 1),
                Days = History(5, today, (100, null), [(19, null, "Checks were paused for maintenance.")])
            };

            return [northwind, contoso, fabrikam, northwindEu, batch, edge];
        }

        /// <summary>Whether the check after <paramref name="check"/> checks fails in the story.</summary>
        public static bool FailsOn(int check) => Step(check) == FailingStep;

        /// <summary>Where <paramref name="check"/> is in the looping story.</summary>
        public static int Step(int check) => ((check % StoryLength) + StoryLength) % StoryLength;

        /// <summary>A plausible latency: the base, a ±8% wobble per check and a factor while slow.</summary>
        private static double Latency(double baseMs, int check, int provider, double factor)
        {
            var wobble = (Noise(provider * 131 + check * 17) - 0.5) * 0.16;
            return Math.Round(baseMs * factor * (1 + wobble));
        }

        /// <summary>A repeatable number in [0, 1).</summary>
        private static double Noise(int seed)
        {
            unchecked
            {
                var x = (uint)seed * 2654435761u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (x & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// <summary>
        /// Thirty days ending today: mostly 100% with a few near misses, the listed <paramref name="events"/> (days counted
        /// from the oldest) and today's value. Days before <paramref name="startDay"/> are left out.
        /// </summary>
        private static List<ProviderStatusDay> History(int provider, DateOnly today, (double? Uptime, string? Note) todayValue,
            (int Day, double? Uptime, string? Note)[] events, int startDay = 0)
        {
            var days = new List<ProviderStatusDay>(ProviderStatusFormat.WindowDays);
            var first = today.AddDays(-(ProviderStatusFormat.WindowDays - 1));
            for (var d = startDay; d < ProviderStatusFormat.WindowDays; d++)
            {
                double? uptime = 100;
                string? note = null;
                var noise = Noise(provider * 977 + d * 31);
                if (noise < 0.22)
                {
                    uptime = Math.Round(99.94 + noise * 0.25, 2);
                }
                foreach (var e in events)
                {
                    if (e.Day == d)
                    {
                        uptime = e.Uptime;
                        note = e.Note;
                    }
                }
                if (d == ProviderStatusFormat.WindowDays - 1)
                {
                    uptime = todayValue.Uptime;
                    note = todayValue.Note;
                }
                days.Add(new ProviderStatusDay { Date = first.AddDays(d), Uptime = uptime, Note = note });
            }
            return days;
        }

        private static ProviderStatusIncident Incident(string id, string title, DateTimeOffset start,
            (ProviderStatusStage Stage, int Minutes, string Message)[] script, int posted)
        {
            var updates = new List<ProviderStatusUpdate>();
            for (var i = 0; i < Math.Clamp(posted, 0, script.Length); i++)
            {
                updates.Add(new ProviderStatusUpdate { Stage = script[i].Stage, At = start.AddMinutes(script[i].Minutes), Message = script[i].Message });
            }
            return new ProviderStatusIncident { Id = id, Title = title, Updates = updates };
        }

        /// <summary>The story as simulation steps: step 0 first, the failing step fails.</summary>
        public static IReadOnlyList<ProviderStatusSimulationStep> Story(DateTimeOffset now) =>
            Enumerable.Range(0, StoryLength)
                .Select(step => step == FailingStep
                    ? new ProviderStatusSimulationStep { Fails = true }
                    : new ProviderStatusSimulationStep { Providers = Providers(step, now) })
                .ToList();
    }

    /// <summary>Opens a card that plays <see cref="Sample"/> with instant checks, unless <paramref name="empty"/>.</summary>
    private static (ProviderStatus Card, Window Window) Open(Action<ProviderStatus>? setup = null, double width = 760,
        ArcMotionLevel level = ArcMotionLevel.Off, bool empty = false)
    {
        var card = new ProviderStatus { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (!empty)
        {
            card.Simulation = new ProviderStatusSimulation { Steps = Sample.Story(DateTimeOffset.UtcNow), CheckDelay = TimeSpan.Zero };
        }
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

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static void RunCheck(ProviderStatus card)
    {
        card.AdvanceCountdown(card.SecondsLeft + 0.01);
        Pump();
    }

    private static ProviderStatusItem Provider(string id, ProviderStatusHealth health, ProviderStatusIncident? incident = null,
        double? latency = 300) => new()
    {
        Id = id,
        Name = id,
        Health = health,
        LatencyMs = latency,
        Detail = "Atlas 3",
        Incident = incident,
        Days = Enumerable.Range(0, 30)
            .Select(i => new ProviderStatusDay { Date = new DateOnly(2026, 10, 1).AddDays(i), Uptime = 100 })
            .ToList()
    };

    [AvaloniaFact]
    public void Opens_On_The_Sample_Story()
    {
        var (card, window) = Open();
        try
        {
            card.UsesSample.ShouldBeTrue();
            card.RowList!.Children.Count.ShouldBe(6);
            var northwind = card.RowOf("northwind")!;
            northwind.HealthText.ShouldBe("Outage");
            northwind.DetailText.ShouldBe("Elevated error rates on Atlas 3 Pro");
            northwind.Toggle!.IsVisible.ShouldBeTrue();
            card.RowOf("contoso")!.HealthText.ShouldBe("Degraded");
            card.RowOf("northwind-eu")!.HealthText.ShouldBe("Operational");
            card.RowOf("northwind-eu")!.Toggle!.IsVisible.ShouldBeFalse();
            card.SummaryText.ShouldBe("1 outage · 1 degraded · 4 operational");
            card.RefreshButton!.IsVisible.ShouldBeTrue();
            card.CountdownLength.ShouldBe(8);
            card.CountdownText.ShouldBe("Next check in 8s");
            card.FootnotePresenter!.Content.ShouldBeOfType<TextBlock>().Text.ShouldBe(ProviderStatusLabels.Default.SampleFootnote);
            // The batch provider started 24 days ago: six padded days of no data.
            card.RowOf("contoso-batch")!.Window.Take(6).ShouldAllBe(c => c.IsEmpty);
            card.RowOf("fabrikam-edge")!.Monogram().ShouldBe("F");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Drains_And_Checks_Again()
    {
        var (card, window) = Open();
        try
        {
            card.IsCountingDown.ShouldBeTrue();
            card.AdvanceCountdown(2.5);
            card.CountdownText.ShouldBe("Next check in 6s");
            card.Ring!.Value.ShouldBe(card.SecondsLeft / 8, 1e-6);
            card.Ring.Value.ShouldBeLessThan(5.51 / 8);
            card.AdvanceCountdown(5.6);
            Pump();
            card.Simulation!.Check.ShouldBe(1);
            card.IsChecking.ShouldBeFalse();
            card.SecondsLeft.ShouldBe(8, 0.5);
            card.Announcer!.Announcement.ShouldStartWith("Checked.");
            // Step 1: Northwind's incident is identified.
            card.RowOf("northwind")!.Item!.Incident!.Updates.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Check_Keeps_The_Results_And_Retries()
    {
        var (card, window) = Open();
        try
        {
            for (var i = 0; i < Sample.FailingStep - 1; i++)
            {
                RunCheck(card);
            }
            card.Simulation!.Check.ShouldBe(8);
            var before = card.EffectiveProviders;
            RunCheck(card);
            card.Simulation!.Check.ShouldBe(9);
            card.EffectiveProviders.ShouldBeSameAs(before);
            card.DisplayedError.ShouldBe(ProviderStatusLabels.Default.RefreshFailed);
            card.SummaryText.ShouldBe("!" + ProviderStatusLabels.Default.RefreshFailed);
            card.Classes.Contains(":error").ShouldBeTrue();
            card.CountdownText.ShouldBe("Retry in 8s");
            AutomationProperties.GetName(card.RefreshButton!).ShouldBe("Retry");
            card.Announcer!.Announcement.ShouldBe(ProviderStatusLabels.Default.RefreshFailed);
            RunCheck(card);
            card.DisplayedError.ShouldBeNull();
            card.CountdownText.ShouldBe("Next check in 8s");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retries_Wait_At_Most_Ten_Seconds()
    {
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Operational)];
            c.RefreshInterval = TimeSpan.FromSeconds(60);
            c.RefreshHandler = _ => throw new InvalidOperationException("Feeds down");
        });
        try
        {
            card.CountdownLength.ShouldBe(60);
            RunCheck(card);
            card.DisplayedError.ShouldBe("Feeds down");
            card.CountdownLength.ShouldBe(10);
            card.Providers!.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Refresh_Handler_Results_Replace_The_Providers()
    {
        var calls = new List<CancellationToken>();
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Operational), Provider("b", ProviderStatusHealth.Operational)];
            c.RefreshHandler = token =>
            {
                calls.Add(token);
                return Task.FromResult<IReadOnlyList<ProviderStatusItem>?>([Provider("b", ProviderStatusHealth.Degraded, latency: 1920)]);
            };
        });
        try
        {
            card.UsesSample.ShouldBeFalse();
            card.CountdownLength.ShouldBe(30);
            card.FootnotePresenter!.Content.ShouldBeNull();
            var rowB = card.RowOf("b")!;
            card.RefreshButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            calls.Count.ShouldBe(1);
            card.Providers!.Count.ShouldBe(1);
            card.RowList!.Children.Count.ShouldBe(1);
            card.RowOf("b").ShouldBeSameAs(rowB);
            rowB.HealthText.ShouldBe("Degraded");
            rowB.LatencyText.ShouldBe("1.92 s");
            card.SummaryText.ShouldBe("1 degraded");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Newer_Check_Cancels_The_Older_One()
    {
        var pending = new List<TaskCompletionSource<IReadOnlyList<ProviderStatusItem>?>>();
        var tokens = new List<CancellationToken>();
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Operational)];
            c.RefreshHandler = token =>
            {
                tokens.Add(token);
                var source = new TaskCompletionSource<IReadOnlyList<ProviderStatusItem>?>();
                token.Register(() => source.TrySetCanceled(token));
                pending.Add(source);
                return source.Task;
            };
        });
        try
        {
            _ = card.CheckNow();
            card.IsChecking.ShouldBeTrue();
            card.Classes.Contains(":checking").ShouldBeTrue();
            _ = card.CheckNow();
            Pump();
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            card.IsChecking.ShouldBeTrue();
            pending[1].SetResult([Provider("a", ProviderStatusHealth.Outage)]);
            Pump();
            card.IsChecking.ShouldBeFalse();
            card.RowOf("a")!.HealthText.ShouldBe("Outage");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Handler_The_Refresh_Controls_Hide()
    {
        var (card, window) = Open(c => c.Providers = [Provider("a", ProviderStatusHealth.Operational)]);
        try
        {
            card.RefreshButton!.IsVisible.ShouldBeFalse();
            card.IsCountingDown.ShouldBeFalse();
            card.SummaryText.ShouldBe("Operational");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Null_Interval_Keeps_Only_The_Button()
    {
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Operational)];
            c.RefreshInterval = null;
            c.RefreshHandler = _ => Task.FromResult<IReadOnlyList<ProviderStatusItem>?>(null);
        });
        try
        {
            card.RefreshButton!.IsVisible.ShouldBeTrue();
            card.IsCountingDown.ShouldBeFalse();
            TestHelpers.Descendant<Control>(card, ProviderStatus.CountdownPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Incident_Row_Opens_Its_Updates_Newest_First()
    {
        var events = new List<string?>();
        var (card, window) = Open();
        try
        {
            card.ExpandedChanged += (_, e) => events.Add(e.ExpandedId);
            var row = card.RowOf("northwind")!;
            row.Panel!.IsExpanded.ShouldBeFalse();
            AutomationProperties.GetName(row.Toggle!).ShouldBe("Northwind AI, Outage, Elevated error rates on Atlas 3 Pro");
            row.Toggle!.Focus();
            Press(window, PhysicalKey.Enter);
            card.ExpandedId.ShouldBe("northwind");
            events.ShouldBe(["northwind"]);
            row.Toggle.IsExpanded.ShouldBeTrue();
            row.Panel.IsExpanded.ShouldBeTrue();
            row.UpdateViews.Count.ShouldBe(1);
            row.UpdateViews[0].StageText.ShouldBe("Investigating");
            row.UpdateViews[0].TimeText.ShouldEndWith(" UTC");

            // A new update arrives at the top while the panel is open; the earlier view is reused.
            var first = row.UpdateViews[0];
            RunCheck(card);
            row.UpdateViews.Count.ShouldBe(2);
            row.UpdateViews[0].StageText.ShouldBe("Identified");
            row.UpdateViews[1].ShouldBeSameAs(first);

            Press(window, PhysicalKey.Space);
            card.ExpandedId.ShouldBeNull();
            events.ShouldBe(["northwind", null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expanded_Id_Opens_A_Row_From_The_App()
    {
        var (card, window) = Open(c => c.ExpandedId = "contoso");
        try
        {
            card.RowOf("contoso")!.Panel!.IsExpanded.ShouldBeTrue();
            card.RowOf("contoso")!.UpdateViews.Count.ShouldBe(1);
            // A provider without an incident never opens.
            card.ExpandedId = "northwind-eu";
            Pump();
            card.RowOf("northwind-eu")!.Panel!.IsExpanded.ShouldBeFalse();
            card.RowOf("contoso")!.Panel!.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_Page_Link_Raises_IncidentOpened()
    {
        var incident = new ProviderStatusIncident
        {
            Id = "i1",
            Title = "Errors",
            Updates = [new ProviderStatusUpdate { Stage = ProviderStatusStage.Investigating, AtText = "09:12", Message = "Looking." }]
        };
        ProviderStatusIncidentEventArgs? opened = null;
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Outage, incident)];
            c.ExpandedId = "a";
        });
        try
        {
            var row = card.RowOf("a")!;
            row.Link!.IsVisible.ShouldBeFalse();
            card.IncidentOpened += (_, e) =>
            {
                opened = e;
                e.Handled = true;
            };
            row.Link.IsVisible.ShouldBeTrue();
            row.UpdateViews[0].TimeText.ShouldBe("09:12");
            row.Link.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            opened.ShouldNotBeNull();
            opened!.Provider.Id.ShouldBe("a");
            opened.Incident.ShouldBeSameAs(incident);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sample_Link_Is_Simulated()
    {
        var (card, window) = Open(c => c.ExpandedId = "northwind");
        try
        {
            var row = card.RowOf("northwind")!;
            row.Link!.IsVisible.ShouldBeTrue();
            row.Link.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            card.Announcer!.Announcement.ShouldBe("Simulated, no requests were sent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Uptime_Bar_Keyboard_Moves_The_Day_Tooltip()
    {
        var (card, window) = Open();
        try
        {
            var bar = card.RowOf("northwind")!.Bar!;
            AutomationProperties.GetName(bar).ShouldStartWith("Northwind AI, last 30 days: ");
            bar.Focus(NavigationMethod.Tab);
            Pump();
            bar.ActiveIndex.ShouldBe(29);
            card.TipPopup!.IsOpen.ShouldBeTrue();
            card.Tip!.DateText.ShouldBe("Today");
            card.Tip.HealthText.ShouldBe("Outage");
            card.Tip.UptimeText.ShouldBe("98.62% uptime");
            card.Announcer!.Announcement.ShouldStartWith("Today: Outage, 98.62% uptime.");

            Press(window, PhysicalKey.ArrowLeft);
            bar.ActiveIndex.ShouldBe(28);
            card.Tip.DateText.ShouldNotBe("Today");
            Press(window, PhysicalKey.Home);
            bar.ActiveIndex.ShouldBe(0);
            Press(window, PhysicalKey.ArrowLeft);
            bar.ActiveIndex.ShouldBe(0);
            Press(window, PhysicalKey.End);
            bar.ActiveIndex.ShouldBe(29);
            Press(window, PhysicalKey.Escape);
            bar.ActiveIndex.ShouldBe(-1);
            card.TipPopup.IsOpen.ShouldBeFalse();
            Press(window, PhysicalKey.ArrowRight);
            bar.ActiveIndex.ShouldBe(29);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void One_Tooltip_Serves_Every_Bar()
    {
        var (card, window) = Open();
        try
        {
            var first = card.RowOf("northwind")!.Bar!;
            var second = card.RowOf("contoso")!.Bar!;
            first.SetActive(10, fromKeyboard: false);
            var popup = card.TipPopup!;
            popup.IsOpen.ShouldBeTrue();
            popup.PlacementTarget.ShouldBeSameAs(first);
            popup.PlacementRect.ShouldBe(first.TickRect(10));
            second.SetActive(3, fromKeyboard: false);
            card.TipPopup.ShouldBeSameAs(popup);
            popup.PlacementTarget.ShouldBeSameAs(second);
            first.ActiveIndex.ShouldBe(-1);
            second.SetActive(-1, fromKeyboard: false);
            popup.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bar_Maps_Pointer_Positions_To_Days()
    {
        var (card, window) = Open();
        try
        {
            var bar = card.RowOf("fabrikam")!.Bar!;
            var width = bar.Bounds.Width;
            width.ShouldBeGreaterThan(100);
            bar.IndexAt(0).ShouldBe(0);
            bar.IndexAt(width - 0.5).ShouldBe(29);
            bar.IndexAt(width / 2 + 0.1).ShouldBe(15);
            bar.TickRect(29).Right.ShouldBe(width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Take_Two_Lines()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.IsNarrow.ShouldBeTrue();
            card.Classes.Contains(":narrow").ShouldBeTrue();
            var row = card.RowOf("northwind")!;
            row.Head!.IsNarrow.ShouldBeTrue();
            row.LatencyText.ShouldStartWith("p50 ");
            TestHelpers.Descendant<ProviderStatusRowPanel>(card, ProviderStatus.ColumnsPart).IsVisible.ShouldBeFalse();
            var cells = row.Head.Cells;
            // The bar sits on the second line under the name and spans from the left edge.
            cells[4].Y.ShouldBeGreaterThan(cells[1].Bottom - 0.01);
            cells[4].X.ShouldBe(0);
            cells[3].Y.ShouldBeGreaterThanOrEqualTo(cells[1].Bottom - 0.01);
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(400);
            card.CountdownText.ShouldBe("8s");

            window.Width = 760;
            Pump();
            card.IsNarrow.ShouldBeFalse();
            row.Head.IsNarrow.ShouldBeFalse();
            row.Head.Cells[4].Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wide_Columns_Line_Up_Across_Rows()
    {
        var (card, window) = Open();
        try
        {
            var a = card.RowOf("northwind")!.Head!.Cells;
            var b = card.RowOf("contoso-batch")!.Head!.Cells;
            var header = TestHelpers.Descendant<ProviderStatusRowPanel>(card, ProviderStatus.ColumnsPart).Cells;
            for (var i = 0; i < 7; i++)
            {
                a[i].X.ShouldBe(b[i].X, 0.01);
                a[i].Width.ShouldBe(b[i].Width, 0.01);
                header[i].X.ShouldBe(a[i].X, 0.01);
            }
            card.RowOf("northwind")!.Bounds.Height.ShouldBe(card.RowOf("northwind-eu")!.Bounds.Height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Skeleton_Rows()
    {
        var (card, window) = Open(c =>
        {
            c.IsLoading = true;
            c.Providers = [];
            c.RefreshHandler = _ => Task.FromResult<IReadOnlyList<ProviderStatusItem>?>(null);
        });
        try
        {
            card.Skeleton!.IsVisible.ShouldBeTrue();
            card.Skeleton.Children.Count.ShouldBe(4);
            card.RowList!.IsVisible.ShouldBeFalse();
            card.SummaryText.ShouldBe("Loading providers");
            card.Skeleton.IsPulsing.ShouldBeFalse();
            card.IsLoading = false;
            Pump();
            card.Skeleton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Text_Shows_In_The_Header()
    {
        var (card, window) = Open(c =>
        {
            c.Providers = [Provider("a", ProviderStatusHealth.Operational)];
            c.ErrorText = "Couldn’t load providers.";
        });
        try
        {
            card.SummaryText.ShouldBe("!Couldn’t load providers.");
            card.Classes.Contains(":error").ShouldBeTrue();
            card.ErrorText = null;
            Pump();
            card.SummaryText.ShouldBe("Operational");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (card, window) = Open(c => c.Labels = new ProviderStatusLabels
        {
            Outage = "Panne",
            Degraded = "Dégradé",
            Operational = "Opérationnel",
            Summary = (o, d, x) => $"{x} panne, {d} dégradé, {o} ok",
            NextCheck = s => $"Prochaine vérification dans {s} s",
            Latency = ms => $"{ms:0} millisecondes"
        });
        try
        {
            card.RowOf("northwind")!.HealthText.ShouldBe("Panne");
            card.SummaryText.ShouldBe("1 panne, 1 dégradé, 4 ok");
            card.CountdownText.ShouldBe("Prochaine vérification dans 8 s");
            card.RowOf("northwind-eu")!.LatencyText.ShouldEndWith(" millisecondes");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Notice_Is_Announced()
    {
        var (card, window) = Open();
        try
        {
            card.Notice = "Routing switched to Contoso Labs.";
            card.Announcer!.Announcement.ShouldBe("Routing switched to Contoso Labs.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ticks_Recolor_Over_240ms()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var bar = card.RowOf("fabrikam")!.Bar!;
            var cells = bar.Cells.ToArray();
            var before = bar.ColorAt(29);
            cells[29] = cells[29] with { Uptime = 98, Health = ProviderStatusHealth.Outage };
            bar.SetDays(cells);
            bar.IsRecoloring.ShouldBeTrue();
            bar.ColorAt(29).ShouldBe(before);
            bar.Advance(0.12);
            bar.ColorAt(29).ShouldNotBe(before);
            bar.Advance(0.13);
            bar.ColorAt(29).ShouldBe(bar.RestColor(ProviderStatusHealth.Outage));
            bar.IsRecoloring.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Incident_Panel_Springs_With_Full_Motion()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var row = card.RowOf("northwind")!;
            card.ExpandedId = "northwind";
            Pump();
            var panel = row.Panel!;
            panel.IsMounted.ShouldBeTrue();
            panel.NaturalHeight.ShouldBeGreaterThan(0);
            panel.TargetHeight.ShouldBe(panel.NaturalHeight, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Window_Pads_And_Derives_Health()
    {
        var today = new DateOnly(2026, 10, 29);
        var days = new List<ProviderStatusDay>
        {
            new() { Date = today.AddDays(-3), Uptime = 99.95 },
            new() { Date = today.AddDays(-2), Uptime = 99.5 },
            new() { Date = today.AddDays(-1), Uptime = 98.9, Note = "Errors" },
            new() { Date = today, Uptime = null, Health = ProviderStatusHealth.Degraded }
        };
        var window = ProviderStatusFormat.Window(days, today);
        window.Length.ShouldBe(30);
        window[29].Date.ShouldBe(today);
        window.Take(26).ShouldAllBe(c => c.IsEmpty);
        window[26].Health.ShouldBe(ProviderStatusHealth.Operational);
        window[27].Health.ShouldBe(ProviderStatusHealth.Degraded);
        window[28].Health.ShouldBe(ProviderStatusHealth.Outage);
        window[28].Note.ShouldBe("Errors");
        window[29].Health.ShouldBe(ProviderStatusHealth.Degraded);
        ProviderStatusFormat.Average(window)!.Value.ShouldBe((99.95 + 99.5 + 98.9) / 3, 1e-9);
        ProviderStatusFormat.Counts(window).ShouldBe((2, 1));
        ProviderStatusFormat.Average(ProviderStatusFormat.Window(null, today)).ShouldBeNull();
    }

    [Fact]
    public void Formats_Numbers()
    {
        var labels = ProviderStatusLabels.Default;
        labels.Latency(412.4).ShouldBe("412 ms");
        labels.Latency(1920).ShouldBe("1.92 s");
        labels.Percent(99.614).ShouldBe("99.61%");
        labels.Percent(100).ShouldBe("100%");
        labels.Percent(99.999).ShouldBe("99.99%");
        ProviderStatusFormat.Latency(null, labels).ShouldBe("–");
        labels.Summary(6, 0, 0).ShouldBe("All systems operational");
        labels.Summary(3, 1, 2).ShouldBe("2 outages · 1 degraded · 3 operational");
        labels.Time(new DateTimeOffset(2026, 10, 29, 14, 32, 10, TimeSpan.FromHours(2))).ShouldBe("12:32 UTC");
        ProviderStatusFormat.Monogram(" fabrikam").ShouldBe("F");
    }

    [Fact]
    public void Updates_Sort_Newest_First()
    {
        var at = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
        var incident = new ProviderStatusIncident
        {
            Updates =
            [
                new ProviderStatusUpdate { Stage = ProviderStatusStage.Identified, At = at.AddMinutes(5), Message = "b" },
                new ProviderStatusUpdate { Stage = ProviderStatusStage.Investigating, At = at, Message = "a" },
                new ProviderStatusUpdate { Stage = ProviderStatusStage.Resolved, At = at.AddMinutes(30), Message = "c" }
            ]
        };
        ProviderStatusFormat.Newest(incident).Select(u => u.Message).ShouldBe(["c", "b", "a"]);
        ProviderStatusFormat.IsResolved(incident).ShouldBeTrue();
        var untimed = new ProviderStatusIncident
        {
            Updates =
            [
                new ProviderStatusUpdate { Stage = ProviderStatusStage.Investigating, Message = "a" },
                new ProviderStatusUpdate { Stage = ProviderStatusStage.Monitoring, Message = "b" }
            ]
        };
        ProviderStatusFormat.Newest(untimed).Select(u => u.Message).ShouldBe(["b", "a"]);
        ProviderStatusFormat.IsResolved(untimed).ShouldBeFalse();
    }

    [Fact]
    public void Sample_Story_Loops_Every_Eleven_Checks()
    {
        var now = new DateTimeOffset(2026, 10, 29, 14, 30, 0, TimeSpan.Zero);
        var start = Sample.Providers(0, now);
        start.Count.ShouldBe(6);
        start[0].Health.ShouldBe(ProviderStatusHealth.Outage);
        start[0].Incident!.Updates.Single().Stage.ShouldBe(ProviderStatusStage.Investigating);
        start.All(p => p.Days.Count > 0).ShouldBeTrue();
        var resolved = Sample.Providers(4, now);
        resolved[0].Health.ShouldBe(ProviderStatusHealth.Operational);
        ProviderStatusFormat.IsResolved(resolved[0].Incident!).ShouldBeTrue();
        Sample.Providers(6, now)[0].Incident.ShouldBeNull();
        Sample.Providers(6, now)[2].Incident.ShouldNotBeNull();
        Sample.FailsOn(9).ShouldBeTrue();
        Sample.FailsOn(20).ShouldBeTrue();
        Sample.FailsOn(8).ShouldBeFalse();
        Sample.Providers(11, now)[0].Health.ShouldBe(ProviderStatusHealth.Outage);
        Sample.Providers(3, now)[0].LatencyMs.ShouldBe(Sample.Providers(3, now)[0].LatencyMs);
    }

    [Fact]
    public void A_Simulation_Plays_Its_Steps_In_A_Loop_And_Fails_On_A_Failing_Step()
    {
        var a = new[] { Provider("a", ProviderStatusHealth.Operational) };
        var b = new[] { Provider("b", ProviderStatusHealth.Degraded) };
        var simulation = new ProviderStatusSimulation
        {
            CheckDelay = TimeSpan.Zero,
            Steps =
            [
                new ProviderStatusSimulationStep { Providers = a },
                new ProviderStatusSimulationStep { Providers = b },
                new ProviderStatusSimulationStep { Fails = true },
            ],
        };
        simulation.Current.ShouldBeSameAs(a);
        simulation.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().ShouldBeSameAs(b);
        Should.Throw<Exception>(() => simulation.CheckAsync(CancellationToken.None).GetAwaiter().GetResult());
        simulation.Current.ShouldBeSameAs(b);
        simulation.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().ShouldBeSameAs(a);
        simulation.Check.ShouldBe(3);
        simulation.Restart();
        simulation.Check.ShouldBe(0);
        // Without steps a check returns nothing and never fails.
        new ProviderStatusSimulation { CheckDelay = TimeSpan.Zero }.CheckAsync(CancellationToken.None).GetAwaiter().GetResult().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Without_Providers_A_Handler_Or_A_Simulation_The_Card_Is_Empty()
    {
        var (card, window) = Open(empty: true);
        try
        {
            card.UsesSample.ShouldBeFalse();
            card.RowList!.Children.Count.ShouldBe(0);
            card.SummaryText.ShouldBe("No providers");
            card.RefreshButton!.IsVisible.ShouldBeFalse();
            card.IsCountingDown.ShouldBeFalse();
            card.FootnotePresenter!.Content.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Providers_Win_Over_The_Simulation_And_Clearing_It_Stops_Playback()
    {
        var (card, window) = Open();
        try
        {
            card.UsesSample.ShouldBeTrue();
            card.RowList!.Children.Count.ShouldBe(6);
            card.Providers = [Provider("own", ProviderStatusHealth.Operational)];
            Pump();
            card.UsesSample.ShouldBeFalse();
            card.RowList.Children.Count.ShouldBe(1);
            card.RowOf("own").ShouldNotBeNull();
            card.FootnotePresenter!.Content.ShouldBeNull();
            // Back to the simulation, then without one: no rows, no countdown.
            card.Providers = null;
            Pump();
            card.RowList.Children.Count.ShouldBe(6);
            card.Simulation = null;
            Pump();
            card.RowList.Children.Count.ShouldBe(0);
            card.SummaryText.ShouldBe("No providers");
            card.IsCountingDown.ShouldBeFalse();
            card.RefreshButton!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class ProviderStatusRowTestExtensions
{
    /// <summary>The monogram a row without a mark shows.</summary>
    public static string? Monogram(this ProviderStatusRow row) =>
        TestHelpers.Descendant<TextBlock>(row, ProviderStatusRow.MonogramTextPart) is { } text && text.IsEffectivelyVisible ? text.Text : null;
}
