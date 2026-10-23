using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The vote, the standings and their popover, reveal and the blind privacy rule.</summary>
public class ModelArenaVoteTests
{
    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Executed { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Executed.Add(parameter);
    }

    /// <summary>Every string a person or a screen reader could get from the window and the arena's trees.</summary>
    private static List<string> Strings(ModelArenaRig rig)
    {
        var strings = new List<string>();
        var controls = new HashSet<Control>();
        foreach (var visual in rig.Window.GetVisualDescendants())
        {
            if (visual is Control control)
            {
                controls.Add(control);
            }
        }
        foreach (var logical in rig.Arena.GetLogicalDescendants())
        {
            if (logical is Control control)
            {
                controls.Add(control);
            }
        }
        foreach (var control in controls)
        {
            Add(strings, AutomationProperties.GetName(control));
            Add(strings, AutomationProperties.GetHelpText(control));
            Add(strings, AutomationProperties.GetItemStatus(control));
            Add(strings, ToolTip.GetTip(control) as string);
            Add(strings, Tooltip.GetTip(control) as string);
            switch (control)
            {
                case TextBlock block:
                    Add(strings, block.Text);
                    if (block.Inlines is { } inlines)
                    {
                        foreach (var inline in inlines)
                        {
                            Add(strings, (inline as Run)?.Text);
                        }
                    }
                    break;
                case TextBox box:
                    Add(strings, box.Text);
                    break;
                case ContentControl content:
                    Add(strings, content.Content as string);
                    break;
            }
        }
        Walk(ControlAutomationPeer.CreatePeerForElement(rig.Arena), strings, 0);
        return strings;
    }

    private static void Walk(AutomationPeer peer, List<string> strings, int depth)
    {
        Add(strings, peer.GetName());
        Add(strings, peer.GetHelpText());
        if (depth > 40)
        {
            return;
        }
        foreach (var child in peer.GetChildren())
        {
            Walk(child, strings, depth + 1);
        }
    }

