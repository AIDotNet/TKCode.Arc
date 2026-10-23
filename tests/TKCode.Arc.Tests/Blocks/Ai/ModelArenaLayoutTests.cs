using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The arena's geometry, its narrow and compact layouts, its words and accessible names.</summary>
public class ModelArenaLayoutTests
{
    private static Rect BoxOf(ModelArenaRig rig, Visual visual) =>
        new(visual.TranslatePoint(default, rig.Window)!.Value, visual.Bounds.Size);

    private static void Resize(ModelArenaRig rig, double width)
    {
        rig.Host.Width = width;
        rig.Pump();
    }

    [AvaloniaFact]
    public void The_Wide_Card_Has_The_Header_Prompt_Row_Stage_And_Vote_Strip_Of_The_Spec()
    {
        using var rig = new ModelArenaRig(width: 784);
        var arena = rig.Arena;
        rig.RoundToVote();
        Assert.Equal(784, arena.Bounds.Width);
        Assert.Equal(new Thickness(1), arena.BorderThickness);
        Assert.Equal(26, arena.CornerRadius.TopLeft);
        var head = arena.TitleText!.Parent!.Parent!.Parent as Border;
        Assert.NotNull(head);
        Assert.Equal(52, head.Bounds.Height);
        Assert.Equal(new Thickness(20, 8, 10, 0), head.Padding);
        Assert.Equal(106, arena.Form!.Bounds.Height, 0);
        var stage = arena.Stage!;
        Assert.Equal(new Thickness(0, 1, 0, 0), ((Border)stage.Parent!).BorderThickness);
        var a = BoxOf(rig, arena.PaneViewA!);
        var b = BoxOf(rig, arena.PaneViewB!);
        // 782 inside the border: 390, the 1px line, 391.
        Assert.Equal(390, a.Width);
        Assert.Equal(391, b.Width);
        Assert.Equal(a.Right + 1, b.Left);
        Assert.Equal(272, arena.PaneViewA!.AnswerHeight);
        Assert.False(arena.Tabs!.IsVisible);
        Assert.Equal(new Thickness(0, 1, 0, 0), arena.VoteBar!.BorderThickness);
        Assert.Equal(new Thickness(20, 12, 12, 12), arena.VoteBar.Padding);
        Assert.Equal("Arena", arena.TitleText.Text);
        Assert.Equal("Standings", arena.LeaderboardText!.Text);
        Assert.Equal(new Thickness(10, 0), arena.LeaderboardButton!.Padding);
        Assert.Equal(36, arena.LeaderboardButton.Bounds.Height);
    }

    [AvaloniaFact]
    public void The_Flat_Card_Drops_Its_Border_And_Both_Rules()
    {
        using var rig = new ModelArenaRig(arena => arena.Classes.Add("flat"));
        rig.RoundToVote();
        Assert.Equal(default, rig.Arena.BorderThickness);
        Assert.Equal(default, ((Border)rig.Arena.Stage!.Parent!).BorderThickness);
        Assert.Equal(default, rig.Arena.VoteBar!.BorderThickness);
        Assert.True(rig.Arena.Stage.Divider!.IsVisible);
    }

    [AvaloniaFact]
    public void Narrow_And_Compact_Follow_The_Arena_Width_With_One_Pixel_Of_Hysteresis()
    {
        using var rig = new ModelArenaRig(width: 600);
        var arena = rig.Arena;
        Assert.False(arena.IsNarrowLayout);
        Resize(rig, 560);
        Assert.False(arena.IsNarrowLayout);
        Resize(rig, 559);
        Assert.True(arena.IsNarrowLayout);
        Assert.Contains(":narrow", arena.Classes);
        Assert.DoesNotContain(":compact", arena.Classes);
        Resize(rig, 560);
        Assert.True(arena.IsNarrowLayout);
        Resize(rig, 561);
        Assert.False(arena.IsNarrowLayout);

        Resize(rig, 480);
        Assert.True(arena.IsNarrowLayout);
        Assert.False(arena.IsCompactLayout);
        Resize(rig, 479);
        Assert.True(arena.IsCompactLayout);
        Assert.Contains(":compact", arena.Classes);
        Resize(rig, 480);
        Assert.True(arena.IsCompactLayout);
        Resize(rig, 481);
        Assert.False(arena.IsCompactLayout);
        Assert.True(arena.IsNarrowLayout);
    }

