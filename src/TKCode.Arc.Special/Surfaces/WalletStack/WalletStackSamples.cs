using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample wallets for the gallery and tests. Every issuer, product, person and merchant is fictional.
/// </summary>
public static class WalletStackSamples
{
    /// <summary>The preview's heading.</summary>
    public const string Label = "Mira’s wallet";

    /// <summary>The preview's card holder.</summary>
    public const string Holder = "Mira Castell";

    /// <summary>The tint of the sample color card: a deep brick red, oklch(0.42 0.11 28).</summary>
    public static Color BrickTint => Oklch.ToColor(0.42, 0.11, 28);

    /// <summary>A fresh copy of <see cref="Cards"/> (for <c>{x:Static}</c> in XAML).</summary>
    public static IList<WalletStackCard> Wallet => Cards();

    /// <summary>A fresh copy of <see cref="TravelCards"/> (for <c>{x:Static}</c> in XAML).</summary>
    public static IList<WalletStackCard> Travel => TravelCards();

    /// <summary>
    /// Four cards, one of each material: a metal current account, a black charge card, a glass ride card and a gym
    /// membership in brick red.
    /// </summary>
    public static IList<WalletStackCard> Cards() => new List<WalletStackCard>
    {
        new()
        {
            Id = "larkspur", Issuer = "Larkspur", Product = "Current account", LastFour = "5306", Holder = Holder,
            Expires = "08/29", Material = WalletStackMaterial.Metal, Balance = 2914.35m, BalanceLabel = "Available",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "l1", Merchant = "Pinecrest Power", Detail = "Electricity · This morning", Amount = -96.40m, Icon = WalletStackIcons.Zap },
                new() { Id = "l2", Merchant = "Odile Brandt", Detail = "Rent share · Monday", Amount = 640.00m, AvatarInitials = "OB" },
                new() { Id = "l3", Merchant = "Second Story Books", Detail = "Two paperbacks · Oct 20", Amount = -27.15m, Icon = WalletStackIcons.BookOpen },
            },
        },
        new()
        {
            Id = "obsidian", Issuer = "Obsidian", Product = "Charge card", LastFour = "1148", Holder = Holder,
            Expires = "02/28", Material = WalletStackMaterial.Black, Balance = 1472.18m, BalanceLabel = "Statement balance",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "o1", Merchant = "Ruth Okafor", Detail = "Paid you back · Oct 23", Amount = 75.00m, AvatarInitials = "RO" },
                new() { Id = "o2", Merchant = "Marlow & Finch", Detail = "Birthday present · Oct 21", Amount = -84.00m, Icon = WalletStackIcons.Gift },
                new() { Id = "o3", Merchant = "Kestrel Fuel", Detail = "Pump 6 · Oct 19", Amount = -58.30m, Icon = WalletStackIcons.Fuel },
            },
        },
        new()
        {
            Id = "bayline", Issuer = "Bayline", Product = "Ride card", LastFour = "6620", Holder = Holder,
            Expires = "10/27", Material = WalletStackMaterial.Glass, Balance = 37.80m, BalanceLabel = "Stored value",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "b1", Merchant = "Tram 12", Detail = "Quayside to Elm Park", Amount = -2.75m, Icon = WalletStackIcons.TrainFront },
                new() { Id = "b2", Merchant = "Weekly fare cap", Detail = "Rides 9–11 refunded", Amount = 4.50m, Icon = WalletStackIcons.ArrowDownLeft },
                new() { Id = "b3", Merchant = "Harbor Night Market", Detail = "Entry · Oct 14", Amount = -6.00m, Icon = WalletStackIcons.Ticket },
            },
        },
        new()
        {
            Id = "pebble", Issuer = "Pebble Gym", Product = "Member", LastFour = "2093", Holder = Holder,
            Expires = "05/28", Material = WalletStackMaterial.Color, Tint = BrickTint, Balance = 21.60m, BalanceLabel = "Class credit",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "p1", Merchant = "Rowing class", Detail = "Studio B · 6:30 AM", Amount = -12.00m, Icon = WalletStackIcons.Dumbbell },
                new() { Id = "p2", Merchant = "Friend referral", Detail = "Credit added", Amount = 15.00m, Icon = WalletStackIcons.Gift },
                new() { Id = "p3", Merchant = "Front desk", Detail = "Towel hire · Oct 9", Amount = -2.40m, Icon = WalletStackIcons.ShoppingCart },
            },
        },
    };

    /// <summary>Three travel cards in euros for a de-DE example: a color card, a glass pass and a metal card.</summary>
    public static IList<WalletStackCard> TravelCards() => new List<WalletStackCard>
    {
        new()
        {
            Id = "wanderlust", Issuer = "Halde Bank", Product = "Reisekarte", LastFour = "7712", Holder = "Jonas Brenner",
            Expires = "11/28", Material = WalletStackMaterial.Color, Tint = Oklch.ToColor(0.38, 0.09, 250),
            Balance = 1284.50m, BalanceLabel = "Verfügbar",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "w1", Merchant = "Buchhandlung Ecke", Detail = "Zwei Romane · Heute", Amount = -18.90m, Icon = WalletStackIcons.BookOpen },
                new() { Id = "w2", Merchant = "Nordbahn", Detail = "Reise · Gestern", Amount = -89.00m, Icon = WalletStackIcons.TrainFront },
                new() { Id = "w3", Merchant = "Lena Vogt", Detail = "Abendessen · 20. Okt.", Amount = 31.25m, AvatarInitials = "LV" },
            },
        },
        new()
        {
            Id = "tram", Issuer = "Stadtlinie", Product = "Monatskarte", LastFour = "0458", Holder = "Jonas Brenner",
            Expires = "12/26", Material = WalletStackMaterial.Glass, Balance = 12.40m, BalanceLabel = "Guthaben",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "t1", Merchant = "Linie 7", Detail = "Fahrt · Heute", Amount = -2.60m, Icon = WalletStackIcons.TrainFront },
                new() { Id = "t2", Merchant = "Leihrad", Detail = "22 Min. · Gestern", Amount = -1.90m, Icon = WalletStackIcons.Bike },
                new() { Id = "t3", Merchant = "Fahrradstellplatz", Detail = "Oktober · 14. Okt.", Amount = -9.00m, Icon = WalletStackIcons.Ticket },
            },
        },
        new()
        {
            Id = "halde", Issuer = "Halde Bank", Product = "Girokonto", LastFour = "3391", Holder = "Jonas Brenner",
            Expires = "04/30", Material = WalletStackMaterial.Metal, Balance = 3410.07m, BalanceLabel = "Kontostand",
            Transactions = new List<WalletStackTransaction>
            {
                new() { Id = "h1", Merchant = "Marktplatz 3", Detail = "Lebensmittel · Heute", Amount = -27.45m, Icon = WalletStackIcons.ShoppingCart },
                new() { Id = "h2", Merchant = "Atelier Kranich", Detail = "Gehalt · 15. Okt.", Amount = 2640.00m, Icon = WalletStackIcons.ArrowDownLeft },
                new() { Id = "h3", Merchant = "Trattoria Sole", Detail = "Essen · 12. Okt.", Amount = -46.30m, Icon = WalletStackIcons.Utensils },
            },
        },
    };
}
