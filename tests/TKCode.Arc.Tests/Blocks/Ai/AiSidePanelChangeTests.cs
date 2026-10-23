using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The side panel's change cards: proposed, applied, undone, discarded, several pending, tables, the page data.</summary>
public class AiSidePanelChangeTests
{
    private sealed class Recorder : ICommand
    {
        public List<object?> Calls { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Calls.Add(parameter);
    }

    private static AiPanelChangeProposal Shorter() =>
        new("summary", new AiPanelParagraph { Id = "summary", Label = "Summary", Text = AiSidePanelSamples.ShortSummary });

    private static AiPanelChangeProposal SyncColumn() => new("limits-table", AiSidePanelSamples.Table(withSync: true));

    private static string SummaryText(AiSidePanelRig rig) => ((AiPanelParagraph)rig.Panel.Page!.Find("summary")!).Text;

    [AvaloniaFact]
    public void A_paragraph_change_shows_a_proposed_card_with_a_word_diff()
    {
        using var rig = new AiSidePanelRig();
        var turn = rig.Answer("Shorten the summary", "Here is a shorter summary.", Shorter());
        turn.State.ShouldBe(AiPanelTurnState.Done);
        var change = turn.Change.ShouldNotBeNull();
        change.State.ShouldBe(AiPanelChangeState.Proposed);
        change.BlockId.ShouldBe("summary");
        ((AiPanelParagraph)change.Before!).Text.ShouldBe(AiSidePanelSamples.Summary);
        ((AiPanelParagraph)change.After!).Text.ShouldBe(AiSidePanelSamples.ShortSummary);
        var card = rig.LastView.Card.ShouldNotBeNull();
        card.BlockLabel.ShouldBe("Summary");
        card.Summary.ShouldBe("22 words (was 42)");
        card.DiffText.ShouldNotBeNull();
        card.DiffText!.Before.ShouldBe(AiSidePanelSamples.Summary);
        card.DiffText.After.ShouldBe(AiSidePanelSamples.ShortSummary);
        card.ApplyButton.ShouldNotBeNull();
        card.DiscardButton.ShouldNotBeNull();
        AutomationProperties.GetName(card).ShouldBe("Suggested edit to Summary");
        rig.Announcement.ShouldBe("Reply ready, with a suggested edit to Summary");
        // The page does not change until the person applies it.
        SummaryText(rig).ShouldBe(AiSidePanelSamples.Summary);
    }

    [AvaloniaFact]
    public void Apply_updates_the_page_raises_the_event_and_moves_the_focus_to_the_undo_pill()
    {
        var command = new Recorder();
        using var rig = new AiSidePanelRig(p => p.ApplyCommand = command);
        var change = rig.Answer("Shorten the summary", "Shorter.", Shorter()).Change!;
        var card = rig.LastView.Card!;
        rig.Click(card.ApplyButton!);
        change.State.ShouldBe(AiPanelChangeState.Applied);
        SummaryText(rig).ShouldBe(AiSidePanelSamples.ShortSummary);
        rig.Applied.Count.ShouldBe(1);
        rig.Applied[0].Change.ShouldBeSameAs(change);
        rig.Applied[0].Page.ShouldBeSameAs(rig.Panel.Page);
        command.Calls.ShouldBe([change]);
        rig.Panel.LastApplied.ShouldBeSameAs(change);
        var pill = rig.Panel.UndoPill!;
        pill.IsActive.ShouldBeTrue();
        pill.Text.ShouldBe("Summary changed");
        pill.UndoText.ShouldBe("Undo");
        rig.Focused.ShouldBeSameAs(pill);
        card.ApplyButton.ShouldBeNull();
        rig.Announcement.ShouldBe("Summary changed. You can undo it.");
        // The page shows the new text.
        rig.PageView.ViewOf("summary")!.Texts[0].Text.Text.ShouldBe(AiSidePanelSamples.ShortSummary);
    }

    [AvaloniaFact]
    public void Undo_restores_the_block_and_gives_the_focus_to_the_cards_apply()
    {
        var command = new Recorder();
        using var rig = new AiSidePanelRig(p => p.UndoCommand = command);
        var change = rig.Answer("Shorten the summary", "Shorter.", Shorter()).Change!;
        rig.Click(rig.LastView.Card!.ApplyButton!);
        rig.Click(rig.Panel.UndoPill!);
        change.State.ShouldBe(AiPanelChangeState.Undone);
        SummaryText(rig).ShouldBe(AiSidePanelSamples.Summary);
        rig.Undone.Single().Change.ShouldBeSameAs(change);
        command.Calls.ShouldBe([change]);
        rig.Panel.LastApplied.ShouldBeNull();
        rig.Panel.UndoPill!.IsActive.ShouldBeFalse();
        var apply = rig.LastView.Card!.ApplyButton.ShouldNotBeNull();
        rig.Focused.ShouldBeSameAs(apply);
        rig.Announcement.ShouldBe("Summary restored.");

        // Apply again.
        rig.Click(apply);
        change.State.ShouldBe(AiPanelChangeState.Applied);
        SummaryText(rig).ShouldBe(AiSidePanelSamples.ShortSummary);
        rig.Panel.LastApplied.ShouldBeSameAs(change);
    }

