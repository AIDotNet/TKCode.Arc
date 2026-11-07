using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What opened or closed a <see cref="CardToArticle"/> article.</summary>
public enum CardToArticleChangeReason
{
    /// <summary>Code: <see cref="CardToArticle.OpenItemId"/> was set, or <see cref="CardToArticle.Open"/> / <see cref="CardToArticle.Close"/> was called.</summary>
    Programmatic,

    /// <summary>A pointer pressed a card.</summary>
    Click,

    /// <summary>Enter or Space on a focused card.</summary>
    Keyboard,

    /// <summary>The article's close button.</summary>
    CloseButton,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The article was pulled down and let go past the threshold (or flicked).</summary>
    Drag,

    /// <summary>The mouse wheel or touchpad scrolled up past the top of the article.</summary>
    Wheel
}

/// <summary>Raised when the open article of a <see cref="CardToArticle"/> changes.</summary>
public sealed class CardToArticleOpenChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CardToArticleOpenChangedEventArgs(RoutedEvent routedEvent, string? oldId, string? newId, CardToArticleChangeReason reason)
        : base(routedEvent)
    {
        OldId = oldId;
        NewId = newId;
        Reason = reason;
    }

    /// <summary>The id of the article that was open before; null when none was.</summary>
    public string? OldId { get; }

    /// <summary>The id of the article that is open now; null when the article closed.</summary>
    public string? NewId { get; }

    /// <summary>What caused the change.</summary>
    public CardToArticleChangeReason Reason { get; }
}
