using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class KanbanBoard
{
    private MotionValue<double>? _composerHeight;
    private MotionValue<double>? _composerOpacity;
    private Control? _composerOpener;
    private bool _composerClosing;
    private int _composerToken;

    /// <summary>True while the composer is open (not closing).</summary>
    internal bool IsComposerOpen => _panel?.Composer is { IsVisible: true } && !_composerClosing;

    /// <summary>True while the composer collapses (the Add card row is already back under it).</summary>
    internal bool IsComposerClosing => _composerClosing;

    /// <summary>The composer's height in the layout: its spring while it opens or closes, else its natural height.</summary>
    internal double ComposerHeight
    {
        get
        {
            var composer = _panel?.Composer;
            if (composer is null)
            {
                return 0;
            }
            if (_composerHeight is { } height && (height.IsAnimating || _composerClosing))
            {
                return height.Value;
            }
            return composer.DesiredSize.Height;
        }
    }

    /// <summary>
    /// Opens quick add at the end of <paramref name="column"/> (in swimlanes: in <paramref name="person"/>'s lane, by
    /// default the current user's). Only one composer is open on the board: opening it elsewhere drops the draft.
    /// </summary>
    public void BeginQuickAdd(KanbanColumn column, ProjectPerson? person = null) => BeginQuickAdd(column, person, null);

    internal void BeginQuickAdd(KanbanColumn column, ProjectPerson? person, Control? opener)
    {
        FlushRebuild();
        if (!CanQuickAdd || _panel is null || _grid is null)
        {
            return;
        }
        var columnIndex = _grid.ColumnIndex(column.Identity);
        if (columnIndex < 0)
        {
            return;
        }
        CancelDrag();
        var lane = 0;
        if (_grid.HasLaneHeads)
        {
            lane = _grid.LaneIndexFor((person ?? EffectiveCurrentUser)?.Identity);
            if (lane < 0)
            {
                lane = 0;
            }
        }
        var composer = _panel.Composer;
        composer.ApplyTemplate();
        var sameCell = IsComposerOpen && composer.Cell == (lane, columnIndex);
        composer.Cell = (lane, columnIndex);
        composer.BoxName = Strings.ComposerName(column.Name);
        if (!sameCell)
        {
            composer.Clear();
        }
        _composerOpener = opener ?? _composerOpener;
        _composerClosing = false;
        _composerToken++;
        EnsureComposerMotion();
        if (!sameCell || !composer.IsVisible)
        {
            _composerHeight!.Jump(0);
            _composerOpacity!.Jump(0);
        }
        composer.IsVisible = true;
        _panel.UpdateAddRowVisibility();
        var width = _panel.CurrentLayout?.Metrics.CardWidth ?? 300;
        composer.Measure(new Size(width, double.PositiveInfinity));
        // The composer grows on the layout spring (Arc: K172 / C26.3, Smooth) while it fades in; reduced motion: at once.
        _ = _composerHeight!.AnimateTo(composer.DesiredSize.Height, KanbanMotion.Layout);
        _ = _composerOpacity!.AnimateTo(1, KanbanMotion.Layout);
        _panel.ApplyTabOrder();
        _panel.InvalidateMeasure();
        composer.FocusBox();
    }

    /// <summary>Closes quick add without adding (the draft is dropped).</summary>
    public void CancelQuickAdd() => CloseComposer(returnFocus: true, animate: true);

    private void EnsureComposerMotion()
    {
        if (_composerHeight is not null || _panel is null)
        {
            return;
        }
        // Code-driven: the composer's height is measured at run time and the Add card row rides under it.
        _composerHeight = new MotionValue<double>(_panel, 0, MotionVectors.Double);
        _composerHeight.Changed += _ => _panel.InvalidateMeasure();
        _composerOpacity = new MotionValue<double>(_panel.Composer, 0, MotionVectors.Double);
        _composerOpacity.Changed += v => _panel.Composer.Opacity = v;
    }

    internal void AttachComposer()
    {
        if (_panel is null)
        {
            return;
        }
        _panel.Composer.Submitted += OnComposerSubmitted;
        _panel.Composer.Cancelled += OnComposerCancelled;
    }

    internal void DetachComposer()
    {
        if (_panel is null)
        {
            return;
        }
        _panel.Composer.Submitted -= OnComposerSubmitted;
        _panel.Composer.Cancelled -= OnComposerCancelled;
    }

    private void OnComposerCancelled() => CloseComposer(returnFocus: true, animate: true);

    private void OnComposerSubmitted(string text)
    {
        if (_panel is null || _grid is null)
        {
            return;
        }
        var title = text.Trim();
        if (title.Length == 0)
        {
            // Arc: Enter on an empty box closes the composer without a card.
            CloseComposer(returnFocus: true, animate: true);
            return;
        }
        var (lane, columnIndex) = _panel.Composer.Cell;
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return;
        }
        var column = _grid.Columns[columnIndex];
        var assignee = _grid.HasLaneHeads && lane >= 0 && lane < _grid.Lanes.Count ? _grid.Lanes[lane].Person : EffectiveCurrentUser;
        var key = CardKeyFactory?.Invoke(column) ?? NextKey(_cards.Select(c => c.Key));
        var creating = new KanbanCardCreatingEventArgs(CardCreatingEvent, key, title, column, assignee);
        RaiseEvent(creating);
        if (creating.Cancel)
        {
            return;
        }
        var card = new KanbanCard(creating.Key, creating.Title, column.Identity) { AssigneeKey = assignee?.Identity };
        // New cards appear at the end of their cell without an entrance (Arc), the composer is pushed down at once.
        Append(card);
        ApplyGrid(animate: false);
        _panel.Composer.Clear();
        _panel.Composer.FocusBox();
        var created = new KanbanCardCreatedEventArgs(CardCreatedEvent, card, column, assignee);
        RaiseEvent(created);
        if (CardCreatedCommand is { } command && command.CanExecute(card))
        {
            command.Execute(card);
        }
        RaiseCardsChanged(KanbanChangeReason.Created, card);
        Announce(Strings.Added(card.Title, column.Name));
    }

    /// <summary>Collapses the composer (on the layout spring) and gives the focus back to the "+" or Add card that opened it.</summary>
    internal void CloseComposer(bool returnFocus, bool animate)
    {
        var composer = _panel?.Composer;
        if (composer is null || !composer.IsVisible)
        {
            return;
        }
        var opener = _composerOpener;
        _composerOpener = null;
        var token = ++_composerToken;
        var cell = composer.Cell;
        // The Add card row comes back at once, so the focus can go to it before the composer (which holds the focus)
        // collapses or hides.
        _composerClosing = true;
        _panel!.UpdateAddRowVisibility();
        if (returnFocus)
        {
            // Arc dropped the focus to the page; we hand it back to the button that opened the composer.
            var target = opener is { IsEffectivelyVisible: true } ? opener : null;
            if (target is null && cell.Column >= 0 && cell.Column < _panel.AddRows.Count)
            {
                target = _panel.AddRows[cell.Column];
            }
            if (target is null && cell.Column >= 0 && cell.Column < _panel.Heads.Count)
            {
                target = _panel.Heads[cell.Column].AddButton;
            }
            target?.Focus(NavigationMethod.Tab);
        }
        if (!animate || _composerHeight is null || _composerOpacity is null)
        {
            HideComposer();
            return;
        }
        _panel.InvalidateMeasure();
        _ = _composerOpacity.AnimateTo(0, KanbanMotion.Layout);
        var task = _composerHeight.AnimateTo(0, KanbanMotion.Layout);
        if (task.IsCompleted)
        {
            HideComposer();
            return;
        }
        task.ContinueWith(_ =>
        {
            if (token == _composerToken)
            {
                HideComposer();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void HideComposer()
    {
        if (_panel is null)
        {
            return;
        }
        var composer = _panel.Composer;
        _composerClosing = false;
        composer.IsVisible = false;
        composer.Clear();
        composer.Cell = (-1, -1);
        _composerHeight?.Jump(0);
        _composerOpacity?.Jump(0);
        _panel.UpdateAddRowVisibility();
        _panel.ApplyTabOrder();
        _panel.InvalidateMeasure();
    }
}
