using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiInsightCard"/> on its invented sample: the change tone, causes and the band, feedback, the action and the chart's keyboard.</summary>
public class AiInsightCardTests
{
    private static (AiInsightCard Card, Window Window) Show(Action<AiInsightCard>? configure = null)
    {
        var card = new AiInsightCard();
        configure?.Invoke(card);
        var window = TestHelpers.Show(card);
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    [AvaloniaFact]
    public void Sample_shows_a_bad_drop_with_three_causes_and_the_focus_band()
    {
        var (card, window) = Show();
        try
        {
            card.Classes.Contains(":bad").ShouldBeTrue();
            card.CauseRows.Count.ShouldBe(3);
            card.Chart.ShouldNotBeNull().Count.ShouldBe(14);
            card.Chart!.BandValue.ShouldBe(new Avalonia.Point(9, 13));
            card.Readout.ShouldBe("Nov 9: 18,420, before 21,020");
            AutomationProperties.GetName(card.CauseRows[0]).ShouldBe("Android 4.2 release, 64% of the change");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_rise_of_a_cost_is_bad_and_a_rise_of_users_is_good()
    {
        var (card, window) = Show(c => c.Change = 5);
        try
        {
            card.Classes.Contains(":good").ShouldBeTrue();
            card.IsIncreaseGood = false;
            card.Classes.Contains(":bad").ShouldBeTrue();
            card.Change = null;
            card.Classes.Contains(":good").ShouldBeFalse();
            card.Classes.Contains(":bad").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checking_a_cause_selects_it_moves_the_band_and_unchecking_returns_to_the_focus()
    {
        var (card, window) = Show();
        var raised = new List<AiInsightCause?>();
        card.SelectedCauseChanged += (_, e) => raised.Add(e.Cause);
        try
        {
            card.CauseRows[2].IsChecked = true;
            card.SelectedCause.ShouldBeSameAs(card.Causes![2]);
            card.Chart!.BandValue.ShouldBe(new Avalonia.Point(9, 10));
            card.CauseRows[0].IsChecked.ShouldBe(false);

            card.CauseRows[1].IsChecked = true;
            card.CauseRows[2].IsChecked.ShouldBe(false);

            card.CauseRows[1].IsChecked = false;
            card.SelectedCause.ShouldBeNull();
            card.Chart.BandValue.ShouldBe(new Avalonia.Point(9, 13));
            raised.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Feedback_toggles_and_is_announced()
    {
        var (card, window) = Show();
        var events = new List<AiInsightFeedback>();
        card.FeedbackChanged += (_, e) => events.Add(e.NewFeedback);
        try
        {
            var helpful = TestHelpers.Descendant<Button>(card, AiInsightCard.HelpfulPart);
            helpful.Command = null;
            helpful.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            card.Feedback.ShouldBe(AiInsightFeedback.Helpful);
            card.Classes.Contains(":helpful").ShouldBeTrue();
            card.Announcement.ShouldBe("Thanks for the feedback");
            helpful.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            card.Feedback.ShouldBe(AiInsightFeedback.None);
            events.ShouldBe([AiInsightFeedback.Helpful, AiInsightFeedback.None]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_action_reports_the_step_and_the_selected_cause()
    {
        var (card, window) = Show();
        AiInsightActionEventArgs? args = null;
        card.ActionInvoked += (_, e) => args = e;
        try
        {
            card.SelectedCause = card.Causes![1];
            card.InvokeAction();
            args.ShouldNotBeNull().NextStep.ShouldBe(AiInsightCardSamples.NextStep);
            args.SelectedCause.ShouldBeSameAs(card.Causes[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_pick_points_on_the_chart()
    {
        var (card, window) = Show();
        try
        {
            card.Chart!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            card.Chart.HoverIndex.ShouldBe(13);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            card.Chart.HoverIndex.ShouldBe(12);
            card.Readout.ShouldBe("Nov 8: 18,530, before 21,060");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            card.Chart.HoverIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            card.Chart.HoverIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Analyzing_hides_the_text_and_compact_follows_the_width()
    {
        var (card, window) = Show(c => c.IsAnalyzing = true);
        try
        {
            card.Classes.Contains(":analyzing").ShouldBeTrue();
            TestHelpers.Descendant<StackPanel>(card, "PART_Body").IsVisible.ShouldBeFalse();
            card.Width = 380;
            Dispatcher.UIThread.RunJobs();
            card.IsCompact.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
