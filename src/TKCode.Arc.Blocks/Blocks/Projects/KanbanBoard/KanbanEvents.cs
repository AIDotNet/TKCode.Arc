using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Data of <see cref="KanbanBoard.CardsChanged"/>: why the board changed and the full card list afterwards, in board
/// order (the order inside each column). Persist <see cref="Cards"/> to keep the board as it is.
/// </summary>
public sealed class KanbanCardsChangedEventArgs : RoutedEventArgs
{
    public KanbanCardsChangedEventArgs(RoutedEvent routedEvent, KanbanChangeReason reason, KanbanCard? card,
                                       IReadOnlyList<KanbanCard> cards) : base(routedEvent)
    {
        Reason = reason;
        Card = card;
        Cards = cards;
    }

    /// <summary>What changed.</summary>
    public KanbanChangeReason Reason { get; }

    /// <summary>The card that changed.</summary>
    public KanbanCard? Card { get; }

    /// <summary>A snapshot of every card in board order, after the change.</summary>
    public IReadOnlyList<KanbanCard> Cards { get; }
}

/// <summary>Data of <see cref="KanbanBoard.CardMoved"/>: where a card was and where it is now.</summary>
public sealed class KanbanCardMovedEventArgs : RoutedEventArgs
{
    public KanbanCardMovedEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanColumn? fromColumn, KanbanColumn? toColumn,
                                    ProjectPerson? fromAssignee, ProjectPerson? toAssignee, int fromIndex, int toIndex,
                                    KanbanSlot from, KanbanSlot to, KanbanMoveSource changeSource) : base(routedEvent)
    {
        Card = card;
        FromColumn = fromColumn;
        ToColumn = toColumn;
        FromAssignee = fromAssignee;
        ToAssignee = toAssignee;
        FromIndex = fromIndex;
        ToIndex = toIndex;
        From = from;
        To = to;
        ChangeSource = changeSource;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>The column it left (the same as <see cref="ToColumn"/> for a move inside a column).</summary>
    public KanbanColumn? FromColumn { get; }

    /// <summary>The column it is in now.</summary>
    public KanbanColumn? ToColumn { get; }

    /// <summary>Its owner before the move (null: nobody).</summary>
    public ProjectPerson? FromAssignee { get; }

    /// <summary>Its owner after the move (it changes when a card is dropped in another person's swimlane).</summary>
    public ProjectPerson? ToAssignee { get; }

    /// <summary>Its position in the board's card list before the move.</summary>
    public int FromIndex { get; }

    /// <summary>Its position in the board's card list after the move.</summary>
    public int ToIndex { get; }

    /// <summary>The cell and position it left.</summary>
    public KanbanSlot From { get; }

    /// <summary>The cell and position it landed in.</summary>
    public KanbanSlot To { get; }

    /// <summary>What moved it: a pointer, touch or keyboard drag, the details drawer or a method call.</summary>
    public KanbanMoveSource ChangeSource { get; }
}

/// <summary>Data of <see cref="KanbanBoard.CardCreating"/>: a card is about to be added; change its key or title, or cancel it.</summary>
public sealed class KanbanCardCreatingEventArgs : RoutedEventArgs
{
    public KanbanCardCreatingEventArgs(RoutedEvent routedEvent, string key, string title, KanbanColumn column,
                                       ProjectPerson? assignee) : base(routedEvent)
    {
        Key = key;
        Title = title;
        Column = column;
        Assignee = assignee;
    }

    /// <summary>The id the card will get (from <see cref="KanbanBoard.CardKeyFactory"/> or the next number).</summary>
    public string Key { get; set; }

    /// <summary>The typed title, trimmed.</summary>
    public string Title { get; set; }

    /// <summary>The column the card goes into.</summary>
    public KanbanColumn Column { get; }

    /// <summary>The card's owner (the current user, or the swimlane's person).</summary>
    public ProjectPerson? Assignee { get; }

