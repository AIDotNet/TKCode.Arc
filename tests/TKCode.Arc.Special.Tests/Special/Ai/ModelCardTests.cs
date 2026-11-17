using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ModelCard: the empty card, the test's own invented models (passed as data), the values of the details view, the volume slider and cost estimate, the
/// benchmark rows (ranks, readouts, hover and focus), the compare table (better values, margins, tally), the rival menu,
/// loading and error, the footer buttons, the breakpoints, one size in every state, the events and the labels.
/// </summary>
public class ModelCardTests
{
    /// <summary>The test's own invented models and benchmarks (made-up names, prices and scores).</summary>
    private static class Sample
    {
        public static readonly ModelCardModel Model = new()
        {
            Id = "lumen-5-pro",
            Name = "Lumen 5 Pro",
            Provider = "Halcyon Labs",
            Mark = ArcIcons.Sparkles,
            MarkColor = Color.FromRgb(0xC8, 0x6A, 0x4A),
            Summary = "The flagship for long agent runs, careful coding and analysis over very large documents.",
            Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Pdf
                         | ModelCardModalities.Tools | ModelCardModalities.Reasoning,
            ContextWindow = 1_000_000,
            MaxOutput = 128_000,
            KnowledgeCutoff = "2026-04",
            Pricing = new ModelCardPricing { Input = 5, Output = 25, CachedInput = 0.5 },
            Scores = new Dictionary<string, double> { ["swe-bench"] = 84.6, ["gpqa"] = 88.2, ["terminal-bench"] = 61.4 },
        };

        public static readonly ModelCardModel[] Rivals =
        [
            new()
            {
                Id = "orion-4-2",
                Name = "Orion 4.2",
                Provider = "Orbital Research",
                Mark = ArcIcons.Star,
                MarkColor = Color.FromRgb(0x3D, 0x7E, 0xC9),
                Summary = "A fast generalist with strong science answers and native audio input.",
                Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Audio
                             | ModelCardModalities.Tools | ModelCardModalities.Reasoning,
                ContextWindow = 400_000,
                MaxOutput = 128_000,
                KnowledgeCutoff = "2026-01",
                Pricing = new ModelCardPricing { Input = 2.5, Output = 15, CachedInput = 0.25 },
                Scores = new Dictionary<string, double> { ["swe-bench"] = 81.9, ["gpqa"] = 89.1, ["terminal-bench"] = 58.0 },
            },
            new()
            {
                Id = "cirrus-3-flash",
                Name = "Cirrus 3 Flash",
                Provider = "Cirrus AI",
                Mark = ArcIcons.Cloud,
                MarkColor = Color.FromRgb(0x2F, 0x9E, 0x8F),
                Summary = "A low cost model for high volume work, with every input type and a two million token window.",
                Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Pdf
                             | ModelCardModalities.Audio | ModelCardModalities.Video | ModelCardModalities.Tools,
                ContextWindow = 2_000_000,
                MaxOutput = 64_000,
                KnowledgeCutoff = "2025-11",
                Pricing = new ModelCardPricing { Input = 0.3, Output = 2.5, CachedInput = 0.075 },
                Scores = new Dictionary<string, double> { ["swe-bench"] = 66.3, ["gpqa"] = 79.4, ["terminal-bench"] = 41.2 },
            },
            new()
            {
                Id = "kestrel-2",
                Name = "Kestrel 2",
                Provider = "Kestrel Systems",
                Mark = ArcIcons.Zap,
                MarkColor = Color.FromRgb(0x7A, 0x5C, 0xC4),
                Summary = "A reasoning model tuned for terminal work and quick tool use.",
                Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Tools
                             | ModelCardModalities.Reasoning,
                ContextWindow = 256_000,
                MaxOutput = 64_000,
                KnowledgeCutoff = "2026-02",
                Pricing = new ModelCardPricing { Input = 3, Output = 15 },
                Scores = new Dictionary<string, double> { ["swe-bench"] = 74.8, ["terminal-bench"] = 63.5 },
            },
        ];

        public static readonly ModelCardBenchmark[] Benchmarks =
        [
            new() { Id = "swe-bench", Name = "SWE-bench Verified", ShortName = "SWE-bench", Description = "real GitHub issues resolved" },
            new() { Id = "gpqa", Name = "GPQA Diamond", ShortName = "GPQA", Description = "graduate level science questions" },
            new() { Id = "terminal-bench", Name = "Terminal-Bench 2.0", ShortName = "Terminal", Description = "tasks done in a shell" },
        ];
    }

