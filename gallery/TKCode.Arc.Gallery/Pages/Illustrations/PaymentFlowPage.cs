using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Pages;

public static class PaymentFlowPage
{
    public static Avalonia.Controls.Control Create()
    {
        var payment = new PaymentFlow();
        var approved = 0;
        var total = 0;
        var log = GalleryUi.Text("No payments yet", ArcDesignTokenKind.TextMuted, 13);
        payment.PaymentCompleted += (_, e) =>
        {
            total++;
            approved += e.Approved ? 1 : 0;
            log.Text = $"{(e.Approved ? "Approved" : "Declined")} · {approved} of {total} approved";
        };

        var euro = new PaymentFlow { Amount = 129.90m, Currency = "EUR", MerchantName = "Atelier", CardLast4 = "0119", DeclineEvery = 0 };

        return GalleryUi.Column(20,
            GalleryUi.Card("Card payment",
                "Tokenize at the gateway, authorize through the card network, decide at the issuing bank, return on the other lane and settle to the merchant. Every fourth payment is declined (DeclineEvery).",
                GalleryUi.Column(12, payment, GalleryUi.Row(IllustrationSamples.SpeedRow(payment), log))),
            GalleryUi.Card("Amount, currency and merchant",
                "Amount = 129.90, Currency = EUR, MerchantName and CardLast4; DeclineEvery = 0 approves every payment.",
                euro),
            GalleryUi.Card("Reduced motion", "The approved path as a still frame.",
                IllustrationSamples.Reduced(new PaymentFlow())));
    }
}