    private static void Add(List<string> strings, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            strings.Add(text);
        }
    }

    private static void AssertNoNames(ModelArenaRig rig, params AiModel[] models)
    {
        var strings = Strings(rig);
        foreach (var model in models)
        {
            foreach (var name in new[] { model.Name, model.ShortName, model.DisplayName })
            {
                if (!string.IsNullOrEmpty(name))
                {
                    Assert.DoesNotContain(strings, s => s.Contains(name, StringComparison.Ordinal));
                }
            }
        }
    }

    [AvaloniaFact]
    public void A_Vote_Scores_Both_Models_Reveals_Them_And_Is_Reported_Once()
    {
        var command = new RecordingCommand();
        using var rig = new ModelArenaRig(arena => arena.VoteCommand = command);
        var arena = rig.Arena;
        var cast = new List<ArenaVote>();
        arena.VoteCast += (_, e) => cast.Add(e.Vote);
        rig.RoundToVote();
        Assert.False(arena.HasUnseenResult);

        arena.Vote(ArenaChoice.A);
        rig.Pump();

        var vote = Assert.Single(cast);
        Assert.Same(vote, Assert.Single(command.Executed));
        Assert.Same(vote, arena.LastVote);
        Assert.Equal(1, vote.Round);
        Assert.Equal(ModelArenaSamples.Prompts()[0].Text, vote.Prompt);
        Assert.Equal("Lumen 3 Pro", vote.ModelA.Name);
        Assert.Equal("Kite 5", vote.ModelB.Name);
        Assert.Same(vote.ModelA, vote.Winner);
        Assert.Equal(ArenaChoice.A, vote.Choice);
        Assert.Same(arena.PaneA.Metrics, vote.MetricsA);
        Assert.Same(arena.PaneB.Metrics, vote.MetricsB);
        Assert.Equal(new ArenaRecord(1, 0, 0), arena.Standings["lumen-pro"]);
        Assert.Equal(new ArenaRecord(0, 0, 1), arena.Standings["kite-5"]);
        Assert.Equal(ArenaPhase.Voted, arena.Phase);
        Assert.True(arena.HasUnseenResult);
        Assert.True(arena.PaneViewA!.IsRevealed);
        Assert.True(arena.PaneViewB!.IsRevealed);
        Assert.Equal(ArenaVoteKind.Voted, arena.VoteBar!.Kind);
        Assert.Equal("You chose", arena.VoteBar.ResultPrefixText);
        Assert.Equal("Lumen 3 Pro", arena.VoteBar.ResultName);
        Assert.Same(vote.ModelA.Mark, arena.VoteBar.ResultMark);
        Assert.Equal("You chose Lumen 3 Pro. Model A was Lumen 3 Pro, model B was Kite 5. Standings updated.", arena.LastAnnouncement);

        // A second vote, or a vote outside the voting phase, is ignored.
        arena.Vote(ArenaChoice.B);
        Assert.Single(cast);
        Assert.Equal(new ArenaRecord(1, 0, 0), arena.Standings["lumen-pro"]);
        _ = rig.Run();
        arena.Vote(ArenaChoice.B);
        Assert.Single(cast);
    }

    [AvaloniaFact]
    public void Each_Choice_Scores_As_The_Spec_Says()
    {
        (ArenaChoice Choice, ArenaRecord A, ArenaRecord B, string Result)[] cases =
        [
            (ArenaChoice.B, new(0, 0, 1), new(1, 0, 0), "You chose Kite 5"),
            (ArenaChoice.Tie, new(0, 1, 0), new(0, 1, 0), "Called a tie"),
            (ArenaChoice.BothBad, new(0, 0, 1), new(0, 0, 1), "Marked both as bad")
        ];
        foreach (var (choice, a, b, result) in cases)
        {
            using var rig = new ModelArenaRig();
            rig.RoundToVote();
            rig.Arena.Vote(choice);
            rig.Pump();
            Assert.Equal(a, rig.Arena.Standings["lumen-pro"]);
            Assert.Equal(b, rig.Arena.Standings["kite-5"]);
            Assert.StartsWith(result + ". Model A was Lumen 3 Pro", rig.Arena.LastAnnouncement);
            if (choice == ArenaChoice.B)
            {
                Assert.Equal("Kite 5", rig.Arena.VoteBar!.ResultName);
            }
            else
            {
                Assert.Null(rig.Arena.LastVote!.Winner);
                Assert.Null(rig.Arena.VoteBar!.ResultName);
                Assert.Equal(result, rig.Arena.VoteBar.ResultText);
            }
        }
    }

    [AvaloniaFact]
    public void A_Keyboard_Vote_Moves_The_Focus_To_Next_Round_And_Next_Round_To_Stop()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        rig.RoundToVote();
        Assert.True(arena.VoteBar!.FocusFirstVote());
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(ArenaPhase.Voted, arena.Phase);
        Assert.Equal(ArenaChoice.A, arena.LastVote!.Choice);
        Assert.True(rig.FocusIsIn(arena.VoteBar.ActionButton));

        rig.Key(PhysicalKey.Enter);
        Assert.Equal(2, arena.Round);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.True(rig.FocusIsIn(arena.Form!.RunButton));
        Assert.True(arena.Form.RunButton!.IsRunning);
        Assert.Equal("Stop", AutomationProperties.GetName(arena.Form.RunButton));
    }

    [AvaloniaFact]
    public void Blind_Off_Reveals_At_Once_And_Blind_On_Hides_Again_Even_After_The_Vote()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        Assert.Contains(":blind", arena.Classes);
        Assert.False(arena.PaneViewA!.IsRevealed);
        Assert.True(arena.BlindSwitch!.IsChecked);

        arena.IsBlind = false;
        Assert.DoesNotContain(":blind", arena.Classes);
        Assert.False(arena.BlindSwitch.IsChecked);
        Assert.True(arena.PaneViewA.IsRevealed);
        Assert.True(arena.PaneViewB!.IsRevealed);
        Assert.True(arena.Tabs!.IsRevealed);
        arena.IsBlind = true;
        Assert.False(arena.PaneViewA.IsRevealed);
        Assert.False(arena.Tabs.IsRevealed);

        rig.Finish();
        arena.Vote(ArenaChoice.Tie);
        Assert.True(arena.PaneViewA.IsRevealed);
        arena.IsBlind = false;
        Assert.True(arena.PaneViewA.IsRevealed);
        arena.IsBlind = true;
        Assert.False(arena.PaneViewA.IsRevealed);

        // With Blind off a new round shows its models from the start; the vote still scores.
        arena.IsBlind = false;
        _ = rig.Run();
        Assert.True(arena.PaneViewA.IsRevealed);
        rig.Finish();
        arena.Vote(ArenaChoice.A);
        Assert.Equal(new ArenaRecord(1, 1, 0), arena.Standings["lumen-pro"]);
    }

    [AvaloniaFact]
    public void The_Switch_And_Its_Word_Turn_Blind_Off_And_On()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        rig.Click(arena.BlindSwitch!);
        Assert.False(arena.IsBlind);
        rig.Click(arena.BlindHost!.GetVisualDescendants().OfType<TextBlock>().First());
        Assert.True(arena.IsBlind);
        Assert.True(arena.BlindSwitch!.IsChecked);
        Assert.Equal("Blind", AutomationProperties.GetName(arena.BlindSwitch));
    }

    [AvaloniaFact]
    public void Before_The_Vote_No_Tree_Holds_A_Model_Name()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        rig.RoundToVote();
        var pair = arena.CurrentPair!.Value;
        AssertNoNames(rig, pair.A, pair.B);
        Assert.Null(arena.VoteBar!.ResultName);

        // The open standings may name models; once closed they leave the trees again.
        rig.Click(arena.LeaderboardButton!);
        Assert.True(arena.IsLeaderboardOpen);
        Assert.Contains(Strings(rig), s => s.Contains("Lumen 3 Pro", StringComparison.Ordinal));
        rig.Key(PhysicalKey.Escape);
        rig.Frame(30);
        Assert.False(arena.IsLeaderboardOpen);
        AssertNoNames(rig, pair.A, pair.B);

        arena.Vote(ArenaChoice.A);
        rig.Pump();
        Assert.Contains(Strings(rig), s => s.Contains("Lumen 3 Pro", StringComparison.Ordinal));

        // The next round hides them again.
        arena.NextRound();
        rig.Pump();
        var next = arena.CurrentPair!.Value;
        AssertNoNames(rig, next.A, next.B);
    }

    [AvaloniaFact]
    public void The_Standings_Rank_By_Win_Rate_Then_Wins_Then_Name()
    {
        using var rig = new ModelArenaRig(arena =>
        {
            ModelArenaSamples.PresetStandings(arena);
            arena.Standings["marlin-xl"] = new ArenaRecord(1, 0, 3);
            arena.Standings["lumen-swift"] = new ArenaRecord(1, 0, 3);
            arena.Standings["nobody"] = new ArenaRecord(9, 0, 0);
            arena.Standings["vireo-4"] = new ArenaRecord(1, 0, 2);
            arena.LeaderboardSize = 7;
        });
        var arena = rig.Arena;
        arena.IsLeaderboardOpen = true;
        rig.Pump();
        var items = arena.StandingsView!.Items!;
        Assert.Equal(
            ["Lumen 3 Pro", "Kite 5", "Basalt 2 Ultra", "Vireo 4", "Lumen 3 Swift", "Marlin XL", "Tern 1.2 Flash"],
            items.Select(item => item.Name).ToArray());
        Assert.Equal(0.7, items[0].Value, 6);
        Assert.Equal("Rank 1, Lumen 3 Pro, win rate 70%", items[0].AutomationName);
        // Series tones by provider in pool order: lumen-labs 3, kitework 4, basalt-ai 1, vireo 2, tern-research 3, marlin-systems 4.
        Assert.Equal([3, 4, 1, 2, 3, 4, 3], items.Select(item => item.Tone).ToArray());
        // Every record counts toward the votes, also one whose model is not in the pool: 35 games, two per vote, rounded up.
        Assert.Equal("18 votes", arena.StandingsView.VotesText);
        Assert.True(arena.StandingsView.CanReset);

        arena.LeaderboardSize = 3;
        rig.Pump();
        Assert.Equal(3, arena.StandingsView.Items!.Count);
    }

    [AvaloniaFact]
    public void The_Standings_Follow_Changes_While_Open()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        arena.IsLeaderboardOpen = true;
        rig.Pump();
        arena.Standings["marlin-xl"] = new ArenaRecord(4, 0, 0);
        rig.Pump();
        Assert.Equal("Marlin XL", arena.StandingsView!.Items![0].Name);
        Assert.Equal("11 votes", arena.StandingsView.VotesText);
    }

    [AvaloniaFact]
    public void Opening_After_A_Vote_Replays_Once_And_The_First_Look_Grows_From_Zero()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings, ArcMotionLevel.Full);
        var arena = rig.Arena;
        rig.RoundToVote();
        arena.Vote(ArenaChoice.B);
        rig.Frame(30);
        Assert.True(arena.HasUnseenResult);
        Assert.Contains(":unseen", arena.Classes);
        Assert.Equal("Standings, updated", AutomationProperties.GetName(arena.LeaderboardButton!));

        rig.Click(arena.LeaderboardButton!);
        rig.Frame();
        var list = arena.StandingsView!.List!;
        Assert.True(list.IsReplaying);
        Assert.False(arena.HasUnseenResult);
        Assert.DoesNotContain(":unseen", arena.Classes);
        Assert.Equal("Standings", AutomationProperties.GetName(arena.LeaderboardButton!));
        rig.Advance(TimeSpan.FromSeconds(2));
        Assert.False(list.IsReplaying);

        // Closed and opened again without a vote: no replay.
        rig.Click(arena.LeaderboardButton!);
        rig.Advance(TimeSpan.FromSeconds(1));
        Assert.False(arena.IsLeaderboardOpen);
        rig.Click(arena.LeaderboardButton!);
        rig.Frame();
        Assert.True(arena.IsLeaderboardOpen);
        Assert.False(list.IsReplaying);
        Assert.Equal(5, list.ShownRows.Count);

        // After the next vote it replays again.
        rig.Click(arena.LeaderboardButton!);
        rig.Advance(TimeSpan.FromSeconds(1));
        arena.NextRound();
        rig.Pump();
        rig.Finish();
        arena.Vote(ArenaChoice.A);
        rig.Frame(30);
        rig.Click(arena.LeaderboardButton!);
        rig.Frame();
        Assert.True(list.IsReplaying);
        // Let it end before the window closes: closing mid-replay while a row drops out of the list throws inside
        // RankedBarList (see the notes; its owner fixes it).
        rig.Advance(TimeSpan.FromSeconds(2));
        Assert.False(list.IsReplaying);
    }

    [AvaloniaFact]
    public void Reset_Empties_The_Standings_And_Keeps_The_Focus_In_The_Popover()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        var resets = 0;
        arena.LeaderboardReset += (_, _) => resets++;
        rig.Click(arena.LeaderboardButton!);
        var standings = arena.StandingsView!;
        Assert.True(rig.FocusIsIn(standings));
        Assert.Equal("9 votes", standings.VotesText);

        standings.ResetButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);

        Assert.Empty(arena.Standings);
        Assert.Equal(1, resets);
        Assert.Equal("Standings cleared.", arena.LastAnnouncement);
        Assert.True(standings.ShowsEmpty);
        Assert.False(standings.CanReset);
        Assert.Equal("No votes yet", standings.VotesText);
        Assert.True(arena.IsLeaderboardOpen);
        Assert.True(rig.FocusIsIn(standings));

        arena.ResetLeaderboard();
        Assert.Equal(1, resets);
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Standings_And_Returns_The_Focus_To_The_Button()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        arena.LeaderboardButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.True(arena.IsLeaderboardOpen);
        Assert.True(arena.StandingsPopover!.IsOpen);
        Assert.Contains(":leaderboard-open", arena.Classes);
        Assert.True(rig.FocusIsIn(arena.StandingsView));

        rig.Key(PhysicalKey.Escape);
        Assert.False(arena.IsLeaderboardOpen);
        Assert.DoesNotContain(":leaderboard-open", arena.Classes);
        Assert.True(rig.FocusIsIn(arena.LeaderboardButton));

        arena.IsLeaderboardOpen = true;
        rig.Pump();
        Assert.True(arena.StandingsPopover.IsOpen);
        arena.IsLeaderboardOpen = false;
        rig.Pump();
        Assert.False(arena.StandingsPopover.IsOpen);
    }

    [AvaloniaFact]
    public void The_Standings_Popover_Has_No_Padding_Of_Its_Own_And_Sits_Below_The_Button_At_Its_End()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        rig.Click(arena.LeaderboardButton!);
        var surface = arena.StandingsPopover!.Surface!;
        Assert.Equal(default, surface.Padding);
        Assert.Null(surface.ContentTemplate);
        var button = arena.LeaderboardButton!;
        var buttonBox = new Rect(button.TranslatePoint(default, rig.Window)!.Value, button.Bounds.Size);
        var surfaceBox = new Rect(surface.TranslatePoint(default, rig.Window)!.Value, surface.Bounds.Size);
        Assert.Equal(320, surfaceBox.Width, 1);
        Assert.Equal(buttonBox.Bottom + 8, surfaceBox.Top, 1);
        Assert.Equal(buttonBox.Right, surfaceBox.Right, 1);
        Assert.Equal("Standings", AutomationProperties.GetName(surface));
    }
}
