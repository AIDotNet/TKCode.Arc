using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class KanbanBoard
{
    private NavigationMethod _detailsMethod = NavigationMethod.Pointer;
    private MotionValue<double>? _snapX;
    private LiveLoop? _snapLoop;
    private TimeSpan _lastHorizontalScroll;
    private double _lastOffsetX;
    private bool _snapping;

    private void AttachPanel()
    {
        if (_panel is null)
        {
            return;
        }
        _panel.Board = this;
        AttachComposer();
    }

    private void DetachPanel()
    {
        if (_panel is null)
        {
            return;
        }
        DetachComposer();
        _panel.Board = null;
    }

    private void AttachDrawer()
    {
        if (_drawer is not null)
        {
            _drawer.Board = this;
            _drawer.UpdateWidth(Bounds.Width);
        }
    }

    private void DetachDrawer()
    {
        if (_drawer is not null)
        {
            _drawer.Board = null;
        }
    }

    // Opening and closing ----------------------------------------------------------------------------------------------

    /// <summary>Opens the details drawer for <paramref name="card"/>.</summary>
    public void OpenCard(KanbanCard card) => SetCurrentValue(OpenedCardProperty, card);

    /// <summary>Closes the details drawer; the focus returns to its card.</summary>
    public void CloseDetails() => SetCurrentValue(OpenedCardProperty, null);

    /// <summary>Scrolls <paramref name="card"/> into view and gives it keyboard focus.</summary>
    public void FocusCard(KanbanCard card)
    {
        FlushRebuild();
        if (_panel?.ViewOf(card) is { } view)
        {
            view.BringIntoView();
            view.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>Scrolls the board until <paramref name="card"/> is visible.</summary>
    public void ScrollIntoView(KanbanCard card)
    {
        FlushRebuild();
        _panel?.ViewOf(card)?.BringIntoView();
    }

    /// <summary>A click or Enter on a card: <see cref="CardInvoked"/>, then the drawer unless a handler took it.</summary>
    internal void InvokeCard(KanbanCard card, KanbanCardView? view, NavigationMethod method)
    {
        var args = new KanbanCardEventArgs(CardInvokedEvent, card);
        RaiseEvent(args);
        if (CardInvokedCommand is { } command && command.CanExecute(card))
        {
            command.Execute(card);
        }
        if (!args.Handled && IsDetailsEnabled)
        {
            _detailsMethod = method;
            OpenCard(card);
        }
    }

    private void OnOpenedCardChanged(KanbanCard? old, KanbanCard? card)
    {
        FlushRebuild();
        if (card is not null && !_cards.Contains(card))
        {
            SetCurrentValue(OpenedCardProperty, old is not null && _cards.Contains(old) ? old : null);
            return;
        }
        if (card is null)
        {
            PseudoClasses.Set(DetailsOpenPseudoClass, false);
            _drawer?.Hide();
            if (old is not null && _panel?.ViewOf(old) is { } view)
            {
                view.Focus(_detailsMethod == NavigationMethod.Pointer ? NavigationMethod.Pointer : NavigationMethod.Tab);
            }
            if (old is not null)
            {
                RaiseEvent(new KanbanCardEventArgs(DetailsClosedEvent, old));
            }
            return;
        }
        CancelDrag();
        PseudoClasses.Set(DetailsOpenPseudoClass, true);
        _drawer?.Show(card, _detailsMethod);
        if (old is not null && !ReferenceEquals(old, card))
        {
            RaiseEvent(new KanbanCardEventArgs(DetailsClosedEvent, old));
        }
        RaiseEvent(new KanbanCardEventArgs(DetailsOpenedEvent, card));
        _detailsMethod = NavigationMethod.Tab;
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        _detailsMethod = NavigationMethod.Pointer;
        CloseDetails();
    }

    // Edits from the drawer --------------------------------------------------------------------------------------------

    internal void EditTitle(KanbanCard card, string text)
    {
        var old = card.Title;
        if (IsReadOnly || old == text)
        {
            return;
        }
        SetQuietly(card, KanbanCard.TitleProperty, text);
        RaiseEvent(new KanbanCardEditedEventArgs(CardEditedEvent, card, KanbanCard.TitleProperty, old, text));
        RaiseCardsChanged(KanbanChangeReason.TitleEdited, card);
    }

    internal void EditDescription(KanbanCard card, string? text)
    {
        var old = card.Description;
        if (IsReadOnly || (old ?? string.Empty) == (text ?? string.Empty))
        {
            return;
        }
        SetQuietly(card, KanbanCard.DescriptionProperty, text);
        RaiseEvent(new KanbanCardEditedEventArgs(CardEditedEvent, card, KanbanCard.DescriptionProperty, old, text));
        RaiseCardsChanged(KanbanChangeReason.DescriptionEdited, card);
    }

    /// <summary>
    /// The drawer's status: only the column changes, the card keeps its place in the board order (Arc), so it lands at
    /// the top of a later column or the bottom of an earlier one. It glides there on the layout spring.
    /// </summary>
    internal void ChangeStatus(KanbanCard card, KanbanColumn column)
    {
        if (IsReadOnly || _grid is null || card.ColumnKey == column.Identity || _grid.SlotOf(card) is not { } from)
        {
            return;
        }
        var fromColumn = ColumnFor(card.ColumnKey);
        var index = _cards.IndexOf(card);
        SetQuietly(card, KanbanCard.ColumnKeyProperty, column.Identity);
        ApplyGrid(animate: true);
        var to = _grid.SlotOf(card) ?? from;
        var owner = PersonFor(card.AssigneeKey);
        var args = new KanbanCardMovedEventArgs(CardMovedEvent, card, fromColumn, column, owner, owner, index, index, from, to,
            KanbanMoveSource.Details);
        RaiseEvent(args);
        if (CardMovedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        RaiseCardsChanged(KanbanChangeReason.StatusChanged, card);
        Announce(Strings.MovedTo(TitleOf(card), column.Name));
    }

    /// <summary>The drawer's owner: only the owner changes; in swimlanes the card glides to the person's lane.</summary>
    internal void ChangeOwner(KanbanCard card, ProjectPerson? person)
    {
        var key = person?.Identity;
        if (IsReadOnly || _grid is null || card.AssigneeKey == key || _grid.SlotOf(card) is not { } from)
        {
            return;
        }
        var fromOwner = PersonFor(card.AssigneeKey);
        var index = _cards.IndexOf(card);
        SetQuietly(card, KanbanCard.AssigneeKeyProperty, key);
        ApplyGrid(animate: true);
        var to = _grid.SlotOf(card) ?? from;
        var column = ColumnFor(card.ColumnKey);
        var args = new KanbanCardMovedEventArgs(CardMovedEvent, card, column, column, fromOwner, person, index, index, from, to,
            KanbanMoveSource.Details);
        RaiseEvent(args);
        if (CardMovedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        RaiseCardsChanged(KanbanChangeReason.AssigneeChanged, card);
    }

    internal void ToggleStep(KanbanCard card, KanbanChecklistItem item, bool done)
    {
        if (IsReadOnly || item.IsDone == done)
        {
            return;
        }
        item.IsDone = done;
        RaiseEvent(new KanbanChecklistItemToggledEventArgs(ChecklistItemToggledEvent, card, item, done));
        RaiseCardsChanged(KanbanChangeReason.ChecklistToggled, card);
    }

    internal void AddStep(KanbanCard card, string text)
    {
        var trimmed = text.Trim();
        if (IsReadOnly || trimmed.Length == 0)
        {
            return;
        }
        card.Checklist.Add(new KanbanChecklistItem(trimmed));
        RaiseCardsChanged(KanbanChangeReason.ChecklistItemAdded, card);
    }

    // Compact snapping -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The compact board snaps sideways to the nearest column once horizontal scrolling rests (proximity, Arc's phone
    /// layout): a block-local helper because a wheel or touch fling must end where it ends first, then glide.
    /// </summary>
    private void OnScrolledForSnap()
    {
        if (_scroller is null || _snapping)
        {
            return;
        }
        var x = _scroller.Offset.X;
        if (Math.Abs(x - _lastOffsetX) < 0.01)
        {
            return;
        }
        _lastOffsetX = x;
        if (!IsCompact || _session is { Mode: KanbanDragMode.Pointer })
        {
            return;
        }
        _snapX?.Stop();
        _lastHorizontalScroll = Now;
        _snapLoop ??= new LiveLoop(this, OnSnapFrame) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        _snapLoop.Start();
    }

    private bool OnSnapFrame(double dt)
    {
        if (!IsCompact || _scroller is null)
        {
            return false;
        }
        if (_session is { Mode: KanbanDragMode.Pointer })
        {
            return false;
        }
        if (Now - _lastHorizontalScroll < TimeSpan.FromMilliseconds(140))
        {
            return true;
        }
        SnapAfterLayout(immediate: false);
        return false;
    }

    /// <summary>The snap point nearest to the current horizontal offset, or null when there is nothing to snap.</summary>
    internal double? SnapTarget()
    {
        if (_scroller is null || _grid is null || !IsCompact)
        {
            return null;
        }
        var viewport = Viewport;
        var metrics = KanbanLayout.Measure(viewport.Width, _grid.Columns.Count, MinColumnWidth, CompactColumnWidth, CompactWidth);
        var max = Math.Max(0, _scroller.Extent.Width - viewport.Width);
        var points = KanbanLayout.SnapPoints(metrics, max);
        return points.Count == 0 ? null : KanbanLayout.NearestSnap(points, _scroller.Offset.X);
    }

    private void SnapAfterLayout(bool immediate)
    {
        if (immediate)
        {
            // After the compact layout is in place: start flush on the first column.
            Dispatcher.UIThread.Post(() =>
            {
                if (SnapTarget() is { } target && _scroller is not null)
                {
                    SetHorizontalOffset(target);
                }
            }, DispatcherPriority.Background);
            return;
        }
        if (SnapTarget() is not { } to || _scroller is null || Math.Abs(to - _scroller.Offset.X) < 0.5)
        {
            return;
        }
        if (_snapX is null)
        {
            _snapX = new MotionValue<double>(this, _scroller.Offset.X, MotionVectors.Double);
            _snapX.Changed += SetHorizontalOffset;
        }
        _snapX.Jump(_scroller.Offset.X);
        _ = _snapX.AnimateTo(to, KanbanMotion.Layout);
    }

    private void SetHorizontalOffset(double x)
    {
        if (_scroller is null)
        {
            return;
        }
        _snapping = true;
        try
        {
            _scroller.Offset = new Vector(x, _scroller.Offset.Y);
            _lastOffsetX = _scroller.Offset.X;
        }
        finally
        {
            _snapping = false;
        }
    }
}
