using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Arguments of <see cref="AiSourcesRow.CardActivatedEvent"/> (a card was pointed at or focused, or left: 0) and
/// <see cref="AiSourcesRow.CardInvokedEvent"/> (a card was pressed: the source to open).
/// </summary>
internal sealed class AiSourceCardEventArgs : RoutedEventArgs
{
    public AiSourceCardEventArgs(RoutedEvent routedEvent, object source, int sourceNumber, AiSource? item)
        : base(routedEvent, source)
    {
        SourceNumber = sourceNumber;
        Item = item;
    }

    /// <summary>The number of the card's source (<see cref="AiSource.Index"/>); 0 when no card is active any more.</summary>
    public int SourceNumber { get; }

    /// <summary>The card's source; null when no card is active any more.</summary>
    public AiSource? Item { get; }
}
