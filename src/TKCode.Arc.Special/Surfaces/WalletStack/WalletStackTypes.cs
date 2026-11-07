using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The finish of a <see cref="WalletStackCard"/>.</summary>
public enum WalletStackMaterial
{
    /// <summary>Brushed metal: turned radial grain, fine horizontal brushing and a soft glint. The default.</summary>
    Metal,

    /// <summary>A dark card with fine grain and an engraved guilloche rosette.</summary>
    Black,

    /// <summary>Frosted glass: the cards behind show through, blurred and slightly more saturated.</summary>
    Glass,

    /// <summary>A deep solid color picked by <see cref="WalletStackCard.Tint"/>.</summary>
    Color,
}

/// <summary>
/// One card in a <see cref="WalletStack"/>: what is printed on its face, its balance and its recent activity. The stack
/// reads a card when it is added (or when <see cref="WalletStack.Cards"/> is replaced), so to change a card on screen,
/// replace it in the list.
/// </summary>
public class WalletStackCard
{
    /// <summary>A stable key for the card (used to keep the open card when the list changes).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The issuer's name, printed large at the top left.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The product name, printed small at the top right (for example "Daily checking").</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>The last four digits of the card number; the rest is masked.</summary>
    public string LastFour { get; set; } = string.Empty;

    /// <summary>The card holder's name, printed at the bottom left.</summary>
    public string Holder { get; set; } = string.Empty;

    /// <summary>The finish of the card. Metal by default.</summary>
    public WalletStackMaterial Material { get; set; } = WalletStackMaterial.Metal;

    /// <summary>The base color of a <see cref="WalletStackMaterial.Color"/> card; any color works (a deep brick red when null).</summary>
    public Color? Tint { get; set; }

    /// <summary>The expiry date as printed under "Valid thru" (for example "08/29"); nothing is printed when null.</summary>
    public string? Expires { get; set; }

    /// <summary>Whether a network mark is printed at the bottom right.</summary>
    public bool ShowNetwork { get; set; } = true;

    /// <summary>
    /// A custom network mark, drawn at the mark's height and right aligned. When null the built-in fictional mark is
    /// printed (two rings and a lowercase wordmark).
    /// </summary>
    public IImage? NetworkMark { get; set; }

    /// <summary>Optional artwork under the finish, scaled to fill the card.</summary>
    public IImage? Artwork { get; set; }

    /// <summary>The balance shown when the card is open.</summary>
    public decimal Balance { get; set; }

    /// <summary>What the balance means (for example "Available" or "Spent in October").</summary>
    public string BalanceLabel { get; set; } = string.Empty;

    /// <summary>Recent activity, newest first. The open card shows the first three.</summary>
    public IList<WalletStackTransaction> Transactions { get; set; } = new List<WalletStackTransaction>();
}

/// <summary>One line of a card's recent activity.</summary>
public class WalletStackTransaction
{
    /// <summary>A stable key for the transaction.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Who was paid or who paid (for example a shop or a person).</summary>
    public string Merchant { get; set; } = string.Empty;

    /// <summary>A short second line, usually "category or place · time".</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The amount: negative for spending, positive for money in.</summary>
    public decimal Amount { get; set; }

    /// <summary>A 24×24 stroke icon (Lucide geometry) shown in a round badge.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>A picture of the other person for transfers between people; replaces the icon.</summary>
    public IImage? Avatar { get; set; }

    /// <summary>Initials shown on a colored disc for transfers between people when there is no picture; replaces the icon.</summary>
    public string? AvatarInitials { get; set; }
}

/// <summary>Data of <see cref="WalletStack.SelectedCardChanged"/>.</summary>
public sealed class WalletStackSelectedCardChangedEventArgs : RoutedEventArgs
{
    internal WalletStackSelectedCardChangedEventArgs(RoutedEvent routedEvent, WalletStackCard? oldCard, WalletStackCard? newCard)
        : base(routedEvent)
    {
        OldCard = oldCard;
        NewCard = newCard;
    }

    /// <summary>The card that was open before, or null when the stack was closed.</summary>
    public WalletStackCard? OldCard { get; }

    /// <summary>The card that is open now, or null when it went back into the stack.</summary>
    public WalletStackCard? NewCard { get; }
}

/// <summary>Data of <see cref="WalletStack.PositionChanged"/>.</summary>
public sealed class WalletStackPositionChangedEventArgs : RoutedEventArgs
{
    internal WalletStackPositionChangedEventArgs(RoutedEvent routedEvent, int oldPosition, int newPosition, WalletStackCard? card)
        : base(routedEvent)
    {
        OldPosition = oldPosition;
        NewPosition = newPosition;
        Card = card;
    }

    /// <summary>The index of the card that was in front.</summary>
    public int OldPosition { get; }

    /// <summary>The index of the card now in front.</summary>
    public int NewPosition { get; }

    /// <summary>The card now in front.</summary>
    public WalletStackCard? Card { get; }
}
