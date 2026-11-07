namespace TKCode.Arc;

/// <summary>
/// Every text a <see cref="WalletStack"/> shows or speaks, with English defaults. Sentences that contain values are
/// delegates so a translation can reorder them. Money arrives already formatted in the stack's currency and culture.
/// </summary>
public sealed class WalletStackLabels
{
    /// <summary>Text before the front card's number in the header count ("" in English).</summary>
    public Func<int, string> CountBefore { get; set; } = _ => string.Empty;

    /// <summary>Text after the front card's number in the header count, given the number of cards (" of 4").</summary>
    public Func<int, string> CountAfter { get; set; } = total => $" of {total}";

    /// <summary>The header button that puts the open card back.</summary>
    public string AllCards { get; set; } = "All cards";

    /// <summary>The small label printed above the expiry date.</summary>
    public string ValidThru { get; set; } = "Valid thru";

    /// <summary>The wordmark next to the built-in fictional network mark.</summary>
    public string NetworkName { get; set; } = "orbit";

    /// <summary>The keyboard hint that describes the stack to screen readers.</summary>
    public string KeyboardHint { get; set; } =
        "Arrow keys bring another card to the front. Enter opens the front card and Escape puts it back.";

    /// <summary>
    /// A card's accessible name, given the card, its 1-based position, the number of cards and its formatted balance.
    /// </summary>
    public Func<WalletStackCard, int, int, string, string> CardName { get; set; } = (card, position, total, balance) =>
        $"{card.Issuer} {card.Product} ending in {card.LastFour}, {position} of {total}, " +
        $"{card.BalanceLabel.ToLowerInvariant()} {balance}";

    /// <summary>Announced when another card comes to the front, given the card, its 1-based position and the number of cards.</summary>
    public Func<WalletStackCard, int, int, string> FrontAnnouncement { get; set; } = (card, position, total) =>
        $"{card.Issuer} {card.Product} in front, {position} of {total}";

    /// <summary>Announced when a card opens.</summary>
    public Func<WalletStackCard, string> OpenAnnouncement { get; set; } = card => $"{card.Issuer} {card.Product} open";

    /// <summary>The accessible name of the open card's activity region.</summary>
    public Func<WalletStackCard, string> ActivityName { get; set; } = card => $"{card.Issuer} activity";

    /// <summary>The accessible name of the open card's transaction list.</summary>
    public Func<WalletStackCard, string> TransactionsName { get; set; } = card => $"Recent activity on {card.Issuer}";
}
