using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary><see cref="CallInsights"/>: the model (talk shares, topics, sentiment), seeking by pointer, key and row, search, topics, next steps and the compact layout.</summary>
public class CallInsightsTests
{
    private static TimeSpan S(int seconds) => TimeSpan.FromSeconds(seconds);

    private static CallInsights Own()
    {
        var call = new CallInsights { Title = "Test call" };
        call.Speakers.Add(new CallInsightsSpeaker("a", "Ana", "Rep", isHost: true));
        call.Speakers.Add(new CallInsightsSpeaker("b", "Ben", "Buyer"));
        call.Utterances.Add(new CallInsightsUtterance("a", S(0), S(30), "Hello, how is the rollout going?", 0.5));
        call.Utterances.Add(new CallInsightsUtterance("b", S(31), S(90), "Slow, the import keeps failing.", -0.6));
        call.Utterances.Add(new CallInsightsUtterance("a", S(91), S(120), "I can fix the import today.", 0.4));
        call.Moments.Add(new CallInsightsMoment(S(31), CallInsightsMomentKind.Objection, "Import fails"));
        call.Moments.Add(new CallInsightsMoment(S(91), CallInsightsMomentKind.Commitment, "Fix today"));
        call.Topics.Add(new CallInsightsTopic("Import", S(31), S(120)));
        call.NextSteps.Add(new CallInsightsNextStep("Fix the import", "Ana", "Today"));
        return call;
    }

    [AvaloniaFact]
    public void Shows_The_Sample_Call_Without_Data()
    {
        var call = new CallInsights();
        var window = TestHelpers.Show(call);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        call.IsShowingSamples.ShouldBeTrue();
        call.Duration.ShouldBe(TimeSpan.FromMinutes(24));
        call.TalkShares.Count.ShouldBe(4);
        call.TalkShares.Sum(s => s.Share).ShouldBe(1, 0.001);
        call.EffectiveNextSteps.Count.ShouldBe(4);
        window.Close();
    }

    [AvaloniaFact]
    public void Model_Works_Out_Shares_Monologues_Questions_And_Sentiment()
    {
        var call = Own();
        call.IsShowingSamples.ShouldBeFalse();
        call.Duration.ShouldBe(S(120));
        var ana = call.TalkShares[0];
        ana.TalkTime.ShouldBe(S(59));
        ana.Questions.ShouldBe(1);
        call.TalkShares[1].LongestMonologue.ShouldBe(S(59));
        call.OverallSentiment.ShouldBeInRange(-0.3, 0.3);
        call.Model.Sentiment.Length.ShouldBe(CallInsightsModel.SentimentSamples);
        call.Model.UtteranceAt(S(40)).ShouldBe(1);
        call.Model.UtteranceAt(S(30)).ShouldBe(0);
        call.Model.UtteranceAt(TimeSpan.FromSeconds(30.5)).ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Keys_On_The_Timeline_Seek_And_Raise_Seeked()
    {
        var call = Own();
        var seeks = new List<TimeSpan>();
        call.Seeked += (_, e) => seeks.Add(e.Position);
        var window = TestHelpers.Show(call);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        var timeline = call.Timeline.ShouldNotBeNull();
        timeline.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        call.Position.ShouldBe(S(5));
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        call.Position.ShouldBe(S(31));
        call.CurrentUtterance!.SpeakerKey.ShouldBe("b");
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        call.Position.ShouldBe(S(120));
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        call.Position.ShouldBe(S(120));
        seeks.ShouldBe([S(5), S(31), S(120), S(120)]);
        call.Announcer!.Announcement.ShouldNotBeNull().ShouldBe("2:00, Ana: I can fix the import today.");
        window.Close();
    }

    [AvaloniaFact]
    public void Position_Is_Clamped_And_Moves_The_Playhead()
    {
        var call = Own();
        var window = TestHelpers.Show(call);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        call.Position = S(500);
        call.Position.ShouldBe(S(120));
        call.Position = S(60);
        var timeline = call.Timeline!;
        timeline.PlayheadX.ShouldBe(timeline.XOf(S(60)) - 6, 0.01);
        timeline.TimeAt(timeline.XOf(S(60))).ShouldBe(S(60));
        window.Close();
    }

    [AvaloniaFact]
    public void Search_Filters_The_Transcript_And_Topics_Toggle()
    {
        var call = Own();
        var window = TestHelpers.Show(call);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        call.SearchText = "import";
        ((IReadOnlyList<CallInsightsUtterance>)call.Transcript!.ItemsSource!).Count.ShouldBe(2);
        call.SearchText = "nothing like this";
        ((IReadOnlyList<CallInsightsUtterance>)call.Transcript!.ItemsSource!).Count.ShouldBe(0);
        call.SearchText = null;
        ((IReadOnlyList<CallInsightsUtterance>)call.Transcript!.ItemsSource!).Count.ShouldBe(3);

        var chip = call.TopicChips["Import"];
        chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        call.SelectedTopic.ShouldBe("Import");
        chip.IsCurrent.ShouldBeTrue();
        chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        call.SelectedTopic.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Ticking_A_Next_Step_Writes_It_And_Raises_The_Event()
    {
        var call = Own();
        var events = new List<CallInsightsNextStepToggledEventArgs>();
        call.NextStepToggled += (_, e) => events.Add(e);
        var window = TestHelpers.Show(call);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        var box = call.StepRows!.Children.OfType<Checkbox>().Single();
        box.IsChecked = true;
        call.NextSteps[0].IsDone.ShouldBeTrue();
        events.Single().IsDone.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Width_Stacks_The_Columns_And_Play_Toggles()
    {
        var call = Own();
        var host = new Border { Width = 600, Child = call };
        var window = TestHelpers.Show(host);
        Dispatcher.UIThread.RunJobs();
        call.IsCompact.ShouldBeTrue();
        call.Classes.Contains(CallInsights.CompactPseudoClass).ShouldBeTrue();
        AutomationProperties.GetName(call.PlayButton!).ShouldBe("Play");
        call.Play();
        call.IsPlaying.ShouldBeTrue();
        AutomationProperties.GetName(call.PlayButton!).ShouldBe("Pause");
        call.Pause();
        call.IsPlaying.ShouldBeFalse();
        window.Close();
    }
}