    [AvaloniaFact]
    public void The_Narrow_Arena_Shows_The_Tabs_And_The_Selected_Answer_Only()
    {
        using var rig = new ModelArenaRig(width: 538);
        var arena = rig.Arena;
        _ = rig.Run();
        Assert.True(arena.Tabs!.IsVisible);
        Assert.Same(arena.PaneA, arena.Tabs.PaneA);
        Assert.Same(arena.PaneB, arena.Tabs.PaneB);
        Assert.Equal("Answers", arena.Tabs.TabsName);
        Assert.True(arena.PaneViewA!.IsVisible);
        Assert.False(arena.PaneViewB!.IsVisible);
        Assert.False(arena.Stage!.Divider!.IsVisible);
        Assert.False(arena.PaneViewA.ShowsHeader);
        Assert.False(arena.PaneViewB.ShowsHeader);
        Assert.True(arena.VoteBar!.IsNarrow);
        var tabs = BoxOf(rig, arena.Tabs);
        var pane = BoxOf(rig, arena.PaneViewA);
        Assert.True(pane.Top >= tabs.Bottom);

        // The hidden answer keeps streaming.
        rig.Push(rig.B, "Hidden words keep coming. ");
        rig.Frame(4);
        Assert.StartsWith("Hidden words", arena.PaneB.Text);

        arena.Tabs.SelectedSlot = ArenaSlot.B;
        rig.Pump();
        Assert.False(arena.PaneViewA.IsVisible);
        Assert.True(arena.PaneViewB.IsVisible);
        Assert.Equal(1, arena.PaneViewB.Opacity);

        // Back to wide: both answers, no tabs.
        Resize(rig, 700);
        Assert.False(arena.Tabs.IsVisible);
        Assert.True(arena.PaneViewA.IsVisible);
        Assert.True(arena.PaneViewB.IsVisible);
        Assert.True(arena.PaneViewA.ShowsHeader);
        Assert.True(arena.PaneViewB.ShowsHeader);
    }

    [AvaloniaFact]
    public void A_Tab_Switch_Fades_The_New_Answer_In_Over_160ms_At_Full_Motion()
    {
        using var rig = new ModelArenaRig(width: 538, level: ArcMotionLevel.Full);
        var arena = rig.Arena;
        arena.Tabs!.SelectedSlot = ArenaSlot.B;
        rig.Frame();
        Assert.True(arena.PaneViewB!.IsVisible);
        Assert.False(arena.PaneViewA!.IsVisible);
        Assert.InRange(arena.PaneViewB.Opacity, 0.01, 0.9);
        rig.Frame(12);
        Assert.Equal(1, arena.PaneViewB.Opacity, 3);
    }

    [AvaloniaFact]
    public void The_Compact_Arena_Tightens_The_Header_And_Keeps_The_Leaderboard_As_An_Icon()
    {
        using var rig = new ModelArenaRig(width: 358);
        var arena = rig.Arena;
        Assert.True(arena.IsCompactLayout);
        Assert.True(arena.Form!.IsCompact);
        Assert.True(arena.VoteBar!.IsCompact);
        var button = arena.LeaderboardButton!;
        Assert.Equal(36, button.Bounds.Width);
        Assert.Equal(36, button.Bounds.Height);
        Assert.False(arena.LeaderboardText!.IsVisible);
        Assert.Equal("Standings", AutomationProperties.GetName(button));
        var head = (Border)arena.TitleText!.Parent!.Parent!.Parent!;
        Assert.Equal(new Thickness(20, 8, 6, 0), head.Padding);
    }