    /// <summary>True keeps the card from being added (the composer stays open with the text).</summary>
    public bool Cancel { get; set; }
}

/// <summary>Data of <see cref="KanbanBoard.CardCreated"/>: a card was added with quick add.</summary>
public sealed class KanbanCardCreatedEventArgs : RoutedEventArgs
{
    public KanbanCardCreatedEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanColumn column, ProjectPerson? assignee)
        : base(routedEvent)
    {
        Card = card;
        Column = column;
        Assignee = assignee;
    }

    /// <summary>The new card (already in the board's list, at the end).</summary>
    public KanbanCard Card { get; }

    /// <summary>Its column.</summary>
    public KanbanColumn Column { get; }

    /// <summary>Its owner.</summary>
    public ProjectPerson? Assignee { get; }
}

/// <summary>Data of the card events that carry only the card (<see cref="KanbanBoard.CardInvoked"/>, details opened and closed).</summary>
public sealed class KanbanCardEventArgs : RoutedEventArgs
{
    public KanbanCardEventArgs(RoutedEvent routedEvent, KanbanCard card) : base(routedEvent) => Card = card;

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }
}

/// <summary>Data of <see cref="KanbanBoard.CardEdited"/>: a card property was edited in the details drawer.</summary>
public sealed class KanbanCardEditedEventArgs : RoutedEventArgs
{
    public KanbanCardEditedEventArgs(RoutedEvent routedEvent, KanbanCard card, AvaloniaProperty property, object? oldValue,
                                     object? newValue) : base(routedEvent)
    {
        Card = card;
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>The edited property (<see cref="KanbanCard.TitleProperty"/>, <see cref="KanbanCard.DescriptionProperty"/>, ...).</summary>
    public AvaloniaProperty Property { get; }

    /// <summary>The value before the edit.</summary>
    public object? OldValue { get; }

    /// <summary>The value after the edit.</summary>
    public object? NewValue { get; }
}

/// <summary>Data of <see cref="KanbanBoard.ChecklistItemToggled"/>: a checklist step was ticked or unticked.</summary>
public sealed class KanbanChecklistItemToggledEventArgs : RoutedEventArgs
{
    public KanbanChecklistItemToggledEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanChecklistItem item, bool isDone)
        : base(routedEvent)
    {
        Card = card;
        Item = item;
        IsDone = isDone;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>The step.</summary>
    public KanbanChecklistItem Item { get; }

    /// <summary>The step's new state.</summary>
    public bool IsDone { get; }
}

/// <summary>Data of <see cref="KanbanBoard.GroupByChanged"/>.</summary>
public sealed class KanbanGroupByChangedEventArgs : RoutedEventArgs
{
    public KanbanGroupByChangedEventArgs(RoutedEvent routedEvent, KanbanGroupBy oldValue, KanbanGroupBy newValue) : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The previous grouping.</summary>
    public KanbanGroupBy OldValue { get; }

    /// <summary>The new grouping.</summary>
    public KanbanGroupBy NewValue { get; }
}

/// <summary>Data of <see cref="KanbanBoard.ColumnLimitStateChanged"/>: a column went over its limit or came back under it.</summary>
public sealed class KanbanColumnLimitEventArgs : RoutedEventArgs
{
    public KanbanColumnLimitEventArgs(RoutedEvent routedEvent, KanbanColumn column, int count, int? limit, bool isOverLimit)
        : base(routedEvent)
    {
        Column = column;
        Count = count;
        Limit = limit;
        IsOverLimit = isOverLimit;
    }

    /// <summary>The column.</summary>
    public KanbanColumn Column { get; }

    /// <summary>Its card count now.</summary>
    public int Count { get; }

    /// <summary>Its limit.</summary>
    public int? Limit { get; }

