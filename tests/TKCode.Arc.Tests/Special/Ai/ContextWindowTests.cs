using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ContextWindow as a control: the sample session it opens on, Send / Attach / Compact now, the warning and the automatic
/// compaction (540ms after a crossing send, 600ms after a model switch, 700ms of summarizing), overflow, data written back
/// to Items, the model menu's keyboard model, focus kept inside while summarizing, the breakpoints and reduced motion.
/// </summary>
public class ContextWindowTests
{
    private static (ContextWindow Card, Window Window, ContextWindowTestTimers Timers) Open(ArcMotionLevel level = ArcMotionLevel.Off,
        Action<ContextWindow>? setup = null, double width = 792)
    {
        var timers = new ContextWindowTestTimers();
        var card = new ContextWindow { Timers = timers, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window, timers);
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

    private static void Repeat(Action action, int times)
    {
        for (var i = 0; i < times; i++)
        {
            action();
        }
    }

    private static ContextWindowLegendRow Row(ContextWindow card, ContextWindowPart part) => card.LegendRows[(int)part]!;

    [AvaloniaFact]
    public void Opens_On_The_Sample_Session()
    {
        var (card, window, _) = Open();
        try
        {
            card.UsesSamples.ShouldBeTrue();
            card.EffectiveItems.Count.ShouldBe(32);
            card.EffectiveModel!.Name.ShouldBe("Atlas 1");
            card.UsedTokens.ShouldBe(436_000);
            card.FreeTokens.ShouldBe(500_000);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            card.CanCompact.ShouldBeTrue();
            var readout = card.ReadoutPartControl!;
            readout.NumberText.ShouldBe("436K");
            readout.PercentText.ShouldBe("44%");
            readout.OfText.ShouldBe("of 1M tokens");
            readout.FreeText.ShouldBe("500K free");
            card.CellCaption!.Text.ShouldBe("Each cell is 5K tokens");
            card.ThresholdCaption!.Text.ShouldBe("Compacts at 80%");
            card.StatusText.ShouldBe("Auto-compacts at 80%");
            card.StatusTone.ShouldBe(ContextWindowTone.Info);
            Row(card, ContextWindowPart.System).TokensText.ShouldBe("3.8K");
            Row(card, ContextWindowPart.System).PercentText.ShouldBe("0.4%");
            Row(card, ContextWindowPart.Files).PercentText.ShouldBe("9.8%");
            Row(card, ContextWindowPart.Messages).TokensText.ShouldBe("304K");
            Row(card, ContextWindowPart.Messages).PercentText.ShouldBe("30%");
            Row(card, ContextWindowPart.Summary).IsEmpty.ShouldBeTrue();
            Row(card, ContextWindowPart.Summary).TokensText.ShouldBe("No summary yet");
            Row(card, ContextWindowPart.Summary).PercentText.ShouldBe(string.Empty);
            Row(card, ContextWindowPart.Reserve).TokensText.ShouldBe("64K");
            Row(card, ContextWindowPart.Reserve).PercentText.ShouldBe("6.4%");
            card.CurrentLayout!.CategoryCells.ShouldBe([1, 6, 19, 0, 61]);
            card.Pill!.ModelName.ShouldBe("Atlas 1");
            card.Pill.WindowText.ShouldBe("1M");
            AutomationProperties.GetName(card.Pill).ShouldBe("Model Atlas 1, 1M window. Change model");
            AutomationProperties.GetName(card.GridPartControl!).ShouldBe(
                "Atlas 1 context window: 436K of 1M tokens used. System prompt 3.8K, Tools 30K, Files & memory 98K, Summary 0, " +
                "Conversation 304K, Free 500K, Output reserve 64K. Compacts at 80%.");
            card.SendButton!.IsVisible.ShouldBeTrue();
            card.AttachButton!.IsVisible.ShouldBeTrue();
            // Opening never announces anything.
            card.Announcer!.History.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Send_Appends_The_Next_Turn_And_Says_So()
    {
        var (card, window, _) = Open();
        try
        {
            var changes = new List<ContextWindowItemsChangedEventArgs>();
            card.ItemsChanged += (_, e) => changes.Add(e);
            card.Send();
            card.EffectiveItems.Count.ShouldBe(33);
            card.EffectiveItems[^1].Label.ShouldBe("Add a dry-run flag to the import job");
            card.EffectiveItems[^1].Id.ShouldBe("turn-1");
            card.UsedTokens.ShouldBe(466_600);
            card.StatusText.ShouldBe("Added “Add a dry-run flag to the import job” (+31K)");
            card.StatusTone.ShouldBe(ContextWindowTone.Info);
            card.Announcer!.Announcement.ShouldBe(card.StatusText);
            changes.Count.ShouldBe(1);
            changes[0].Change.ShouldBe(ContextWindowChange.Sent);
            changes[0].Items.Count.ShouldBe(33);
            changes[0].Item!.Tokens.ShouldBe(30_600);
            // The free tokens never roll; at Off the rolling numbers land at once too.
            card.ReadoutPartControl!.FreeText.ShouldBe("469K free");
            card.ReadoutPartControl.NumberText.ShouldBe("467K");
            // The sample's Items stay empty: the card works on its own copy.
            card.Items.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sends_Warn_From_85_Percent_Of_The_Threshold()
    {
        var (card, window, _) = Open();
        try
        {
            Repeat(card.Send, 8);
            card.UsedTokens.ShouldBe(665_800);
            card.StatusTone.ShouldBe(ContextWindowTone.Info);
            card.Classes.Contains(ContextWindow.WarningPseudoClass).ShouldBeFalse();
            card.Send();
            card.UsedTokens.ShouldBe(690_600);
            card.StatusTone.ShouldBe(ContextWindowTone.Warning);
            card.StatusText.ShouldBe("Added “Rename the queue metrics to snake_case” (+25K) · compacts at 80%");
            card.Classes.Contains(ContextWindow.WarningPseudoClass).ShouldBeTrue();
            card.Classes.Contains(ContextWindow.OverPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Crossing_The_Threshold_Folds_On_Its_Own_After_540ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, timers) = Open(ArcMotionLevel.Full);
        try
        {
            var compactions = new List<ContextWindowCompactedEventArgs>();
            card.Compacted += (_, e) => compactions.Add(e);
            Repeat(card.Send, 13);
            card.UsedTokens.ShouldBe(802_400);
            card.StatusText.ShouldBe("Added “Why does the nightly sync time out?” (+35K) · past 80%");
            card.StatusTone.ShouldBe(ContextWindowTone.Warning);
            timers.Requested[^1].ShouldBe(540);
            timers.Advance(539);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            timers.Advance(1);
            card.Phase.ShouldBe(ContextWindowPhase.Summarizing);
            card.Classes.Contains(ContextWindow.SummarizingPseudoClass).ShouldBeTrue();
            card.StatusText.ShouldBe("Past 80%: folding 35 earlier messages…");
            card.StatusTone.ShouldBe(ContextWindowTone.Working);
            card.SendButton!.IsEnabled.ShouldBeFalse();
            card.AttachButton!.IsEnabled.ShouldBeFalse();
            card.CompactButton!.IsEnabled.ShouldBeFalse();
            card.Pill!.IsEnabled.ShouldBeFalse();
            card.GridPartControl!.IsFolding.ShouldBeTrue();
            timers.Requested[^1].ShouldBe(700);
            timers.Advance(699);
            card.Phase.ShouldBe(ContextWindowPhase.Summarizing);
            timers.Advance(1);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            card.GridPartControl.IsFolding.ShouldBeFalse();
            compactions.Count.ShouldBe(1);
            var e = compactions[0];
            e.FoldedCount.ShouldBe(35);
            e.FoldedTokens.ShouldBe(607_800);
            e.SummaryTokens.ShouldBe(24_000);
            e.Before.ShouldBe(802_400);
            e.After.ShouldBe(218_600);
            e.Reason.ShouldBe(ContextWindowCompactReason.Threshold);
            e.IsAutomatic.ShouldBeTrue();
            card.UsedTokens.ShouldBe(218_600);
            card.StatusText.ShouldBe("Folded 608K into a 24K summary");
            card.StatusTone.ShouldBe(ContextWindowTone.Success);
            card.CanCompact.ShouldBeFalse();
            card.Announcer!.History.ShouldContain("Past 80%: folding 35 earlier messages…");
            card.Announcer.History[^1].ShouldBe("Folded 608K into a 24K summary");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Now_Folds_All_But_The_Newest_Two()
    {
        var (card, window, timers) = Open();
        try
        {
            var changes = new List<ContextWindowItemsChangedEventArgs>();
            card.ItemsChanged += (_, e) => changes.Add(e);
            card.Compact();
            card.Phase.ShouldBe(ContextWindowPhase.Summarizing);
            card.StatusText.ShouldBe("Folding 22 earlier messages…");
            // Without Full motion the summarizing phase lasts one dispatcher pass.
            timers.Requested[^1].ShouldBe(0);
            timers.Advance(0);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            card.UsedTokens.ShouldBe(173_740);
            card.StatusText.ShouldBe("Folded 279K into a 17K summary");
            card.CanCompact.ShouldBeFalse();
            card.CompactButton!.IsEnabled.ShouldBeFalse();
            Row(card, ContextWindowPart.Summary).IsEmpty.ShouldBeFalse();
            Row(card, ContextWindowPart.Summary).TokensText.ShouldBe("17K");
            Row(card, ContextWindowPart.Summary).PercentText.ShouldBe("1.7%");
            changes.Single().Change.ShouldBe(ContextWindowChange.Compacted);
            changes[0].Item!.Category.ShouldBe(ContextWindowCategory.Summary);
            // A send makes it available again; the next compaction folds one message and adds to the summary.
            card.Send();
            card.CanCompact.ShouldBeTrue();
            card.Compact();
            card.StatusText.ShouldBe("Folding 1 earlier message…");
            timers.Advance(0);
            Row(card, ContextWindowPart.Summary).TokensText.ShouldBe("21K");
            card.EffectiveItems.Count(i => i.Category == ContextWindowCategory.Summary).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Smaller_Model_Relayouts_Then_Folds_After_600ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (card, window, timers) = Open(ArcMotionLevel.Full);
        try
        {
            var models = new List<ContextWindowModelChangedEventArgs>();
            var compactions = new List<ContextWindowCompactedEventArgs>();
            card.ModelChanged += (_, e) => models.Add(e);
            card.Compacted += (_, e) => compactions.Add(e);
            var cirrus = card.EffectiveModels[1];
            card.SelectedModel = cirrus;
            models.Single().NewModel.ShouldBeSameAs(cirrus);
            models[0].OldModel!.Name.ShouldBe("Atlas 1");
            card.CellCaption!.Text.ShouldBe("Each cell is 2.6K tokens");
            card.ReadoutPartControl!.OfText.ShouldBe("of 512K tokens");
            card.StatusText.ShouldBe("436K is over 80% of Cirrus M");
            card.StatusTone.ShouldBe(ContextWindowTone.Warning);
            timers.Requested[^1].ShouldBe(600);
            timers.Advance(600);
            card.StatusText.ShouldBe("Too full for Cirrus M: folding 22 earlier messages…");
            timers.Advance(700);
            compactions.Single().Reason.ShouldBe(ContextWindowCompactReason.ModelSwitch);
            card.StatusText.ShouldBe("Folded 279K into a 17K summary");
            // Back to the large window: only the relayout and the switch note.
            card.SelectedModel = card.EffectiveModels[0];
            card.StatusText.ShouldBe("Now on Atlas 1 (1M window)");
            card.StatusTone.ShouldBe(ContextWindowTone.Info);
            timers.Pending.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Full_Small_Window_Has_Nothing_To_Fold_And_Overflows()
    {
        var (card, window, timers) = Open();
        try
        {
            card.SelectedModel = card.EffectiveModels[2];
            timers.Advance(0);
            timers.Advance(0);
            card.UsedTokens.ShouldBe(173_740);
            Repeat(card.Attach, 3);
            card.StatusTone.ShouldBe(ContextWindowTone.Warning);
            card.StatusText.ShouldBe("Attached src/ingest/dedupe.go (+19K) · compacts at 80%");
            card.Attach();
            card.StatusText.ShouldEndWith(" · past 80%");
            timers.Advance(0);
            card.Phase.ShouldBe(ContextWindowPhase.Idle);
            card.StatusText.ShouldBe("Nothing to fold yet");
            card.StatusTone.ShouldBe(ContextWindowTone.Info);
            Repeat(card.Attach, 3);
            timers.Advance(0);
            card.UsedTokens.ShouldBe(438_740);
            card.FreeTokens.ShouldBe(0);
            card.ReadoutPartControl!.PercentText.ShouldBe("110%");
            card.ReadoutPartControl.FreeText.ShouldBe("0 free");
            Row(card, ContextWindowPart.Free).IsEmpty.ShouldBeTrue();
            Row(card, ContextWindowPart.Free).TokensText.ShouldBe("0");
            Row(card, ContextWindowPart.Free).PercentText.ShouldBe(string.Empty);
            var layout = card.CurrentLayout!;
            layout.UsedCells.ShouldBe(184);
            layout.ReserveCells.ShouldBe(16);
            card.CanCompact.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Given_Data_The_Card_Writes_Its_Changes_Back_To_Items()
    {
        var items = new AvaloniaList<ContextWindowItem>
        {
            new(ContextWindowCategory.System, "Instructions", 2_000),
            new(ContextWindowCategory.Messages, "First", 10_000),
            new(ContextWindowCategory.Messages, "Second", 12_000),
            new(ContextWindowCategory.Messages, "Third", 8_000)
        };
        var model = new ContextWindowModel("small", "Small", 100_000, 8_000);
        var (card, window, timers) = Open(setup: c =>
        {
            c.Items = items;
            c.Models.Add(model);
        });
        try
        {
            card.UsesSamples.ShouldBeFalse();
            card.SendButton!.IsVisible.ShouldBeFalse();
            card.AttachButton!.IsVisible.ShouldBeFalse();
            card.CompactButton!.IsVisible.ShouldBeTrue();
            card.UsedTokens.ShouldBe(32_000);
            card.CellCaption!.Text.ShouldBe("Each cell is 500 tokens");
            card.AddItem(new ContextWindowItem(ContextWindowCategory.Files, "notes.md", 4_000));
            items.Count.ShouldBe(5);
            items[1].Label.ShouldBe("notes.md");
            card.StatusText.ShouldBe("Attached notes.md (+4K)");
            card.Compact();
            timers.Advance(0);
            items.Select(i => i.Category).ShouldBe([ContextWindowCategory.System, ContextWindowCategory.Files, ContextWindowCategory.Summary,
                ContextWindowCategory.Messages, ContextWindowCategory.Messages]);
            items[2].Tokens.ShouldBe(4_000);
            items[3].Label.ShouldBe("Second");
            // A change from the app relayouts without a status line.
            var status = card.StatusText;
            items.Add(new ContextWindowItem(ContextWindowCategory.Messages, "Fourth", 6_000));
            card.UsedTokens.ShouldBe(36_000 - 10_000 + 4_000 + 6_000);
            card.StatusText.ShouldBe(status);
            items[^1].Tokens = 7_000;
            card.UsedTokens.ShouldBe(37_000);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Order_Is_The_Pill_Then_The_Actions()
    {
        var (card, window, _) = Open();
        try
        {
            var focus = window.FocusManager!;
            Press(window, PhysicalKey.Tab);
            focus.GetFocusedElement().ShouldBeSameAs(card.Pill);
            Press(window, PhysicalKey.Tab);
            focus.GetFocusedElement().ShouldBeSameAs(card.SendButton);
            Press(window, PhysicalKey.Tab);
            focus.GetFocusedElement().ShouldBeSameAs(card.AttachButton);
            Press(window, PhysicalKey.Tab);
            focus.GetFocusedElement().ShouldBeSameAs(card.CompactButton);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Model_Menu_Opens_On_The_Selected_Model_And_Wraps()
    {
        var (card, window, _) = Open();
        try
        {
            var menu = card.ModelMenu!;
            card.Pill!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            menu.IsOpen.ShouldBeTrue();
            card.Pill.IsOpen.ShouldBeTrue();
            ((ContextWindowModel)menu.HighlightedEntry!.Tag!).Name.ShouldBe("Atlas 1");
            Press(window, PhysicalKey.ArrowUp);
            ((ContextWindowModel)menu.HighlightedEntry!.Tag!).Name.ShouldBe("Kestrel S");
            Press(window, PhysicalKey.ArrowDown);
            ((ContextWindowModel)menu.HighlightedEntry!.Tag!).Name.ShouldBe("Atlas 1");
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            menu.IsOpen.ShouldBeFalse();
            card.SelectedModel!.Name.ShouldBe("Cirrus M");
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(card.Pill);
            // Escape closes and gives focus back to the pill.
            Press(window, PhysicalKey.Space);
            menu.IsOpen.ShouldBeTrue();
            ((ContextWindowModel)menu.HighlightedEntry!.Tag!).Name.ShouldBe("Cirrus M");
            Press(window, PhysicalKey.Escape);
            menu.IsOpen.ShouldBeFalse();
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(card.Pill);
            card.SelectedModel!.Name.ShouldBe("Cirrus M");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Stays_In_The_Card_While_Summarizing_And_Returns_To_An_Action()
    {
        var (card, window, timers) = Open();
        try
        {
            card.CompactButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            card.Phase.ShouldBe(ContextWindowPhase.Summarizing);
            var focus = window.FocusManager!;
            focus.GetFocusedElement().ShouldBeSameAs(card.FocusHolder);
            AutomationProperties.GetName(card.FocusHolder!).ShouldBe("Folding 22 earlier messages…");
            timers.Advance(0);
            Pump();
            // Compact now is off after the compaction: focus lands on Send message, still from the keyboard.
            focus.GetFocusedElement().ShouldBeSameAs(card.SendButton);
            card.SendButton!.Classes.Contains(":focus-visible").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(688, true, false)]
    [InlineData(690, false, false)]
    [InlineData(472, true, false)]
    [InlineData(468, true, true)]
    public void Breakpoints_Follow_The_Content_Width(double width, bool stacked, bool compactHeader)
    {
        var (card, window, _) = Open(setup: c => c.Width = width, width: 800);
        try
        {
            card.Classes.Contains(ContextWindow.StackedPseudoClass).ShouldBe(stacked);
            card.Classes.Contains(ContextWindow.CompactHeaderPseudoClass).ShouldBe(compactHeader);
            card.GridPartControl!.CellGap.ShouldBe(stacked ? 2 : 3);
            card.GridPartControl.CellRadius.ShouldBe(stacked ? 2 : 3);
            Grid.GetRow(card.Pill!).ShouldBe(compactHeader ? 1 : 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Custom_Threshold_Moves_The_Line_To_A_Row_And_The_Warning()
    {
        var (card, window, _) = Open(setup: c => c.Threshold = 0.5);
        try
        {
            card.CurrentLayout!.ThresholdRow.ShouldBe(5);
            card.GridPartControl!.ThresholdRow.ShouldBe(5);
            card.ThresholdCaption!.Text.ShouldBe("Compacts at 50%");
            card.StatusText.ShouldBe("Auto-compacts at 50%");
            // 436K is past 42.5% (85% of 50%) but under 50%.
            card.Classes.Contains(ContextWindow.WarningPseudoClass).ShouldBeTrue();
            card.Classes.Contains(ContextWindow.OverPseudoClass).ShouldBeFalse();
            card.GridPartControl.KindAt(99).ShouldBe(ContextWindowCellKind.Free);
            card.GridPartControl.KindAt(100).ShouldBe(ContextWindowCellKind.FreeAfter);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (card, window, _) = Open(setup: c => c.Labels = new ContextWindowLabels
        {
            Send = "Envoyer",
            Messages = "Messages",
            Idle = p => $"Compactage auto à {p} %",
            OfTokens = w => $"sur {w}"
        });
        try
        {
            AutomationProperties.GetName(card.SendButton!).ShouldBe("Envoyer");
            Row(card, ContextWindowPart.Messages).Label.ShouldBe("Messages");
            card.StatusText.ShouldBe("Compactage auto à 80 %");
            card.ReadoutPartControl!.OfText.ShouldBe("sur 1M");
        }
        finally
        {
            window.Close();
        }
    }

    private static AvaloniaList<ContextWindowItem> SmallSession() =>
    [
        new(ContextWindowCategory.System, "Instructions", 2_000),
        new(ContextWindowCategory.Messages, "First", 10_000)
    ];

    [AvaloniaFact]
    public void A_Card_Is_Collected_While_Its_Items_And_Models_Live_On()
    {
        var items = SmallSession();
        var models = new AvaloniaList<ContextWindowModel> { new("small", "Small", 100_000, 8_000) };
        var turns = new AvaloniaList<ContextWindowTurn>();
        var weak = TestHelpers.ShowAndClose(() => new ContextWindow { Timers = new ContextWindowTestTimers(), Items = items, Models = models, Turns = turns },
            _ => Pump());
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        // The data still works without the card.
        items.Add(new ContextWindowItem(ContextWindowCategory.Messages, "Second", 5_000));
        items[0].Tokens = 3_000;
        models.Clear();
    }

    [AvaloniaFact]
    public void A_Card_Taken_Out_And_Put_Back_Shows_What_Changed_While_It_Was_Away()
    {
        var items = SmallSession();
        var models = new AvaloniaList<ContextWindowModel> { new("small", "Small", 100_000, 8_000) };
        var card = new ContextWindow
        {
            Timers = new ContextWindowTestTimers(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Items = items,
            Models = models
        };
        var host = new Border { Child = card };
        var window = TestHelpers.Show(host);
        try
        {
            window.Width = 792;
            window.Height = 900;
            Pump();
            card.UsedTokens.ShouldBe(12_000);
            // Changes made while away show once it is back.
            host.Child = null;
            Pump();
            items.Add(new ContextWindowItem(ContextWindowCategory.Messages, "Second", 5_000));
            items[0].Tokens = 3_000;
            host.Child = card;
            Pump();
            card.UsedTokens.ShouldBe(18_000);
            // And it follows changes made after it came back.
            items[1].Tokens = 11_000;
            Pump();
            card.UsedTokens.ShouldBe(19_000);
            items.RemoveAt(2);
            Pump();
            card.UsedTokens.ShouldBe(14_000);
        }
        finally
        {
            window.Close();
        }
    }
}
