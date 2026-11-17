using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// LlmCostCalculator: the arithmetic and the money formats, the empty panel without models, the test's own invented models
/// and presets (passed as data), the sliders, the switch and the presets
/// writing the workload, controlled values between stops, the selection and its keyboard model, the loading, error and
/// empty states, the breakpoints, the status region, the labels, and the rank spring with full and reduced motion.
/// </summary>
public class LlmCostCalculatorTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>The test's own invented models and presets (made-up names and prices).</summary>
    private static class Sample
    {
        public static List<LlmModelPrice> Models() =>
        [
            new("atlas-mini", "Atlas Mini", "Northwind", 0.15, 0.6, 0.015, ArcIcons.Sparkles),
            new("atlas-pro", "Atlas Pro", "Northwind", 2.5, 10, 0.25, ArcIcons.Sparkles),
            new("cirrus-flash", "Cirrus Flash", "Lumen Labs", 0.3, 2.5, 0.03, ArcIcons.Zap),
            new("cirrus-ultra", "Cirrus Ultra", "Lumen Labs", 1.25, 10, 0.125, ArcIcons.Zap),
            new("vega-large", "Vega Large", "Meridian", 3, 15, 0.3, ArcIcons.Brain),
            new("vega-max", "Vega Max", "Meridian", 15, 75, 1.5, ArcIcons.Brain),
            // No cache price and no mark: cached tokens bill at the input price, and the row shows a monogram.
            new("orca-open", "Orca Open 70B", "Tidewater", 0.6, 0.8),
            new("kestrel-r", "Kestrel Reasoner", "Halcyon", 0.55, 2.2, 0.14, ArcIcons.Cpu),
        ];

        public static List<LlmUsagePreset> Presets() =>
        [
            new("chat", "Chat", new LlmUsage(2_000, 500, 10_000, false)),
            new("rag", "RAG", new LlmUsage(16_000, 500, 5_000, true)),
            new("agent", "Agent", new LlmUsage(32_000, 1_000, 2_500, true)),
            new("batch", "Batch", new LlmUsage(4_000, 1_000, 100_000, false)),
        ];
    }

    /// <summary>Opens a panel on <see cref="Sample"/>'s models and presets, unless <paramref name="empty"/>.</summary>
    private static (LlmCostCalculator Calc, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off,
        Action<LlmCostCalculator>? setup = null, double width = 760, bool empty = false)
    {
        var calc = new LlmCostCalculator { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (!empty)
        {
            calc.Models = Sample.Models();
            calc.Presets = Sample.Presets();
        }
        setup?.Invoke(calc);
        var window = TestHelpers.Show(calc, level);
        window.Width = width;
        window.Height = 2400;
        Pump();
        return (calc, window);
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

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static LlmModelPrice Model(string id, double input, double output, double? cached = null) =>
        new(id, id, "Test", input, output, cached);

    #region Arithmetic and formats

    [Fact]
    public void ComputeCost_Bills_Uncached_Cached_And_Output_Per_Month()
    {
        var model = Model("m", input: 2.5, output: 10, cached: 0.25);
        var cost = LlmCostCalculator.ComputeCost(model, new LlmUsage(16_000, 500, 5_000, true), 0.6, 30);
        // 150,000 requests: 2.4B input tokens (60% cached), 75M output tokens.
        cost.Input.ShouldBe(2400, 1e-6);
        cost.CachedInput.ShouldBe(360, 1e-6);
        cost.Output.ShouldBe(750, 1e-6);
        cost.Total.ShouldBe(3510, 1e-6);
        cost.PerRequest.ShouldBe(0.0234, 1e-9);
    }

    [Fact]
    public void ComputeCost_Without_A_Cache_Price_Bills_Cached_Tokens_At_The_Input_Price()
    {
        var model = Model("m", input: 1, output: 0);
        var cached = LlmCostCalculator.ComputeCost(model, new LlmUsage(1_000_000, 0, 1, true), 0.5, 1);
        cached.Input.ShouldBe(0.5, 1e-9);
        cached.CachedInput.ShouldBe(0.5, 1e-9);
        cached.Total.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void ComputeCost_With_Caching_Off_Bills_No_Cached_Tokens_And_Clamps_Bad_Inputs()
    {
        var model = Model("m", input: 3, output: 15, cached: 0.3);
        var off = LlmCostCalculator.ComputeCost(model, new LlmUsage(10_000, 1_000, 100, false), 0.9, 30);
        off.CachedInput.ShouldBe(0);
        off.Input.ShouldBe(10_000 * 3000 / 1e6 * 3, 1e-9);
        LlmCostCalculator.ComputeCost(model, new LlmUsage(10_000, 1_000, 0, true)).PerRequest.ShouldBe(0);
        LlmCostCalculator.ComputeCost(model, new LlmUsage(10_000, 0, 100, true), cacheHitRate: 7).Input.ShouldBe(0);
    }

    [Fact]
    public void Monthly_Figures_Go_Compact_From_100000_And_The_Total_Keeps_Every_Digit()
    {
        LlmCostCalculatorMath.Monthly(45.204).ShouldBe(new LlmCostFigure(45.2, 2, ""));
        LlmCostCalculatorMath.Monthly(12_345.6).ShouldBe(new LlmCostFigure(12_346, 0, ""));
        LlmCostCalculatorMath.Monthly(99_999).Suffix.ShouldBe("");
        LlmCostCalculatorMath.Monthly(420_000).ShouldBe(new LlmCostFigure(420, 0, "K"));
        LlmCostCalculatorMath.Monthly(4_200_000).ShouldBe(new LlmCostFigure(4.2, 1, "M"));
        LlmCostCalculatorMath.Monthly(420_000).Text.ShouldBe("$420K");
        LlmCostCalculatorMath.Total(4_200_000.456).Text.ShouldBe("$4,200,000.46");
        LlmCostCalculatorMath.PerRequest(0.02345).Text.ShouldBe("$0.0235");
        LlmCostCalculatorMath.Price(3).ShouldBe("$3");
        LlmCostCalculatorMath.Price(0.3).ShouldBe("$0.30");
        LlmCostCalculatorMath.Price(0.025).ShouldBe("$0.025");
    }

    [Fact]
    public void Stops_And_Ranking()
    {
        LlmCostCalculatorMath.InputStops[0].ShouldBe(250);
        LlmCostCalculatorMath.InputStops[^1].ShouldBe(200_000);
        LlmCostCalculatorMath.OutputStops[0].ShouldBe(100);
        LlmCostCalculatorMath.OutputStops[^1].ShouldBe(16_000);
        LlmCostCalculatorMath.RequestStops[0].ShouldBe(100);
        LlmCostCalculatorMath.RequestStops[^1].ShouldBe(1_000_000);
        LlmCostCalculatorMath.NearestStop(LlmCostCalculatorMath.InputStops, 15_000)
            .ShouldBe(Array.IndexOf(LlmCostCalculatorMath.InputStops, 16_000));
        LlmCostCalculatorMath.NearestStop(LlmCostCalculatorMath.InputStops, 1).ShouldBe(0);
        LlmCostCalculatorMath.NearestStop(LlmCostCalculatorMath.InputStops, 9_999_999).ShouldBe(LlmCostCalculatorMath.InputStops.Length - 1);
        var costs = new[] { new LlmCost(0, 0, 0, 5, 0), new LlmCost(0, 0, 0, 1, 0), new LlmCost(0, 0, 0, 5, 0) };
        LlmCostCalculatorMath.Rank(costs).ShouldBe([1, 0, 2]);
    }

    #endregion

    #region Data and inputs

    [AvaloniaFact]
    public void Without_Models_Or_Presets_The_Panel_Is_Empty()
    {
        var (calc, window) = Open(empty: true);
        try
        {
            calc.Models.ShouldBeNull();
            calc.Classes.Contains(LlmCostCalculator.EmptyPseudoClass).ShouldBeTrue();
            calc.Rows.Count.ShouldBe(0);
            calc.SelectedModel.ShouldBeNull();
            calc.DisplayedPresets.ShouldBeEmpty();
            calc.PresetsControl!.IsVisible.ShouldBeFalse();
            calc.Usage.ShouldBe(new LlmUsage(16_000, 500, 5_000, true));
            // Models fill it in.
            calc.Models = Sample.Models();
            Pump();
            calc.Classes.Contains(LlmCostCalculator.EmptyPseudoClass).ShouldBeFalse();
            calc.Rows.Count.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opens_On_The_Given_Models_Ranked_By_Cost_With_The_Second_Model_Selected()
    {
        var (calc, window) = Open();
        try
        {
            calc.Rows.Count.ShouldBe(8);
            calc.Usage.ShouldBe(new LlmUsage(16_000, 500, 5_000, true));
            calc.SelectedModel!.Id.ShouldBe(calc.DisplayedModels[1].Id);
            var ranked = calc.RankedRows;
            for (var i = 1; i < ranked.Count; i++)
            {
                LlmCostCalculator.ComputeCost(ranked[i].Model, calc.Usage).Total
                    .ShouldBeGreaterThanOrEqualTo(LlmCostCalculator.ComputeCost(ranked[i - 1].Model, calc.Usage).Total);
                ranked[i].TargetY.ShouldBe(i * calc.List!.RowHeight);
            }
            // The most expensive model fills its bar; the others are shares of it.
            ranked[^1].Fraction.ShouldBe(1);
            ranked[0].Fraction.ShouldBeLessThan(1);
            calc.TotalDigits!.Value.ShouldBe(Math.Round(calc.SelectedCost.Total, 2));
            calc.TotalDigits.Prefix.ShouldBe("$");
            calc.PresetsControl!.SelectedValue.ShouldBe("rag");
            calc.CacheSwitch!.IsChecked.ShouldBe(true);
            calc.InputSlider!.Value.ShouldBe(Array.IndexOf(LlmCostCalculatorMath.InputStops, 16_000));
            calc.ShownName.ShouldBe("Atlas Pro · Northwind");
            calc.ShownPriceLine.ShouldBe("$2.50 in · $0.25 cached · $10 out per 1M tokens");
            calc.DisplayedModels.Select(m => m.Id).ShouldBe(Sample.Models().Select(m => m.Id));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Slider_Step_Writes_The_Usage_Raises_UsageChanged_And_Lights_Custom()
    {
        var (calc, window) = Open();
        try
        {
            var events = new List<LlmUsageChangedEventArgs>();
            calc.UsageChanged += (_, e) => events.Add(e);
            calc.RequestsSlider!.Value += 1;
            Pump();
            events.Count.ShouldBe(1);
            events[0].OldUsage.RequestsPerDay.ShouldBe(5_000);
            events[0].Usage.RequestsPerDay.ShouldBe(10_000);
            calc.Usage.RequestsPerDay.ShouldBe(10_000);
            calc.MatchedPresetId.ShouldBeNull();
            calc.PresetsControl!.SelectedValue.ShouldNotBe("rag");
            calc.SelectedCost.Total.ShouldBe(LlmCostCalculator.ComputeCost(calc.SelectedModel!, calc.Usage).Total, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sliders_Speak_Real_Counts_Not_Stop_Indexes()
    {
        var (calc, window) = Open();
        try
        {
            calc.InputSlider!.ValueFormatter!(calc.InputSlider.Value).ShouldBe("16,000");
            calc.RequestsSlider!.ValueFormatter!(calc.RequestsSlider.Maximum).ShouldBe("1,000,000");
            calc.InputSlider.Label.ShouldBe("Input tokens");
            calc.InputSlider.LargeChange.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Preset_Sets_Its_Workload_And_The_Switch_Toggles_Caching()
    {
        var (calc, window) = Open();
        try
        {
            var events = new List<LlmUsage>();
            calc.UsageChanged += (_, e) => events.Add(e.Usage);
            calc.PresetsControl!.SelectedValue = "batch";
            Pump();
            calc.Usage.ShouldBe(calc.DisplayedPresets.Single(p => p.Id == "batch").Usage);
            calc.CacheSwitch!.IsChecked = true;
            Pump();
            calc.Usage.Cached.ShouldBeTrue();
            calc.Classes.Contains(LlmCostCalculator.CachedPseudoClass).ShouldBeTrue();
            events.Count.ShouldBe(2);
            calc.MatchedPresetId.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Controlled_Usage_Between_Stops_Computes_Exactly_And_Shows_The_Nearest_Stop()
    {
        var (calc, window) = Open();
        try
        {
            var raised = 0;
            calc.UsageChanged += (_, _) => raised++;
            calc.Usage = new LlmUsage(15_000, 700, 4_321, false);
            Pump();
            raised.ShouldBe(0);
            calc.Usage.ShouldBe(new LlmUsage(15_000, 700, 4_321, false));
            calc.InputSlider!.Value.ShouldBe(Array.IndexOf(LlmCostCalculatorMath.InputStops, 16_000));
            calc.RequestsSlider!.Value.ShouldBe(Array.IndexOf(LlmCostCalculatorMath.RequestStops, 5_000));
            calc.CacheSwitch!.IsChecked.ShouldBe(false);
            calc.SelectedCost.ShouldBe(LlmCostCalculator.ComputeCost(calc.SelectedModel!, calc.Usage));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Preset_List_Hides_The_Row()
    {
        var (calc, window) = Open(setup: c => c.Presets = new List<LlmUsagePreset>());
        try
        {
            calc.PresetsControl!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Selection and keyboard

    [AvaloniaFact]
    public void An_Unknown_Id_Falls_Back_To_The_Cheapest_And_A_Known_Id_Selects_It()
    {
        var (calc, window) = Open(setup: c => c.SelectedId = "nope");
        try
        {
            calc.SelectedModel.ShouldBe(calc.RankedRows[0].Model);
            calc.SelectedId = "vega-max";
            Pump();
            calc.SelectedModel!.Id.ShouldBe("vega-max");
            calc.Rows.Single(r => r.IsSelected).Model.Id.ShouldBe("vega-max");
            calc.Rows.Count(r => r.IsTabStop).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Home_And_End_Select_By_Rank_And_Move_Focus()
    {
        var (calc, window) = Open();
        try
        {
            var picks = new List<LlmSelectedModelChangedEventArgs>();
            calc.SelectedModelChanged += (_, e) => picks.Add(e);
            var selected = calc.Rows.Single(r => r.IsSelected);
            selected.Focus(NavigationMethod.Tab);
            Pump();
            var rank = selected.Rank;

            Press(window, PhysicalKey.ArrowDown);
            calc.SelectedModel.ShouldBe(calc.RankedRows[rank + 1].Model);
            calc.RankedRows[rank + 1].IsFocused.ShouldBeTrue();
            picks.Count.ShouldBe(1);
            picks[0].Id.ShouldBe(calc.SelectedModel!.Id);
            picks[0].Cost.ShouldBe(calc.SelectedCost);

            Press(window, PhysicalKey.Home);
            calc.SelectedModel.ShouldBe(calc.RankedRows[0].Model);
            Press(window, PhysicalKey.ArrowUp);
            calc.SelectedModel.ShouldBe(calc.RankedRows[0].Model);
            Press(window, PhysicalKey.End);
            calc.SelectedModel.ShouldBe(calc.RankedRows[^1].Model);
            calc.RankedRows[^1].IsFocused.ShouldBeTrue();
            calc.SelectedId.ShouldBe(calc.RankedRows[^1].Model.Id);
            picks.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Are_Radio_Buttons_Named_With_Rank_And_Cost_And_Listed_By_Rank()
    {
        var (calc, window) = Open();
        try
        {
            var first = calc.RankedRows[0];
            var name = AutomationProperties.GetName(first);
            name.ShouldStartWith(first.Model.Name + ", " + first.Model.Provider + ", rank 1 of 8, $");
            name.ShouldEndWith(" a month");
            var peer = ControlAutomationPeer.CreatePeerForElement(first);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
            var listPeer = ControlAutomationPeer.CreatePeerForElement(calc.List!);
            listPeer.GetName().ShouldBe("Models ranked by monthly cost, lowest first");
            listPeer.GetChildren().Select(p => p.GetName()).ShouldBe(calc.RankedRows.Select(r => AutomationProperties.GetName(r)));

            // Selecting through the accessibility tree picks the row like a click.
            var picked = 0;
            calc.SelectedModelChanged += (_, _) => picked++;
            ((ISelectionItemProvider)peer).Select();
            Pump();
            calc.SelectedModel.ShouldBe(first.Model);
            ((ISelectionItemProvider)peer).IsSelected.ShouldBeTrue();
            picked.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region States

    [AvaloniaFact]
    public void Loading_Shows_Placeholders_At_The_Same_Height_And_Disables_The_Controls()
    {
        var (calc, window) = Open();
        try
        {
            var area = calc.List!.Parent as Control;
            var height = area!.Bounds.Height;
            calc.IsLoading = true;
            Pump();
            calc.Classes.Contains(LlmCostCalculator.LoadingPseudoClass).ShouldBeTrue();
            calc.Classes.Contains(LlmCostCalculator.NoDataPseudoClass).ShouldBeTrue();
            calc.Skeleton!.Rows.ShouldBe(8);
            calc.Skeleton.Opacity.ShouldBe(1);
            calc.Skeleton.IsPulsing.ShouldBeTrue();
            calc.List.Opacity.ShouldBe(0);
            calc.List.IsEnabled.ShouldBeFalse();
            calc.InputSlider!.IsEnabled.ShouldBeFalse();
            calc.CacheSwitch!.IsEnabled.ShouldBeFalse();
            calc.PresetsControl!.IsEnabled.ShouldBeFalse();
            calc.SelectedModel.ShouldBeNull();
            area.Bounds.Height.ShouldBe(height);

            calc.IsLoading = false;
            Pump();
            calc.Skeleton.IsPulsing.ShouldBeFalse();
            calc.InputSlider.IsEnabled.ShouldBeTrue();
            calc.SelectedModel.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Error_Replaces_The_List_And_Try_Again_Raises_RetryRequested()
    {
        var (calc, window) = Open(setup: c => c.AllowsRetry = true);
        try
        {
            calc.ErrorText = "Timed out";
            Pump();
            calc.Classes.Contains(LlmCostCalculator.ErrorPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(calc, LlmCostCalculator.ErrorTitlePart).Text.ShouldBe("Prices did not load");
            TestHelpers.Descendant<TextBlock>(calc, LlmCostCalculator.ErrorTextPart).Text.ShouldBe("Timed out");
            calc.RetryButton!.IsVisible.ShouldBeTrue();
            calc.RetryButton.IsEffectivelyEnabled.ShouldBeTrue();
            var retried = 0;
            calc.RetryRequested += (_, _) => retried++;
            calc.RetryButton.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            retried.ShouldBe(1);

            calc.AllowsRetry = false;
            Pump();
            calc.RetryButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Model_List_Shows_The_Empty_State()
    {
        var (calc, window) = Open(setup: c => c.Models = new List<LlmModelPrice>());
        try
        {
            calc.Classes.Contains(LlmCostCalculator.EmptyPseudoClass).ShouldBeTrue();
            calc.Rows.Count.ShouldBe(0);
            calc.SelectedModel.ShouldBeNull();
            calc.Skeleton!.Rows.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Models_Replace_The_Sample_And_Changes_To_The_List_Are_Followed()
    {
        var models = new Avalonia.Collections.AvaloniaList<LlmModelPrice>
        {
            Model("a", 1, 1),
            Model("b", 2, 2),
        };
        var (calc, window) = Open(setup: c => c.Models = models);
        try
        {
            calc.Rows.Count.ShouldBe(2);
            calc.SelectedModel!.Id.ShouldBe("b");
            var kept = calc.Rows[0];
            models.Add(Model("c", 0.1, 0.1));
            Pump();
            calc.Rows.Count.ShouldBe(3);
            calc.Rows[0].ShouldBeSameAs(kept);
            calc.RankedRows[0].Model.Id.ShouldBe("c");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Layout, status and labels

    [AvaloniaFact]
    public void The_Panel_Width_Picks_The_Layout()
    {
        var (calc, window) = Open(width: 700);
        try
        {
            calc.Classes.Contains(LlmCostCalculator.NarrowPseudoClass).ShouldBeFalse();
            calc.List!.RowHeight.ShouldBe(44);

            window.Width = 560;
            Pump();
            calc.Classes.Contains(LlmCostCalculator.NarrowPseudoClass).ShouldBeTrue();
            calc.Classes.Contains(LlmCostCalculator.CompactPseudoClass).ShouldBeFalse();

            window.Width = 400;
            Pump();
            calc.Classes.Contains(LlmCostCalculator.CompactPseudoClass).ShouldBeTrue();
            calc.List.RowHeight.ShouldBe(58);
            calc.Rows.All(r => r.IsCompact).ShouldBeTrue();
            calc.RankedRows[1].TargetY.ShouldBe(58);
            calc.Bounds.Width.ShouldBe(400);
            // The panel grows with the taller rows: the list area ends inside it.
            var area = (Control)calc.List.Parent!;
            area.Bounds.Height.ShouldBe(8 * 58);
            area.TranslatePoint(new Point(0, area.Bounds.Height), calc)!.Value.Y.ShouldBeLessThan(calc.Bounds.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Fills_Its_Container_Up_To_720()
    {
        var (calc, window) = Open(width: 1000);
        try
        {
            calc.Bounds.Width.ShouldBe(720);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Status_Waits_For_The_Numbers_To_Settle_Then_Speaks_Once()
    {
        var (calc, window) = Open();
        try
        {
            calc.RequestsSlider!.Value += 1;
            Pump();
            calc.IsAnnouncePending.ShouldBeTrue();
            calc.Announcer!.History.Count.ShouldBe(0);
            calc.Announce();
            calc.IsAnnouncePending.ShouldBeFalse();
            var text = calc.Announcer.Announcement!;
            text.ShouldStartWith("Atlas Pro costs $");
            text.ShouldContain(" a month, rank ");
            text.ShouldEndWith(" of 8.");
            calc.Announce();
            calc.Announcer.History.Count.ShouldBe(1);
            TestHelpers.PumpUntil(() => false, 600);
            calc.Announcer.History.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Texts_Reword_The_Panel()
    {
        var (calc, window) = Open();
        try
        {
            TestHelpers.Descendant<TextBlock>(calc, LlmCostCalculator.TitlePart).Text.ShouldBe("Estimated monthly cost");
            AutomationProperties.GetName(calc).ShouldBe("Estimated monthly cost");
            calc.Title = "Budget";
            calc.Labels = new LlmCostCalculatorLabels { PerMonth = "/mois", InputTokens = "Entrée", Custom = "Perso" };
            Pump();
            TestHelpers.Descendant<TextBlock>(calc, LlmCostCalculator.TitlePart).Text.ShouldBe("Budget");
            TestHelpers.Descendant<TextBlock>(calc, LlmCostCalculator.TotalUnitPart).Text.ShouldBe("/mois");
            calc.InputSlider!.Label.ShouldBe("Entrée");
            AutomationProperties.GetName(calc).ShouldBe("Budget");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    private static LlmUsage ReRankingUsage(LlmCostCalculator calc, out LlmCostCalculatorRow moved)
    {
        // A workload under which the ranking differs from the starting one.
        foreach (var preset in calc.DisplayedPresets)
        {
            var order = calc.DisplayedModels.OrderBy(m => LlmCostCalculator.ComputeCost(m, preset.Usage).Total).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                var row = calc.Rows.Single(r => ReferenceEquals(r.Model, order[i]));
                if (row.Rank != i)
                {
                    moved = row;
                    return preset.Usage;
                }
            }
        }
        throw new InvalidOperationException("No preset re-ranks the sample.");
    }

    [AvaloniaFact]
    public void A_Re_Rank_Glides_On_The_Rank_Spring_Without_Bouncing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (calc, window) = Open(ArcMotionLevel.Full);
        try
        {
            var usage = ReRankingUsage(calc, out var row);
            var from = row.ShownY;
            calc.Usage = usage;
            Pump();
            var to = row.TargetY;
            to.ShouldNotBe(from);
            row.ShownY.ShouldBe(from, 0.5);
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            var mid = row.ShownY;
            (Math.Sign(mid - from) == Math.Sign(to - from) && Math.Abs(mid - from) < Math.Abs(to - from)).ShouldBeTrue();
            var peak = 0.0;
            for (var i = 0; i < 60; i++)
            {
                clock.Advance(Frame, Frame);
                peak = Math.Max(peak, (row.ShownY - from) / (to - from));
            }
            row.ShownY.ShouldBe(to, 0.5);
            peak.ShouldBeLessThan(1.01);
            // Bars follow the same spring to their new share.
            calc.Rows.All(r => Math.Abs(r.Bar!.ShownFraction - r.Bar.TargetFraction) < 0.01).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_Rows_And_Bars_And_Holds_The_Pulse()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (calc, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            var usage = ReRankingUsage(calc, out var row);
            calc.Usage = usage;
            Pump();
            row.ShownY.ShouldBe(row.TargetY);
            calc.Rows.All(r => r.Bar!.ShownFraction == r.Bar.TargetFraction).ShouldBeTrue();
            calc.IsLoading = true;
            Pump();
            calc.Skeleton!.Pulse.IsRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    [AvaloniaFact]
    public void Prices_Roll_In_240ms_On_Ease_Out()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (calc, window) = Open(ArcMotionLevel.Full);
        try
        {
            var roll = MotionSpec.Tween(TimeSpan.FromMilliseconds(240), AiHeightBox.EaseOut);
            var figures = new[] { calc.TotalDigits, calc.PerRequestDigits }.Concat(calc.Rows.Select(r => r.CostDigits)).ToList();
            figures.ShouldAllBe(d => d != null && Equals(d.WheelMotion, roll));
            var usage = ReRankingUsage(calc, out var _);
            calc.Usage = usage;
            Pump();
            var total = calc.TotalDigits!;
            total.WheelPositions.ShouldNotBe(total.WheelTargets);
            clock.Advance(TimeSpan.FromMilliseconds(120), Frame);
            total.WheelPositions.ShouldNotBe(total.WheelTargets);
            // Every price lands at 240ms (a 0.25s spring would still be moving).
            clock.Advance(TimeSpan.FromMilliseconds(130), Frame);
            figures.ShouldAllBe(d => d!.WheelPositions.SequenceEqual(d.WheelTargets));
        }
        finally
        {
            window.Close();
        }
    }
}