    [AvaloniaFact]
    public void Every_Word_Comes_From_The_Labels()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        arena.Labels = new ModelArenaLabels
        {
            Title = "Arène",
            RoundWord = "Manche",
            RoundName = round => $"Manche {round}",
            Leaderboard = "Classement",
            Blind = "À l'aveugle",
            Placeholder = "Posez une question",
            Run = "Lancer",
            Waiting = "Attendez"
        };
        rig.Pump();
        _ = rig.Run();
        Assert.Equal("Arène", arena.TitleText!.Text);
        Assert.Equal("Manche", arena.RoundWord!.Text);
        Assert.Equal("Manche 1", AutomationProperties.GetName(arena.RoundWord));
        Assert.Equal("Classement", AutomationProperties.GetName(arena.LeaderboardButton!));
        Assert.Equal("À l'aveugle", AutomationProperties.GetName(arena.BlindSwitch!));
        Assert.Equal("Posez une question", arena.Form!.Placeholder);
        Assert.Equal("Lancer", arena.Form.RunText);
        Assert.Equal("Attendez", arena.VoteBar!.PendingText);
        Assert.Same(arena.Labels, arena.PaneViewA!.Labels);
        Assert.Same(arena.Labels, arena.Tabs!.Labels);
    }

    [AvaloniaFact]
    public void The_Parts_Carry_Their_Accessible_Names_And_The_Live_Region_Is_Polite()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        Assert.Equal("Prompt sent to both models", AutomationProperties.GetName(arena.Form!.Box!));
        Assert.Equal("Sample prompts", AutomationProperties.GetName(arena.Form.ChipsPanel!));
        Assert.Equal("Run", AutomationProperties.GetName(arena.Form.RunButton!));
        Assert.Equal("Standings", AutomationProperties.GetName(arena.LeaderboardButton!));
        Assert.Equal("Blind", AutomationProperties.GetName(arena.BlindSwitch!));
        Assert.Equal("Standings", AutomationProperties.GetName(arena.StandingsPopover!));
        var announcer = TestHelpers.Descendant<AiAnnouncer>(arena);
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(announcer));
        rig.RoundToVote();
        Assert.Equal("Which answer wins?", arena.VoteBar!.QuestionText);
        Assert.Equal("Which answer wins?", AutomationProperties.GetName(arena.VoteBar.Choices!));
        Assert.Equal("A is better", AutomationProperties.GetName(arena.VoteBar.VoteButtons[0]));
    }

    [AvaloniaFact]
    public void Focus_Prompt_Puts_The_Focus_In_The_Box()
    {
        using var rig = new ModelArenaRig();
        rig.Arena.FocusPrompt();
        rig.Pump();
        Assert.True(rig.FocusIsIn(rig.Arena.Form!.Box));
    }

    [AvaloniaFact]
    public void The_Announcements_Follow_The_Round()
    {
        using var rig = new ModelArenaRig(ModelArenaSamples.PresetStandings);
        var arena = rig.Arena;
        var said = new List<string>();
        var announcer = TestHelpers.Descendant<AiAnnouncer>(arena);
        announcer.PropertyChanged += (_, e) =>
        {
            if (e.Property == AutomationProperties.NameProperty && e.NewValue is string text)
            {
                said.Add(text.TrimEnd('​'));
            }
        };
        rig.RoundToVote();
        arena.Vote(ArenaChoice.BothBad);
        _ = rig.Run();
        arena.Stop();
        arena.ResetLeaderboard();
        Assert.Equal(
        [
            "Round started. Two hidden models are answering.",
            "Model B finished first.",
            "Both answers are in. Vote to see the models.",
            "Marked both as bad. Model A was Lumen 3 Pro, model B was Kite 5. Standings updated.",
            "Round started. Two hidden models are answering.",
            "Round stopped.",
            "Standings cleared."
        ], said);
    }
}
