using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class BillingTogglePage
{
    public static Control Create()
    {
        var toggle = new BillingToggle { Value = "yearly", HorizontalAlignment = HorizontalAlignment.Center };
        var prices = new (string Name, decimal Monthly, decimal Yearly)[] { ("Starter", 12, 10), ("Pro", 30, 24), ("Team", 60, 48) };
        var plans = new WrapPanel { ItemSpacing = 32, LineSpacing = 16, HorizontalAlignment = HorizontalAlignment.Center };
        var priceControls = new List<(BillingPrice Price, decimal Monthly, decimal Yearly)>();
        foreach (var (name, monthly, yearly) in prices)
        {
            var price = new BillingPrice();
            priceControls.Add((price, monthly, yearly));
            plans.Children.Add(GalleryUi.Column(6, GalleryUi.Text(name, ArcDesignTokenKind.TextSecondary, 14), price));
        }
        void Update()
        {
            var yearlyBilling = toggle.Value == "yearly";
            foreach (var (price, monthly, yearly) in priceControls)
            {
                price.Amount = yearlyBilling ? yearly : monthly;
                price.WasAmount = yearlyBilling ? monthly : null;
                price.Period = yearlyBilling ? "per month, billed yearly" : "per month";
            }
        }
        toggle.ValueChanged += (_, _) => Update();
        Update();

        var large = new BillingToggle
        {
            Size = BillingToggleSize.Large,
            ItemsSource = new[]
            {
                new BillingToggleOption("monthly", "Monthly"),
                new BillingToggleOption("yearly", "Yearly", "Save 20%", "You save $48"),
                new BillingToggleOption("lifetime", "Lifetime", "Best value")
            },
            Value = "monthly"
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Pricing", "The thumb glides on a critically damped spring; the savings note tints and rewrites itself in place, and prices roll to the new amount.",
                GalleryUi.Column(28, toggle, plans)),
            GalleryUi.Card("Large, three periods", "Badges can change once their option is chosen (\"Save 20%\" becomes \"You save $48\") without moving anything around them.",
                GalleryUi.Row(large)));
    }
}
