using System.Collections.ObjectModel;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// The story of <see cref="PromptCache"/>: one write then hits, a partial hit from the change toggle, the idle drain, the
/// expiry and the loop, stepping (one request per step, pauses skipped, a new round after a finished one), pausing,
/// restarting, reduced motion, the data and the layout. Times are story seconds of the built-in story at full motion:
/// request 1 runs 0.704–2.263 (its numbers settle at 2.683), request 2 3.718–4.302, request 3 5.757–6.341, request 4
/// 7.796–8.380, the cache idles from 9.28 and expires at 13.48.
/// </summary>
public class PromptCacheTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (PromptCache Card, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Full, bool autoPlay = false,
        double width = 800, Action<PromptCache>? setup = null)
    {
        var card = new PromptCache { AutoPlay = autoPlay };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    private static List<PromptCacheResult> Record(PromptCache card)
    {
        var results = new List<PromptCacheResult>();
        card.RequestCompleted += (_, e) => results.Add(e.Result);
        return results;
    }

    // Theme transitions run on the render clock, which only moves when render ticks are pumped in real time.
    private static void SettleTransitions()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 500)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private static string? Text(PromptCache card, string part) => TestHelpers.Descendant<TextBlock>(card, part).Text;

    [AvaloniaFact]
    public void The_First_Request_Writes_The_Prefix_And_The_Others_Read_It()
    {
        var (card, window) = Open();
        try
        {
            var results = Record(card);
            card.CacheState.ShouldBe(PromptCacheState.Empty);
            card.Advance(9.0);
            results.Select(r => r.Kind).ShouldBe([PromptCacheRequestKind.Write, PromptCacheRequestKind.Hit, PromptCacheRequestKind.Hit, PromptCacheRequestKind.Hit]);
            results.Select(r => r.Index).ShouldBe([0, 1, 2, 3]);
            results.Select(r => r.Time).ShouldBe([0, 38, 71, 125]);
            results[0].IsCold.ShouldBeTrue();
            results[1].IsCold.ShouldBeFalse();
            results[0].Message.ShouldBe("List the parcels held at customs today");
            card.Results.Count.ShouldBe(4);
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            Text(card, "PART_CostValue").ShouldBe("61%");
            Text(card, "PART_CostDetail").ShouldBe("$0.2147 instead of $0.5491");
            Text(card, "PART_SpeedValue").ShouldBe("2.9x");
            Text(card, "PART_CachedTokens").ShouldBe("45.5K tokens cached");
            var table = TestHelpers.Descendant<PromptCacheTable>(card);
            table.Rows[0].Numbers.Select(n => n.Text).ShouldBe(["45.7K", "$0.1712", "1.42s"]);
            table.Rows[1].Numbers.Select(n => n.Text).ShouldBe(["260", "$0.0144", "0.48s"]);
            table.Rows[3].Time.Text.ShouldBe("2:05");
            AutomationProperties.GetName(table.Rows[1].TitleMuted).ShouldStartWith("Request 2 at 0:38");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Numbers_Roll_From_The_Row_Above()
    {
        var (card, window) = Open();
        try
        {
            card.Advance(4.302 + 0.05);
            var row = TestHelpers.Descendant<PromptCacheTable>(card).Rows[1];
            // A twentieth of a second into the roll the processed tokens are still far above the hit's 260.
            PromptCacheMath.FormatTokens(45_690).ShouldBe("45.7K");
            row.Numbers[0].Text!.ShouldEndWith("K");
            card.Advance(0.5);
            row.Numbers[0].Text.ShouldBe("260");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Change_Toggle_Turns_The_Next_Request_Into_A_Partial_Hit()
    {
        var (card, window) = Open();
        try
        {
            var results = Record(card);
            card.Advance(2.8);
            var toggle = TestHelpers.Descendant<ToggleButton>(card, "PART_Toggle");
            toggle.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(toggle).ShouldBe("Edit the instructions");
            toggle.IsChecked = true;
            card.IsChangeArmed.ShouldBeTrue();
            card.Classes.ShouldContain(PromptCache.ArmedPseudoClass);
            card.Caption.ShouldBe(card.Labels.ChangeArmed("Instructions"));
            card.Advance(1.0);
            card.IsChangeArmed.ShouldBeFalse();
            toggle.IsChecked.ShouldBe(false);
            card.Advance(2.0);
            var partial = results[1];
            partial.Kind.ShouldBe(PromptCacheRequestKind.Partial);
            partial.BreakIndex.ShouldBe(1);
            partial.BreakSegmentId.ShouldBe("rules");
            partial.IsChanged.ShouldBeTrue();
            partial.CachedTokens.ShouldBe(4_100);
            partial.WrittenTokens.ShouldBe(41_400);
            PromptCacheMath.FormatCost(partial.Cost).ShouldBe("$0.1573");
            card.RowBar(1)!.Mark.ShouldBe(1);
            card.RowBar(1)!.MarkIndex.ShouldBe(1);
            card.Advance(2.0);
            results[2].Kind.ShouldBe(PromptCacheRequestKind.Hit);
            results[2].IsChanged.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Releasing_The_Toggle_Before_The_Request_Keeps_It_A_Hit()
    {
        var (card, window) = Open();
        try
        {
            var results = Record(card);
            card.Advance(2.8);
            card.IsChangeArmed = true;
            card.IsChangeArmed = false;
            card.Caption.ShouldBe(card.Labels.ChangeReleased("Instructions"));
            card.Advance(2.0);
            results[1].Kind.ShouldBe(PromptCacheRequestKind.Hit);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Idle_Cache_Drains_Turns_Low_And_Expires_Then_The_Round_Starts_Over()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            var expired = 0;
            card.Expired += (_, _) => expired++;
            var results = Record(card);
            card.Advance(10.0);
            card.Caption.ShouldBe(card.Labels.Idle("5 minutes"));
            card.Classes.ShouldNotContain(PromptCache.LowPseudoClass);
            card.Advance(3.3);
            card.Classes.ShouldContain(PromptCache.LowPseudoClass);
            card.RingShown.ShouldBeLessThan(0.2);
            // The warning color arrives through the theme's 240ms brush transition (on the render clock).
            SettleTransitions();
            var ring = TestHelpers.Descendant<Avalonia.Controls.Control>(card, "PART_Ring");
            // Within a step per channel: the transition's last frame can land one rounding step short under load.
            var shown = TestHelpers.ColorOf(ring.GetValue(TKCode.Arc.Primitives.ArcRing.ForegroundProperty));
            var warning = ArcTokens.GetColor(card, DesignTokens.ArcDesignTokenKind.Warning);
            Math.Abs(shown.R - warning.R).ShouldBeLessThanOrEqualTo(2);
            Math.Abs(shown.G - warning.G).ShouldBeLessThanOrEqualTo(2);
            Math.Abs(shown.B - warning.B).ShouldBeLessThanOrEqualTo(2);
            expired.ShouldBe(0);
            card.Advance(0.3);
            expired.ShouldBe(1);
            // Looping: the next round is already under way, its first request written at once.
            card.Results.Count.ShouldBe(1);
            card.Results[0].Kind.ShouldBe(PromptCacheRequestKind.Write);
            card.Results[0].Time.ShouldBe(0);
            results.Count.ShouldBe(5);
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            card.IsFinished.ShouldBeFalse();
            Text(card, "PART_CostValue").ShouldNotBe("61%");
            card.Advance(1.5);
            card.Results.Count.ShouldBe(2);
            card.Results[1].Kind.ShouldBe(PromptCacheRequestKind.Hit);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Looping_The_Story_Ends_On_An_Expired_Cache()
    {
        var (card, window) = Open(setup: c => c.IsLooping = false);
        try
        {
            var finished = 0;
            card.Finished += (_, _) => finished++;
            card.Advance(14.0);
            card.CacheState.ShouldBe(PromptCacheState.Expired);
            card.Classes.ShouldContain(PromptCache.ExpiredPseudoClass);
            card.IsFinished.ShouldBeTrue();
            card.CanStep.ShouldBeFalse();
            finished.ShouldBe(1);
            card.Caption.ShouldBe(card.Labels.ExpiredCaption);
            Text(card, "PART_CachedTokens").ShouldBe("0 tokens cached");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_Sends_One_Request_Without_The_Pause_Before_It()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            card.StepForward();
            card.IsStepping.ShouldBeTrue();
            card.IsPlaying.ShouldBeFalse();
            // The opening pause is skipped: request 1 starts at once.
            card.StoryTime.ShouldBe(0.704, 0.001);
            clock.Advance(TimeSpan.FromSeconds(2.5), Frame);
            card.IsStepping.ShouldBeFalse();
            card.Results.Count.ShouldBe(1);
            card.StoryTime.ShouldBe(2.683, 0.01);
            card.StepForward();
            // The dwell after a request is skipped too: the simulated gap starts at once.
            card.StoryTime.ShouldBe(3.163, 0.01);
            clock.Advance(TimeSpan.FromSeconds(1.0), Frame);
            card.IsStepping.ShouldBeTrue();
            card.Results.Count.ShouldBe(1);
            // Pressing again while a step runs changes nothing.
            card.StepForward();
            clock.Advance(TimeSpan.FromSeconds(0.8), Frame);
            card.IsStepping.ShouldBeFalse();
            card.Results.Count.ShouldBe(2);
            card.Results[1].Kind.ShouldBe(PromptCacheRequestKind.Hit);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_After_A_Finished_Round_Starts_A_New_One_That_Hits_The_Warm_Cache()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            card.Advance(10.5);
            card.Results.Count.ShouldBe(4);
            var sim = card.SimulatedTime;
            card.StepForward();
            card.IsStepping.ShouldBeTrue();
            // The idle drain does not run on: the simulated clock waits for the new request.
            card.SimulatedTime.ShouldBe(sim, 1e-6);
            clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
            card.IsStepping.ShouldBeFalse();
            card.Results.Count.ShouldBe(1);
            card.Results[0].Kind.ShouldBe(PromptCacheRequestKind.Hit);
            card.Results[0].Time.ShouldBe(0);
            Text(card, "PART_CostValue").ShouldBe("90%");
            var table = TestHelpers.Descendant<PromptCacheTable>(card);
            table.Rows[1].Numbers[0].Text.ShouldBe(PromptCacheRowView.Dash);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pausing_Freezes_Every_Fill_Mid_Sweep()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1.3), Frame);
            card.Pause();
            var bar = card.RowBar(0)!;
            var processed = bar.Processed.ToArray();
            processed.ShouldContain(p => p > 0 && p < 1);
            var time = card.StoryTime;
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.StoryTime.ShouldBe(time);
            bar.Processed.ShouldBe(processed);
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            bar.Processed.ShouldAllBe(p => p == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Story()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open(setup: c => c.Speed = 2);
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1.2), Frame);
            card.Results.Count.ShouldBe(1);
            card.StoryTime.ShouldBe(2.4, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restart_Empties_The_Cache_And_Keeps_A_Paused_Story_Paused()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Advance(6.0);
            card.Results.Count.ShouldBe(2);
            card.Restart();
            card.IsPlaying.ShouldBeFalse();
            card.StoryTime.ShouldBe(0);
            card.Results.ShouldBeEmpty();
            card.CacheState.ShouldBe(PromptCacheState.Empty);
            card.Classes.ShouldContain(PromptCache.EmptyPseudoClass);
            SettleTransitions();
            var frame = TestHelpers.Descendant<PromptCacheFrame>(card);
            frame.IsDashed.ShouldBeTrue();
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(card, DesignTokens.ArcDesignTokenKind.BorderStrong));
            Text(card, "PART_CostValue").ShouldBe(PromptCacheRowView.Dash);
            TestHelpers.Descendant<PromptCacheTable>(card).Rows[0].Numbers[0].Text.ShouldBe(PromptCacheRowView.Dash);
            card.Caption.ShouldBe(card.Labels.Opening("45.5K"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_On_A_Finished_Round_And_Applies_Changes_At_Once()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open(ArcMotionLevel.Reduced, autoPlay: true);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            card.IsPlaying.ShouldBeFalse();
            card.Results.Count.ShouldBe(4);
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            Text(card, "PART_CostValue").ShouldBe("61%");
            card.RowBar(0)!.Processed.ShouldAllBe(p => p == 1);
            // A step sends the next round's first request at once; the cache is still warm.
            card.StepForward();
            card.IsStepping.ShouldBeFalse();
            card.Results.Count.ShouldBe(1);
            card.Results[0].Kind.ShouldBe(PromptCacheRequestKind.Hit);
            card.StepForward();
            card.Results.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Data_Prices_And_First_Token_Drive_The_Numbers()
    {
        var (card, window) = Open(setup: c =>
        {
            c.Segments.Add(new PromptCacheSegment("sys", "System", 1_000));
            c.Segments.Add(new PromptCacheSegment("docs", "Docs", 9_000));
            c.Requests.Add(new PromptCacheRequest("one", 100));
            c.Requests.Add(new PromptCacheRequest("two", 200, 10));
            c.Pricing = new PromptCachePricing { InputPrice = 1, CacheWritePrice = 1, CacheReadPrice = 0.5 };
            c.FirstTokenEstimator = (processed, cached) => processed / 1000.0 + cached / 10_000.0;
            c.ChangeSegmentId = "docs";
            c.Ttl = 60;
        });
        try
        {
            var results = Record(card);
            card.Advance(6);
            results.Count.ShouldBe(2);
            results[0].Cost.ShouldBe(10_100 / 1e6, 1e-12);
            results[0].FirstToken.ShouldBe(10.1, 1e-9);
            results[1].Cost.ShouldBe(10_000 * 0.5 / 1e6 + 200 / 1e6, 1e-12);
            results[1].FirstToken.ShouldBe(0.2 + 1.0, 1e-9);
            results[1].Time.ShouldBe(10);
            Text(card, "PART_Ttl").ShouldBe("1-minute cache");
            AutomationProperties.GetName(TestHelpers.Descendant<ToggleButton>(card, "PART_Toggle")).ShouldBe("Edit the docs");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Gap_Longer_Than_The_Time_To_Live_Expires_The_Cache_Before_The_Next_Request()
    {
        var (card, window) = Open(setup: c =>
        {
            c.Ttl = 30;
            c.Requests.Add(new PromptCacheRequest("first", 100));
            c.Requests.Add(new PromptCacheRequest("much later", 100, 45));
        });
        try
        {
            var expired = 0;
            card.Expired += (_, _) => expired++;
            var results = Record(card);
            card.Advance(6);
            expired.ShouldBe(1);
            results[1].Kind.ShouldBe(PromptCacheRequestKind.Write);
            results[1].IsCold.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Most_Four_Requests_And_No_Toggle_Without_A_Matching_Segment()
    {
        var (card, window) = Open(setup: c =>
        {
            for (var i = 0; i < 6; i++)
            {
                c.Requests.Add(new PromptCacheRequest($"q{i}", 100, 5));
            }
            c.ChangeSegmentId = null;
        });
        try
        {
            TestHelpers.Descendant<PromptCacheTable>(card).Rows.Count.ShouldBe(4);
            TestHelpers.Descendant<ToggleButton>(card, "PART_Toggle").IsVisible.ShouldBeFalse();
            card.IsChangeArmed = true;
            var results = Record(card);
            card.Advance(5);
            results[1].Kind.ShouldBe(PromptCacheRequestKind.Hit);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Data_Restarts_The_Story()
    {
        var (card, window) = Open();
        try
        {
            card.Advance(5);
            card.Results.Count.ShouldBe(2);
            card.Segments.Add(new PromptCacheSegment("a", "Alpha", 500));
            card.RebuildNow();
            card.StoryTime.ShouldBe(0);
            card.Results.ShouldBeEmpty();
            TestHelpers.Descendant<PromptCacheTable>(card).Rows.Count.ShouldBe(4);
            card.RowBar(0)!.Grows.Length.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Layout_Follows_The_Card_Width()
    {
        var (card, window) = Open(width: 800);
        try
        {
            var split = TestHelpers.Descendant<PromptCacheSplit>(card);
            card.Classes.ShouldNotContain(PromptCache.NarrowPseudoClass);
            split.IsStacked.ShouldBeFalse();
            window.Width = 680;
            Dispatcher.UIThread.RunJobs();
            card.Classes.ShouldContain(PromptCache.NarrowPseudoClass);
            card.Classes.ShouldNotContain(PromptCache.CompactPseudoClass);
            split.IsStacked.ShouldBeTrue();
            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            card.Classes.ShouldContain(PromptCache.CompactPseudoClass);
            TestHelpers.Descendant<PromptCacheTable>(card).Mode.ShouldBe(PromptCacheWidthMode.Compact);
            window.Width = 300;
            Dispatcher.UIThread.RunJobs();
            card.Classes.ShouldContain(PromptCache.TinyPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Flight_Carries_The_Written_Part_Into_The_Cache()
    {
        var (card, window) = Open();
        try
        {
            card.Advance(1.9);
            var flight = TestHelpers.Descendant<PromptCacheFlight>(card);
            flight.Progress.ShouldBeInRange(0.01, 0.99);
            flight.FirstSegment.ShouldBe(0);
            card.CacheState.ShouldBe(PromptCacheState.Writing);
            card.Advance(0.4);
            double.IsNaN(flight.Progress).ShouldBeTrue();
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            card.RingShown.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Reader_Hears_Steps_The_Toggle_Pauses_And_Restarts_But_Not_Autoplay()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            var announcer = TestHelpers.Descendant<ExplainerAnnouncer>(card);
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(3), Frame);
            announcer.History.ShouldBeEmpty();
            var play = TestHelpers.Descendant<Button>(card, Explainer.PlayButtonPart);
            play.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsPlaying.ShouldBeFalse();
            announcer.History.ShouldContain(card.Labels.Paused);
            card.StepForward();
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            announcer.History.Last().ShouldEndWith(card.Labels.Simulated);
            card.IsChangeArmed = true;
            announcer.History.Last().ShouldBe(card.Labels.ChangeArmed("Instructions"));
            var restart = TestHelpers.Descendant<Button>(card, Explainer.RestartButtonPart);
            restart.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            announcer.History.Last().ShouldBe(card.Labels.Restarted);
            card.StoryTime.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Is_Named_And_Its_Parts_Are_Labelled()
    {
        var (card, window) = Open();
        try
        {
            AutomationProperties.GetName(card).ShouldBe("Prompt cache, a simulation with sample prices");
            TestHelpers.Descendant<Button>(card, Explainer.StepButtonPart).GetValue(AutomationProperties.NameProperty).ShouldBe("Send the next request");
            AutomationProperties.GetName(TestHelpers.Descendant<PromptCacheTable>(card)).ShouldBe("Requests");
            AutomationProperties.GetName(TestHelpers.Descendant<PromptCacheTable>(card).Rows[0].TitleMuted).ShouldBe("Request 1: not sent yet.");
            Text(card, "PART_ModelName").ShouldBe("Nimbus 4 Pro");
            Text(card, "PART_Ttl").ShouldBe("5-minute cache");
            card.Labels = new PromptCacheLabels { RequestsName = "Calls", TtlNote = _ => "TTL" };
            Text(card, "PART_Ttl").ShouldBe("TTL");
        }
        finally
        {
            window.Close();
        }
    }
    [AvaloniaFact]
    public void Autoplay_Starts_When_Seen_And_Loops_Without_Ending()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open(autoPlay: true);
        try
        {
            var expired = 0;
            card.Expired += (_, _) => expired++;
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            card.IsPlaying.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(15), Frame);
            expired.ShouldBe(1);
            card.IsPlaying.ShouldBeTrue();
            card.IsFinished.ShouldBeFalse();
            card.Results.Count.ShouldBe(2);
            card.Results[0].Kind.ShouldBe(PromptCacheRequestKind.Write);
            card.Results[1].Kind.ShouldBe(PromptCacheRequestKind.Hit);
            // The ring refilled after the expiry and the story never showed the expired panel.
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            card.RingShown.ShouldBeGreaterThan(0.8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Reduced_Motion_Playing_Expires_The_Cache_Then_Writes_It_Again()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open(ArcMotionLevel.Reduced, autoPlay: true);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            card.Results.Count.ShouldBe(4);
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1.6), Frame);
            card.CacheState.ShouldBe(PromptCacheState.Expired);
            card.Caption.ShouldBe(card.Labels.ExpiredCaption);
            clock.Advance(TimeSpan.FromSeconds(1.8), Frame);
            card.CacheState.ShouldBe(PromptCacheState.Warm);
            card.Results.Count.ShouldBe(1);
            card.Results[0].Kind.ShouldBe(PromptCacheRequestKind.Write);
            card.Results[0].IsCold.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Bars_Follow_The_Theme()
    {
        var (card, window) = Open();
        try
        {
            card.Advance(3);
            var bar = card.RowBar(0)!;
            var light = bar.Palette!.Surface;
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().WithAlgorithms(ThemeAlgorithm.Dark).Build());
            card.Advance(0.1);
            bar.Palette!.Surface.ShouldNotBe(light);
            bar.Palette.Surface.ShouldBe(ArcTokens.GetColor(card, DesignTokens.ArcDesignTokenKind.Surface));
        }
        finally
        {
            await TestHelpers.ApplyAsync(new ThemeConfigBuilder().Build());
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Moves_Through_Play_Step_Restart_And_The_Toggle()
    {
        var (card, window) = Open();
        try
        {
            var play = TestHelpers.Descendant<Button>(card, Explainer.PlayButtonPart);
            play.Focus(NavigationMethod.Tab);
            var order = new List<string?> { (window.FocusManager?.GetFocusedElement() as Control)?.Name };
            for (var i = 0; i < 3; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                order.Add((window.FocusManager?.GetFocusedElement() as Control)?.Name);
            }
            order.ShouldBe([Explainer.PlayButtonPart, Explainer.StepButtonPart, Explainer.RestartButtonPart, "PART_Toggle"]);
            var toggle = TestHelpers.Descendant<ToggleButton>(card, "PART_Toggle");
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked.ShouldBe(true);
            card.IsChangeArmed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Is_Collected_While_Its_Segments_And_Requests_Live_On()
    {
        var segments = new AvaloniaList<PromptCacheSegment> { new("sys", "System", 1_000), new("docs", "Docs", 9_000) };
        var requests = new ObservableCollection<PromptCacheRequest> { new("one", 100), new("two", 200, 10) };
        var weak = TestHelpers.ShowAndClose(() => new PromptCache { AutoPlay = false, Segments = segments, RequestsSource = requests });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        // The data still works without the card.
        segments.Add(new PromptCacheSegment("chat", "Chat", 3_000));
        segments[0].Tokens = 2_000;
        requests.Add(new PromptCacheRequest("three", 50));
        requests[0].Gap = 5;
    }

    [AvaloniaFact]
    public void A_Card_Taken_Out_And_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var segments = new AvaloniaList<PromptCacheSegment> { new("sys", "System", 1_000), new("docs", "Docs", 9_000) };
        var requests = new ObservableCollection<PromptCacheRequest> { new("one", 100), new("two", 200, 10) };
        var card = new PromptCache { AutoPlay = false, Segments = segments, RequestsSource = requests };
        var host = new Border { Child = card };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            window.Width = 800;
            window.Height = 900;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<PromptCacheTable>(card).Rows.Count.ShouldBe(2);
            card.Advance(2);
            // Away and back with nothing changed: the story carries on from where it was.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            host.Child = card;
            Dispatcher.UIThread.RunJobs();
            card.StoryTime.ShouldBe(2);
            // A new name while away shows in the legend without starting over.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            segments[0].Label = "Rules";
            host.Child = card;
            Dispatcher.UIThread.RunJobs();
            card.StoryTime.ShouldBe(2);
            card.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Rules");
            // A new request while away starts the story over with it.
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            requests.Add(new PromptCacheRequest("three", 50));
            host.Child = card;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<PromptCacheTable>(card).Rows.Count.ShouldBe(3);
            card.StoryTime.ShouldBe(0);
            // And it follows changes made after it came back.
            requests.Add(new PromptCacheRequest("four", 60));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<PromptCacheTable>(card).Rows.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }
}
