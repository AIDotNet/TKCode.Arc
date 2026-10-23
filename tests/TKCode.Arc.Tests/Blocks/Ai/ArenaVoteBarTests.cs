using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The model arena's vote strip: every kind (hidden, waiting, vote, voted, stopped, failed), the requests, the focus helpers,
/// the row swap and the height spring on a manual clock, Reduced and Off, the narrow and compact layouts, words changed in
/// place, and every name.
/// </summary>
public class ArenaVoteBarTests
{
    private static readonly Geometry s_mark = Geometry.Parse("M12 2 L21 12 L12 22 L3 12 Z");

    private static ArenaVoteBar Create(ArenaVoteKind kind = ArenaVoteKind.Hidden) => new()
    {
        PendingText = "Votes open once both answers are in",
        PendingCountText = "0 of 2 finished",
        QuestionText = "Which answer wins?",
        VoteAText = "A is better",
        TieText = "Tie",
        VoteBText = "B is better",
        BothBadText = "Both bad",
        GroupName = "Pick the better answer",
        ResultPrefixText = "You chose",
        ResultName = "Lumen 3 Pro",
        ResultMark = s_mark,
        ResultMarkColor = Brushes.Goldenrod,
        ResultText = "Called a tie",
        NextRoundText = "Next round",
        StoppedText = "Run halted early",
        RunAgainText = "Run again",
        FailedText = "Model A failed. Retry it or move on.",
        Kind = kind
    };

