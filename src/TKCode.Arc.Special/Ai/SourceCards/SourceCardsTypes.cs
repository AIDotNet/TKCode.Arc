using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where the search behind a <see cref="SourceCards"/> stands.</summary>
public enum SourceCardsStatus
{
    /// <summary>The model is searching: a spinner, a shimmering "Searching", a rolling count and reserved slots.</summary>
    Searching,

    /// <summary>The search has finished: "7 sources" and the time, or "No sources found" when nothing came back.</summary>
    Done,

    /// <summary>The search failed: what arrived stays, the reserved slots go, and "Search failed" shows with Retry.</summary>
    Error
}

/// <summary>Which story a <see cref="SourceCardsSimulation"/> tells.</summary>
public enum SourceCardsScenario
{
    /// <summary>Searches, streams a cited answer and tours two citations, looping until someone interacts.</summary>
    Live,

    /// <summary>Fails after two sources and offers Retry.</summary>
    Error,

    /// <summary>Finds nothing and offers Search again.</summary>
    Empty
}

/// <summary>Data of <see cref="SourceCards.SourceInvoked"/>: the source a card stands for and its citation number.</summary>
public sealed class SourceCardsSourceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SourceCardsSourceEventArgs(RoutedEvent routedEvent, SourceCardsSource item, int number)
        : base(routedEvent)
    {
        Item = item;
        Number = number;
    }

    /// <summary>The source of the card (named <c>Item</c> so it does not hide <see cref="RoutedEventArgs.Source"/>).</summary>
    public SourceCardsSource Item { get; }

    /// <summary>The source's 1-based citation number.</summary>
    public int Number { get; }
}

/// <summary>Data of <see cref="SourceCards.ExpandedChanged"/>: whether the stack is now open.</summary>
public sealed class SourceCardsExpandedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SourceCardsExpandedEventArgs(RoutedEvent routedEvent, bool isExpanded)
        : base(routedEvent)
    {
        IsExpanded = isExpanded;
    }

    /// <summary>Whether every card is now shown.</summary>
    public bool IsExpanded { get; }
}

/// <summary>Data of <see cref="SourceCards.HighlightChanged"/>: the source number a citation highlighted, or null.</summary>
public sealed class SourceCardsHighlightEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SourceCardsHighlightEventArgs(RoutedEvent routedEvent, int? number)
        : base(routedEvent)
    {
        Number = number;
    }

    /// <summary>The 1-based source number now highlighted, or null when the highlight ended.</summary>
    public int? Number { get; }
}
