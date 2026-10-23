using System.Windows.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

public partial class AiSidePanel
{
    #region Making a change

    /// <summary>
    /// The card of a proposal: the block as it is on the page now (<see cref="AiPanelChange.Before"/>) and a copy of the
    /// proposed block with the same id. No card when the page has no such block or the kinds differ.
    /// </summary>
    private AiPanelChange? MakeChange(AiPanelChangeProposal? proposal)
    {
        if (proposal is null || Page?.Find(proposal.BlockId) is not { } block || proposal.After.GetType() != block.GetType())
        {
            return null;
        }
        var after = proposal.After.Clone();
        after.Id = block.Id;
        if (string.IsNullOrEmpty(after.Label))
        {
            after.Label = block.Label;
        }
        return new AiPanelChange { BlockId = block.Id, Before = block.Clone(), After = after, State = AiPanelChangeState.Proposed };
    }

    /// <summary>Gives <paramref name="target"/> the content of <paramref name="source"/> (same kind; the id and label stay).</summary>
    private static void CopyContent(AiPanelBlock source, AiPanelBlock target)
    {
        switch (target)
        {
            case AiPanelParagraph paragraph when source is AiPanelParagraph from:
                paragraph.Text = from.Text;
                break;
            case AiPanelHeading heading when source is AiPanelHeading from:
                heading.Text = from.Text;
                break;
            case AiPanelTable table when source is AiPanelTable from:
            {
                var copy = (AiPanelTable)from.Clone();
                table.Columns.Clear();
                table.Columns.AddRange(copy.Columns);
                table.Rows.Clear();
                table.Rows.AddRange(copy.Rows);
                break;
            }
        }
    }

    private static void Execute(ICommand? command, AiPanelChange change)
    {
        if (command?.CanExecute(change) == true)
        {
            command.Execute(change);
        }
    }

    #endregion

    #region Apply, undo, discard

    private void OnApplyRequested(object? sender, AiPanelChangeRequestEventArgs e)
    {
        e.Handled = true;
        Apply(e.Change);
    }

    private void OnDiscardRequested(object? sender, AiPanelChangeRequestEventArgs e)
    {
        e.Handled = true;
        Discard(e.Change);
    }

    private void OnUndoRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Undo();
    }

    /// <summary>
    /// Applies a proposed (or undone) change (F13): the block on the page takes the new content (the page data first, then
    /// the sweep), the card says so, the Undo pill takes this change (an earlier one can no longer be undone) and the focus
    /// moves to it; <see cref="ChangeApplied"/> and <see cref="ApplyCommand"/> follow.
    /// </summary>
    internal void Apply(AiPanelChange change)
    {
        if (change.State is not (AiPanelChangeState.Proposed or AiPanelChangeState.Undone) || Page is not { } page ||
            page.Find(change.BlockId) is not { } block || change.After is not { } after || after.GetType() != block.GetType())
        {
            return;
        }
        // Undo restores what the page shows now (another change may have been applied to the block since the card came).
        change.Before = block.Clone();
        CopyContent(after, block);
        _pageView?.PlayChange(block.Id, after, isUndo: false);
        change.State = AiPanelChangeState.Applied;
        SetLastApplied(change);
        RaiseEvent(new AiPanelChangeEventArgs(ChangeAppliedEvent, this, change, page));
        Execute(ApplyCommand, change);
        Announce(Labels.AnnounceApplied(AiPanelChangeCard.LabelOf(block)));
        if (_undoPill is not null)
        {
            UpdateLayout();
            _undoPill.Focus(FocusMethod);
        }
    }

    /// <summary>
    /// Undoes the last applied change (the Undo pill does this): the block gets its old content back with the same sweep,
    /// the card offers Apply again and takes the focus, the pill fades out; <see cref="ChangeUndone"/> and
    /// <see cref="UndoCommand"/> follow. Nothing happens when there is nothing to undo.
    /// </summary>
    public void Undo()
    {
        if (LastApplied is not { State: AiPanelChangeState.Applied } change || Page is not { } page ||
            page.Find(change.BlockId) is not { } block || change.Before is not { } before || before.GetType() != block.GetType())
        {
            return;
        }
        var focusCard = _undoPill?.IsFocused == true || _undoPill?.IsKeyboardFocusWithin == true;
        CopyContent(before, block);
        _pageView?.PlayChange(block.Id, before, isUndo: true);
        change.State = AiPanelChangeState.Undone;
        SetLastApplied(null);
        RaiseEvent(new AiPanelChangeEventArgs(ChangeUndoneEvent, this, change, page));
        Execute(UndoCommand, change);
        Announce(Labels.AnnounceUndone(AiPanelChangeCard.LabelOf(block)));
        if (focusCard)
        {
            // The pill goes away: the focus goes to the card's Apply, which can apply the change again.
            FocusCardApply(change);
        }
    }

    /// <summary>
    /// Discards a proposed change (F15): the card dims and says so for good, the page does not change, the focus goes to the
    /// box; <see cref="ChangeDiscarded"/> and <see cref="DiscardCommand"/> follow.
    /// </summary>
    internal void Discard(AiPanelChange change)
    {
        if (change.State != AiPanelChangeState.Proposed || Page is not { } page)
        {
            return;
        }
        change.State = AiPanelChangeState.Discarded;
        RaiseEvent(new AiPanelChangeEventArgs(ChangeDiscardedEvent, this, change, page));
        Execute(DiscardCommand, change);
        Announce(Labels.AnnounceDiscarded);
        _box?.FocusInput(FocusMethod);
    }

    /// <summary>The card of <paramref name="change"/> on screen, or null.</summary>
    internal AiPanelChangeCard? CardOf(AiPanelChange change)
    {
        foreach (var view in _views.Values)
        {
            if (view.Card is { } card && ReferenceEquals(card.Change, change))
            {
                return card;
            }
        }
        return null;
    }

    /// <summary>Focuses the Apply button that the card of <paramref name="change"/> shows again.</summary>
    private void FocusCardApply(AiPanelChange change)
    {
        if (CardOf(change) is not { } card)
        {
            _box?.FocusInput(FocusMethod);
            return;
        }
        UpdateLayout();
        if (card.ApplyButton?.Focus(FocusMethod) != true)
        {
            _box?.FocusInput(FocusMethod);
        }
    }

    #endregion

    #region Undo pill

    private void SetLastApplied(AiPanelChange? change)
    {
        LastApplied = change;
        RefreshUndoPill();
    }

    /// <summary>The pill above the box says what the last applied change did ("Summary changed │ ↺ Undo") until it is undone.</summary>
    private void RefreshUndoPill()
    {
        if (_undoPill is null)
        {
            return;
        }
        if (LastApplied is { } change)
        {
            var block = Page?.Find(change.BlockId) ?? change.Before ?? change.After;
            _undoPill.Text = Labels.Updated(AiPanelChangeCard.LabelOf(block));
            _undoPill.IsActive = true;
        }
        else
        {
            _undoPill.IsActive = false;
        }
    }

    #endregion
}