    private static Window Show(ArenaVoteBar bar, ArcMotionLevel level = ArcMotionLevel.Off, double width = 784)
    {
        var window = TestHelpers.Show(new Border { Width = width, Child = bar, VerticalAlignment = VerticalAlignment.Top }, level);
        window.Width = 900;
        window.Height = 400;
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Click(Window window, Visual visual)
    {
        var point = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle(window);
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle(window);
    }

    private static string LabelOf(Button button) =>
        ((StackPanel)button.Content!).Children.OfType<TextBlock>().Single().Text!;

    private static Geometry?[] GlyphsOf(Button button) =>
        ((StackPanel)button.Content!).Children.Select(c => (c as ArcGlyph)?.Data).ToArray();

    private static List<ContentPresenter> Layers(ArcContentSwap swap) => swap.GetVisualChildren().OfType<ContentPresenter>().ToList();

    /// <summary>The swap's layers once a finished exit has been removed (the removal is posted from a continuation).</summary>
    private static int LayersAfterRemoval(ArcContentSwap swap, int expected)
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            count = Layers(swap).Count;
            if (count == expected)
            {
                break;
            }
            Thread.Sleep(5);
        }
        return count;
    }

    [AvaloniaFact]
    public void Hidden_Has_No_Height_And_No_Rule()
    {
        var bar = Create();
        var window = Show(bar);
        try
        {
            bar.Row.ShouldBeNull();
            bar.Bounds.Height.ShouldBe(0);
            bar.BorderThickness.ShouldBe(new Thickness(0));
            bar.Classes.Contains(ArenaVoteBar.HiddenPseudoClass).ShouldBeTrue();

            bar.Kind = ArenaVoteKind.Pending;
            Settle(window);
            bar.Classes.Contains(ArenaVoteBar.HiddenPseudoClass).ShouldBeFalse();
            bar.BorderThickness.ShouldBe(new Thickness(0, 1, 0, 0));
            TestHelpers.ColorOf(bar.BorderBrush).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Border));
            // A 36px row inside 12 above and below, under the 1px rule.
            bar.Bounds.Height.ShouldBe(61, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Waiting_Shows_The_Note_And_The_Count_In_One_Paragraph()
    {
        var bar = Create(ArenaVoteKind.Pending);
        var window = Show(bar);
        try
        {
            var note = bar.Note!;
            var runs = note.Inlines!.OfType<Run>().ToList();
            runs[0].Text.ShouldBe("Votes open once both answers are in");
            runs[^1].Text.ShouldBe("0 of 2 finished");
            TestHelpers.ColorOf(note.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(runs[^1].Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.TextMuted));
            note.FontSize.ShouldBe(14);
            note.TextWrapping.ShouldBe(TextWrapping.Wrap);
            bar.Row!.Bounds.Height.ShouldBe(36);
            note.TranslatePoint(default, bar)!.Value.X.ShouldBe(20, 0.01);
            bar.ActionButton.ShouldBeNull();
            bar.VoteButtons.ShouldBeEmpty();

            // The count changes in place: no new row.
            var row = bar.Row;
            bar.PendingCountText = "1 of 2 finished";
            bar.Row.ShouldBeSameAs(row);
            runs[^1].Text.ShouldBe("1 of 2 finished");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vote_Shows_The_Question_Left_And_Four_Secondary_Buttons_Right()
    {
        // Wide enough for the headless stub font (wider than Inter) to keep one line.
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar, width: 1100);
        try
        {
            bar.Note!.Text.ShouldBe("Which answer wins?");
            TestHelpers.ColorOf(bar.Note.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.TextSecondary));
            var buttons = bar.VoteButtons;
            buttons.Select(LabelOf).ShouldBe(["A is better", "Tie", "B is better", "Both bad"]);
            GlyphsOf(buttons[0]).ShouldBe([ArcIcons.ArrowLeft, null]);
            GlyphsOf(buttons[1]).ShouldBe([ArcIcons.Equal, null]);
            GlyphsOf(buttons[2]).ShouldBe([null, ArcIcons.ArrowRight]);
            GlyphsOf(buttons[3]).ShouldBe([ArcIcons.ThumbsDown, null]);
            foreach (var button in buttons)
            {
                button.Variant.ShouldBe(ButtonVariant.Secondary);
                button.Size.ShouldBe(ControlSize.Small);
                button.Bounds.Height.ShouldBe(36);
            }
            // One line: the last button ends 12 from the right edge, 6 apart.
            bar.Row!.IsInline.ShouldBeTrue();
            var last = buttons[3];
            (1100 - last.TranslatePoint(new Point(last.Bounds.Width, 0), bar)!.Value.X).ShouldBe(12, 0.01);
            (buttons[1].TranslatePoint(default, bar)!.Value.X - buttons[0].TranslatePoint(new Point(buttons[0].Bounds.Width, 0), bar)!.Value.X)
                .ShouldBe(6, 0.01);
            bar.Bounds.Height.ShouldBe(61, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Vote_Buttons_Ask_For_A_Tie_B_And_Both_Bad()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar);
        try
        {
            var choices = new List<ArenaChoice>();
            bar.VoteRequested += (_, e) => choices.Add(e.Choice);
            foreach (var button in bar.VoteButtons)
            {
                Click(window, button);
            }
            choices.ShouldBe([ArenaChoice.A, ArenaChoice.Tie, ArenaChoice.B, ArenaChoice.BothBad]);

            // Enter and Space on a focused button too.
            bar.FocusFirstVote().ShouldBeTrue();
            Key(window, PhysicalKey.Enter);
            choices[^1].ShouldBe(ArenaChoice.A);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Voted_Shows_A_Check_The_Mark_Before_The_Bold_Name_And_A_Primary_Accent_Next_Round()
    {
        var bar = Create(ArenaVoteKind.Voted);
        var window = Show(bar);
        try
        {
            var (prefix, mark, name) = bar.NamedResult!.Value;
            prefix.Text.ShouldBe("You chose");
            name.Text.ShouldBe("Lumen 3 Pro");
            name.FontWeight.ShouldBe(FontWeight.Medium);
            mark.Mark.ShouldBeSameAs(s_mark);
            mark.MarkColor.ShouldBeSameAs(Brushes.Goldenrod);
            mark.Size.ShouldBe(16);
            mark.IsVisible.ShouldBeTrue();
            // The mark sits between the words and the name, 6 before the name.
            var markX = mark.TranslatePoint(default, bar)!.Value.X;
            markX.ShouldBeGreaterThan(prefix.TranslatePoint(new Point(prefix.Bounds.Width, 0), bar)!.Value.X);
            (name.TranslatePoint(default, bar)!.Value.X - (markX + 16)).ShouldBe(6, 0.01);
            bar.Note!.IsVisible.ShouldBeFalse();
            var check = TestHelpers.Descendant<ArcGlyph>(bar.Row!);
            check.Data.ShouldBeSameAs(ArcIcons.Check);
            check.Size.ShouldBe(16);
            TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Success));

            var next = bar.ActionButton!;
            LabelOf(next).ShouldBe("Next round");
            GlyphsOf(next).ShouldBe([null, ArcIcons.ArrowRight]);
            next.Variant.ShouldBe(ButtonVariant.Primary);
            TestHelpers.ColorOf(next.Background).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.Accent));
            TestHelpers.ColorOf(next.Foreground).ShouldBe(ArcTokens.GetColor(bar, ArcDesignTokenKind.AccentForeground));

            // Without a name the plain result shows, and no mark.
            bar.ResultName = null;
            bar.Note.IsVisible.ShouldBeTrue();
            bar.Note.Text.ShouldBe("Called a tie");
            bar.ResultMark = null;
            mark.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stopped_Offers_Run_Again_And_Failed_Offers_A_Secondary_Next_Round()
    {
        var bar = Create(ArenaVoteKind.Stopped);
        var window = Show(bar);
        try
        {
            var log = new List<string>();
            bar.RunAgainRequested += (_, _) => log.Add("again");
            bar.NextRoundRequested += (_, _) => log.Add("next");

            bar.Note!.Text.ShouldBe("Run halted early");
            var again = bar.ActionButton!;
            LabelOf(again).ShouldBe("Run again");
            GlyphsOf(again).ShouldBe([ArcIcons.RotateCcw, null]);
            again.Variant.ShouldBe(ButtonVariant.Secondary);
            Click(window, again);
            bar.FocusNextRound().ShouldBeFalse();

            bar.Kind = ArenaVoteKind.Failed;
            Settle(window);
            bar.Note!.Text.ShouldBe("Model A failed. Retry it or move on.");
            var next = bar.ActionButton!;
            LabelOf(next).ShouldBe("Next round");
            GlyphsOf(next).ShouldBe([null, ArcIcons.ArrowRight]);
            next.Variant.ShouldBe(ButtonVariant.Secondary);
            Click(window, next);

            bar.Kind = ArenaVoteKind.Voted;
            Settle(window);
            Click(window, bar.ActionButton!);
            log.ShouldBe(["again", "next", "next"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Focus_Helpers_Reach_The_First_Vote_And_Next_Round_With_A_Ring()
    {
        var bar = Create(ArenaVoteKind.Pending);
        var window = Show(bar);
        try
        {
            bar.FocusFirstVote().ShouldBeFalse();
            bar.FocusNextRound().ShouldBeFalse();

            // Right after the kind changes (before any layout) the new row's buttons are reachable.
            bar.Kind = ArenaVoteKind.Vote;
            bar.FocusFirstVote().ShouldBeTrue();
            bar.VoteButtons[0].IsFocused.ShouldBeTrue();
            Settle(window);
            TestHelpers.Descendant<Border>(bar.VoteButtons[0], "PART_FocusRing").IsVisible.ShouldBeTrue();

            bar.Kind = ArenaVoteKind.Voted;
            bar.FocusNextRound().ShouldBeTrue();
            bar.ActionButton!.IsFocused.ShouldBeTrue();
            bar.FocusFirstVote().ShouldBeFalse();

            bar.Kind = ArenaVoteKind.Failed;
            bar.FocusNextRound().ShouldBeTrue();
            bar.ActionButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Vote_Pressed_From_The_Keyboard_Leaves_The_Focus_On_Next_Round()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar);
        try
        {
            // The owner reveals the result in its handler; the focus inside the old row moves to the new row's button.
            bar.VoteRequested += (_, _) => bar.Kind = ArenaVoteKind.Voted;
            bar.FocusFirstVote();
            Key(window, PhysicalKey.Space);
            bar.Kind.ShouldBe(ArenaVoteKind.Voted);
            bar.ActionButton!.IsFocused.ShouldBeTrue();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(bar.ActionButton);
            TestHelpers.Descendant<Border>(bar.ActionButton, "PART_FocusRing").IsVisible.ShouldBeTrue();

            // A click moves the focus too, but draws no ring.
            bar.Kind = ArenaVoteKind.Vote;
            Settle(window);
            Click(window, bar.VoteButtons[2]);
            bar.ActionButton!.IsFocused.ShouldBeTrue();
            TestHelpers.Descendant<Border>(bar.ActionButton, "PART_FocusRing").IsVisible.ShouldBeFalse();

            // A row without buttons leaves the focus alone (the owner moves it to Run).
            bar.Kind = ArenaVoteKind.Pending;
            Settle(window);
            bar.Row!.IsKeyboardFocusWithin.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Rows_Crossfade_And_The_Height_Follows_On_The_Smooth_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var bar = Create(ArenaVoteKind.Pending);
        bar.PendingText = "Waiting";
        bar.PendingCountText = "0 of 2";
        var window = Show(bar, ArcMotionLevel.Full, width: 538);
        try
        {
            bar.IsNarrow = true;
            Settle(window);
            bar.Bounds.Height.ShouldBe(61, 0.5);
            var swap = bar.RowSwap!;
            swap.Mode.ShouldBe(ContentSwapMode.Crossfade);
            bar.SizeDecorator!.Spring.ShouldBe(ArcSpring.Smooth);
            bar.SizeDecorator.Axis.ShouldBe(SpringSizeAxis.Height);
            var pending = bar.Row!;

            bar.Kind = ArenaVoteKind.Vote;
            Settle(window);
            var layers = Layers(swap);
            layers.Count.ShouldBe(2);
            var incoming = layers.Single(p => ReferenceEquals(p.Content, bar.Row));
            var outgoing = layers.Single(p => ReferenceEquals(p.Content, pending));
            // The old row leaves layout and input; the new one starts invisible at its place.
            pending.IsHitTestVisible.ShouldBeFalse();
            incoming.Opacity.ShouldBe(0, 0.001);
            outgoing.Bounds.Y.ShouldBe(0, 0.01);
            outgoing.Bounds.Width.ShouldBe(538, 0.01);

            // The wrapped vote row is 12 + 20 + 8 + 36 + 12 = 88 (+ the rule).
            var elapsed = CoreFrames.Run(clock, 4);
            Settle(window);
            outgoing.Opacity.ShouldBeGreaterThan(0);
            outgoing.Opacity.ShouldBeLessThan(1);
            incoming.Opacity.ShouldBeGreaterThan(0);
            incoming.Opacity.ShouldBeLessThan(1);
            var height = bar.Bounds.Height;
            height.ShouldBeGreaterThan(61);
            height.ShouldBeLessThan(89);
            var (displacement, _) = SpringSolver.Evaluate(ArcMotionPresets.Smooth, 60 - 88, 0, elapsed);
            (height - 1).ShouldBe(88 + displacement, 2);

            CoreFrames.Run(clock, 4);
            LayersAfterRemoval(swap, 1).ShouldBe(1);
            CoreFrames.Run(clock, 60);
            Settle(window);
            incoming.Opacity.ShouldBe(1, 0.001);
            bar.Bounds.Height.ShouldBe(89, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_In_120ms_And_Jumps_The_Height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var bar = Create(ArenaVoteKind.Pending);
        bar.PendingText = "Waiting";
        bar.PendingCountText = "0 of 2";
        var window = Show(bar, ArcMotionLevel.Reduced, width: 538);
        try
        {
            bar.IsNarrow = true;
            Settle(window);
            bar.Kind = ArenaVoteKind.Vote;
            Settle(window);
            bar.Bounds.Height.ShouldBe(89, 0.5);
            var incoming = Layers(bar.RowSwap!).Single(p => ReferenceEquals(p.Content, bar.Row));
            ((TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0);
            CoreFrames.Run(clock, 4);
            incoming.Opacity.ShouldBeGreaterThan(0);
            incoming.Opacity.ShouldBeLessThan(1);
            CoreFrames.Run(clock, 4);
            incoming.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Replaces_The_Row_At_Once()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar);
        try
        {
            bar.Kind = ArenaVoteKind.Stopped;
            Settle(window);
            var layers = Layers(bar.RowSwap!);
            layers.Count.ShouldBe(1);
            layers[0].Opacity.ShouldBe(1);
            layers[0].Content.ShouldBeSameAs(bar.Row);
            bar.Bounds.Height.ShouldBe(61, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Puts_The_Question_On_Its_Own_Line_Above_The_Buttons()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar, width: 538);
        try
        {
            bar.IsNarrow = true;
            Settle(window);
            bar.Classes.Contains(ArenaVoteBar.NarrowPseudoClass).ShouldBeTrue();
            bar.Row!.IsInline.ShouldBeFalse();
            var first = bar.VoteButtons[0];
            first.TranslatePoint(default, bar)!.Value.X.ShouldBe(20, 0.01);
            first.TranslatePoint(default, bar)!.Value.Y.ShouldBe(1 + 12 + 19.6 + 8, 0.6);
            bar.Note!.TranslatePoint(default, bar)!.Value.Y.ShouldBe(13, 0.01);
            bar.Bounds.Height.ShouldBe(1 + 12 + 19.6 + 8 + 36 + 12, 0.6);

            bar.IsNarrow = false;
            Settle(window);
            // 538 is too narrow for the question and the buttons on one line anyway (Arc's flex wraps too).
            bar.Row.IsInline.ShouldBeFalse();
            bar.Kind = ArenaVoteKind.Stopped;
            Settle(window);
            bar.Row!.IsInline.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Hides_The_Question_And_Lays_The_Buttons_Out_As_A_Stretched_Two_By_Two_Grid()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar, width: 358);
        try
        {
            bar.IsCompact = true;
            bar.IsNarrow = true;
            Settle(window);
            bar.Classes.Contains(ArenaVoteBar.CompactPseudoClass).ShouldBeTrue();
            bar.Note!.IsVisible.ShouldBeFalse();
            bar.Choices!.IsGrid.ShouldBeTrue();
            var buttons = bar.VoteButtons;
            var column = (358 - 20 - 12 - 6) / 2.0;
            foreach (var button in buttons)
            {
                button.Bounds.Width.ShouldBe(column, 0.01);
            }
            var a = buttons[0].TranslatePoint(default, bar)!.Value;
            var tie = buttons[1].TranslatePoint(default, bar)!.Value;
            var b = buttons[2].TranslatePoint(default, bar)!.Value;
            a.X.ShouldBe(20, 0.01);
            tie.X.ShouldBe(20 + column + 6, 0.01);
            tie.Y.ShouldBe(a.Y, 0.01);
            b.X.ShouldBe(20, 0.01);
            b.Y.ShouldBe(a.Y + 36 + 6, 0.01);
            // 36 + 6 + 36 = 78 inside 12 and 12, under the rule.
            bar.Bounds.Height.ShouldBe(1 + 12 + 78 + 12, 0.5);

            bar.IsCompact = false;
            Settle(window);
            bar.Note.IsVisible.ShouldBeTrue();
            bar.Choices.IsGrid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Stacks_The_Result_Above_Next_Round_And_Wraps_The_Waiting_Count()
    {
        var bar = Create(ArenaVoteKind.Voted);
        bar.ResultName = null;
        bar.ResultText = "Tie";
        var window = Show(bar, width: 358);
        try
        {
            bar.Row!.IsInline.ShouldBeTrue();
            bar.IsCompact = true;
            Settle(window);
            bar.Row!.IsInline.ShouldBeFalse();
            var next = bar.ActionButton!;
            next.TranslatePoint(default, bar)!.Value.X.ShouldBe(20, 0.01);
            next.Bounds.Width.ShouldBeLessThan(200);
            next.TranslatePoint(default, bar)!.Value.Y.ShouldBeGreaterThan(bar.Note!.TranslatePoint(default, bar)!.Value.Y + 19);

            bar.Kind = ArenaVoteKind.Pending;
            bar.PendingText = "Votes open once both answers are in, so wait a moment";
            Settle(window);
            bar.Note!.Bounds.Height.ShouldBeGreaterThan(30);
            bar.Bounds.Height.ShouldBeGreaterThan(61);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Words_Change_In_Place_Without_A_New_Row()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar);
        try
        {
            var row = bar.Row;
            bar.VoteAText = "A wins";
            bar.QuestionText = "Which one is better?";
            bar.GroupName = null;
            bar.Row.ShouldBeSameAs(row);
            LabelOf(bar.VoteButtons[0]).ShouldBe("A wins");
            AutomationProperties.GetName(bar.VoteButtons[0]).ShouldBe("A wins");
            bar.Note!.Text.ShouldBe("Which one is better?");
            AutomationProperties.GetName(bar.Choices!).ShouldBe("Which one is better?");
            Layers(bar.RowSwap!).Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_A_Named_Group_Of_Named_Buttons()
    {
        var bar = Create(ArenaVoteKind.Vote);
        var window = Show(bar);
        try
        {
            var group = ControlAutomationPeer.CreatePeerForElement(bar.Choices!);
            group.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            group.GetName().ShouldBe("Pick the better answer");
            bar.VoteButtons.Select(b => ControlAutomationPeer.CreatePeerForElement(b).GetName())
                .ShouldBe(["A is better", "Tie", "B is better", "Both bad"]);

            bar.Kind = ArenaVoteKind.Voted;
            Settle(window);
            ControlAutomationPeer.CreatePeerForElement(bar.ActionButton!).GetName().ShouldBe("Next round");
            bar.Kind = ArenaVoteKind.Stopped;
            Settle(window);
            ControlAutomationPeer.CreatePeerForElement(bar.ActionButton!).GetName().ShouldBe("Run again");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Back_To_Hidden_The_Strip_Closes()
    {
        var bar = Create(ArenaVoteKind.Stopped);
        var window = Show(bar);
        try
        {
            bar.Bounds.Height.ShouldBe(61, 0.5);
            bar.Kind = ArenaVoteKind.Hidden;
            Settle(window);
            bar.Row.ShouldBeNull();
            bar.ActionButton.ShouldBeNull();
            bar.Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
