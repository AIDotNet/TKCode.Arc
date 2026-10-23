using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio's output panel: the idle line, the head for every run state (glyphs, shimmer, case), paragraphs, the
/// whole-word cadence on the frame clock with fades only at full motion, complete / stop / a whole reply, the error box
/// and Retry, Copy reply, the height spring and the hold while a rerun starts, and what screen readers get.
/// </summary>
public class PromptRunPanelTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static PromptRunPanel Create() => new()
    {
        IdleText = "Run Broken spar to see the reply here. Runs are simulated.",
        CaseText = "Broken spar",
        RetryText = "Retry",
        CopyText = "Copy reply",
        CopiedText = "Copied",
        AccessibleName = "Output"
    };

    private static Window Show(PromptRunPanel panel, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { panel } }, level);
        // Wide enough that the idle line fits on one line in the headless font (its glyphs are wider than Inter's).
        window.Width = 1400;
        window.Height = 900;
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 2; i++)
        {
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        window.UpdateLayout();
    }

    /// <summary>Runs <paramref name="count"/> 60 Hz frames, letting the dispatcher and layout run between them.</summary>
    private static void Frames(ManualFrameClock clock, Window window, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static void Click(Window window, Visual visual)
    {
        var point = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle(window);
    }

    private static void Start(PromptRunPanel panel, PromptRunState state = PromptRunState.Thinking, string status = "Thinking")
    {
        panel.Reset();
        panel.State = state;
        panel.StatusText = status;
        panel.FooterText = "Simulated · Atlas Medium · High effort";
    }

    private static void Finish(PromptRunPanel panel, string text = PromptRunSamples.Reply)
    {
        panel.State = PromptRunState.Done;
        panel.StatusText = "Reply";
        panel.Text = text;
        panel.FooterText = "Simulated · 1.42 s · 168 tokens · $0.0011";
    }

    private static StackPanel StatusRow(PromptRunPanel panel) => (StackPanel)panel.StatusSwap!.Content!;

    private static int Words(string text) => text.Split((char[])[' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

    #region Idle and states

    [AvaloniaFact]
    public void Idle_Shows_One_Muted_Line_With_The_Case_Picked_Out()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            panel.Classes.Contains(PromptRunPanel.IdlePseudoClass).ShouldBeTrue();
            var idle = panel.IdleTextBlock!;
            idle.IsEffectivelyVisible.ShouldBeTrue();
            var runs = idle.Inlines!.OfType<Run>().ToArray();
            runs.Select(r => r.Text).ShouldBe(["Run ", "Broken spar", " to see the reply here. Runs are simulated."]);
            TestHelpers.ColorOf(idle.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(runs[1].Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextSecondary));
            TestHelpers.Descendant<StackPanel>(panel, "PART_Run").IsVisible.ShouldBeFalse();
            panel.CopyButton!.IsVisible.ShouldBeFalse();
            // Arc's idle panel: 52 high with its top edge.
            panel.Bounds.Height.ShouldBe(52, 0.6);

            panel.CaseText = "Late parcel";
            panel.IdleText = "Run Late parcel to see the reply here.";
            runs.Select(r => r.Text).ShouldBe(["Run ", "Late parcel", " to see the reply here."]);
            // A label that is not in the line: the line as it is.
            panel.CaseText = "Other";
            runs.Select(r => r.Text).ShouldBe(["Run Late parcel to see the reply here.", "", ""]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Is_Surface_Muted_With_A_Top_Border_And_Rounded_Bottom_Corners()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            var frame = TestHelpers.Descendant<Border>(panel, "PART_Frame");
            TestHelpers.ColorOf(frame.Background).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.SurfaceMuted));
            TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.Border));
            frame.BorderThickness.ShouldBe(new Thickness(0, 1, 0, 0));
            frame.CornerRadius.ShouldBe(new CornerRadius(0, 0, 33, 33));
            frame.ClipToBounds.ShouldBeTrue();
            panel.Padding.ShouldBe(new Thickness(16, 0));
            panel.Sizer!.Axis.ShouldBe(SpringSizeAxis.Height);
            panel.Sizer.Spring.ShouldBe(ArcSpring.Smooth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thinking_Shows_The_Status_The_Case_And_The_Footer_But_No_Copy()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel);
            Settle(window);
            panel.Classes.Contains(PromptRunPanel.ThinkingPseudoClass).ShouldBeTrue();
            panel.IdleTextBlock!.IsEffectivelyVisible.ShouldBeFalse();
            var row = StatusRow(panel);
            // Off: plain secondary text (no shimmer), then "· case" in muted text, 6 apart.
            row.Spacing.ShouldBe(6);
            var texts = row.Children.OfType<TextBlock>().ToArray();
            texts.Select(t => t.Text).ShouldBe(["Thinking", "· Broken spar"]);
            TestHelpers.ColorOf(texts[0].Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(texts[1].Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextMuted));
            row.Children.OfType<ArcGlyph>().ShouldBeEmpty();
            var footer = (TextBlock)panel.FooterSwap!.Content!;
            footer.Text.ShouldBe("Simulated · Atlas Medium · High effort");
            footer.FontSize.ShouldBe(12);
            footer.FontFeatures!.ShouldContain(f => f.Tag == "tnum");
            panel.CopyButton!.IsVisible.ShouldBeFalse();
            // Arc's first run: 88 high (10 + 36 head + 4 body + 8 + 16.8 footer + 12, and the edge).
            panel.Bounds.Height.ShouldBe(88, 1);
            AutomationProperties.GetName(panel.StatusSwap!).ShouldBe("Thinking · Broken spar");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_Thinking_And_Writing_Shimmer()
    {
        var panel = Create();
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            Start(panel);
            Settle(window);
            var shimmer = StatusRow(panel).Children.OfType<TextShimmer>().Single();
            shimmer.Text.ShouldBe("Thinking");
            shimmer.IsActive.ShouldBeTrue();
            panel.State = PromptRunState.Streaming;
            panel.StatusText = "Writing";
            StatusRow(panel).Children.OfType<TextShimmer>().Single().Text.ShouldBe("Writing");
            panel.State = PromptRunState.Done;
            panel.StatusText = "Reply";
            StatusRow(panel).Children.OfType<TextShimmer>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_The_Status_Is_Plain_Text_And_The_Height_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel, ArcMotionLevel.Reduced);
        try
        {
            Start(panel);
            Settle(window);
            var row = StatusRow(panel);
            row.Children.OfType<TextShimmer>().ShouldBeEmpty();
            var text = row.Children.OfType<TextBlock>().First();
            text.Text.ShouldBe("Thinking");
            TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextSecondary));
            panel.Sizer!.IsMoving.ShouldBeFalse();
            panel.Bounds.Height.ShouldBe(88, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_End_State_Has_Its_Glyph()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel);
            Finish(panel);
            Settle(window);
            var check = StatusRow(panel).Children.OfType<ArcGlyph>().Single();
            check.Data.ShouldBeSameAs(ArcIcons.Check);
            check.Size.ShouldBe(14);
            check.StrokeThickness.ShouldBe(2);
            TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.Success));

            panel.State = PromptRunState.Stopped;
            panel.StatusText = "Stopped";
            var square = StatusRow(panel).Children.OfType<PromptRunGlyph>().Single();
            square.Data.ShouldBeSameAs(ArcIcons.Square);
            square.Width.ShouldBe(11);
            TestHelpers.ColorOf(square.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextMuted));

            panel.State = PromptRunState.Failed;
            panel.StatusText = "Run failed";
            var alert = StatusRow(panel).Children.OfType<ArcGlyph>().Single();
            alert.Data.ShouldBeSameAs(ArcIcons.CircleAlert);
            alert.StrokeThickness.ShouldBe(2);
            TestHelpers.ColorOf(alert.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.Danger));
            StatusRow(panel).Children.OfType<TextBlock>().First().Text.ShouldBe("Run failed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_And_Footer_Swap_In_Label_Mode_And_The_Reply_Crossfades()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            panel.StatusSwap!.Mode.ShouldBe(ContentSwapMode.Label);
            panel.FooterSwap!.Mode.ShouldBe(ContentSwapMode.Label);
            TestHelpers.Descendant<ArcContentSwap>(panel, "PART_Reply").Mode.ShouldBe(ContentSwapMode.Crossfade);

            Start(panel);
            var thinking = panel.StatusSwap.Content;
            panel.StatusText = "Thinking";
            // The same words: no new row.
            panel.StatusSwap.Content.ShouldBeSameAs(thinking);
            // Thinking to Streaming alone looks the same: the row stays until the words change.
            panel.State = PromptRunState.Streaming;
            panel.StatusSwap.Content.ShouldBeSameAs(thinking);
            panel.StatusText = "Writing";
            panel.StatusSwap.Content.ShouldNotBeSameAs(thinking);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Reply text

    [Theory]
    [InlineData("Hi Mira,\n\nThanks.\n\nBye", new[] { "Hi Mira,", "Thanks.", "Bye" })]
    [InlineData("One line\nand the next\n\n\n  \nLast", new[] { "One line\nand the next", "Last" })]
    [InlineData("\n\nLeading blank lines", new[] { "Leading blank lines" })]
    [InlineData("Ends with a break\n", new[] { "Ends with a break" })]
    [InlineData("", new string[0])]
    public void Blank_Lines_Separate_Paragraphs_And_Single_Breaks_Stay_Inside(string text, string[] paragraphs)
    {
        var result = new List<string>();
        PromptRunReply.Split(text, result);
        result.ShouldBe(paragraphs);
    }

    [AvaloniaFact]
    public void A_Whole_Reply_Shows_At_Once_As_Paragraphs()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            var completed = 0;
            panel.RevealCompleted += (_, _) => completed++;
            Start(panel);
            Finish(panel);
            Settle(window);
            panel.Reply.Text.ShouldBe(PromptRunSamples.Reply);
            panel.Reply.Paragraphs.Select(p => p.Text).ShouldBe(PromptRunSamples.Reply.Split("\n\n"));
            panel.Reply.Spacing.ShouldBe(10.5);
            var paragraph = panel.Reply.Paragraphs[1];
            paragraph.LineHeight.ShouldBe(23.1);
            paragraph.FontSize.ShouldBe(14);
            paragraph.TextWrapping.ShouldBe(TextWrapping.Wrap);
            TestHelpers.ColorOf(paragraph.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.Foreground));
            paragraph.Fades.IsActive.ShouldBeFalse();
            panel.IsRevealing.ShouldBeFalse();
            completed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streamed_Text_Comes_In_Whole_Words_On_The_Clock()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel, PromptRunState.Streaming, "Writing");
            panel.Push(PromptRunSamples.Reply);
            panel.IsRevealing.ShouldBeTrue();
            panel.Classes.Contains(PromptRunPanel.RevealingPseudoClass).ShouldBeTrue();
            panel.Text.ShouldBe("");

            // The first batch does not wait; then one batch every third 60 Hz frame (50 ms).
            Frames(clock, window, 1);
            var first = panel.Text;
            Words(first).ShouldBeInRange(1, 8);
            Frames(clock, window, 2);
            panel.Text.ShouldBe(first);
            Frames(clock, window, 1);
            var second = panel.Text;
            second.Length.ShouldBeGreaterThan(first.Length);
            (Words(second) - Words(first)).ShouldBeInRange(3, 7);

            // Every release ends at a word boundary and is a prefix of the reply.
            for (var i = 0; i < 6; i++)
            {
                Frames(clock, window, 3);
                PromptRunSamples.Reply.ShouldStartWith(panel.Text);
                char.IsWhiteSpace(panel.Text[^1]).ShouldBeTrue();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Complete_Shows_The_Rest_At_The_Reading_Pace_Then_Says_So()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel);
        try
        {
            var completed = 0;
            panel.RevealCompleted += (_, _) => completed++;
            Start(panel, PromptRunState.Streaming, "Writing");
            panel.Push(PromptRunSamples.Reply);
            Frames(clock, window, 1);
            panel.Complete();
            // Pushes after Complete are ignored.
            panel.Push(" more");
            Frames(clock, window, 3);
            completed.ShouldBe(0);
            panel.IsRevealing.ShouldBeTrue();
            // Under a second for a hundred words (Arc ≈ 0.75 s).
            Frames(clock, window, 60);
            panel.Text.ShouldBe(PromptRunSamples.Reply);
            completed.ShouldBe(1);
            panel.IsRevealing.ShouldBeFalse();
            panel.Reply.Paragraphs.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Keeps_What_Is_Shown_And_Drops_The_Rest()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel);
        try
        {
            var completed = 0;
            panel.RevealCompleted += (_, _) => completed++;
            Start(panel, PromptRunState.Streaming, "Writing");
            panel.Push(PromptRunSamples.Reply);
            Frames(clock, window, 7);
            var shown = panel.Text;
            shown.Length.ShouldBeGreaterThan(0);
            panel.Stop();
            panel.State = PromptRunState.Stopped;
            panel.StatusText = "Stopped";
            Frames(clock, window, 30);
            panel.Text.ShouldBe(shown);
            panel.IsRevealing.ShouldBeFalse();
            completed.ShouldBe(0);
            panel.Push("ignored");
            Frames(clock, window, 5);
            panel.Text.ShouldBe(shown);
            // A stopped reply can be copied.
            panel.CopyButton!.IsVisible.ShouldBeTrue();
            panel.CopyButton.Value.ShouldBe(shown);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Full_Each_Batch_Fades_In_And_Below_It_Keeps_The_Cadence_Without_Fades()
    {
        foreach (var level in new[] { ArcMotionLevel.Full, ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            var clock = new ManualFrameClock();
            using var installed = clock.Install();
            var panel = Create();
            var window = Show(panel, level);
            try
            {
                Start(panel, PromptRunState.Streaming, "Writing");
                panel.Push(PromptRunSamples.Reply);
                Frames(clock, window, 4);
                var text = panel.Text;
                Words(text).ShouldBeGreaterThan(4);
                var fading = panel.Reply.Paragraphs.Any(p => p.Fades.IsActive);
                fading.ShouldBe(level == ArcMotionLevel.Full);
                if (level == ArcMotionLevel.Full)
                {
                    // 150 ms after the first batch it is solid while the newest batch is still on its way in.
                    Frames(clock, window, 9);
                    panel.Reply.Paragraphs[0].Fades.AlphaAt(0).ShouldBe(1);
                    panel.Reply.Paragraphs[^1].Fades.Ranges.Last().Progress.ShouldBeLessThan(1);
                    // 150 ms later every batch is solid.
                    Frames(clock, window, 12);
                    panel.Stop();
                    Frames(clock, window, 12);
                    panel.Reply.Paragraphs.ShouldAllBe(p => !p.Fades.IsActive);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Line_Breaks_From_Any_Platform_Make_The_Same_Paragraphs()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel, PromptRunState.Streaming, "Writing");
            panel.Push("Hi Mira,\r");
            panel.Push("\n\r\nThanks for writing.");
            panel.Complete();
            Frames(clock, window, 60);
            panel.Text.ShouldBe("Hi Mira,\n\nThanks for writing.");
            panel.Reply.Paragraphs.Select(p => p.Text).ShouldBe(["Hi Mira,", "Thanks for writing."]);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Failure, retry, copy

    [AvaloniaFact]
    public void A_Failure_Shows_The_Error_Box_With_Retry()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            var retries = 0;
            panel.RetryRequested += (_, _) => retries++;
            Start(panel);
            panel.State = PromptRunState.Failed;
            panel.StatusText = "Run failed";
            panel.ErrorText = PromptRunSamples.Error;
            panel.FooterText = "Simulated error · Atlas Medium";
            Settle(window);
            var error = TestHelpers.Descendant<Grid>(panel, "PART_Error");
            error.IsVisible.ShouldBeTrue();
            var message = TestHelpers.Descendant<TextBlock>(panel, "PART_ErrorText");
            message.Text.ShouldBe(PromptRunSamples.Error);
            TestHelpers.ColorOf(message.Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextSecondary));
            AutomationProperties.GetLiveSetting(message).ShouldBe(AutomationLiveSetting.Assertive);
            var retry = panel.RetryButton!;
            retry.Content.ShouldBe("Retry");
            retry.Variant.ShouldBe(ButtonVariant.Secondary);
            retry.Size.ShouldBe(ControlSize.Small);
            retry.Bounds.Height.ShouldBe(36);
            panel.CopyButton!.IsVisible.ShouldBeFalse();

            Click(window, retry);
            retries.ShouldBe(1);

            // The rerun hides the box.
            Start(panel);
            Settle(window);
            error.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Reply_Is_There_Only_For_A_Finished_Reply_And_Copies_It()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            var copy = panel.CopyButton!;
            copy.IsVisible.ShouldBeFalse();
            Start(panel);
            Settle(window);
            copy.IsVisible.ShouldBeFalse();
            Finish(panel);
            Settle(window);
            copy.IsVisible.ShouldBeTrue();
            copy.Value.ShouldBe(PromptRunSamples.Reply);
            copy.IsIconOnly.ShouldBeTrue();
            copy.Variant.ShouldBe(CopyButtonVariant.Plain);
            copy.Label.ShouldBe("Copy reply");
            copy.CopiedLabel.ShouldBe("Copied");
            copy.Bounds.Size.ShouldBe(new Size(36, 36));

            // A done run without text has nothing to copy.
            Start(panel);
            panel.State = PromptRunState.Done;
            Settle(window);
            copy.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Height

    [AvaloniaFact]
    public void At_Full_The_Height_Springs_From_Idle_To_The_Run()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            panel.Bounds.Height.ShouldBe(52, 0.6);
            Start(panel);
            Settle(window);
            Frames(clock, window, 4);
            panel.Sizer!.IsMoving.ShouldBeTrue();
            panel.Bounds.Height.ShouldBeInRange(53, 87);
            Frames(clock, window, 60);
            panel.Bounds.Height.ShouldBe(88, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rerun_Keeps_The_Old_Height_Until_The_New_Reply_Fills_It()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel);
            Finish(panel);
            Settle(window);
            var full = panel.Bounds.Height;
            full.ShouldBeGreaterThan(200);
            var oldReply = panel.Reply;

            // Rerun: the old reply goes, the panel keeps its height while thinking and while the first words arrive.
            Start(panel);
            Settle(window);
            panel.Reply.ShouldNotBeSameAs(oldReply);
            panel.Text.ShouldBe("");
            panel.IsHolding.ShouldBeTrue();
            panel.Sizer!.IsHeld.ShouldBeTrue();
            panel.Bounds.Height.ShouldBe(full, 0.6);
            panel.State = PromptRunState.Streaming;
            panel.StatusText = "Writing";
            panel.Push(PromptRunSamples.Reply);
            Frames(clock, window, 4);
            Settle(window);
            panel.IsHolding.ShouldBeTrue();
            panel.Bounds.Height.ShouldBe(full, 0.6);

            // Once the new reply takes the room, the panel follows it again.
            panel.Complete();
            Frames(clock, window, 60);
            Settle(window);
            panel.IsHolding.ShouldBeFalse();
            panel.Sizer.IsHeld.ShouldBeFalse();
            panel.Bounds.Height.ShouldBe(full, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Rerun_That_Fails_Shrinks_When_The_Error_Arrives()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel);
            Finish(panel);
            Settle(window);
            var full = panel.Bounds.Height;
            Start(panel);
            Settle(window);
            panel.Bounds.Height.ShouldBe(full, 0.6);
            panel.State = PromptRunState.Failed;
            panel.StatusText = "Run failed";
            panel.ErrorText = PromptRunSamples.Error;
            Settle(window);
            panel.IsHolding.ShouldBeFalse();
            panel.Bounds.Height.ShouldBeLessThan(full - 50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_First_Run_From_Idle_Is_Not_Held()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            Start(panel);
            Settle(window);
            panel.IsHolding.ShouldBeFalse();
            panel.Bounds.Height.ShouldBe(88, 1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Named_Group_With_Named_Buttons()
    {
        var panel = Create();
        var window = Show(panel);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(panel);
            peer.GetName().ShouldBe("Output");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            AutomationProperties.GetName(panel.IdleTextBlock!).ShouldBe("Run Broken spar to see the reply here. Runs are simulated.");
            Start(panel);
            Finish(panel);
            Settle(window);
            ControlAutomationPeer.CreatePeerForElement(panel.CopyButton!).GetName().ShouldBe("Copy reply");
            AutomationProperties.GetName(panel.StatusSwap!).ShouldBe("Reply · Broken spar");
            AutomationProperties.GetName(panel.FooterSwap!).ShouldBe("Simulated · 1.42 s · 168 tokens · $0.0011");
            panel.Reply.Paragraphs.ShouldAllBe(p => !KeyboardNavigation.GetIsTabStop(p));
        }
        finally
        {
            window.Close();
        }
    }
}
