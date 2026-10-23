using TKCode.Arc.Motion;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ModelSummary"/>: the facts it prints, the 4 / 2×2 layout, the balanced description, the crossfades that
/// only touch what changed, the change line and its timer, and what screen readers get. Sample models are invented.
/// The headless stub font advances 1em (14px) per character, so wrapping is predictable.
/// </summary>
public class ModelSummaryTests
{
    private static readonly ModelEffort[] Five = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
    private static readonly ModelEffort[] Three = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    private static AiModel Prime() => new()
    {
        Id = "aurora-prime-3", Name = "Aurora Prime 3", ProviderId = "aurora",
        Description = "A careful planner for long tasks.",
        ContextTokens = 800_000, InputPrice = 6m, OutputPrice = 24m, EffortLevels = Five, DefaultEffort = ModelEffort.Medium
    };

    private static AiModel Swift() => new()
    {
        Id = "aurora-swift-3", Name = "Aurora Swift 3", ProviderId = "aurora",
        Description = "Fast replies for daily chats.",
        ContextTokens = 800_000, InputPrice = 1.5m, OutputPrice = 6m, EffortLevels = Five, DefaultEffort = ModelEffort.High
    };

    private static AiModel Lite() => new()
    {
        Id = "cobalt-atlas-lite", Name = "Cobalt Atlas Lite", ProviderId = "cobalt",
        Description = "A small model for instant replies.",
        ContextTokens = 128_000, InputPrice = 0.3m, OutputPrice = 1.2m
    };

    private const string Note = "Prices in USD per 1M tokens · demo values";

