using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class TextMorphPage
{
    private static readonly string[] Words = ["Deploy", "Deploying", "Deployed", "Rolled back"];
    private static readonly string[] Prices = ["$12 / month", "$120 / year", "$10 / month"];

    public static Control Create()
    {
        var status = new TextMorph { Text = Words[0], FontSize = 28 };
        var statusIndex = 0;
        var next = new Button { Content = "Next status", Variant = ButtonVariant.Secondary };
        next.Click += (_, _) => status.Text = Words[statusIndex = (statusIndex + 1) % Words.Length];

        var price = new TextMorph { Text = Prices[0], FontSize = 20, Matching = TextMorphMatching.PrefixSuffix };
        var priceIndex = 0;
        var cycle = new Button { Content = "Change plan", Variant = ButtonVariant.Secondary };
        cycle.Click += (_, _) => price.Text = Prices[priceIndex = (priceIndex + 1) % Prices.Length];

        return GalleryUi.Column(20,
            GalleryUi.Card("Status", "Only the characters that change animate: shared letters glide, new ones rise out of a blur.", GalleryUi.Column(16, status, GalleryUi.Row(next))),
            GalleryUi.Card("Prefix and suffix matching", "Keeps the common start and end in place, for prices and counters.", GalleryUi.Column(16, price, GalleryUi.Row(cycle))));
    }
}
