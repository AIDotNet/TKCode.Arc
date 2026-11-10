using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Support;

/// <summary>FeatureVoting: the sample, picking (single and several), the counts and shares, keyboard, the thank-you and its event.</summary>
public class FeatureVotingTests
{
    private static (FeatureVoting Card, Avalonia.Controls.Window Window) Show(Action<FeatureVoting>? configure = null)
    {
        var card = new FeatureVoting();
        configure?.Invoke(card);
        var window = TestHelpers.Show(card);
        window.Width = 480;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    [AvaloniaFact]
    public void Without_Options_It_Shows_The_Sample_And_No_Pick()
    {
        var (card, window) = Show();
        try
        {
            card.Effective.Count.ShouldBe(4);
            card.Rows.Count.ShouldBe(4);
            card.SelectedOptions.ShouldBeEmpty();
            card.TotalVotes.ShouldBe(412 + 296 + 241 + 157);
            card.SubmitButton.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_Adds_The_Viewers_Vote_And_A_Second_Pick_Moves_It()
    {
        var a = new FeatureVotingOption("a", "Alpha", null, 10);
        var b = new FeatureVotingOption("b", "Beta", null, 30);
        var (card, window) = Show(c => c.Options.AddRange([a, b]));
        try
        {
            card.Toggle(a).ShouldBeTrue();
            card.SelectedOptions.ShouldBe([a]);
            card.TotalVotes.ShouldBe(41);
            card.Rows[0].Count.Value.ShouldBe(11);
            card.Rows[0].Bar.Fraction.ShouldBe(11.0 / 41, 1e-9);
            card.Rows[0].Bar.Shown.ShouldBe(11.0 / 41, 1e-9);
            AutomationProperties.GetName(card.Rows[0].Row)!.ShouldContain("your vote");

            card.Toggle(b).ShouldBeTrue();
            card.SelectedOptions.ShouldBe([b]);
            card.Rows[0].Count.Value.ShouldBe(10);
            card.Rows[1].Count.Value.ShouldBe(31);
            card.Rows[0].Row.IsChecked.ShouldBe(false);
            card.Rows[1].Row.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Several_Picks_Stop_At_MaxVotes()
    {
        var options = Enumerable.Range(0, 3).Select(i => new FeatureVotingOption($"o{i}", $"Option {i}", null, i)).ToArray();
        var (card, window) = Show(c =>
        {
            c.MaxVotes = 2;
            c.Options.AddRange(options);
        });
        try
        {
            card.Toggle(options[0]).ShouldBeTrue();
            card.Toggle(options[1]).ShouldBeTrue();
            card.Toggle(options[2]).ShouldBeFalse();
            card.SelectedOptions.Count.ShouldBe(2);
            card.LastAnnouncement!.ShouldContain("up to 2");
            card.Toggle(options[0]).ShouldBeTrue();
            card.Toggle(options[2]).ShouldBeTrue();
            card.SelectedOptions.ShouldBe([options[1], options[2]]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Submitting_Raises_Voted_Shows_The_Thanks_And_Locks_The_Choice()
    {
        var (card, window) = Show();
        try
        {
            IReadOnlyList<FeatureVotingOption>? voted = null;
            card.Voted += (_, e) => voted = e.Options;
            card.Submit();
            card.IsSubmitted.ShouldBeFalse("nothing is picked yet");

            var first = card.Effective[0];
            card.Toggle(first);
            card.Submit();
            card.IsSubmitted.ShouldBeTrue();
            voted.ShouldBe([first]);
            card.Classes.Contains(FeatureVoting.SubmittedPseudoClass).ShouldBeTrue();
            card.Toggle(card.Effective[1]).ShouldBeFalse("the choice is locked after the vote");

            card.ChangeVote();
            card.IsSubmitted.ShouldBeFalse();
            card.SelectedOptions.ShouldBe([first]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Results_Show_Only_After_The_Vote()
    {
        var (card, window) = Show(c => c.ShowsResultsBeforeVoting = false);
        try
        {
            card.Rows[0].Count.IsVisible.ShouldBeFalse();
            card.Toggle(card.Effective[0]);
            card.Submit();
            card.Rows[0].Count.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Move_Between_Options_And_Space_Picks()
    {
        var (card, window) = Show();
        try
        {
            card.Rows[0].Row.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            card.Rows[1].Row.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            card.Rows[^1].Row.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.SelectedOptions.ShouldBe([card.Effective[^1]]);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            card.IsSubmitted.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Live_Count_Change_Updates_The_Row()
    {
        var option = new FeatureVotingOption("x", "X", null, 5);
        var (card, window) = Show(c => c.Options.Add(option));
        try
        {
            option.Votes = 9;
            card.Rows[0].Count.Value.ShouldBe(9);
            card.TotalVotes.ShouldBe(9);
        }
        finally
        {
            window.Close();
        }
    }
}
