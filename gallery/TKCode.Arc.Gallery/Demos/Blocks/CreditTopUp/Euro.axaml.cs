using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CreditTopUpEuroDemo : UserControl
{
    public CreditTopUpEuroDemo()
    {
        InitializeComponent();
        Card.Pricing = new CreditTopUpPricing
        {
            UnitPrice = 0.009m,
            Currency = "EUR",
            BonusTiers = [new CreditTopUpBonusTier(10_000, 8), new CreditTopUpBonusTier(25_000, 12)]
        };
        Card.Packs = [2_500, 10_000, 25_000];
        Card.Thresholds = [1_000, 2_000, 5_000];
        Card.Usage = CreditTopUpSamples.UsageEnding(DateOnly.FromDateTime(DateTime.Today));
        Card.PaymentMethods =
        [
            new CreditTopUpPaymentMethod { Id = "card_orbit_0412", Brand = CreditTopUpCardBrand.Orbit, Last4 = "0412", ExpiryMonth = 2, ExpiryYear = 2030 }
        ];
        Card.PurchaseHandler = CreditTopUpSamples.SimulatedPurchase(TimeSpan.FromMilliseconds(900));
        Card.Labels = new CreditTopUpLabels
        {
            BalanceTitle = "Prepaid credits",
            AddTitle = "Buy a pack",
            CustomTitle = "Or choose an amount",
            AutoTitle = "Automatic refill",
            AutoSubtitle = "Buys the amount above when you run low",
            Buy = "Buy credits",
            Done = "Credits added",
            FooterLive = "Billed in euros. VAT included.",
            DoneLiveFormat = "{0} credits are on your balance."
        };
    }
}
