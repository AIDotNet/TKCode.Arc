using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The printed finish of a <see cref="WalletCard"/>'s face.</summary>
public enum WalletCardFinish
{
    /// <summary>Deep blue-black with a cool sheen (the default).</summary>
    Midnight,

    /// <summary>Teal to sea blue.</summary>
    Ocean,

    /// <summary>Warm coral to plum.</summary>
    Coral,

    /// <summary>Moss to pine green.</summary>
    Forest,

    /// <summary>The app's accent, from its strong shade to its base shade.</summary>
    Accent
}

/// <summary>Where a <see cref="WalletTransaction"/> stands.</summary>
public enum WalletTransactionStatus
{
    /// <summary>Settled.</summary>
    Completed,

    /// <summary>Authorized but not settled yet; the amount may still change.</summary>
    Pending,

    /// <summary>Refused (for example while the card was frozen); no money moved.</summary>
    Declined
}

/// <summary>One line of a <see cref="WalletCard"/>'s recent activity.</summary>
public sealed class WalletTransaction
{
    /// <summary>Creates an empty transaction (set the properties).</summary>
    public WalletTransaction()
    {
    }

    /// <summary>Creates a transaction with its merchant, amount and time.</summary>
    public WalletTransaction(string merchant, decimal amount, DateTimeOffset at, string? category = null)
    {
        Merchant = merchant;
        Amount = amount;
        At = at;
        Category = category;
    }

    /// <summary>Stable identifier for events; defaults to the merchant and time when empty.</summary>
    public string? Id { get; set; }

    /// <summary>Who was paid, or who paid back ("Juniper Grocers").</summary>
    public string Merchant { get; set; } = string.Empty;

    /// <summary>The kind of purchase shown after the date ("Groceries").</summary>
    public string? Category { get; set; }

    /// <summary>The amount, positive for money spent; see <see cref="IsCredit"/> for money received.</summary>
    public decimal Amount { get; set; }

    /// <summary>True for a refund or a top-up: the amount is shown with a plus sign in the success color.</summary>
    public bool IsCredit { get; set; }

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Settled, pending or declined.</summary>
    public WalletTransactionStatus Status { get; set; }

    /// <summary>The identifier events use: <see cref="Id"/>, or the merchant and time.</summary>
    public string Key => string.IsNullOrEmpty(Id) ? $"{Merchant}@{At.UtcTicks}" : Id;
}

/// <summary>A <see cref="WalletCard"/> was frozen or unfrozen.</summary>
public sealed class WalletCardFrozenChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WalletCardFrozenChangedEventArgs(RoutedEvent routedEvent, bool isFrozen) : base(routedEvent) => IsFrozen = isFrozen;

    /// <summary>True when the card is now frozen.</summary>
    public bool IsFrozen { get; }
}

/// <summary>A row of a <see cref="WalletCard"/>'s activity was pressed.</summary>
public sealed class WalletTransactionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WalletTransactionEventArgs(RoutedEvent routedEvent, WalletTransaction transaction) : base(routedEvent) => Transaction = transaction;

    /// <summary>The transaction of the row.</summary>
    public WalletTransaction Transaction { get; }
}
