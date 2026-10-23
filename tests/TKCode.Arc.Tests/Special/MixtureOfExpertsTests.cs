using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The <see cref="MixtureOfExperts"/> card: story, routing events, top k, inspection, pins, keyboard and reduced motion.</summary>
public class MixtureOfExpertsTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (MixtureOfExperts Card, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Full, MixtureOfExperts? card = null,
        double width = 792, double height = 900)
    {
        card ??= new MixtureOfExperts { AutoPlay = false };
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = height;
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static Point CenterOf(Visual visual, Window window)
        => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window) ?? default;

    [AvaloniaFact]
    public void Default_Card_Plans_The_Sample_At_Top_Two()
    {
        var (card, window) = Open();
        try
        {
            card.Routes.Count.ShouldBe(15);
            card.Capacity.ShouldBe(4);
            card.ActiveParameters.ShouldBe(30);
            card.IsLooping.ShouldBeTrue();
            card.Stage!.Tiles.Count.ShouldBe(8);
            card.Sequence!.Tokens.Count.ShouldBe(15);
            card.Stage.IsWide.ShouldBeTrue();
            card.Stage.Bounds.Height.ShouldBe(294);
            AutomationProperties.GetName(card).ShouldBe("Mixture of experts routing");
            // Before anything plays: the first token enters and nothing is counted.
            card.CurrentTokenIndex.ShouldBe(0);
            card.LoadBalance.ShouldBeNull();
            card.Scene!.Footer.ShouldBe(MixtureOfExpertsFooterKind.Scoring);
            card.Stage.InputToken.ShouldBe(0);
            card.Stage.OutputToken.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Story_Routes_Token_By_Token_On_The_Clock()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(3 * 2.2 + 1.25), Frame);
            var moment = card.Moment;
            moment.Token.ShouldBe(3);
            moment.Phase.ShouldBe(MixtureOfExpertsPhase.Lit);
            var scene = card.Scene!;
            // "(" goes to Marks (first) and Syntax (second).
            scene.Tiles[2].ShouldBe(MixtureOfExpertsLooks.TileFirst);
            scene.Tiles[0].ShouldBe(MixtureOfExpertsLooks.TileSecond);
            scene.Counted.ShouldBe(4);
            card.Stage!.Tiles[0].Count.ShouldBe(3);
            MixtureOfExpertsPlanner.Percent(card.LoadBalance!.Value).ShouldBe(71);
            card.Sequence!.Tokens[3].Look.ShouldBe(MixtureOfExpertsLooks.TokenCurrent);
            card.Sequence.Tokens[2].Look.ShouldBe(MixtureOfExpertsLooks.TokenDone);
            card.Sequence.Tokens[4].Look.ShouldBe(MixtureOfExpertsLooks.TokenNext);
            card.FooterText.Line.ShouldBe("Marks 75%, Syntax 25%");
            card.FooterText.Note.ShouldBe("Marks and Syntax both work on (, and the result mixes them 75 to 25.");
            card.Stage.OutputToken.ShouldBe(2);
            card.Stage.Packets.IsShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Routed_Fires_As_Each_Token_Leaves_The_Router()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        var routed = new List<MixtureOfExpertsRoutedEventArgs>();
        card.Routed += (_, e) => routed.Add(e);
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(0.7), Frame);
            routed.ShouldBeEmpty();
            clock.Advance(TimeSpan.FromSeconds(0.1), Frame);
            routed.Count.ShouldBe(1);
            routed[0].Index.ShouldBe(0);
            routed[0].Token.ShouldBe("Ada");
            routed[0].Experts.ShouldBe([3, 0]);
            routed[0].Weights.Select(MixtureOfExpertsPlanner.Percent).ShouldBe([71, 29]);
            routed[0].Dropped.ShouldBeEmpty();
            routed[0].Scores.Sum().ShouldBe(1, 1e-9);
            card.Advance(7 * 2.2);
            routed.Count.ShouldBe(8);
            routed[7].Token.ShouldBe("7");
            routed[7].Dropped.ShouldBe([7]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Top_K_Replans_Without_Moving_The_Clock()
    {
        var (card, window) = Open();
        var announcer = new ExplainerAnnouncer();
        card.Announcer = announcer;
        try
        {
            card.Advance(4 * 2.2 + 1.65);
            var before = card.SceneTime;
            card.Scene!.Counted.ShouldBe(5);
            card.TopK = 1;
            card.SceneTime.ShouldBe(before);
            card.ActiveParameters.ShouldBe(18);
            card.Capacity.ShouldBe(2);
            card.Routes[^1].IsPassThrough.ShouldBeTrue();
            card.Scene.Counted.ShouldBe(5);
            card.Plan.RoutesAfter(card.Scene.Counted).ShouldBe(5);
            announcer.Announcement.ShouldBe("1 expert per token: 18B of 480B parameters active");
            // The header switch follows and drives the property.
            var topK = TestHelpers.Descendant<SegmentedControl>(card, MixtureOfExperts.TopKPart);
            topK.SelectedIndex.ShouldBe(0);
            topK.SelectedIndex = 1;
            card.TopK.ShouldBe(2);
            card.TopK = 7;
            card.TopK.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Token_Holds_The_Story_And_Shows_Its_Route()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(5 * 2.2 + 0.2), Frame);
            var token = card.Sequence!.Tokens[1];
            window.MouseMove(CenterOf(token, window), RawInputModifiers.None);
            Pump();
            card.InspectedTokenIndex.ShouldBe(1);
            var held = card.SceneTime;
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.SceneTime.ShouldBe(held, 0.02);
            card.IsPlaying.ShouldBeTrue();
            var scene = card.Scene!;
            // "wrote": Actions first, Glue second, with the loads after it.
            scene.Tiles[5].ShouldBe(MixtureOfExpertsLooks.TileFirst);
            scene.Tiles[4].ShouldBe(MixtureOfExpertsLooks.TileSecond);
            scene.Counted.ShouldBe(2);
            card.Stage!.InputToken.ShouldBe(1);
            card.Stage.OutputToken.ShouldBe(1);
            card.Stage.Packets.IsShown.ShouldBeFalse();
            token.Look.ShouldBe(MixtureOfExpertsLooks.TokenInspected);
            // Moving straight on to the next token inspects it at once.
            window.MouseMove(CenterOf(card.Sequence.Tokens[2], window), RawInputModifiers.None);
            Pump();
            card.InspectedTokenIndex.ShouldBe(2);
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Pump();
            // The hover outlives the pointer for a moment (crossing a gap between tokens does not flash).
            card.InspectedTokenIndex.ShouldBe(2);
            clock.Advance(TimeSpan.FromSeconds(0.1), Frame);
            card.InspectedTokenIndex.ShouldBe(-1);
            var resumed = card.SceneTime;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.SceneTime.ShouldBeGreaterThan(resumed + 0.9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_An_Expert_Highlights_The_Tokens_It_Took()
    {
        var (card, window) = Open();
        try
        {
            card.Advance(8 * 2.2 + 0.6);
            window.MouseMove(CenterOf(card.Stage!.Tiles[7], window), RawInputModifiers.None);
            Pump();
            card.InspectedExpertIndex.ShouldBe(7);
            var scene = card.Scene!;
            scene.Tiles[7].ShouldBe(MixtureOfExpertsLooks.TileInspected);
            scene.Gates.ShouldAllBe(g => g == MixtureOfExpertsLooks.LineIdle);
            // Math took f (second), x (first), ) (second) and = (first); "7" was skipped.
            scene.TokenLooks[2].ShouldBe(MixtureOfExpertsLooks.TokenSecond);
            scene.TokenLooks[4].ShouldBe(MixtureOfExpertsLooks.TokenFirst);
            scene.TokenDim[7].ShouldBeTrue();
            scene.TokenDim[0].ShouldBeTrue();
            card.FooterText.Line.ShouldBe("Math 4 of 4 slots used");
            card.FooterText.Note.ShouldBe("Operators, variables and equations.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Pins_A_Token_And_Pauses_Until_It_Is_Released()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        var announcer = new ExplainerAnnouncer();
        card.Announcer = announcer;
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            var token = card.Sequence!.Tokens[12];
            var center = CenterOf(token, window);
            window.MouseMove(center, RawInputModifiers.None);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Pump();
            clock.Advance(TimeSpan.FromSeconds(0.1), Frame);
            card.Pinned.ShouldBe((MixtureOfExpertsInspectKind.Token, 12));
            token.IsPinned.ShouldBeTrue();
            card.IsPlaying.ShouldBeFalse();
            card.InspectedTokenIndex.ShouldBe(12);
            announcer.Announcement.ShouldBe("Rust pinned. Press Escape to let routing continue.");
            card.FooterText.Note.ShouldBe("Syntax has no room left, so People handles Rust alone.");
            card.Scene!.Tiles[0].ShouldBe(MixtureOfExpertsLooks.TileDropped);
            card.Scene.Curves[0].ShouldBe(MixtureOfExpertsLooks.LineIdle);
            card.Scene.Gates[0].ShouldBe(MixtureOfExpertsLooks.LineDropped);
            // The pinned token is a pressed toggle to screen readers.
            var peer = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(token);
            peer.ToggleState.ShouldBe(Avalonia.Automation.Provider.ToggleState.On);
            card.Unpin();
            card.IsPlaying.ShouldBeTrue();
            card.InspectedTokenIndex.ShouldBe(-1);
            announcer.Announcement.ShouldBe("Unpinned. Routing continues.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Inspects_Pins_And_Releases()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            clock.Advance(TimeSpan.FromSeconds(0.5), Frame);
            var tiles = card.Stage!.Tiles;
            tiles[0].Focus(NavigationMethod.Tab);
            Pump();
            card.InspectedExpertIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            tiles[1].IsFocused.ShouldBeTrue();
            card.InspectedExpertIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            tiles[7].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            tiles[7].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            tiles[0].IsFocused.ShouldBeTrue();
            // Keyboard focus holds the story without pausing it.
            var held = card.SceneTime;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.SceneTime.ShouldBe(held, 0.02);
            card.IsPlaying.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.Pinned.ShouldBe((MixtureOfExpertsInspectKind.Expert, 0));
            card.IsPlaying.ShouldBeFalse();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            card.Pinned.Kind.ShouldBe(MixtureOfExpertsInspectKind.None);
            card.IsPlaying.ShouldBeTrue();
            // Focus is still on the tile, so it stays inspected (and held) until focus leaves.
            card.InspectedExpertIndex.ShouldBe(0);
            // A pointer click focuses without inspecting.
            var token = card.Sequence!.Tokens[3];
            token.Focus(NavigationMethod.Pointer);
            Pump();
            card.InspectedTokenIndex.ShouldBe(-1);
            card.InspectedExpertIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_Play_While_Pinned_Releases_The_Pin()
    {
        var (card, window) = Open();
        try
        {
            card.Play();
            card.PinExpert(3);
            card.IsPlaying.ShouldBeFalse();
            card.InspectedExpertIndex.ShouldBe(3);
            card.Play();
            card.Pinned.Kind.ShouldBe(MixtureOfExpertsInspectKind.None);
            card.InspectedExpertIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Summary_Holds_Before_The_Story_Loops()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (card, window) = Open();
        try
        {
            card.Play();
            card.Advance(15 * 2.2 - 0.05);
            clock.Advance(TimeSpan.FromSeconds(0.2), Frame);
            card.IsFinished.ShouldBeTrue();
            card.CurrentTokenIndex.ShouldBe(-1);
            card.Scene!.Footer.ShouldBe(MixtureOfExpertsFooterKind.Summary);
            card.FooterText.Line.ShouldBe("15 tokens routed");
            card.FooterText.Note.ShouldBe("Each token woke 2 of 8 experts, so 30B of the 480B parameters did the work.");
            MixtureOfExpertsPlanner.Percent(card.LoadBalance!.Value).ShouldBe(98);
            card.Stage!.InputToken.ShouldBe(-1);
            card.Stage.OutputToken.ShouldBe(14);
            clock.Advance(TimeSpan.FromSeconds(3), Frame);
            card.IsFinished.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.IsFinished.ShouldBeFalse();
            card.CurrentTokenIndex.ShouldBe(0);
            card.LoadBalance.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Uses_Slower_Beats_Without_Travel_And_Stops_On_The_Summary()
    {
        var (card, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            card.Restart();
            card.IsReducedRun.ShouldBeTrue();
            card.IsLooping.ShouldBeFalse();
            card.IsTravelShown.ShouldBeFalse();
            card.Scene!.Footer.ShouldBe(MixtureOfExpertsFooterKind.Route);
            card.Advance(0.65);
            card.Scene.Tiles[3].ShouldBe(MixtureOfExpertsLooks.TileFirst);
            card.Scene.Gates[3].ShouldBe(MixtureOfExpertsLooks.LineFirst);
            card.Advance(0.5);
            card.Stage!.OutputToken.ShouldBe(0);
            card.Scene.Gates[3].ShouldBe(MixtureOfExpertsLooks.LineIdle);
            card.Advance(0.7);
            card.CurrentTokenIndex.ShouldBe(1);
            card.Advance(14 * 1.8);
            card.IsFinished.ShouldBeTrue();
            card.IsPlaying.ShouldBeFalse();
            card.Scene.Footer.ShouldBe(MixtureOfExpertsFooterKind.Summary);
            var play = TestHelpers.Descendant<Avalonia.Controls.Button>(card, Explainer.PlayButtonPart);
            AutomationProperties.GetName(play).ShouldBe("Route again");
            var glyph = (play.Content as TKCode.Arc.Primitives.ArcContentSwap)?.Content as ArcGlyph;
            glyph.ShouldNotBeNull();
            glyph.Data.ShouldBe(ArcIcons.RotateCcw);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Unlimited_Capacity_Drops_Nothing()
    {
        var (card, window) = Open();
        try
        {
            card.CapacityFactor = double.PositiveInfinity;
            card.Capacity.ShouldBeNull();
            card.Routes.ShouldAllBe(r => r.Dropped.Count == 0);
            card.CapacityFactor = double.NaN;
            card.CapacityFactor.ShouldBe(1);
            card.Capacity.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Experts_And_Tokens_Replace_The_Sample()
    {
        var card = new MixtureOfExperts { AutoPlay = false, TopK = 1, Parameters = new MixtureOfExpertsParameters(100, 4, 8) };
        card.Experts.Add(new MixtureOfExpertsExpert("Code"));
        card.Experts.Add(new MixtureOfExpertsExpert("Prose", "Ordinary words."));
        card.Experts.Add(new MixtureOfExpertsExpert("Numbers"));
        card.Tokens.Add(new MixtureOfExpertsTokenItem("let", 6, 1, 1));
        card.Tokens.Add(new MixtureOfExpertsTokenItem("the"));
        card.Tokens.Add(new MixtureOfExpertsTokenItem("42"));
        var (_, window) = Open(card: card);
        try
        {
            card.Routes.Count.ShouldBe(3);
            card.Stage!.Tiles.Count.ShouldBe(3);
            card.Sequence!.Tokens.Count.ShouldBe(3);
            card.Stage.Bounds.Height.ShouldBe(22 + 32 * 3 + 16);
            card.Routes[0].Experts.ShouldBe([0]);
            card.Routes[1].Scores.ShouldBe(MixtureOfExpertsPlanner.Synthesize("the", 3));
            card.ActiveParameters.ShouldBe(12);
            card.Capacity.ShouldBe(1);
            // A change to the data replans and starts over.
            card.Tokens.Add(new MixtureOfExpertsTokenItem("+"));
            Pump();
            card.Routes.Count.ShouldBe(4);
            card.Capacity.ShouldBe(2);
            // Fewer than two experts fall back to the default eight.
            card.Experts.Clear();
            card.Experts.Add(new MixtureOfExpertsExpert("Lonely"));
            Pump();
            card.Stage.Tiles.Count.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tokens_And_Experts_Carry_Their_Routes_In_Their_Names()
    {
        var (card, window) = Open();
        try
        {
            AutomationProperties.GetName(card.Sequence!.Tokens[0]).ShouldBe("Ada: People 71%, Syntax 29%");
            AutomationProperties.GetName(card.Sequence.Tokens[7]).ShouldBe("7: Digits 76%, Math full, skipped");
            card.Advance(3 * 2.2 + 1.2);
            AutomationProperties.GetName(card.Stage!.Tiles[2]).ShouldBe("Marks expert, 1 token, first choice for (");
            AutomationProperties.GetName(card.Stage.Tiles[0]).ShouldBe("Syntax expert, 3 tokens, second choice for (");
            card.Advance(2.2);
            AutomationProperties.GetName(card.Stage.Tiles[0]).ShouldBe("Syntax expert, 4 tokens, at capacity, second choice for x");
            AutomationProperties.GetName(card.Sequence).ShouldBe("Tokens. Point at, focus or pin one to see its route.");
            var announcer = new ExplainerAnnouncer();
            card.Announcer = announcer;
            card.Labels = new MixtureOfExpertsLabels { Pinned = name => $"Fixed on {name}" };
            card.PinToken(0);
            announcer.Announcement.ShouldBe("Fixed on Ada");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_And_Compact_Cards_Stack_The_Stage()
    {
        var (card, window) = Open(width: 420);
        try
        {
            card.Classes.Contains(":narrow").ShouldBeTrue();
            card.Classes.Contains(":compact").ShouldBeTrue();
            card.Stage!.IsWide.ShouldBeFalse();
            card.Stage.Bounds.Height.ShouldBe(286);
            card.Stage.Tiles[0].IsCompact.ShouldBeTrue();
            window.Width = 600;
            Pump();
            card.Classes.Contains(":narrow").ShouldBeFalse();
            card.Classes.Contains(":compact").ShouldBeFalse();
            card.Stage.IsWide.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