    [AvaloniaFact]
    public void Undo_from_code_restores_the_block_and_does_nothing_without_an_applied_change()
    {
        using var rig = new AiSidePanelRig();
        rig.Panel.Undo();
        rig.Undone.ShouldBeEmpty();
        var change = rig.Answer("Shorten the summary", "Shorter.", Shorter()).Change!;
        rig.Panel.Apply(change);
        rig.Settle();
        rig.Panel.Undo();
        rig.Settle();
        change.State.ShouldBe(AiPanelChangeState.Undone);
        SummaryText(rig).ShouldBe(AiSidePanelSamples.Summary);
        rig.Panel.Undo();
        rig.Undone.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Discard_dims_the_card_for_good_and_focuses_the_box()
    {
        var command = new Recorder();
        using var rig = new AiSidePanelRig(p => p.DiscardCommand = command);
        var change = rig.Answer("Shorten the summary", "Shorter.", Shorter()).Change!;
        var card = rig.LastView.Card!;
        rig.Click(card.DiscardButton!);
        change.State.ShouldBe(AiPanelChangeState.Discarded);
        rig.Discarded.Single().Change.ShouldBeSameAs(change);
        command.Calls.ShouldBe([change]);
        card.Classes.Contains(":discarded").ShouldBeTrue();
        card.ApplyButton.ShouldBeNull();
        card.DiscardButton.ShouldBeNull();
        SummaryText(rig).ShouldBe(AiSidePanelSamples.Summary);
        rig.Focused.ShouldBeSameAs(rig.Input);
        rig.Announcement.ShouldBe("Edit discarded");
        // A discarded change cannot be applied.
        rig.Panel.Apply(change);
        change.State.ShouldBe(AiPanelChangeState.Discarded);
        rig.Applied.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Several_cards_can_wait_and_only_the_last_applied_can_be_undone()
    {
        using var rig = new AiSidePanelRig();
        var paragraph = rig.Answer("Shorten the summary", "Shorter.", Shorter()).Change!;
        var paragraphCard = rig.LastView.Card!;
        var table = rig.Answer("Add a sync interval column", "Added.", SyncColumn()).Change!;
        var tableCard = rig.LastView.Card!;
        paragraph.State.ShouldBe(AiPanelChangeState.Proposed);
        table.State.ShouldBe(AiPanelChangeState.Proposed);

        // The older card first.
        rig.Click(paragraphCard.ApplyButton!);
        rig.Panel.LastApplied.ShouldBeSameAs(paragraph);
        rig.Click(tableCard.ApplyButton!);
        rig.Panel.LastApplied.ShouldBeSameAs(table);
        rig.Panel.UndoPill!.Text.ShouldBe("Storage table changed");
        paragraph.State.ShouldBe(AiPanelChangeState.Applied);

        rig.Panel.Undo();
        rig.Settle();
        table.State.ShouldBe(AiPanelChangeState.Undone);
        // The older change stays applied and can no longer be undone.
        paragraph.State.ShouldBe(AiPanelChangeState.Applied);
        SummaryText(rig).ShouldBe(AiSidePanelSamples.ShortSummary);
        rig.Panel.LastApplied.ShouldBeNull();
        rig.Panel.Undo();
        paragraph.State.ShouldBe(AiPanelChangeState.Applied);
    }

    [AvaloniaFact]
    public void A_table_change_adds_the_column_to_the_page()
    {
        using var rig = new AiSidePanelRig();
        var change = rig.Answer("Add a sync interval column", "I added a column.", SyncColumn()).Change!;
        var card = rig.LastView.Card!;
        card.DiffTable.ShouldNotBeNull();
        card.Summary.ShouldBe("New column: Sync");
        card.BlockLabel.ShouldBe("Storage table");
        rig.Click(card.ApplyButton!);
        var table = (AiPanelTable)rig.Panel.Page!.Find("limits-table")!;
        table.Columns.Select(c => c.Header).ShouldBe(["Tier", "Devices", "Cache", "Sync"]);
        table.Rows[0].Cells.ShouldBe(["Basic", "1 device", "200 MB", "15 min"]);
        change.State.ShouldBe(AiPanelChangeState.Applied);
        ((AiPanelTable)change.Before!).Columns.Count.ShouldBe(3);
        rig.Panel.Undo();
        rig.Settle();
        table.Columns.Count.ShouldBe(3);
        table.Rows[2].Cells.ShouldBe(["Org", "Unlimited", "5 GB"]);
    }

    [AvaloniaFact]
    public void The_responder_sees_the_page_with_the_applied_changes()
    {
        using var rig = new AiSidePanelRig();
        rig.Answer("Shorten the summary", "Shorter.", Shorter());
        rig.Click(rig.LastView.Card!.ApplyButton!);
        _ = rig.Panel.AskAsync("Shorten the summary");
        rig.Settle();
        ((AiPanelParagraph)rig.Call.Request.Page.Find("summary")!).Text.ShouldBe(AiSidePanelSamples.ShortSummary);
    }

    [AvaloniaFact]
    public void A_proposal_for_a_block_the_page_does_not_have_makes_no_card()
    {
        using var rig = new AiSidePanelRig();
        var turn = rig.Answer("Change it", "Done.", new AiPanelChangeProposal("missing", new AiPanelParagraph { Id = "missing", Text = "x" }));
        turn.Change.ShouldBeNull();
        rig.LastView.Card.ShouldBeNull();
        var kind = rig.Answer("Change it", "Done.", new AiPanelChangeProposal("summary", new AiPanelHeading { Id = "summary", Text = "x" }));
        kind.Change.ShouldBeNull();
        rig.Announcement.ShouldBe("Reply ready");
    }

    [AvaloniaFact]
    public void Undo_restores_what_the_page_showed_when_the_change_was_applied()
    {
        using var rig = new AiSidePanelRig();
        var first = rig.Answer("Shorten the summary", "One.", Shorter()).Change!;
        var other = rig.Answer("Rewrite the summary", "Two.",
            new AiPanelChangeProposal("summary", new AiPanelParagraph { Id = "summary", Text = "A third version." })).Change!;
        rig.Panel.Apply(first);
        rig.Panel.Apply(other);
        rig.Settle();
        SummaryText(rig).ShouldBe("A third version.");
        rig.Panel.Undo();
        rig.Settle();
        // Back to the first change's text, not to the text the second card was made from.
        SummaryText(rig).ShouldBe(AiSidePanelSamples.ShortSummary);
    }

    [AvaloniaFact]
    public void A_new_page_resets_the_thread_the_undo_and_the_selection()
    {
        using var rig = new AiSidePanelRig();
        rig.Answer("Shorten the summary", "Shorter.", Shorter());
        rig.Click(rig.LastView.Card!.ApplyButton!);
        rig.AskAboutBlock("scope");
        rig.Panel.Selection.ShouldNotBeNull();
        rig.Panel.Page = AiSidePanelSamples.Page();
        rig.Run(0.3);
        rig.Panel.Turns.ShouldBeEmpty();
        rig.Panel.TurnViews.ShouldBeEmpty();
        rig.Panel.LastApplied.ShouldBeNull();
        rig.Panel.UndoPill!.IsActive.ShouldBeFalse();
        rig.Panel.Selection.ShouldBeNull();
        rig.PageView.MarkedBlockId.ShouldBeNull();
        rig.Panel.EmptyState!.IsVisible.ShouldBeTrue();
        rig.Panel.EmptySuggestions!.Prompts.Count.ShouldBe(3);
        rig.PageView.Page.ShouldBeSameAs(rig.Panel.Page);
    }

    [AvaloniaFact]
    public void A_new_page_stops_a_running_answer()
    {
        using var rig = new AiSidePanelRig();
        _ = rig.Panel.AskAsync("Explain");
        rig.Settle();
        var call = rig.Call;
        rig.Panel.Page = AiSidePanelSamples.Page();
        rig.Settle();
        call.IsCancelled.ShouldBeTrue();
        rig.Panel.IsBusy.ShouldBeFalse();
        rig.Panel.Turns.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Apply_plays_the_sweep_at_full_motion_and_the_tint_fade_at_reduced()
    {
        using (var rig = new AiSidePanelRig(level: ArcMotionLevel.Full))
        {
            rig.Answer("Shorten the summary", "Shorter.", Shorter());
            rig.Click(rig.LastView.Card!.ApplyButton!);
            var box = rig.PageView.ViewOf("summary")!.Box;
            box.Sweep.IsPlaying.ShouldBeTrue();
            box.Leaving.ShouldNotBeNull();
            rig.Run(0.2);
            box.Sweep.Reveal.ShouldBeInRange(0.3, 0.99);
            rig.Run(1.5);
            box.Sweep.IsPlaying.ShouldBeFalse();
        }
        using (var rig = new AiSidePanelRig(level: ArcMotionLevel.Reduced))
        {
            rig.Answer("Shorten the summary", "Shorter.", Shorter());
            rig.Click(rig.LastView.Card!.ApplyButton!);
            var box = rig.PageView.ViewOf("summary")!.Box;
            box.Sweep.IsPlaying.ShouldBeTrue();
            // The whole block is tinted at once (no travel), then the tint fades.
            box.Sweep.Reveal.ShouldBe(1);
            rig.Run(0.8);
            box.Sweep.IsPlaying.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public void Keyboard_apply_moves_the_focus_to_the_pill_with_its_ring()
    {
        using var rig = new AiSidePanelRig();
        rig.Answer("Shorten the summary", "Shorter.", Shorter());
        var apply = rig.LastView.Card!.ApplyButton!;
        apply.Focus(NavigationMethod.Tab);
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        rig.Panel.LastApplied.ShouldNotBeNull();
        rig.Focused.ShouldBeSameAs(rig.Panel.UndoPill);
        rig.Panel.UndoPill!.Classes.Contains(":focus-visible").ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        rig.Panel.LastApplied.ShouldBeNull();
        rig.Focused.ShouldBeSameAs(rig.LastView.Card!.ApplyButton);
    }
}