    /// <summary>Opens a card on <see cref="Sample"/>, labeled as samples, unless <paramref name="empty"/>.</summary>
    private static (ModelCard Card, Window Window) Open(Action<ModelCard>? setup = null, double width = 600,
        ArcMotionLevel level = ArcMotionLevel.Off, bool empty = false)
    {
        var card = new ModelCard { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (!empty)
        {
            card.Model = Sample.Model;
            card.Rivals = Sample.Rivals;
            card.Benchmarks = Sample.Benchmarks;
            card.IsSample = true;
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1200;
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static ModelCardModel Plain(string id, string name, double? score = null) => new()
    {
        Id = id,
        Name = name,
        ContextWindow = 200_000,
        Pricing = new ModelCardPricing { Input = 1, Output = 4 },
        Scores = score is { } s ? new Dictionary<string, double> { ["swe-bench"] = s } : null,
    };

    [AvaloniaFact]
    public void Without_A_Model_The_Card_Keeps_Its_Frame_And_Is_Empty()
    {
        var (card, window) = Open(empty: true);
        try
        {
            card.IsSample.ShouldBeFalse();
            card.NameText!.Text.ShouldBe("No model selected");
            card.ProviderText!.Text.ShouldBe(string.Empty);
            AutomationProperties.GetName(card).ShouldBe("No model selected");
            card.ShownRivals.ShouldBeEmpty();
            card.CompareSwitch!.IsVisible.ShouldBeFalse();
            card.CurrentBenchmark.ShouldBeNull();
            card.BenchmarkSection!.IsVisible.ShouldBeFalse();
            card.CutoffText!.Text.ShouldBe("Not listed");
            card.MaxOutputValue!.Text.ShouldBe("Not listed");
            card.CopyIdButton!.IsEnabled.ShouldBeFalse();
            // Giving it a model fills it in.
            card.Model = Plain("plain", "Plain 1");
            Pump();
            card.NameText.Text.ShouldBe("Plain 1");
            card.CopyIdButton.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Note_Is_The_Compare_Footnote()
    {
        var (card, window) = Open(c =>
        {
            c.IsSample = false;
            c.View = ModelCardView.Compare;
        });
        try
        {
            card.FootnoteText!.Text.ShouldBe(string.Empty);
            card.Note = "List prices, checked 2026-10-29.";
            Pump();
            card.FootnoteText.Text.ShouldBe("List prices, checked 2026-10-29.");
            card.IsSample = true;
            Pump();
            card.FootnoteText.Text.ShouldBe("List prices, checked 2026-10-29.");
            card.Note = null;
            Pump();
            card.FootnoteText.Text.ShouldBe("Prices and scores are samples.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opens_On_The_Sample_With_Every_Value()
    {
        var (card, window) = Open();
        try
        {
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Details);
            card.NameText!.Text.ShouldBe("Lumen 5 Pro");
            card.ProviderText!.Text.ShouldBe("Halcyon Labs");
            AutomationProperties.GetName(card).ShouldBe("Lumen 5 Pro");
            card.SummaryText!.Text.ShouldStartWith("The flagship");
            card.ContextValue!.Text.ShouldBe("1M");
            card.PagesValue!.Text.ShouldBe("≈ 1,500 pages");
            card.Gauge!.Target.ShouldBe(0.5, 1e-9);
            card.MaxOutputValue!.Text.ShouldBe("128K");
            card.CutoffText!.Text.ShouldBe("Apr 2026");
            card.InputPrice!.Text.ShouldBe("$5.00");
            card.OutputPrice!.Text.ShouldBe("$25.00");
            card.CachedPrice!.Text.ShouldBe("$0.50");
            card.PriceCells.Count(c => c.IsVisible).ShouldBe(3);
            // 100M a month, 75% at $5 and 25% at $25.
            card.CostValue!.Text.ShouldBe("$1,000.00/mo");
            card.VolumeSlider!.Value.ShouldBe(5);
            card.CaptionText!.Text.ShouldBe("SWE-bench Verified, real GitHub issues resolved. Sample scores");
            card.IdText!.Text.ShouldBe("lumen-5-pro");
            card.CopyIdButton!.Value.ShouldBe("lumen-5-pro");
            card.SelectButton!.IsVisible.ShouldBeFalse();
            card.CompareSwitch!.IsVisible.ShouldBeTrue();
            card.CompareSwitch.IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Show_All_Seven_With_Unsupported_Ones_Dashed()
    {
        var (card, window) = Open();
        try
        {
            card.Chips.Count.ShouldBe(7);
            var pdf = card.Chips.Single(c => c.Modality == ModelCardModalities.Pdf);
            pdf.IsSupported.ShouldBeTrue();
            pdf.IsDashed.ShouldBeFalse();
            AutomationProperties.GetName(pdf).ShouldBe("PDF, supported");
            var audio = card.Chips.Single(c => c.Modality == ModelCardModalities.Audio);
            audio.IsSupported.ShouldBeFalse();
            audio.IsDashed.ShouldBeTrue();
            AutomationProperties.GetName(audio).ShouldBe("Audio, not supported");

            // Switching to a model with audio flips the chip in place; the seven chips stay.
            card.Model = Sample.Rivals[0];
            Pump();
            card.Chips.Count.ShouldBe(7);
            audio.IsSupported.ShouldBeTrue();
            pdf.IsDashed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Benchmark_Rows_Rank_The_Model_Against_Every_Rival()
    {
        var (card, window) = Open();
        try
        {
            var rows = card.BenchmarkRowParts;
            rows.Count.ShouldBe(3);
            rows[0].ModelName.ShouldBe("Lumen 5 Pro");
            rows[1].ModelName.ShouldBe("Orion 4.2");
            rows[2].ModelName.ShouldBe("Cirrus 3 Flash");
            rows[0].Score.Text.ShouldBe("84.6%");
            rows[0].Bar.Target.ShouldBe(0.846, 1e-9);
            // The rank counts every rival with a score (four), not only the two rows on screen.
            rows[0].Readout.ShouldBe("Lumen 5 Pro: 84.6%, 1st of 4, leads Orion 4.2 by 2.7");
            rows[2].Readout.ShouldBe("Cirrus 3 Flash: 66.3%, 4th of 4, 18.3 behind Lumen 5 Pro");
            AutomationProperties.GetName(rows[1]).ShouldBe("Orion 4.2: 81.9%, 2nd of 4, 2.7 behind Lumen 5 Pro");
            Tooltip.GetTip(rows[1]).ShouldBe(rows[1].Readout);
            // At rest the readout is the model's own row.
            card.ReadoutText!.Text.ShouldBe(rows[0].Readout);
            // The bars have been revealed (motion is off, so at once).
            rows[0].Bar.IsArmed.ShouldBeFalse();
            rows[0].Bar.Shown.ShouldBe(0.846, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_Or_Focusing_A_Row_Shows_Its_Readout_And_Dims_The_Others()
    {
        var (card, window) = Open();
        try
        {
            var rows = card.BenchmarkRowParts;
            rows[2].RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, rows[2], new Pointer(1, PointerType.Mouse, true),
                window, default, 0, PointerPointProperties.None, KeyModifiers.None));
            Pump();
            card.ActiveRow.ShouldBe(2);
            card.ReadoutText!.Text.ShouldBe(rows[2].Readout);
            rows[2].Classes.Contains(ModelCardBenchmarkRow.HighlightedPseudoClass).ShouldBeTrue();
            rows[0].Classes.Contains(ModelCardBenchmarkRow.DimmedPseudoClass).ShouldBeTrue();
            rows[2].Classes.Contains(ModelCardBenchmarkRow.DimmedPseudoClass).ShouldBeFalse();
            rows[2].RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, rows[2], new Pointer(1, PointerType.Mouse, true),
                window, default, 0, PointerPointProperties.None, KeyModifiers.None));
            Pump();
            card.ActiveRow.ShouldBeNull();
            rows[0].Classes.Contains(ModelCardBenchmarkRow.DimmedPseudoClass).ShouldBeFalse();
            card.ReadoutText.Text.ShouldBe(rows[0].Readout);

            rows[1].Focus(NavigationMethod.Tab);
            Pump();
            card.ActiveRow.ShouldBe(1);
            card.ReadoutText.Text.ShouldBe(rows[1].Readout);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Switcher_Offers_Only_Scored_Benchmarks_And_Raises_BenchmarkChanged()
    {
        var (card, window) = Open(c => c.Model = Sample.Rivals[2]);
        try
        {
            // Kestrel 2 has no GPQA score, so the switcher offers two benchmarks.
            card.OfferedBenchmarks.Select(b => b.Id).ShouldBe(["swe-bench", "terminal-bench"]);
            card.Switcher!.ItemCount.ShouldBe(2);
            var changes = new List<string>();
            card.BenchmarkChanged += (_, e) => changes.Add(e.Id);
            card.Switcher.SelectedIndex = 1;
            Pump();
            card.Benchmark.ShouldBe("terminal-bench");
            changes.ShouldBe(["terminal-bench"]);
            card.BenchmarkRowParts[0].Readout.ShouldBe("Kestrel 2: 63.5%, 1st of 3, leads Orion 4.2 by 5.5");
            card.StatusText!.Text.ShouldBe("Showing Terminal-Bench 2.0");

            // A rival with no score on the chosen benchmark shows None and says so.
            card.Model = Sample.Model;
            card.Rivals = [Sample.Rivals[2], Sample.Rivals[0]];
            card.Benchmark = "gpqa";
            Pump();
            card.BenchmarkRowParts[1].Score.Text.ShouldBe("None");
            card.BenchmarkRowParts[1].Readout.ShouldBe("Kestrel 2 has no score on this benchmark");
            card.BenchmarkRowParts[0].Readout.ShouldBe("Lumen 5 Pro: 88.2%, 2nd of 2, 0.9 behind Orion 4.2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Model_Without_Scores_Hides_The_Benchmarks()
    {
        var (card, window) = Open(c => c.Model = Plain("plain", "Plain 1"));
        try
        {
            card.CurrentBenchmark.ShouldBeNull();
            card.BenchmarkSection!.IsVisible.ShouldBeFalse();
            card.CutoffText!.Text.ShouldBe("Not listed");
            card.MaxOutputValue!.Text.ShouldBe("Not listed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Volume_Slider_Steps_Through_The_Stops_And_Rolls_The_Cost()
    {
        var (card, window) = Open();
        try
        {
            var changes = new List<long>();
            card.MonthlyTokensChanged += (_, e) => changes.Add(e.Tokens);
            card.VolumeSlider!.Value = 4;
            Pump();
            card.MonthlyTokens.ShouldBe(50_000_000);
            changes.ShouldBe([50_000_000L]);
            card.CostValue!.Text.ShouldBe("$500.00/mo");
            AutomationProperties.GetName(card.CostValue).ShouldBe("Estimated $500.00/mo a month at 50M tokens");

            // Keyboard: the slider steps one stop per arrow.
            card.VolumeSlider.Rail!.Thumbs[0].Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            card.MonthlyTokens.ShouldBe(100_000_000);
            Press(window, PhysicalKey.End);
            card.MonthlyTokens.ShouldBe(1_000_000_000);
            card.CostValue.Text.ShouldBe("$10,000/mo");
            Press(window, PhysicalKey.Home);
            card.MonthlyTokens.ShouldBe(1_000_000);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MonthlyTokens_Snaps_To_The_Nearest_Stop()
    {
        var card = new ModelCard { MonthlyTokens = 60_000_000 };
        card.MonthlyTokens.ShouldBe(50_000_000);
        card.MonthlyTokens = 3_000_000_000;
        card.MonthlyTokens.ShouldBe(1_000_000_000);
        card.MonthlyTokens = 0;
        card.MonthlyTokens.ShouldBe(1_000_000);
    }

    [AvaloniaFact]
    public void The_Compare_Switch_Crossfades_To_The_Table_And_Announces_It()
    {
        var (card, window) = Open();
        try
        {
            var views = new List<ModelCardView>();
            card.ViewChanged += (_, e) => views.Add(e.View);
            card.CompareSwitch!.IsChecked = true;
            Pump();
            card.View.ShouldBe(ModelCardView.Compare);
            views.ShouldBe([ModelCardView.Compare]);
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Compare);
            card.PanelOf(ModelCardPanelKind.Compare)!.Opacity.ShouldBe(1);
            card.PanelOf(ModelCardPanelKind.Details)!.Opacity.ShouldBe(0);
            card.PanelOf(ModelCardPanelKind.Details)!.IsHitTestVisible.ShouldBeFalse();
            KeyboardNavigation.GetTabNavigation(card.PanelOf(ModelCardPanelKind.Details)!).ShouldBe(KeyboardNavigationMode.None);
            card.StatusText!.Text.ShouldBe("Comparing Lumen 5 Pro with Orion 4.2");
            AutomationProperties.GetLiveSetting(card.StatusText).ShouldBe(AutomationLiveSetting.Polite);

            card.CompareSwitch.IsChecked = false;
            Pump();
            card.View.ShouldBe(ModelCardView.Details);
            card.StatusText.Text.ShouldBe("Showing details for Lumen 5 Pro");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Compare_Table_Marks_The_Better_Value_With_Its_Margin()
    {
        var (card, window) = Open(c => c.View = ModelCardView.Compare);
        try
        {
            var rows = card.CompareRowParts;
            rows.Select(r => r.Label).ShouldBe([
                "Context window", "Max output", "Knowledge cutoff", "Input per 1M", "Output per 1M", "Cached input per 1M",
                "Cost at 100M/mo", "SWE-bench Verified", "GPQA Diamond", "Terminal-Bench 2.0"
            ]);
            // 1M against 400K: the model, by 2.5x.
            rows[0].Cell(0).IsBetter.ShouldBeTrue();
            rows[0].Cell(0).MarginText.ShouldBe("2.5x");
            rows[0].Cell(0).ShowsMargin.ShouldBeTrue();
            AutomationProperties.GetName(rows[0].Cell(0)).ShouldBe("Lumen 5 Pro: 1M, better");
            // Equal max output: neither.
            rows[1].Cell(0).IsBetter.ShouldBeFalse();
            rows[1].Cell(1).IsBetter.ShouldBeFalse();
            // Apr 2026 against Jan 2026.
            rows[2].Cell(0).MarginText.ShouldBe("+3 mo");
            // $5 against $2.50: the rival is half the price.
            rows[3].Cell(1).IsBetter.ShouldBeTrue();
            rows[3].Cell(1).MarginText.ShouldBe("2x");
            // $25 against $15: the rival, 40% less.
            rows[4].Cell(1).MarginText.ShouldBe("−40%");
            // SWE-bench: the model by 2.7 points; GPQA: the rival by 0.9.
            rows[7].Cell(0).MarginText.ShouldBe("+2.7");
            rows[8].Cell(1).MarginText.ShouldBe("+0.9");
            rows[7].Cell(0).Bar.IsVisible.ShouldBeTrue();
            // Model wins: context, cutoff, SWE-bench, Terminal-Bench (4); rival: input, output, cached, cost, GPQA (5).
            card.TallyText!.Text.ShouldBe("Orion 4.2 leads on 5 of 10");
            card.FootnoteText!.Text.ShouldBe("Prices and scores are samples.");

            // The cost row follows the volume.
            card.MonthlyTokens = 50_000_000;
            Pump();
            rows[6].Label.ShouldBe("Cost at 50M/mo");
            rows[6].Cell(0).Number.Text.ShouldBe("$500.00");

            card.IsSample = false;
            Pump();
            card.FootnoteText.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Rival_Raises_CompareWithChanged()
    {
        var (card, window) = Open(c => c.View = ModelCardView.Compare);
        try
        {
            var picks = new List<string>();
            card.CompareWithChanged += (_, e) => picks.Add(e.Id);
            var menu = card.RivalMenu!;
            menu.Items.Count.ShouldBe(3);
            ((MenuEntry)menu.Items[0]!).IsChecked.ShouldBeTrue();
            var cirrus = (MenuEntry)menu.Items[1]!;
            cirrus.Header.ShouldBe("Cirrus 3 Flash");
            cirrus.PerformClick();
            Pump();
            card.CompareWith.ShouldBe("cirrus-3-flash");
            picks.ShouldBe(["cirrus-3-flash"]);
            card.CurrentRival!.Name.ShouldBe("Cirrus 3 Flash");
            card.StatusText!.Text.ShouldBe("Comparing Lumen 5 Pro with Cirrus 3 Flash");
            card.CompareRowParts[0].Cell(1).IsBetter.ShouldBeTrue();
            card.CompareRowParts[0].Cell(1).MarginText.ShouldBe("2x");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Rival_Menu_Opens_From_The_Keyboard()
    {
        var (card, window) = Open(c => c.View = ModelCardView.Compare);
        try
        {
            card.RivalButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            card.RivalMenu!.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            card.RivalMenu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Model_Itself_Is_Filtered_From_The_Rivals_And_No_Rivals_Hide_Compare()
    {
        var (card, window) = Open();
        try
        {
            card.Rivals = [Sample.Model, .. Sample.Rivals];
            Pump();
            card.ShownRivals.Count.ShouldBe(3);
            card.BenchmarkRowParts[1].ModelName.ShouldBe("Orion 4.2");

            card.Rivals = [];
            card.View = ModelCardView.Compare;
            Pump();
            card.CompareSwitch!.IsVisible.ShouldBeFalse();
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Details);
            card.BenchmarkRowParts[1].Classes.Contains(ModelCardBenchmarkRow.EmptyPseudoClass).ShouldBeTrue();
            card.BenchmarkRowParts[0].Readout.ShouldBe("Lumen 5 Pro: 84.6%, 1st of 1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_The_Skeleton_And_Disables_Compare()
    {
        var (card, window) = Open();
        try
        {
            card.IsLoading = true;
            Pump();
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Loading);
            card.CompareSwitch!.IsEnabled.ShouldBeFalse();
            card.StatusText!.Text.ShouldBe("Loading model details");
            card.IsLoading = false;
            Pump();
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Details);
            card.CompareSwitch.IsEnabled.ShouldBeTrue();
            ShouldShowOnly(card, ModelCardPanelKind.Details);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_The_Shown_Panel_Is_Visible_After_The_Card_Loads()
    {
        // The skeleton pulse once reset the hidden loading panel to full opacity, drawing it over the details.
        var (card, window) = Open();
        try
        {
            ShouldShowOnly(card, ModelCardPanelKind.Details);
        }
        finally
        {
            window.Close();
        }
    }

    private static void ShouldShowOnly(ModelCard card, ModelCardPanelKind shown)
    {
        foreach (var kind in Enum.GetValues<ModelCardPanelKind>())
        {
            card.PanelOf(kind)!.Opacity.ShouldBe(kind == shown ? 1 : 0, kind.ToString());
        }
    }

    [AvaloniaFact]
    public void An_Error_Replaces_The_Body_And_Offers_Retry()
    {
        var (card, window) = Open(c =>
        {
            c.ErrorText = "Could not load the model's details.";
            c.CanRetry = true;
        });
        try
        {
            card.ShownPanel.ShouldBe(ModelCardPanelKind.Error);
            card.NameText!.Text.ShouldBe("Lumen 5 Pro");
            card.ErrorMessage!.Text.ShouldBe("Could not load the model's details.");
            AutomationProperties.GetLiveSetting(card.ErrorMessage).ShouldBe(AutomationLiveSetting.Assertive);
            card.RetryButton!.IsVisible.ShouldBeTrue();
            var retries = 0;
            card.RetryRequested += (_, _) => retries++;
            card.RetryButton.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            retries.ShouldBe(1);
            card.CanRetry = false;
            Pump();
            card.RetryButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Use_Model_Raises_SelectRequested_And_Shows_Selected()
    {
        var model = Plain("plain", "Plain 1");
        var (card, window) = Open(c =>
        {
            c.Model = model;
            c.CanSelect = true;
        });
        try
        {
            card.SelectButton!.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(card.SelectButton).ShouldBe("Use model");
            ModelCardModel? picked = null;
            card.SelectRequested += (_, e) => picked = e.Model;
            card.SelectButton.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            picked.ShouldBe(model);
            card.IsSelected = true;
            Pump();
            AutomationProperties.GetName(card.SelectButton).ShouldBe("Selected");
            card.SelectLabel = "Pick";
            card.IsSelected = false;
            Pump();
            AutomationProperties.GetName(card.SelectButton).ShouldBe("Pick");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Keeps_One_Size_Across_Views_States_And_Models()
    {
        var (card, window) = Open();
        try
        {
            var height = card.Bounds.Height;
            height.ShouldBeGreaterThan(300);
            var width = card.Bounds.Width;
            void Same()
            {
                Pump();
                card.Bounds.Height.ShouldBe(height, 0.5);
                card.Bounds.Width.ShouldBe(width, 0.5);
            }
            card.View = ModelCardView.Compare;
            Same();
            card.View = ModelCardView.Details;
            Same();
            card.IsLoading = true;
            Same();
            card.IsLoading = false;
            card.ErrorText = "Failed";
            Same();
            card.ErrorText = null;
            foreach (var rival in Sample.Rivals)
            {
                card.Model = rival;
                Same();
            }
            card.Model = Sample.Model;
            card.Benchmark = "gpqa";
            Same();
            card.MonthlyTokens = 1_000_000_000;
            Same();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Stack_Specs_Estimate_Switcher_And_Footer()
    {
        var (card, window) = Open(width: 600);
        try
        {
            card.IsNarrow.ShouldBeFalse();
            card.SpecCells[0].IsStacked.ShouldBeFalse();
            card.SummaryText!.Lines.ShouldBe(2);
            card.ReadoutText!.Lines.ShouldBe(2);
            card.IsCompareWide.ShouldBeTrue();

            window.Width = 460;
            Pump();
            card.IsNarrow.ShouldBeTrue();
            card.IsCompact.ShouldBeFalse();
            card.Classes.Contains(ModelCard.NarrowPseudoClass).ShouldBeTrue();
            card.SpecCells[0].IsStacked.ShouldBeTrue();
            card.PriceCells[0].IsStacked.ShouldBeTrue();
            card.ReadoutText.Lines.ShouldBe(3);
            card.SummaryText.Lines.ShouldBe(2);
            card.IsCompareWide.ShouldBeFalse();
            Grid.GetRow(card.CostValue!).ShouldBe(1);
            Grid.GetRow(card.Switcher!).ShouldBe(1);

            window.Width = 400;
            Pump();
            card.IsCompact.ShouldBeTrue();
            card.SummaryText.Lines.ShouldBe(3);
            card.BenchmarkRowParts[0].IsStacked.ShouldBeFalse();

            window.Width = 340;
            Pump();
            card.IsTiny.ShouldBeTrue();
            card.BenchmarkRowParts[0].IsStacked.ShouldBeTrue();
            card.View = ModelCardView.Compare;
            Pump();
            card.CompareRowParts[0].Cell(0).ShowsMargin.ShouldBeFalse();
            card.CompareRowParts[7].Cell(0).Bar.IsVisible.ShouldBeFalse();
            // Nothing runs off the card at 320px.
            window.Width = 320;
            Pump();
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(320.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Is_Fluid_Up_To_640()
    {
        var (card, window) = Open(width: 900);
        try
        {
            card.Bounds.Width.ShouldBe(640, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Skips_The_Hidden_Panel()
    {
        var (card, window) = Open();
        try
        {
            card.CompareSwitch!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Tab);
            var focused = window.FocusManager!.GetFocusedElement() as Visual;
            focused.ShouldNotBeNull();
            card.PanelOf(ModelCardPanelKind.Details)!.IsVisualAncestorOf(focused).ShouldBeTrue();
            card.PanelOf(ModelCardPanelKind.Compare)!.IsVisualAncestorOf(focused).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Model_Id_Carries_The_Shown_Id()
    {
        var (card, window) = Open(c => c.Model = Sample.Rivals[1]);
        try
        {
            card.CopyIdButton!.Value.ShouldBe("cirrus-3-flash");
            AutomationProperties.GetName(card.CopyIdButton).ShouldBe("Copy model id");
            card.IdText!.Text.ShouldBe("cirrus-3-flash");
            card.InputPrice!.Text.ShouldBe("$0.30");
            card.CachedPrice!.Text.ShouldBe("$0.075");
            card.ContextValue!.Text.ShouldBe("2M");
            card.Gauge!.Target.ShouldBe(1, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Cached_Prices_In_The_Pool_Two_Price_Cells_Show()
    {
        var (card, window) = Open(c =>
        {
            c.Model = Plain("a", "Alpha", 50);
            c.Rivals = [Plain("b", "Beta", 60)];
            c.Currency = "EUR";
        });
        try
        {
            card.PriceCells.Count(c => c.IsVisible).ShouldBe(2);
            card.InputPrice!.Text.ShouldBe("€1.00");
            card.View = ModelCardView.Compare;
            Pump();
            card.CompareRowParts.Any(r => r.Label == "Cached input per 1M").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Card()
    {
        var (card, window) = Open(c => c.Labels = new ModelCardLabels
        {
            Compare = "Vergleichen",
            ContextWindow = "Kontextfenster",
            Rank = (rank, count) => $"Platz {rank} von {count}",
            Leads = (margin, name) => $"vor {name} um {margin}",
        });
        try
        {
            card.CompareSwitch!.Content.ShouldBe("Vergleichen");
            card.SpecCells[0].Label.ShouldBe("Kontextfenster");
            card.BenchmarkRowParts[0].Readout.ShouldBe("Lumen 5 Pro: 84.6%, Platz 1 von 4, vor Orion 4.2 um 2.7");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Numbers_Roll_With_Full_Motion_And_Change_At_Once_When_Reduced()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            card.Model = Sample.Rivals[0];
            Pump();
            card.InputPrice!.Digits.IsInstant.ShouldBeFalse();
            card.InputPrice.Text.ShouldBe("$2.50");
        }
        finally
        {
            window.Close();
        }
        var (reduced, reducedWindow) = Open(level: ArcMotionLevel.Reduced);
        try
        {
            reduced.Model = Sample.Rivals[0];
            Pump();
            reduced.InputPrice!.Digits.IsInstant.ShouldBeTrue();
        }
        finally
        {
            reducedWindow.Close();
        }
    }

    [Fact]
    public void Formatting_Writes_Counts_Months_Prices_And_Margins()
    {
        ModelCardFormat.TokensText(1_000_000).ShouldBe("1M");
        ModelCardFormat.TokensText(64_000).ShouldBe("64K");
        ModelCardFormat.TokensText(1_050_000).ShouldBe("1.05M");
        ModelCardFormat.TokensText(1_000_000_000).ShouldBe("1B");
        ModelCardFormat.TokensText(512).ShouldBe("512");
        ModelCardFormat.Month("2026-03", "Not listed").ShouldBe("Mar 2026");
        ModelCardFormat.Month("Early 2026", "Not listed").ShouldBe("Early 2026");
        ModelCardFormat.Month(null, "Not listed").ShouldBe("Not listed");
        ModelCardFormat.Price(3, "USD").Text.ShouldBe("$3.00");
        ModelCardFormat.Price(0.075, "USD").Text.ShouldBe("$0.075");
        ModelCardFormat.Price(3, "CHF").Text.ShouldBe("CHF 3.00");
        ModelCardFormat.Cost(12_345.6, "USD").Text.ShouldBe("$12,346");
        ModelCardFormat.Pages(1_000_000).ShouldBe(1500);
        ModelCardFormat.SnapIndex(60_000_000).ShouldBe(4);
        ModelCardFormat.SnapIndex(80_000_000).ShouldBe(5);
        ModelCardFormat.MonthlyCost(null, 1_000_000, 0.75).ShouldBeNull();
        ModelCardFormat.Rank(80, [90, 80, 80, 70]).ShouldBe(2);
        ModelCardFormat.Compare(82.1, 79.8, ModelCardCompareKind.Score).ShouldBe(new ModelCardVerdict(1, "+2.3"));
        ModelCardFormat.Compare(4, 5, ModelCardCompareKind.Price).ShouldBe(new ModelCardVerdict(1, "−20%"));
        ModelCardFormat.Compare(10, 5, ModelCardCompareKind.Price).ShouldBe(new ModelCardVerdict(2, "2x"));
        ModelCardFormat.Compare(250_000, 200_000, ModelCardCompareKind.Count).ShouldBe(new ModelCardVerdict(1, "+25%"));
        ModelCardFormat.Compare(5, 5, ModelCardCompareKind.Count).Winner.ShouldBe(0);
        ModelCardFormat.Compare(null, 5, ModelCardCompareKind.Count).Winner.ShouldBe(0);
        ModelCardLabels.Ordinal(1).ShouldBe("1st");
        ModelCardLabels.Ordinal(12).ShouldBe("12th");
        ModelCardLabels.Ordinal(23).ShouldBe("23rd");
    }
}
