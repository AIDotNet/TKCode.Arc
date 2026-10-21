using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BillingTogglePricingDemo : UserControl
{
    public BillingTogglePricingDemo()
    {
        InitializeComponent();
        ShowPrices();
    }

    private void OnValueChanged(object? sender, BillingToggleValueChangedEventArgs e) => ShowPrices();

    private void ShowPrices()
    {
        var yearly = Toggle.Value == "yearly";
        ShowPrice(StarterPrice, 12, 10, yearly);
        ShowPrice(ProPrice, 30, 24, yearly);
        ShowPrice(TeamPrice, 60, 48, yearly);
    }

    // Setting Amount rolls the price to the new number. The monthly price is shown struck through under yearly billing.
    private static void ShowPrice(BillingPrice price, decimal monthly, decimal perMonthYearly, bool yearly)
    {
        price.Amount = yearly ? perMonthYearly : monthly;
        price.WasAmount = yearly ? monthly : null;
        price.Period = yearly ? "per month, billed yearly" : "per month";
    }
}
