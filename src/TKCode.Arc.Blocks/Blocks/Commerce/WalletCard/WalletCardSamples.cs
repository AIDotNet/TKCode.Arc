namespace TKCode.Arc;

/// <summary>
/// Invented sample content for the <see cref="WalletCard"/> preview: a cardholder, an everyday card and a week of purchases
/// at made-up merchants, plus a simulated card service. Replace them with your own data; none of it is required.
/// </summary>
public static class WalletCardSamples
{
    /// <summary>How long the sample card service takes to freeze or unfreeze.</summary>
    public static readonly TimeSpan ServiceDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>Fills <paramref name="card"/> with the sample cardholder, card, balance, limit and transactions.</summary>
    public static void Apply(WalletCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.HolderName = "Ines Moreau";
        card.CardNumber = "4929 1820 5531 4821";
        card.Expiry = "08/29";
        card.SecurityCode = "317";
        card.Balance = 2481.56m;
        card.MonthlyLimit = 3000m;
        card.SpentThisMonth = 1240.18m;
        card.Transactions.Clear();
        card.Transactions.AddRange(CreateTransactions(DateTimeOffset.Now));
    }

    /// <summary>Six transactions over the last week, newest first, relative to <paramref name="now"/>.</summary>
    public static IReadOnlyList<WalletTransaction> CreateTransactions(DateTimeOffset now) =>
    [
        new("Juniper Grocers", 42.10m, now.AddHours(-1), "Groceries") { Id = "t6", Status = WalletTransactionStatus.Pending },
        new("Lantern Coffee", 6.40m, now.AddHours(-5), "Cafes") { Id = "t5" },
        new("Tidewater Transit", 2.90m, now.AddDays(-1), "Transport") { Id = "t4" },
        new("Pinecrest Outfitters", 64.00m, now.AddDays(-2), "Refund") { Id = "t3", IsCredit = true },
        new("Copperleaf Books", 23.75m, now.AddDays(-3), "Books") { Id = "t2" },
        new("Nimbus Streaming", 11.99m, now.AddDays(-5), "Subscriptions") { Id = "t1" },
    ];

    /// <summary>A simulated card service: waits <see cref="ServiceDelay"/>, then accepts the change.</summary>
    public static async Task<bool> SetFrozenAsync(bool frozen, CancellationToken cancellation)
    {
        await Task.Delay(ServiceDelay, cancellation);
        return true;
    }
}