    private static Window Show(ModelSummary summary, double width = 440, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        summary.Width = width;
        var window = TestHelpers.Show(summary, level);
        window.Width = 800;
        window.Height = 600;
        Settle(window);
        return window;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static string?[] Values(ModelSummary summary) => summary.Facts.Select(f => f.Value).ToArray();

    private static string?[] Labels(ModelSummary summary) => summary.Facts.Select(f => f.Label).ToArray();

    private static int Layers(ArcContentSwap swap) => swap.GetVisualChildren().OfType<ContentPresenter>().Count();

    /// <summary>The removal of an outgoing copy is posted from a thread-pool continuation; pump until it lands.</summary>
    private static int LayersAfterRemoval(ArcContentSwap swap, int expected)
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            count = Layers(swap);
            if (count == expected)
            {
                break;
            }
            Thread.Sleep(5);
        }
        return count;
    }

    private static ModelSummaryText DescriptionText(ModelSummary summary) =>
        summary.DescriptionSwap!.GetVisualDescendants().OfType<ModelSummaryText>().Last();

    /// <summary>A manual stand-in for the confirmation timer.</summary>
    private sealed class FakeTimers
    {
        public sealed class Entry(TimeSpan duration, Action elapsed) : IDisposable
        {
            public TimeSpan Duration { get; } = duration;
            public Action Elapsed { get; } = elapsed;
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }

        public List<Entry> Started { get; } = new();

        public IDisposable Start(TimeSpan duration, Action elapsed)
        {
            var entry = new Entry(duration, elapsed);
            Started.Add(entry);
            return entry;
        }

        public int Running => Started.Count(e => !e.IsDisposed);

        public void Fire(Entry entry)
        {
            entry.Elapsed();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static FakeTimers Fake(ModelSummary summary)
    {
        var timers = new FakeTimers();
        summary.NoticeTimerFactory = timers.Start;
        return timers;
    }

    // ---- facts ------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Full_Model_Shows_Context_Prices_And_Effort()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary);
        try
        {
            Labels(summary).ShouldBe(["Context", "Input", "Output", "Effort"]);
            Values(summary).ShouldBe(["800K tokens", "$6.00", "$24.00", "Medium"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Missing_Numbers_And_A_Missing_Effort_Show_An_En_Dash()
    {
        var partial = new AiModel { Id = "partial", Name = "Partial", ContextTokens = 256_000, EffortLevels = Three };
        var summary = new ModelSummary { Model = partial };
        var window = Show(summary);
        try
        {
            Values(summary).ShouldBe(["256K tokens", "–", "–", "–"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Model_Without_Effort_Levels_Shows_The_Not_Adjustable_Word_Whatever_The_Effort()
    {
        var summary = new ModelSummary { Model = Lite(), Effort = ModelEffort.High };
        var window = Show(summary);
        try
        {
            Values(summary).ShouldBe(["128K tokens", "$0.30", "$1.20", summary.Labels.NotAdjustable]);
            summary.Labels.NotAdjustable.ShouldBe("Fixed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Model_Shows_Dashes_And_No_Description()
    {
        var summary = new ModelSummary();
        var window = Show(summary);
        try
        {
            Values(summary).ShouldBe(["–", "–", "–", "–"]);
            summary.DescriptionSwap!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Token_Counts_And_Prices_Are_Formatted_With_The_Currency_Symbol()
    {
        var model = new AiModel { Id = "m", Name = "M", ContextTokens = 3_000_000, InputPrice = 2.5m, OutputPrice = 15m };
        var summary = new ModelSummary { Model = model, CurrencySymbol = "€" };
        var window = Show(summary);
        try
        {
            Values(summary)[..3].ShouldBe(["3M tokens", "€2.50", "€15.00"]);
            model.ContextTokens = 1_250_000;
            Dispatcher.UIThread.RunJobs();
            summary.Facts[0].Value.ShouldBe("1.25M tokens");
            summary.CurrencySymbol = "$";
            summary.Facts[1].Value.ShouldBe("$2.50");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Summary_Follows_The_Model_While_It_Is_In_A_Window()
    {
        var model = Prime();
        var summary = new ModelSummary { Model = model, Effort = ModelEffort.Medium };
        var host = new Border { Child = summary };
        var window = TestHelpers.Show(host);
        try
        {
            model.InputPrice = 7m;
            summary.Facts[1].Value.ShouldBe("$7.00");
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            // Detached: no longer listening; the change is picked up when the summary comes back.
            model.OutputPrice = 30m;
            summary.Facts[2].Value.ShouldBe("$24.00");
            host.Child = summary;
            Settle(window);
            summary.Facts[2].Value.ShouldBe("$30.00");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var summary = new ModelSummary
        {
            Model = Lite(),
            Labels = new ModelSummaryLabels
            {
                Context = "Kontext", Input = "Eingabe", Output = "Ausgabe", Effort = "Aufwand", NotAdjustable = "Fest",
                ContextValue = static tokens => tokens + " Token", Region = "Gewähltes Modell"
            }
        };
        var window = Show(summary);
        try
        {
            Labels(summary).ShouldBe(["Kontext", "Eingabe", "Ausgabe", "Aufwand"]);
            Values(summary).ShouldBe(["128K Token", "$0.30", "$1.20", "Fest"]);
            ControlAutomationPeer.CreatePeerForElement(summary).GetName().ShouldBe("Gewähltes Modell");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- layout -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Facts_Fold_Into_Two_Rows_At_400_Wide_Or_Narrower()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary, width: 401);
        try
        {
            summary.IsCompact.ShouldBeFalse();
            summary.Classes.Contains(ModelSummary.CompactPseudoClass).ShouldBeFalse();
            summary.FactsPanel!.Columns.ShouldBe(4);

            summary.Width = 400;
            Settle(window);
            summary.IsCompact.ShouldBeTrue();
            summary.Classes.Contains(ModelSummary.CompactPseudoClass).ShouldBeTrue();
            summary.FactsPanel.Columns.ShouldBe(2);

            summary.Width = 320;
            Settle(window);
            summary.IsCompact.ShouldBeTrue();

            summary.Width = 440;
            Settle(window);
            summary.IsCompact.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Four_Columns_Are_Equal_With_A_Divider_Before_Each_Later_Column()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary, width: 440);
        try
        {
            var facts = summary.Facts;
            // 440 minus the 1px frame on each side: 438 / 4 = 109.5, edges snapped to whole pixels.
            facts.Select(f => f.Bounds.X).ShouldBe([0, 110, 219, 329]);
            facts.Select(f => f.Bounds.Right).ShouldBe([110, 219, 329, 438]);
            facts.Select(f => f.Bounds.Y).ShouldAllBe(y => y == 0);
            facts.Select(f => f.Dividers).ShouldBe([new Thickness(0), new Thickness(1, 0, 0, 0), new Thickness(1, 0, 0, 0), new Thickness(1, 0, 0, 0)]);
            // 11 + label + 3 + value + 12, all four the same height.
            facts.Select(f => f.Bounds.Height).Distinct().Count().ShouldBe(1);
            summary.FactsPanel!.Bounds.Height.ShouldBe(facts[0].Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_By_Two_Puts_Output_And_Effort_On_A_Second_Row_With_A_Top_Divider()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary, width: 400);
        try
        {
            var facts = summary.Facts;
            var row = facts[0].Bounds.Height;
            facts.Select(f => f.Bounds.Position).ShouldBe([new Point(0, 0), new Point(199, 0), new Point(0, row), new Point(199, row)]);
            facts.Select(f => f.Bounds.Width).ShouldBe([199, 199, 199, 199]);
            facts.Select(f => f.Dividers).ShouldBe([new Thickness(0), new Thickness(1, 0, 0, 0), new Thickness(0, 1, 0, 0), new Thickness(1, 1, 0, 0)]);
            // The second row carries its 1px top divider (border-box, as in the CSS).
            facts[2].Bounds.Height.ShouldBe(row + 1);
            summary.FactsPanel!.Bounds.Height.ShouldBe(row * 2 + 1);

            // Back to four columns: the second-row dividers go away.
            summary.Width = 440;
            Settle(window);
            summary.Facts.Select(f => f.Dividers.Top).ShouldAllBe(top => top == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Parts_Stack_With_The_Measured_Gaps()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium, Note = Note };
        var window = Show(summary);
        try
        {
            var description = summary.DescriptionSwap!;
            description.Bounds.Height.ShouldBe(48);
            var frame = TestHelpers.Descendant<Border>(summary, "PART_FactsFrame");
            // Row gap 12 plus the table's own 4px top margin.
            frame.Bounds.Y.ShouldBe(48 + 12 + 4);
            frame.BorderThickness.ShouldBe(new Thickness(1));
            frame.CornerRadius.ShouldBe(new CornerRadius(18));
            var note = TestHelpers.Descendant<TextBlock>(summary, "PART_Note");
            note.Bounds.Y.ShouldBe(frame.Bounds.Bottom + 12);
            summary.NoticeSwap!.Bounds.Y.ShouldBe(note.Bounds.Bottom + 12);
            summary.NoticeSwap.Bounds.Height.ShouldBe(20);
            summary.MaxWidth.ShouldBe(440);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Change_Line_Keeps_Its_20px_Slot_Shown_Or_Not()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium, Note = Note };
        Fake(summary);
        var window = Show(summary);
        try
        {
            var height = summary.Bounds.Height;
            summary.NoticeSwap!.Bounds.Height.ShouldBe(20);
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            summary.NoticeSwap.Bounds.Height.ShouldBe(20);
            summary.Bounds.Height.ShouldBe(height);
            summary.ShowNotice("A very long confirmation that would never fit on one line of the summary at all, not even close");
            Settle(window);
            summary.Bounds.Height.ShouldBe(height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Note_Shows_Only_When_Set()
    {
        var summary = new ModelSummary { Model = Prime() };
        var window = Show(summary);
        try
        {
            var note = TestHelpers.Descendant<TextBlock>(summary, "PART_Note");
            note.IsVisible.ShouldBeFalse();
            summary.Note = Note;
            note.IsVisible.ShouldBeTrue();
            note.Text.ShouldBe(Note);
            summary.Note = "";
            note.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- description ------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Long_Description_Wraps_To_Three_Lines_At_360_And_320_And_The_Slot_Grows()
    {
        // 55 characters: two lines in the 380px measure (27 per line), three at 360 and 320.
        var model = Prime();
        model.Description = "Keeps a whole repository in view at some cost in speed.";
        var summary = new ModelSummary { Model = model };
        var window = Show(summary, width: 440);
        try
        {
            DescriptionText(summary).TextLayout.TextLines.Count.ShouldBe(2);
            summary.DescriptionSwap!.Bounds.Height.ShouldBe(48);
            foreach (var width in new[] { 360.0, 320.0 })
            {
                summary.Width = width;
                Settle(window);
                DescriptionText(summary).TextLayout.TextLines.Count.ShouldBe(3);
                summary.DescriptionSwap.Bounds.Height.ShouldBe(63);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Two_Line_Description_Is_Balanced_To_The_Smallest_Width_That_Keeps_Two_Lines()
    {
        // Greedy wrapping in 380px gives "Keeps a whole repository in" + "view."; balanced, the lines are 13 and 19 wide.
        var model = Prime();
        model.Description = "Keeps a whole repository in view.";
        var summary = new ModelSummary { Model = model };
        var window = Show(summary, width: 440);
        try
        {
            var text = DescriptionText(summary);
            text.TextLayout.TextLines.Count.ShouldBe(2);
            text.BalancedWidth.ShouldBeLessThan(380 * 0.75);
            text.Bounds.Width.ShouldBeLessThanOrEqualTo(text.BalancedWidth);
            // The paragraph stays centered.
            (text.TranslatePoint(default, summary)!.Value.X + text.Bounds.Width / 2).ShouldBe(220, 1);
            // One character narrower would need a third line: the width is the smallest that keeps two.
            var probe = new ModelSummaryText { Text = model.Description, FontSize = text.FontSize, LetterSpacing = text.LetterSpacing, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            probe.Measure(new Size(text.BalancedWidth - 14, double.PositiveInfinity));
            probe.TextLayout.TextLines.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_One_Line_Description_Is_Not_Narrowed()
    {
        var model = Prime();
        model.Description = "Quick and capable.";
        var summary = new ModelSummary { Model = model };
        var window = Show(summary, width: 440);
        try
        {
            var text = DescriptionText(summary);
            text.TextLayout.TextLines.Count.ShouldBe(1);
            text.BalancedWidth.ShouldBe(380);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- motion -----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Only_The_Facts_Whose_Text_Changed_Crossfade()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            var swaps = summary.Facts.Select(f => f.ValueSwap!).ToArray();
            swaps.Select(Layers).ShouldBe([1, 1, 1, 1]);

            // Same context (800K) and effort (High); both prices change.
            summary.Model = Prime();
            Settle(window);
            swaps.Select(Layers).ShouldBe([1, 2, 2, 1]);
            Layers(summary.DescriptionSwap!).ShouldBe(2);

            // Then only the effort changes: one fact moves, the description stays.
            CoreFrames.Run(clock, 24);
            swaps.Select(s => LayersAfterRemoval(s, 1)).ShouldBe([1, 1, 1, 1]);
            LayersAfterRemoval(summary.DescriptionSwap!, 1).ShouldBe(1);
            summary.Effort = ModelEffort.Low;
            Settle(window);
            swaps.Select(Layers).ShouldBe([1, 1, 1, 2]);
            Layers(summary.DescriptionSwap!).ShouldBe(1);
            CoreFrames.Run(clock, 24);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Changed_Fact_Fades_Out_In_100ms_And_In_Over_160ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            var swap = summary.Facts[1].ValueSwap!;
            summary.Model = Prime();
            Settle(window);
            var presenters = swap.GetVisualChildren().OfType<ContentPresenter>().ToList();
            var outgoing = presenters.Single(p => Equals(p.Content, "$1.50"));
            var incoming = presenters.Single(p => Equals(p.Content, "$6.00"));
            incoming.Opacity.ShouldBe(0, 0.001);
            ((Avalonia.Media.TranslateTransform)incoming.RenderTransform!).Y.ShouldBe(0, 0.001);

            CoreFrames.Run(clock, 4); // ≈ 67ms
            outgoing.Opacity.ShouldBeGreaterThan(0);
            outgoing.Opacity.ShouldBeLessThan(1);
            incoming.Opacity.ShouldBeLessThan(1);

            CoreFrames.Run(clock, 4); // ≈ 133ms: the old value is gone, the new one still fading in
            LayersAfterRemoval(swap, 1).ShouldBe(1);
            incoming.Opacity.ShouldBeLessThan(1);

            CoreFrames.Run(clock, 4); // past 160ms
            incoming.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Description_Crossfades_Out_In_120ms_And_In_Over_160ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            var swap = summary.DescriptionSwap!;
            swap.ExitDuration.ShouldBe(TimeSpan.FromMilliseconds(120));
            swap.EnterDuration.ShouldBe(TimeSpan.FromMilliseconds(160));
            summary.Model = Prime();
            Settle(window);
            var presenters = swap.GetVisualChildren().OfType<ContentPresenter>().ToList();
            var outgoing = presenters.Single(p => Equals(p.Content, Swift().Description));
            var incoming = presenters.Single(p => Equals(p.Content, Prime().Description));

            CoreFrames.Run(clock, 6); // 100ms: the old description is still on its way out
            outgoing.Opacity.ShouldBeGreaterThan(0);
            CoreFrames.Run(clock, 3); // 150ms
            LayersAfterRemoval(swap, 1).ShouldBe(1);
            incoming.Opacity.ShouldBeLessThan(1);
            CoreFrames.Run(clock, 3);
            incoming.Opacity.ShouldBe(1, 0.001);
            DescriptionText(summary).Text.ShouldBe(Prime().Description);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_In_120ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        var window = Show(summary, level: ArcMotionLevel.Reduced);
        try
        {
            summary.Model = Prime();
            Settle(window);
            Layers(summary.Facts[1].ValueSwap!).ShouldBe(2);
            Layers(summary.DescriptionSwap!).ShouldBe(2);
            CoreFrames.Run(clock, 9); // 150ms > 120ms
            LayersAfterRemoval(summary.Facts[1].ValueSwap!, 1).ShouldBe(1);
            LayersAfterRemoval(summary.DescriptionSwap!, 1).ShouldBe(1);
            summary.Facts[1].ValueSwap!.GetVisualChildren().OfType<ContentPresenter>().Single().Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Changes_Everything_At_Once()
    {
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        Fake(summary);
        var window = Show(summary, level: ArcMotionLevel.Off);
        try
        {
            summary.Model = Prime();
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            summary.Facts.Select(f => Layers(f.ValueSwap!)).ShouldBe([1, 1, 1, 1]);
            Layers(summary.DescriptionSwap!).ShouldBe(1);
            Layers(summary.NoticeSwap!).ShouldBe(1);
            summary.Facts[1].ValueSwap!.GetVisualChildren().OfType<ContentPresenter>().Single().Content.ShouldBe("$6.00");
        }
        finally
        {
            window.Close();
        }
    }

    // ---- change line ------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void ShowNotice_Shows_The_Text_With_A_Check_For_The_Notice_Duration()
    {
        var summary = new ModelSummary { Model = Prime(), NoticeDuration = TimeSpan.FromSeconds(3) };
        var timers = Fake(summary);
        var window = Show(summary);
        try
        {
            summary.Notice.ShouldBeNull();
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            summary.Notice.ShouldBe("Aurora Swift 3 → Aurora Prime 3");
            summary.Classes.Contains(ModelSummary.HasNoticePseudoClass).ShouldBeTrue();
            timers.Started.Single().Duration.ShouldBe(TimeSpan.FromSeconds(3));
            var line = summary.NoticeSwap!.GetVisualDescendants().OfType<ModelSummaryNoticeLine>().Single();
            line.Text.ShouldBe("Aurora Swift 3 → Aurora Prime 3");
            var check = TestHelpers.Descendant<ArcGlyph>(line, "PART_Check");
            check.Data.ShouldBe(ArcIcons.Check);
            check.Size.ShouldBe(14);
            check.StrokeThickness.ShouldBe(2);
            TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(summary, ArcDesignTokenKind.Success));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Default_Notice_Duration_Is_2_6_Seconds()
    {
        new ModelSummary().NoticeDuration.ShouldBe(TimeSpan.FromSeconds(2.6));
    }

    [AvaloniaFact]
    public void The_Notice_Expires_When_Its_Timer_Fires()
    {
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var window = Show(summary);
        try
        {
            summary.ShowNotice("Effort: Medium → High");
            timers.Fire(timers.Started.Single());
            summary.Notice.ShouldBeNull();
            summary.IsNoticeTimerRunning.ShouldBeFalse();
            summary.Classes.Contains(ModelSummary.HasNoticePseudoClass).ShouldBeFalse();
            summary.NoticeSwap!.Content.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Notice_Replaces_The_Old_One_And_Restarts_The_Timer()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            summary.ShowNotice("First");
            Settle(window);
            CoreFrames.Run(clock, 12);
            LayersAfterRemoval(summary.NoticeSwap!, 1).ShouldBe(1);
            summary.ShowNotice("Second");
            Settle(window);
            summary.Notice.ShouldBe("Second");
            timers.Started.Count.ShouldBe(2);
            timers.Started[0].IsDisposed.ShouldBeTrue();
            timers.Running.ShouldBe(1);
            // Crossfade: the old line fades out (120ms) while the new one fades in (160ms) at once.
            var presenters = summary.NoticeSwap!.GetVisualChildren().OfType<ContentPresenter>().ToList();
            presenters.Count.ShouldBe(2);
            CoreFrames.Run(clock, 4);
            presenters.ShouldAllBe(p => p.Opacity > 0 && p.Opacity < 1);
            CoreFrames.Run(clock, 12);
            LayersAfterRemoval(summary.NoticeSwap!, 1).ShouldBe(1);

            // The replaced timer firing late does not clear the new notice.
            timers.Fire(timers.Started[0]);
            summary.Notice.ShouldBe("Second");
            timers.Fire(timers.Started[1]);
            summary.Notice.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Same_Text_Again_Still_Crossfades_And_Restarts_The_Timer()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            summary.ShowNotice("Effort: Low → High");
            CoreFrames.Run(clock, 12);
            LayersAfterRemoval(summary.NoticeSwap!, 1).ShouldBe(1);
            summary.ShowNotice("Effort: Low → High");
            Layers(summary.NoticeSwap!).ShouldBe(2);
            timers.Running.ShouldBe(1);
            timers.Started.Count.ShouldBe(2);
            CoreFrames.Run(clock, 12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Notice_Fades_Out_In_120ms_When_It_Expires()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var window = Show(summary, level: ArcMotionLevel.Full);
        try
        {
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            CoreFrames.Run(clock, 12);
            var line = summary.NoticeSwap!.GetVisualChildren().OfType<ContentPresenter>().Single(p => p.Content is not null);
            line.Opacity.ShouldBe(1, 0.001);
            timers.Fire(timers.Started.Single());
            CoreFrames.Run(clock, 4); // ≈ 67ms
            line.Opacity.ShouldBeGreaterThan(0);
            line.Opacity.ShouldBeLessThan(1);
            CoreFrames.Run(clock, 6); // past 120ms
            LayersAfterRemoval(summary.NoticeSwap!, 1).ShouldBe(1);
            summary.NoticeSwap!.GetVisualChildren().OfType<ContentPresenter>().Single().Content.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClearNotice_Stops_The_Timer_And_Clears_The_Line()
    {
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var window = Show(summary);
        try
        {
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            summary.ClearNotice();
            summary.Notice.ShouldBeNull();
            timers.Running.ShouldBe(0);
            summary.NoticeSwap!.Content.ShouldBeNull();
            // Empty text clears as well.
            summary.ShowNotice("Again");
            summary.ShowNotice("");
            summary.Notice.ShouldBeNull();
            timers.Running.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Zero_Notice_Duration_Shows_No_Notice()
    {
        var summary = new ModelSummary { Model = Prime(), NoticeDuration = TimeSpan.Zero };
        var timers = Fake(summary);
        var window = Show(summary);
        try
        {
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            summary.Notice.ShouldBeNull();
            timers.Started.ShouldBeEmpty();

            summary.NoticeDuration = TimeSpan.FromSeconds(1);
            summary.ShowNotice("Shown");
            summary.Notice.ShouldBe("Shown");
            // Turning the line off clears one that is showing.
            summary.NoticeDuration = TimeSpan.Zero;
            summary.Notice.ShouldBeNull();
            timers.Running.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Stops_The_Timer_And_Drops_The_Notice()
    {
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        var host = new Border { Child = summary };
        var window = TestHelpers.Show(host);
        try
        {
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            timers.Running.ShouldBe(1);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            timers.Running.ShouldBe(0);
            summary.IsNoticeTimerRunning.ShouldBeFalse();
            summary.Notice.ShouldBeNull();

            // A notice shown while out of the window starts its time once the summary is back.
            summary.ShowNotice("Later");
            timers.Running.ShouldBe(0);
            host.Child = summary;
            Settle(window);
            timers.Running.ShouldBe(1);
            summary.Notice.ShouldBe("Later");
            summary.NoticeSwap!.GetVisualDescendants().OfType<ModelSummaryNoticeLine>().Single().Text.ShouldBe("Later");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Mid_Crossfade_Leaves_Clean_Parts_Behind()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var summary = new ModelSummary { Model = Swift(), Effort = ModelEffort.High };
        var timers = Fake(summary);
        var host = new Border { Child = summary };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            Settle(window);
            summary.Model = Prime();
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            CoreFrames.Run(clock, 2);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            timers.Running.ShouldBe(0);
            CoreFrames.Run(clock, 30);

            host.Child = summary;
            Settle(window);
            summary.Notice.ShouldBeNull();
            LayersAfterRemoval(summary.DescriptionSwap!, 1).ShouldBe(1);
            LayersAfterRemoval(summary.NoticeSwap!, 1).ShouldBe(1);
            summary.Facts.Select(f => LayersAfterRemoval(f.ValueSwap!, 1)).ShouldBe([1, 1, 1, 1]);
            DescriptionText(summary).Text.ShouldBe(Prime().Description);
            Values(summary).ShouldBe(["800K tokens", "$6.00", "$24.00", "High"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Notice_Shown_Before_The_Template_Appears_Once_The_Summary_Is_Shown()
    {
        var summary = new ModelSummary { Model = Prime() };
        var timers = Fake(summary);
        summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
        summary.Notice.ShouldBe("Aurora Swift 3 → Aurora Prime 3");
        timers.Started.ShouldBeEmpty();
        var window = Show(summary);
        try
        {
            timers.Running.ShouldBe(1);
            summary.NoticeSwap!.GetVisualDescendants().OfType<ModelSummaryNoticeLine>().Single().Text.ShouldBe("Aurora Swift 3 → Aurora Prime 3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Default_Timer_Fires_On_The_Dispatcher()
    {
        var summary = new ModelSummary { Model = Prime(), NoticeDuration = TimeSpan.FromMilliseconds(30) };
        var window = Show(summary);
        try
        {
            summary.ShowNotice("Soon gone");
            summary.IsNoticeTimerRunning.ShouldBeTrue();
            var frame = new DispatcherFrame();
            DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.PushFrame(frame);
            summary.Notice.ShouldBeNull();
            summary.IsNoticeTimerRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- accessibility ----------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Summary_Is_A_Named_Region()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(summary);
            peer.GetName().ShouldBe("Selected model");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetLandmarkType().ShouldBe(AutomationLandmarkType.Region);
            // An explicit name wins.
            AutomationProperties.SetName(summary, "Model details");
            peer.GetName().ShouldBe("Model details");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Fact_Reads_Its_Label_And_Value_Together()
    {
        var summary = new ModelSummary { Model = Prime(), Effort = ModelEffort.Medium };
        var window = Show(summary);
        try
        {
            var peers = summary.Facts.Select(ControlAutomationPeer.CreatePeerForElement).ToArray();
            peers.Select(p => p.GetName()).ShouldBe(["Context: 800K tokens", "Input: $6.00", "Output: $24.00", "Effort: Medium"]);
            peers.ShouldAllBe(p => p.GetAutomationControlType() == AutomationControlType.Text);
            // The label and value inside are not read a second time.
            peers.ShouldAllBe(p => p.GetChildren().Count == 0);
            ControlAutomationPeer.CreatePeerForElement(summary.FactsPanel!).GetAutomationControlType().ShouldBe(AutomationControlType.Group);

            summary.Model = null;
            summary.Facts.Select(f => AutomationProperties.GetName(f)).ShouldBe(
                ["Context: not available", "Input: not available", "Output: not available", "Effort: not available"]);
            summary.Model = Lite();
            AutomationProperties.GetName(summary.Facts[3]).ShouldBe("Effort: Fixed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Change_Line_Reads_As_Its_Text_And_Is_Not_A_Live_Region()
    {
        var summary = new ModelSummary { Model = Prime() };
        Fake(summary);
        var window = Show(summary);
        try
        {
            summary.ShowNotice("Aurora Swift 3 → Aurora Prime 3");
            Settle(window);
            var line = summary.NoticeSwap!.GetVisualDescendants().OfType<ModelSummaryNoticeLine>().Single();
            var peer = ControlAutomationPeer.CreatePeerForElement(line);
            peer.GetName().ShouldBe("Aurora Swift 3 → Aurora Prime 3");
            peer.GetChildren().Count.ShouldBe(0);
            // The owner announces changes; nothing in the summary is a live region.
            summary.GetVisualDescendants().OfType<Control>().Prepend(summary)
                   .ShouldAllBe(c => AutomationProperties.GetLiveSetting(c) == AutomationLiveSetting.Off);
        }
        finally
        {
            window.Close();
        }
    }
}