    /// <summary>True when it now holds more than its limit.</summary>
    public bool IsOverLimit { get; }
}

/// <summary>Data of <see cref="KanbanBoard.CardDragStarting"/>: a card is about to be lifted; set <see cref="Cancel"/> to keep it in place.</summary>
public sealed class KanbanCardDragStartingEventArgs : RoutedEventArgs
{
    public KanbanCardDragStartingEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanSlot origin, KanbanMoveSource changeSource)
        : base(routedEvent)
    {
        Card = card;
        Origin = origin;
        ChangeSource = changeSource;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>Where it is.</summary>
    public KanbanSlot Origin { get; }

    /// <summary>Pointer, touch or keyboard.</summary>
    public KanbanMoveSource ChangeSource { get; }

    /// <summary>True refuses the drag (a locked card, missing permission).</summary>
    public bool Cancel { get; set; }
}

/// <summary>Data of <see cref="KanbanBoard.CardDragStarted"/> and <see cref="KanbanBoard.CardDragTargetChanged"/>.</summary>
public sealed class KanbanCardDragEventArgs : RoutedEventArgs
{
    public KanbanCardDragEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanSlot origin, KanbanSlot target,
                                   KanbanSlot? previousTarget, bool isTargetOverLimit, KanbanMoveSource changeSource)
        : base(routedEvent)
    {
        Card = card;
        Origin = origin;
        Target = target;
        PreviousTarget = previousTarget;
        IsTargetOverLimit = isTargetOverLimit;
        ChangeSource = changeSource;
    }

    /// <summary>The lifted card.</summary>
    public KanbanCard Card { get; }

    /// <summary>Where it was lifted from.</summary>
    public KanbanSlot Origin { get; }

    /// <summary>Where it would land now.</summary>
    public KanbanSlot Target { get; }

    /// <summary>Where it would have landed before this change (null when the drag starts).</summary>
    public KanbanSlot? PreviousTarget { get; }

    /// <summary>True when the target column would hold more cards than its limit.</summary>
    public bool IsTargetOverLimit { get; }

    /// <summary>Pointer, touch or keyboard.</summary>
    public KanbanMoveSource ChangeSource { get; }
}

/// <summary>Data of <see cref="KanbanBoard.CardMoving"/>: a card is about to be dropped; set <see cref="Cancel"/> to send it back.</summary>
public sealed class KanbanCardMovingEventArgs : RoutedEventArgs
{
    public KanbanCardMovingEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanSlot from, KanbanSlot to, bool isTargetOverLimit,
                                     KanbanMoveSource changeSource) : base(routedEvent)
    {
        Card = card;
        From = from;
        To = to;
        IsTargetOverLimit = isTargetOverLimit;
        ChangeSource = changeSource;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>Where it was lifted from.</summary>
    public KanbanSlot From { get; }

    /// <summary>Where it is being dropped.</summary>
    public KanbanSlot To { get; }

    /// <summary>True when the drop puts its column over the limit (the board allows it; cancel to make limits strict).</summary>
    public bool IsTargetOverLimit { get; }

    /// <summary>Pointer, touch or keyboard.</summary>
    public KanbanMoveSource ChangeSource { get; }

    /// <summary>True sends the card back to where it was lifted from.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Data of <see cref="KanbanBoard.CardDragCancelled"/>: a drag ended without a drop and the card is going back.</summary>
public sealed class KanbanCardDragCancelledEventArgs : RoutedEventArgs
{
    public KanbanCardDragCancelledEventArgs(RoutedEvent routedEvent, KanbanCard card, KanbanSlot origin, KanbanDragCancelReason reason)
        : base(routedEvent)
    {
        Card = card;
        Origin = origin;
        Reason = reason;
    }

    /// <summary>The card.</summary>
    public KanbanCard Card { get; }

    /// <summary>Where it goes back to.</summary>
    public KanbanSlot Origin { get; }

    /// <summary>Why the drag ended.</summary>
    public KanbanDragCancelReason Reason { get; }
}
