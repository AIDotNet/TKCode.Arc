using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ArenaStandings"/>: header, rows and footnote geometry, the empty state, Reset (enabled with votes, the
/// request, focus kept in the popover when it turns itself off), the body's height spring and crossfade between rows and
/// the empty text, the replay passing through, and the container's accessible name.
/// </summary>
public class ArenaStandingsTests
{
    private static ArenaStandings Create(RankedBarItem[]? items = null, Action<ArenaStandings>? configure = null)
    {
        var standings = RankedBarSamples.Popover(items, configure);
        standings.HorizontalAlignment = HorizontalAlignment.Left;
        standings.VerticalAlignment = VerticalAlignment.Top;
        return standings;
    }

    private static Window Show(ArenaStandings standings, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(standings, level);
        window.Width = 400;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static double Y(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y;

    private static double X(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.X;

    [AvaloniaFact]
    public void Header_Rows_And_Footnote_Follow_The_Card_Geometry()
    {
        var standings = Create();
        var window = Show(standings);
        try
        {
            standings.Bounds.Width.ShouldBe(318);
            var title = TestHelpers.Descendant<TextBlock>(standings, "PART_Title");
            var votes = TestHelpers.Descendant<TextBlock>(standings, "PART_Votes");
            var foot = TestHelpers.Descendant<Border>(standings, "PART_Foot");
            var footnote = TestHelpers.Descendant<TextBlock>(standings, "PART_Footnote");
            var reset = standings.ResetButton!;
            title.Text.ShouldBe("Standings");
            votes.Text.ShouldBe("15 votes");
            footnote.Text.ShouldBe("Ties count as half a win");
            TestHelpers.Descendant<TextBlock>(standings, "PART_ResetText").Text.ShouldBe("Reset");

            // 14 16 8 inside; header 16.8 (the 12 / 16.8 count) and 6 below it.
            X(title, standings).ShouldBe(16);
            (X(votes, standings) + votes.Bounds.Width).ShouldBe(318 - 16, 0.01);
            var body = standings.BodySize!;
            // Layout rounding puts the 16.8 header on whole pixels.
            Y(body, standings).ShouldBe(14 + 16.8 + 6, 0.5);
            body.Bounds.Height.ShouldBe(5 * 36, 0.01);
            body.Bounds.Width.ShouldBe(318 - 32, 0.01);
            // Footnote: 6 below the body, a 1px line, 4 more, then the 36px Reset row; 8 below.
            Y(foot, standings).ShouldBe(14 + 16.8 + 6 + 180 + 6, 0.5);
            foot.BorderThickness.ShouldBe(new Thickness(0, 1, 0, 0));
            reset.Bounds.Height.ShouldBe(36);
            standings.Bounds.Height.ShouldBe(14 + 16.8 + 6 + 180 + 6 + 1 + 4 + 36 + 8, 0.5);
            // Reset reaches 10 into the right padding.
            (X(reset, standings) + reset.Bounds.Width).ShouldBe(318 - 16 + 10, 0.01);
            reset.Variant.ShouldBe(ButtonVariant.Ghost);
            reset.Size.ShouldBe(ControlSize.Small);

            title.FontFamily.ShouldBe(ArcTokens.Get<Avalonia.Media.FontFamily>(standings, ArcDesignTokenKind.DisplayFontFamily));
            title.FontWeight.ShouldBe(Avalonia.Media.FontWeight.Medium);
            title.FontSize.ShouldBe(14);
            votes.FontSize.ShouldBe(12);
            votes.FontFeatures.ShouldNotBeNull().ShouldContain(f => f.Tag == "tnum");
            TestHelpers.ColorOf(votes.Foreground).ShouldBe(ArcTokens.GetColor(standings, ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(footnote.Foreground).ShouldBe(ArcTokens.GetColor(standings, ArcDesignTokenKind.TextMuted));
            footnote.FontSize.ShouldBe(12);
            TestHelpers.ColorOf(foot.BorderBrush).ShouldBe(ArcTokens.GetColor(standings, ArcDesignTokenKind.BorderSubtle));

            standings.ShowsEmpty.ShouldBeFalse();
            standings.List!.IsVisible.ShouldBeTrue();
            standings.EmptyBlock!.IsVisible.ShouldBeFalse();
            standings.List.ShownRows.Count.ShouldBe(5);
            standings.Classes.Contains(ArenaStandings.EmptyPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Rows_The_Body_Is_The_Empty_Text_And_Reset_Is_Off()
    {
        var standings = Create([]);
        var window = Show(standings);
        try
        {
            standings.ShowsEmpty.ShouldBeTrue();
            standings.Classes.Contains(ArenaStandings.EmptyPseudoClass).ShouldBeTrue();
            var empty = standings.EmptyBlock!;
            empty.IsVisible.ShouldBeTrue();
            empty.Opacity.ShouldBe(1);
            standings.List!.IsVisible.ShouldBeFalse();
            empty.Text.ShouldBe("No votes yet. Judge a round and the models land here, ranked by win rate.");
            TestHelpers.ColorOf(empty.Foreground).ShouldBe(ArcTokens.GetColor(standings, ArcDesignTokenKind.TextSecondary));
            empty.LineHeight.ShouldBe(19.6);
            empty.TextWrapping.ShouldBe(Avalonia.Media.TextWrapping.Wrap);
            // The text sits 2 down and the body is as tall as the text (Arc's 39 for two lines).
            Y(empty, standings.BodySize!).ShouldBe(2, 0.01);
            standings.BodySize!.Bounds.Height.ShouldBe(empty.Bounds.Height, 0.01);
            standings.ResetButton!.IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(standings, "PART_Votes").Text.ShouldBe("No votes yet");

            // Rows arrive: the list comes back, the text goes.
            standings.Items = RankedBarSamples.Standings();
            standings.CanReset = true;
            Dispatcher.UIThread.RunJobs();
            standings.ShowsEmpty.ShouldBeFalse();
            standings.List.IsVisible.ShouldBeTrue();
            standings.List.Opacity.ShouldBe(1);
            empty.IsVisible.ShouldBeFalse();
            standings.BodySize.Bounds.Height.ShouldBe(180, 0.01);
            standings.ResetButton.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Raises_The_Request()
    {
        var standings = Create();
        var window = Show(standings);
        try
        {
            var requests = 0;
            standings.ResetRequested += (_, _) => requests++;
            var reset = standings.ResetButton!;
            reset.IsEnabled.ShouldBeTrue();
            reset.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            requests.ShouldBe(1);
            reset.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            requests.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reset_That_Turns_Itself_Off_Hands_Focus_To_The_Container()
    {
        var standings = Create();
        var window = Show(standings);
        try
        {
            standings.ResetRequested += (_, _) =>
            {
                standings.Items = [];
                standings.CanReset = false;
                standings.VotesText = "No votes yet";
            };
            var reset = standings.ResetButton!;
            reset.Focus(NavigationMethod.Tab);
            reset.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            reset.IsEnabled.ShouldBeFalse();
            standings.ShowsEmpty.ShouldBeTrue();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(standings);
            standings.IsTabStop.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FocusDefault_Focuses_The_Container()
    {
        var standings = Create();
        var window = Show(standings);
        try
        {
            standings.FocusDefault().ShouldBeTrue();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(standings);
            standings.FocusAdorner.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Crossfades_To_The_Empty_Text_While_The_Body_Springs_To_Its_Height()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var standings = Create();
        var window = Show(standings, ArcMotionLevel.Full);
        try
        {
            var body = standings.BodySize!;
            var list = standings.List!;
            var empty = standings.EmptyBlock!;
            body.Bounds.Height.ShouldBe(180, 0.01);
            standings.Items = [];
            standings.CanReset = false;
            Dispatcher.UIThread.RunJobs();
            // The rows stay drawn (and keep their items) while they fade; the text is the body now.
            standings.ShowsEmpty.ShouldBeTrue();
            list.IsVisible.ShouldBeTrue();
            list.ShownRows.Count.ShouldBe(5);
            empty.IsVisible.ShouldBeTrue();
            empty.Opacity.ShouldBe(0);

            CoreFrames.Run(clock, 1);
            list.Opacity.ShouldBeInRange(0.01, 0.99);
            empty.Opacity.ShouldBeInRange(0.01, 0.99);
            CoreFrames.Run(clock, 3);
            Dispatcher.UIThread.RunJobs();
            body.Bounds.Height.ShouldBeInRange(empty.Bounds.Height + 1, 179);

            CoreFrames.Run(clock, 8);
            Dispatcher.UIThread.RunJobs();
            // Gone after 100ms: hidden, its rows released.
            list.IsVisible.ShouldBeFalse();
            list.Items.ShouldBeNull();
            list.ShownRows.ShouldBeEmpty();

            CoreFrames.Run(clock, 60);
            Dispatcher.UIThread.RunJobs();
            empty.Opacity.ShouldBe(1);
            body.Bounds.Height.ShouldBe(empty.Bounds.Height, 0.01);
            body.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_In_120ms_And_The_Height_Jumps()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var standings = Create();
        var window = Show(standings, ArcMotionLevel.Reduced);
        try
        {
            var empty = standings.EmptyBlock!;
            standings.Items = [];
            Dispatcher.UIThread.RunJobs();
            standings.BodySize!.Bounds.Height.ShouldBe(empty.Bounds.Height, 0.01);
            CoreFrames.Run(clock, 4);
            empty.Opacity.ShouldBeInRange(0.4, 0.7);
            CoreFrames.Run(clock, 6);
            Dispatcher.UIThread.RunJobs();
            empty.Opacity.ShouldBe(1);
            standings.List!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replay_Passes_Through_To_The_Rows()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var standings = Create();
        var window = Show(standings, ArcMotionLevel.Full);
        try
        {
            standings.Replay(RankedBarSamples.Earlier());
            Dispatcher.UIThread.RunJobs();
            var list = standings.List!;
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            list.ShownRows[0].Item.Id.ShouldBe("basalt-ultra");
            CoreFrames.Run(clock, 90);
            Dispatcher.UIThread.RunJobs();
            list.ShownRows[0].Item.Id.ShouldBe("lumen-pro");
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
            // Nothing to replay without rows.
            standings.Items = [];
            standings.Replay(RankedBarSamples.Earlier());
            list.Phase.ShouldBe(RankedBarReplayPhase.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Replay_Before_The_Template_Plays_When_Shown_And_Limit_Reaches_The_Rows()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var standings = Create(RankedBarSamples.Six(), s => s.Limit = 3);
        standings.Replay(RankedBarSamples.Earlier());
        var window = Show(standings, ArcMotionLevel.Full);
        try
        {
            var list = standings.List!;
            list.Limit.ShouldBe(3);
            list.Phase.ShouldBe(RankedBarReplayPhase.Holding);
            list.ShownRows.Select(r => r.Item.Id).ShouldBe(["basalt-ultra", "kite-5", "lumen-pro"]);
            CoreFrames.Run(clock, 90);
            Dispatcher.UIThread.RunJobs();
            list.ShownRows.Select(r => r.Item.Id).ShouldBe(["lumen-pro", "kite-5", "basalt-ultra"]);
            standings.BodySize!.Bounds.Height.ShouldBe(3 * 36, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Group_Named_By_The_Title()
    {
        var standings = Create();
        var window = Show(standings);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(standings);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Standings");
            standings.Title = "Leaderboard";
            peer.GetName().ShouldBe("Leaderboard");
            ControlAutomationPeer.CreatePeerForElement(standings.ResetButton!).GetName().ShouldBe("Reset");
            AutomationProperties.GetName(standings.List!.ShownRows[0]).ShouldBe("Rank 1, Lumen 3 Pro, win rate 70%");
        }
        finally
        {
            window.Close();
        }
    }
}
