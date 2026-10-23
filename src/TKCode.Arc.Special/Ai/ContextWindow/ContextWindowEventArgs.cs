using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="ContextWindow.Compacted"/>: what was folded, the summary it became and the totals around it.</summary>
public sealed class ContextWindowCompactedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ContextWindowCompactedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ContextWindowItem> foldedItems, long foldedTokens,
        long summaryTokens, long before, long after, ContextWindowCompactReason reason)
        : base(routedEvent)
    {
        FoldedItems = foldedItems;
        FoldedTokens = foldedTokens;
        SummaryTokens = summaryTokens;
        Before = before;
        After = after;
        Reason = reason;
    }

    /// <summary>The messages that were folded, oldest first (they are no longer in the items).</summary>
    public IReadOnlyList<ContextWindowItem> FoldedItems { get; }

    /// <summary>How many messages were folded.</summary>
    public int FoldedCount => FoldedItems.Count;

    /// <summary>The tokens of the folded messages.</summary>
    public long FoldedTokens { get; }

    /// <summary>The tokens this compaction added to the summary.</summary>
    public long SummaryTokens { get; }

    /// <summary>Tokens used before the compaction.</summary>
    public long Before { get; }

    /// <summary>Tokens used after it.</summary>
    public long After { get; }

    /// <summary>Why it ran.</summary>
    public ContextWindowCompactReason Reason { get; }

    /// <summary>True when the card compacted on its own (past the threshold or after a switch to a smaller model).</summary>
    public bool IsAutomatic => Reason != ContextWindowCompactReason.Manual;
}

/// <summary>Arguments of <see cref="ContextWindow.ModelChanged"/>.</summary>
public sealed class ContextWindowModelChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ContextWindowModelChangedEventArgs(RoutedEvent routedEvent, ContextWindowModel? oldModel, ContextWindowModel? newModel)
        : base(routedEvent)
    {
        OldModel = oldModel;
        NewModel = newModel;
    }

    /// <summary>The model shown before.</summary>
    public ContextWindowModel? OldModel { get; }

    /// <summary>The model shown now.</summary>
    public ContextWindowModel? NewModel { get; }
}

/// <summary>Arguments of <see cref="ContextWindow.ItemsChanged"/>: the items after a send, an attachment, an added item or a compaction.</summary>
public sealed class ContextWindowItemsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ContextWindowItemsChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ContextWindowItem> items, ContextWindowChange change,
        ContextWindowItem? item)
        : base(routedEvent)
    {
        Items = items;
        Change = change;
        Item = item;
    }

    /// <summary>Every item now in the window, in order (a snapshot).</summary>
    public IReadOnlyList<ContextWindowItem> Items { get; }

    /// <summary>What changed them.</summary>
    public ContextWindowChange Change { get; }

    /// <summary>The item that was added, or the summary after a compaction.</summary>
    public ContextWindowItem? Item { get; }
}
