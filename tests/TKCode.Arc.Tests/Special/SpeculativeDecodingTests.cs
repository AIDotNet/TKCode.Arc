using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The <see cref="SpeculativeDecoding"/> card: its story, statistics, compare race, states, layout and controls.</summary>
public class SpeculativeDecodingTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (SpeculativeDecoding Card, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Full, double width = 720,
        Action<SpeculativeDecoding>? setup = null)
    {
        var card = new SpeculativeDecoding { AutoPlay = false, Width = width };
        setup?.Invoke(card);
        return (card, Show(card, level, width + 40));
    }

    // TestHelpers.Show with a window large enough for the whole card (autoplay needs most of it in view).
    private static Window Show(Control content, ArcMotionLevel level, double width = 760, double height = 1000)
    {
        var layers = new Avalonia.Controls.Primitives.VisualLayerManager { Child = content };
        typeof(Avalonia.Controls.Primitives.VisualLayerManager)
            .GetProperty("EnablePopupOverlayLayer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(layers, true);
        var window = new Window { Content = layers, Width = width, Height = height };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static ExplainerAnnouncer Announcer(SpeculativeDecoding card) => TestHelpers.Descendant<ExplainerAnnouncer>(card);

    private static string? StatusText(SpeculativeDecoding card) => TestHelpers.Descendant<SpeculativeDecodingStatusLine>(card).Text;

    [AvaloniaFact]
    public void It_Opens_Ready_With_The_Sample_Answer_And_Empty_Slots()
    {
        var (card, window) = Open();
        try
        {
            card.Title.ShouldBe("Speculative decoding");
            card.DraftLength.ShouldBe(4);
            card.Plan.Passes.ShouldBe(6);
            card.PlannedStats!.Accepted.ShouldBe(16);
            card.Stats.ShouldBeNull();
            card.IsPlaying.ShouldBeFalse();
            StatusText(card).ShouldBe(card.Labels.Ready(4));
            var stage = TestHelpers.Descendant<SpeculativeDecodingStage>(card);
            stage.ShowsSlots.ShouldBeTrue();
            stage.TrackHeight.ShouldBe(72);
            TestHelpers.Descendant<SpeculativeDecodingStat>(card, SpeculativeDecoding.SpeedupPart).HasValue.ShouldBeFalse();
            AutomationProperties.GetName(card).ShouldBe("Speculative decoding");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Playing_Walks_The_Rounds_And_Rolls_The_Stats()
    {
        var (card, window) = Open();
        try
        {
            var timeline = card.Timeline;
            card.Play();
            card.Advance(0.1);
            StatusText(card).ShouldBe(card.Labels.Drafting(4));
            card.Advance(timeline.VerifyStart(0) + 0.01 - card.StoryTime);
            StatusText(card).ShouldBe(card.Labels.Verifying(4));
            card.Stats.ShouldBeNull();
            card.Advance(timeline.Settle(0) + 0.01 - card.StoryTime);
            StatusText(card).ShouldBe(card.Labels.Kept(2, 4));
            card.Stats!.Passes.ShouldBe(1);
            card.Stats.Accepted.ShouldBe(2);
            var speedup = TestHelpers.Descendant<SpeculativeDecodingStat>(card, SpeculativeDecoding.SpeedupPart);
            speedup.HasValue.ShouldBeTrue();
            speedup.Value.ShouldBe(2.1);
            AutomationProperties.GetName(speedup).ShouldBe("Speedup: 2.1 times");
            // Round 3 keeps all four and adds a bonus; round 5 misses its first guess.
            card.Advance(timeline.Settle(2) + 0.01 - card.StoryTime);
            StatusText(card).ShouldBe(card.Labels.AllKept(4));
            card.Advance(timeline.Settle(4) + 0.01 - card.StoryTime);
            StatusText(card).ShouldBe(card.Labels.FirstMissed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_End_Raises_Completed_And_Summarizes()
    {
        var (card, window) = Open();
        try
        {
            DecodeStats? completed = null;
            var finished = 0;
            card.Completed += (_, e) => completed = e.Stats;
            card.Finished += (_, _) => finished++;
            card.Play();
            card.Advance(10);
            card.IsFinished.ShouldBeTrue();
            card.IsPlaying.ShouldBeFalse();
            finished.ShouldBe(1);
            completed.ShouldNotBeNull();
            completed!.Passes.ShouldBe(6);
            completed.Drafted.ShouldBe(24);
            Math.Round(completed.Speedup, 1).ShouldBe(2.6);
            StatusText(card).ShouldBe(card.Labels.Done(6));
            Announcer(card).History.ShouldContain(card.Labels.Finished(22, 6, 2.6));
            var output = TestHelpers.Descendant<SpeculativeDecodingSpeech>(card, SpeculativeDecoding.OutputSpeechPart);
            output.Text.ShouldBe("Output: Tomorrow at 7, the east wing switches to backup power for 25 minutes, while crews replace two old panels.");
            // Play once finished starts over.
            card.Play();
            card.StoryTime.ShouldBe(0);
            card.IsPlaying.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Clock_Pauses_And_Resumes_Where_It_Stopped()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            var held = card.StoryTime;
            held.ShouldBeGreaterThan(0.8);
            card.Pause();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.StoryTime.ShouldBe(held);
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(0.5), Frame);
            card.StoryTime.ShouldBeGreaterThan(held);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Neither_Autoplays_Nor_Opens_Finished()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new SpeculativeDecoding { Width = 720 };
        var window = Show(card, ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.IsPlaying.ShouldBeFalse();
            card.IsFinished.ShouldBeFalse();
            card.StoryTime.ShouldBe(0);
            // Played by hand it runs the same beats.
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.StoryTime.ShouldBeGreaterThan(0.8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Autoplays_When_Seen()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new SpeculativeDecoding { Width = 720 };
        var window = Show(card, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.5), Frame);
            card.IsPlaying.ShouldBeTrue();
            card.StoryTime.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Draft_Length_Replans_And_Plays_From_The_Start()
    {
        var (card, window) = Open();
        try
        {
            card.Play();
            card.Advance(2);
            card.Pause();
            card.DraftLength = 2;
            card.StoryTime.ShouldBe(0);
            card.Plan.Passes.ShouldBe(10);
            card.PlannedStats!.Drafted.ShouldBe(19);
            card.DraftLength = 1;
            card.DraftLength.ShouldBe(2);
            card.DraftLength = 12;
            card.DraftLength.ShouldBe(8);
            var slider = TestHelpers.Descendant<Slider>(card, SpeculativeDecoding.DraftLengthPart);
            slider.Value.ShouldBe(8);
            // Moving the slider replans and plays.
            slider.Value = 3;
            card.DraftLength.ShouldBe(3);
            card.IsPlaying.ShouldBeTrue();
            card.Plan.Passes.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inputs_That_Did_Not_Change_Do_Not_Restart_The_Run()
    {
        var (card, window) = Open();
        try
        {
            card.Play();
            card.Advance(1.5);
            card.TokensSource = SpeculativeDecodingSamples.Tokens.Select(t => new DecodeToken(t.Text, t.Ease, t.Miss)).ToList();
            card.StoryTime.ShouldBe(1.5, 1e-9);
            card.Seed = 26;
            card.StoryTime.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Comparing_Races_The_Target_Alone_On_The_Same_Clock()
    {
        var (card, window) = Open();
        try
        {
            var timeline = card.Timeline;
            card.IsComparing = true;
            card.Classes.ShouldContain(SpeculativeDecoding.ComparingPseudoClass);
            card.Play();
            card.Advance(timeline.MainEnd + 0.1);
            // The speculative line is done; the target alone still writes, so the story plays on.
            card.IsFinished.ShouldBeFalse();
            card.IsPlaying.ShouldBeTrue();
            StatusText(card).ShouldBe(card.Labels.SpeculationDone(6));
            var compare = TestHelpers.Descendant<SpeculativeDecodingSpeech>(card, SpeculativeDecoding.CompareSpeechPart);
            compare.Text.ShouldStartWith("Target only: Tomorrow at 7, the east");
            card.Advance(timeline.CompareEnd);
            card.IsFinished.ShouldBeTrue();
            StatusText(card).ShouldBe(card.Labels.CompareDone(6, 22));
            Announcer(card).History.ShouldContain(card.Labels.TargetOnlyFinished(22));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Comparing_After_The_End_Carries_The_Clock_On()
    {
        var (card, window) = Open();
        try
        {
            var completed = 0;
            card.Completed += (_, _) => completed++;
            card.Play();
            card.Advance(10);
            card.IsFinished.ShouldBeTrue();
            var end = card.StoryTime;
            card.IsComparing = true;
            card.IsFinished.ShouldBeFalse();
            card.IsPlaying.ShouldBeTrue();
            card.StoryTime.ShouldBe(end, 1e-9);
            completed.ShouldBe(1);
            card.Stats!.Passes.ShouldBe(6);
            // Turning it off again ends the story at once (the speculative line is done).
            card.IsComparing = false;
            card.IsFinished.ShouldBeTrue();
            StatusText(card).ShouldBe(card.Labels.Done(6));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Recorded_Run_Replays_And_Hides_The_Slider()
    {
        var (card, window) = Open(setup: c => c.RoundsSource = SpeculativeDecodingSamples.CodeCompletion);
        try
        {
            card.Classes.ShouldContain(SpeculativeDecoding.ReplayPseudoClass);
            TestHelpers.Descendant<Slider>(card, SpeculativeDecoding.DraftLengthPart).IsVisible.ShouldBeFalse();
            card.Plan.Tokens.Count.ShouldBe(24);
            card.Play();
            card.Advance(20);
            Math.Round(card.Stats!.Speedup, 1).ShouldBe(2.9);
            card.Stats.Accepted.ShouldBe(18);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Holds_The_Run_And_Disables_The_Controls()
    {
        var (card, window) = Open(setup: c => c.Status = SpeculativeDecodingStatus.Loading);
        try
        {
            card.Classes.ShouldContain(SpeculativeDecoding.LoadingPseudoClass);
            StatusText(card).ShouldBe(card.Labels.Loading);
            TestHelpers.Descendant<SpeculativeDecodingStatusLine>(card).IsBusy.ShouldBeTrue();
            TestHelpers.Descendant<Slider>(card, SpeculativeDecoding.DraftLengthPart).IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<Switch>(card, SpeculativeDecoding.ComparePart).IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<Avalonia.Controls.Button>(card, Explainer.PlayButtonPart).IsEnabled.ShouldBeFalse();
            card.Play();
            card.IsPlaying.ShouldBeFalse();
            card.Status = SpeculativeDecodingStatus.Ready;
            TestHelpers.Descendant<Slider>(card, SpeculativeDecoding.DraftLengthPart).IsEnabled.ShouldBeTrue();
            StatusText(card).ShouldBe(card.Labels.Ready(4));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Error_State_Offers_A_Retry_Only_When_Someone_Handles_It()
    {
        var (card, window) = Open(setup: c =>
        {
            c.Status = SpeculativeDecodingStatus.Error;
            c.ErrorText = "The trace could not be read";
        });
        try
        {
            StatusText(card).ShouldBe("The trace could not be read");
            card.Classes.ShouldNotContain(SpeculativeDecoding.RetryPseudoClass);
            var retry = TestHelpers.Descendant<Avalonia.Controls.Button>(card, SpeculativeDecoding.RetryPart);
            retry.IsVisible.ShouldBeFalse();
            var retries = 0;
            card.RetryRequested += (_, _) => retries++;
            card.Classes.ShouldContain(SpeculativeDecoding.RetryPseudoClass);
            retry.IsVisible.ShouldBeTrue();
            retry.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            retries.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Switch_Layout_Below_560()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.Classes.ShouldContain(SpeculativeDecoding.NarrowPseudoClass);
            TestHelpers.Descendant<SpeculativeDecodingStage>(card).IsNarrow.ShouldBeTrue();
            card.Width = 600;
            Dispatcher.UIThread.RunJobs();
            card.UpdateLayout();
            card.Classes.ShouldNotContain(SpeculativeDecoding.NarrowPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Compare_Switch_Toggles_The_Race_And_Says_So()
    {
        var (card, window) = Open();
        try
        {
            var toggle = TestHelpers.Descendant<Switch>(card, SpeculativeDecoding.ComparePart);
            toggle.IsChecked = true;
            card.IsComparing.ShouldBeTrue();
            Announcer(card).History.ShouldContain(card.Labels.CompareOn);
            card.IsComparing = false;
            toggle.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Answer_Lays_Out_Once_And_Keeps_Punctuation_With_Its_Word()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.Play();
            card.Advance(10);
            var stage = TestHelpers.Descendant<SpeculativeDecodingStage>(card);
            var lines = stage.AnswerLines;
            lines.ShouldBeGreaterThan(1);
            // A token without a leading space is on the line of the token before it.
            var plan = card.Plan;
            for (var q = 1; q < plan.Tokens.Count; q++)
            {
                if (!plan.Tokens[q].StartsWith(' '))
                {
                    stage.TokenLine(q).ShouldBe(stage.TokenLine(q - 1));
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Steady_Scene_Stops_Building_Text()
    {
        var (card, window) = Open();
        try
        {
            card.Play();
            card.Advance(10);
            var stage = TestHelpers.Descendant<SpeculativeDecodingStage>(card);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var misses = stage.TextCache.Misses;
            for (var i = 0; i < 3; i++)
            {
                stage.InvalidateVisual();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            stage.TextCache.Misses.ShouldBe(misses);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Draft_Length_Slider_Answers_The_Keys()
    {
        var (card, window) = Open();
        try
        {
            var slider = TestHelpers.Descendant<Slider>(card, SpeculativeDecoding.DraftLengthPart);
            var thumb = slider.GetVisualDescendantsOfType<Control>().FirstOrDefault(c => c.Focusable) ?? slider;
            thumb.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.End, RawInputModifiers.None);
            card.DraftLength.ShouldBe(8);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            card.DraftLength.ShouldBe(7);
            Announcer(card).History.Count(h => h.StartsWith("Draft length", StringComparison.Ordinal)).ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }
    [AvaloniaFact]
    public void The_Last_Flights_Finish_After_The_Story_Ends()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(6), Frame);
            card.IsFinished.ShouldBeTrue();
            var stage = TestHelpers.Descendant<SpeculativeDecodingStage>(card);
            var end = card.Timeline.MainEnd;
            // The clock stops at the end, but the drawing runs on for the flights, underlines and the summary to land.
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            stage.Time.ShouldBeGreaterThan(card.StoryTime + 0.5);
            stage.Time.ShouldBeLessThanOrEqualTo(card.StoryTime + 0.8 + 1e-9);
            card.StoryTime.ShouldBeLessThan(end + 0.1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Playback_Buttons_Say_What_They_Did()
    {
        var (card, window) = Open();
        try
        {
            var play = TestHelpers.Descendant<Avalonia.Controls.Button>(card, Explainer.PlayButtonPart);
            var restart = TestHelpers.Descendant<Avalonia.Controls.Button>(card, Explainer.RestartButtonPart);
            var history = Announcer(card).History;
            play.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            card.IsPlaying.ShouldBeTrue();
            history[^1].ShouldBe(card.Labels.Playing);
            card.Advance(1);
            play.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            card.IsPlaying.ShouldBeFalse();
            history[^1].ShouldBe(card.Labels.Paused);
            restart.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            card.IsPlaying.ShouldBeTrue();
            card.StoryTime.ShouldBe(0);
            history[^1].ShouldBe(card.Labels.Restarted);
            card.Advance(10);
            card.IsFinished.ShouldBeTrue();
            play.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            history[^1].ShouldBe(card.Labels.PlayingFromStart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tokens_Written_In_Xaml_Keep_Their_Leading_Spaces()
    {
        var demo = TKCode.Arc.Gallery.Docs.GalleryDemos.Get("SpeculativeDecoding.OwnAnswer").ShouldNotBeNull().Create();
        var window = Show(demo, ArcMotionLevel.Off);
        try
        {
            var card = TestHelpers.Descendant<SpeculativeDecoding>(demo);
            card.Tokens.Count.ShouldBe(19);
            string.Concat(card.Plan.Tokens).ShouldBe("Your refund of $48.20 was approved and should reach your card within 5 business days.");
            card.Tokens[4].Ease.ShouldBe(0.1);
            card.IsComparing.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Round_Is_Summarized_For_Screen_Readers()
    {
        var (card, window) = Open();
        try
        {
            var speech = TestHelpers.Descendant<SpeculativeDecodingSpeech>(card, SpeculativeDecoding.RoundSpeechPart);
            card.Play();
            card.Advance(card.Timeline.LastVerdict(0) + 0.01);
            speech.Text.ShouldBe("Round 1. Draft proposed Tomorrow, at, 9, ,. Target kept 2 and wrote 7 instead of 9.");
            card.Advance(card.Timeline.Start(1) + 0.01 - card.StoryTime);
            speech.Text.ShouldBeNull();
            card.Advance(card.Timeline.LastVerdict(2) + 0.01 - card.StoryTime);
            speech.Text.ShouldBe("Round 3. Draft proposed wing, switches, to, backup. Target kept all 4 and added power.");
        }
        finally
        {
            window.Close();
        }
    }

}

internal static class SpeculativeDecodingTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual root) where T : Visual
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
