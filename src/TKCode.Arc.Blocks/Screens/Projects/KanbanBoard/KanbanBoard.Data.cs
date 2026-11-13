using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class KanbanBoard
{
    private readonly List<KanbanCard> _cards = new();
    private readonly HashSet<KanbanCard> _watchedCards = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<AvaloniaObject> _watchedStructure = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _observedCards;
    private INotifyCollectionChanged? _observedSource;
    private INotifyCollectionChanged? _observedColumns;
    private INotifyCollectionChanged? _observedPeople;
    private INotifyCollectionChanged? _observedLabels;
    private KanbanGrid? _grid;
    private bool _dataAttached;
    private bool _mutating;
    private bool _rebuildPending;
    private bool _rebuildAnimate;

    /// <summary>The committed grid (cards split into cells).</summary>
    internal KanbanGrid? Grid => _grid;

    /// <summary>The cards in board order as the board shows them.</summary>
    internal IReadOnlyList<KanbanCard> EffectiveCards
    {
        get
        {
            FlushRebuild();
            return _cards;
        }
    }

    /// <summary>
    /// The board follows its lists and the cards, columns, people and labels in them only while it is in a visual tree:
    /// they are the app's and usually outlive the board. Back in a tree it reads them again (it missed what changed).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dataAttached = true;
        WatchCollections();
        WatchStructure();
        WatchCards();
        ScheduleRebuild(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dataAttached = false;
        WatchCollections();
        WatchStructure();
        WatchCards();
    }

    private void WatchCollections()
    {
        Observe(ref _observedCards, _dataAttached ? Cards : null, OnCardsCollectionChanged);
        Observe(ref _observedSource, _dataAttached ? CardsSource as INotifyCollectionChanged : null, OnSourceCollectionChanged);
        Observe(ref _observedColumns, _dataAttached ? Columns : null, OnStructureCollectionChanged);
        Observe(ref _observedPeople, _dataAttached ? People : null, OnStructureCollectionChanged);
        Observe(ref _observedLabels, _dataAttached ? Labels : null, OnStructureCollectionChanged);
    }

    private static void Observe(ref INotifyCollectionChanged? observed, INotifyCollectionChanged? next, NotifyCollectionChangedEventHandler handler)
    {
        if (ReferenceEquals(observed, next))
        {
            return;
        }
        if (observed is not null)
        {
            observed.CollectionChanged -= handler;
        }
        observed = next;
        if (observed is not null)
        {
            observed.CollectionChanged += handler;
        }
    }

    private void OnCardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CardsSource is null && !_mutating)
        {
            ScheduleRebuild(animate: true);
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_mutating)
        {
            ScheduleRebuild(animate: true);
        }
    }

    private void OnStructureCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchStructure();
        ScheduleRebuild(animate: true);
    }

    /// <summary>Columns, people and labels can be renamed or re-limited in place; the board follows.</summary>
    private void WatchStructure()
    {
        var now = new HashSet<AvaloniaObject>(ReferenceEqualityComparer.Instance);
        if (_dataAttached)
        {
            foreach (var column in Columns ?? [])
            {
                now.Add(column);
            }
            foreach (var person in People ?? [])
            {
                now.Add(person);
            }
            foreach (var label in Labels ?? [])
            {
                now.Add(label);
            }
        }
        foreach (var gone in _watchedStructure.Where(o => !now.Contains(o)).ToList())
        {
            gone.PropertyChanged -= OnStructurePropertyChanged;
            _watchedStructure.Remove(gone);
        }
        foreach (var item in now)
        {
            if (_watchedStructure.Add(item))
            {
                item.PropertyChanged += OnStructurePropertyChanged;
            }
        }
    }

    private void OnStructurePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KanbanColumn.CountProperty || e.Property == KanbanColumn.IsOverLimitProperty)
        {
            return;
        }
        if (e.Property == KanbanColumn.KeyProperty || e.Property == ProjectPerson.KeyProperty ||
            (sender is ProjectPerson && e.Property == ProjectPerson.NameProperty && string.IsNullOrEmpty(((ProjectPerson)sender).Key)) ||
            (sender is KanbanColumn && e.Property == KanbanColumn.NameProperty && string.IsNullOrEmpty(((KanbanColumn)sender).Key)))
        {
            ScheduleRebuild(animate: true);
            return;
        }
        if (e.Property == KanbanColumn.LimitProperty)
        {
            UpdateCommittedCounts();
        }
        _panel?.RefreshTexts();
        _drawer?.Refresh();
    }

    /// <summary>
    /// Outside changes (a Clear followed by Adds, several property edits) are applied together once per dispatcher turn,
    /// so every card moves once. Public methods apply pending changes first.
    /// </summary>
    private void ScheduleRebuild(bool animate)
    {
        _rebuildAnimate |= animate;
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPending)
            {
                RebuildNow(_rebuildAnimate);
            }
        }, DispatcherPriority.Render);
    }

    /// <summary>Applies outside changes that are still waiting.</summary>
    internal void FlushRebuild()
    {
        if (_rebuildPending)
        {
            RebuildNow(_rebuildAnimate);
        }
    }

    /// <summary>Reads the cards again, splits them into cells and lays the board out (cards glide when <paramref name="animate"/>).</summary>
    internal void RebuildNow(bool animate)
    {
        _rebuildPending = false;
        _rebuildAnimate = false;
        _cards.Clear();
        IEnumerable source = CardsSource ?? (IEnumerable?)Cards ?? Array.Empty<KanbanCard>();
        foreach (var item in source)
        {
            if (item is KanbanCard card && !_cards.Contains(card))
            {
                _cards.Add(card);
            }
        }
        WatchCards();
        ApplyGrid(animate);
    }

    /// <summary>Splits the current order into cells and refreshes everything that depends on it.</summary>
    private void ApplyGrid(bool animate)
    {
        _grid = KanbanGrid.Build(_cards, (IReadOnlyList<KanbanColumn>?)Columns ?? [], (IReadOnlyList<ProjectPerson>?)People ?? [], GroupBy);
        if (animate && _panel is not null)
        {
            _panel.Flip.Arm(KanbanMotion.Layout, keepVelocity: true);
        }
        ValidateDragAfterRebuild();
        _panel?.Sync();
        UpdateCommittedCounts();
        UpdateLiveCounts();
        UpdateTotal();
        if (OpenedCard is { } opened && !_cards.Contains(opened))
        {
            SetCurrentValue(OpenedCardProperty, null);
        }
        _drawer?.Refresh();
    }

    private void WatchCards()
    {
        foreach (var gone in _watchedCards.Where(c => !_dataAttached || !_cards.Contains(c)).ToList())
        {
            gone.PropertyChanged -= OnCardPropertyChanged;
            _watchedCards.Remove(gone);
        }
        if (!_dataAttached)
        {
            return;
        }
        foreach (var card in _cards)
        {
            if (_watchedCards.Add(card))
            {
                card.PropertyChanged += OnCardPropertyChanged;
            }
        }
    }

    private void OnCardPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_mutating || sender is not KanbanCard card)
        {
            return;
        }
        if (e.Property == KanbanCard.ColumnKeyProperty || e.Property == KanbanCard.AssigneeKeyProperty)
        {
            // Moved from outside (a bound view model, another view): glide to the new cell.
            ScheduleRebuild(animate: true);
        }
        else if (ReferenceEquals(card, OpenedCard))
        {
            _drawer?.Refresh();
        }
    }

    /// <summary>Records the committed count on every column and raises <see cref="ColumnLimitStateChanged"/> for flips.</summary>
    private void UpdateCommittedCounts()
    {
        if (_grid is null)
        {
            return;
        }
        for (var i = 0; i < _grid.Columns.Count; i++)
        {
            var column = _grid.Columns[i];
            var count = _grid.ColumnCount(i);
            if (column.SetCount(count))
            {
                RaiseEvent(new KanbanColumnLimitEventArgs(ColumnLimitStateChangedEvent, column, count, column.Limit, column.IsOverLimit));
                if (column.IsOverLimit && column.Limit is { } limit && IsLoaded)
                {
                    Announce(Strings.ColumnOverLimit(column.Name, count, limit));
                }
            }
        }
    }

    // Mutations ----------------------------------------------------------------------------------------------------------

    /// <summary>The list the board writes to: <see cref="CardsSource"/> when it is a writable list, else <see cref="Cards"/>; null when read-only.</summary>
    private IList? WritableSource
    {
        get
        {
            if (CardsSource is null)
            {
                return Cards;
            }
            return CardsSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;
        }
    }

    /// <summary>Moves <paramref name="card"/> to <paramref name="newIndex"/> (in the order without it), in the source list too.</summary>
    private void Reorder(KanbanCard card, int newIndex)
    {
        var old = _cards.IndexOf(card);
        if (old < 0)
        {
            return;
        }
        _cards.RemoveAt(old);
        newIndex = Math.Clamp(newIndex, 0, _cards.Count);
        _cards.Insert(newIndex, card);
        if (old == newIndex)
        {
            return;
        }
        var target = WritableSource;
        if (target is null)
        {
            return;
        }
        _mutating = true;
        try
        {
            var sourceOld = target.IndexOf(card);
            if (sourceOld < 0)
            {
                return;
            }
            // The source may hold other items: insert in front of the card that follows in the board order.
            var anchor = newIndex + 1 < _cards.Count ? _cards[newIndex + 1] : null;
            var at = anchor is null ? target.Count - 1 : target.IndexOf(anchor);
            if (at < 0)
            {
                at = target.Count - 1;
            }
            else if (anchor is not null && at > sourceOld)
            {
                at--;
            }
            switch (target)
            {
                case AvaloniaList<KanbanCard> avalonia:
                    avalonia.Move(sourceOld, at);
                    break;
                case ObservableCollection<KanbanCard> observable:
                    observable.Move(sourceOld, at);
                    break;
                default:
                    target.RemoveAt(sourceOld);
                    target.Insert(at, card);
                    break;
            }
        }
        finally
        {
            _mutating = false;
        }
    }

    /// <summary>Adds <paramref name="card"/> at the end of the board (and of the source list).</summary>
    private void Append(KanbanCard card)
    {
        _cards.Add(card);
        if (WritableSource is { } target)
        {
            _mutating = true;
            try
            {
                target.Add(card);
            }
            finally
            {
                _mutating = false;
            }
        }
        WatchCards();
    }

    /// <summary>Sets a card property without the board treating it as an outside change.</summary>
    private void SetQuietly<T>(KanbanCard card, StyledProperty<T> property, T value)
    {
        _mutating = true;
        try
        {
            card.SetValue(property, value);
        }
        finally
        {
            _mutating = false;
        }
    }

    /// <summary>A snapshot of the board order for event data.</summary>
    internal IReadOnlyList<KanbanCard> Snapshot() => _cards.ToArray();

    internal void RaiseCardsChanged(KanbanChangeReason reason, KanbanCard? card) =>
        RaiseEvent(new KanbanCardsChangedEventArgs(CardsChangedEvent, reason, card, Snapshot()));

    /// <summary>
    /// Moves <paramref name="card"/> to <paramref name="target"/> in the data: its column, its owner (in swimlanes) and
    /// its place in the board order, then raises <see cref="CardMoved"/> and <see cref="CardsChanged"/>.
    /// </summary>
    internal KanbanCardMovedEventArgs? CommitMove(KanbanCard card, KanbanSlot target, KanbanSlot from, KanbanMoveSource source,
                                                  bool animate)
    {
        if (_grid is null)
        {
            return null;
        }
        var fromIndex = _cards.IndexOf(card);
        var fromColumn = ColumnFor(card.ColumnKey);
        var fromAssignee = PersonFor(card.AssigneeKey);
        var column = _grid.ColumnIndex(target.ColumnKey);
        if (fromIndex < 0 || column < 0)
        {
            return null;
        }
        var lane = GroupBy == KanbanGroupBy.Assignee ? _grid.LaneIndexFor(target.AssigneeKey) : 0;
        var cellCards = lane >= 0 ? _grid.Cell(lane, column).Where(c => !ReferenceEquals(c, card)).ToList() : new List<KanbanCard>();
        var without = _cards.Where(c => !ReferenceEquals(c, card)).ToList();
        var newIndex = KanbanLayout.FlatInsertionIndex(without, cellCards, target.Index, fromIndex);
        if (card.ColumnKey != target.ColumnKey)
        {
            SetQuietly(card, KanbanCard.ColumnKeyProperty, target.ColumnKey);
        }
        if (GroupBy == KanbanGroupBy.Assignee && card.AssigneeKey != target.AssigneeKey)
        {
            SetQuietly(card, KanbanCard.AssigneeKeyProperty, target.AssigneeKey);
        }
        Reorder(card, newIndex);
        ApplyGrid(animate);
        var args = new KanbanCardMovedEventArgs(CardMovedEvent, card, fromColumn, ColumnFor(card.ColumnKey), fromAssignee,
            PersonFor(card.AssigneeKey), fromIndex, _cards.IndexOf(card), from, _grid.SlotOf(card) ?? target, source);
        RaiseEvent(args);
        if (CardMovedCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        RaiseCardsChanged(KanbanChangeReason.Moved, card);
        return args;
    }

    /// <summary>The next card id: the highest number among the cards' ids plus one, with the same prefix and width.</summary>
    internal static string NextKey(IEnumerable<string> keys)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        string? bestPrefix = null;
        long best = -1;
        var width = 0;
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }
            existing.Add(key);
            var end = key.Length;
            var start = end;
            while (start > 0 && char.IsAsciiDigit(key[start - 1]))
            {
                start--;
            }
            if (start == end || end - start > 15)
            {
                continue;
            }
            var number = long.Parse(key.AsSpan(start), NumberStyles.None, CultureInfo.InvariantCulture);
            if (number > best)
            {
                best = number;
                bestPrefix = key[..start];
                width = end - start;
            }
        }
        var prefix = bestPrefix ?? string.Empty;
        var next = Math.Max(best, 0) + 1;
        while (true)
        {
            var candidate = prefix + next.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
            next++;
        }
    }
}

/// <summary>The motion specs the board uses, built once.</summary>
internal static class KanbanMotion
{
    public static readonly TKCode.Arc.Motion.MotionSpec Layout = TKCode.Arc.Motion.MotionSpec.Spring(KanbanDragPhysics.Layout);

    public static readonly TKCode.Arc.Motion.MotionSpec Quick = TKCode.Arc.Motion.MotionSpec.Spring(KanbanDragPhysics.Quick);

    public static readonly TKCode.Arc.Motion.MotionSpec Catch = TKCode.Arc.Motion.MotionSpec.Spring(KanbanDragPhysics.Catch);
}
