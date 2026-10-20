using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class MoneyInputPage
{
    private const double FieldWidth = 380;

    public static Control Create()
    {
        // Typing groups as you go; chips and arrow keys roll the digits; the missing cents wait as ghosts.
        var basic = Field(new MoneyInput { Label = "Amount" });

        var limits = Field(new MoneyInput
        {
            Label = "Transfer",
            Minimum = 5,
            Maximum = 1000,
            Description = "Arrow keys step by $1, Shift by $10, PageUp and PageDown by ten steps."
        });

        var yen = Field(new MoneyInput { Label = "Price", Currency = "JPY", Value = 12000, QuickAddAmounts = [1000, 5000, 10000] });

        var single = Field(new MoneyInput { Label = "Budget", Currency = "EUR", Currencies = ["EUR"], Value = 2400 });

        var noChips = Field(new MoneyInput { Label = "Tip", QuickAddAmounts = [], Value = 12.5m });

        var hidden = Field(new MoneyInput { Label = "Donation", IsLabelVisible = false, QuickAddAmounts = [5, 20, 50] });

        var invalid = Field(new MoneyInput
        {
            Label = "Invoice total",
            Description = "Charged when the order ships.",
            ErrorMessage = "Enter the amount on the invoice.",
            QuickAddAmounts = []
        });
        var described = Field(new MoneyInput
        {
            Label = "Deposit",
            Description = "Held until the booking is confirmed.",
            Maximum = 500,
            QuickAddAmounts = [100, 250]
        });

        var disabled = Field(new MoneyInput { Label = "Locked amount", Value = 250, IsEnabled = false });

        var german = Field(new MoneyInput
        {
            Label = "Betrag",
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            Currency = "EUR",
            Value = 1234.56m,
            Maximum = 10000
        });

        // Two-way Value plus the event details Arc reports (major value, minor units and the formatted string).
        var bound = new MoneyInput { Label = "Amount", Maximum = 5000 };
        var readout = GalleryUi.Mono(Describe(bound.Value, bound.MinorUnits, bound.FormattedValue, bound.Currency));
        var eventLine = GalleryUi.Mono("ValueChanged: (none yet)", ArcDesignTokenKind.TextMuted);
        bound.PropertyChanged += (_, e) =>
        {
            if (e.Property == MoneyInput.ValueProperty || e.Property == MoneyInput.CurrencyProperty)
            {
                readout.Text = Describe(bound.Value, bound.MinorUnits, bound.FormattedValue, bound.Currency);
            }
        };
        bound.ValueChanged += (_, e) =>
            eventLine.Text = $"ValueChanged ({e.ChangeSource}): {Show(e.OldValue)} → {Show(e.NewValue)}, {e.Currency}, minor {e.MinorUnits?.ToString(CultureInfo.InvariantCulture) ?? "null"}, \"{e.Formatted}\"";
        var reset = new Button { Content = "Set Value = 99.99", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        reset.Click += (_, _) => bound.Value = 99.99m;
        var clear = new Button { Content = "Clear", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        clear.Click += (_, _) => bound.Value = null;
        var events = GalleryUi.Column(14, Field(bound), readout, eventLine,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { reset, clear } });

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "Type an amount: digits group as you type, missing cents wait as ghosts, chips roll the digits.", basic),
            GalleryUi.Card("Minimum and maximum", "Typing past $1,000 is refused with a kick and the range flashes; the minimum is checked when the field loses focus.", limits),
            GalleryUi.Card("No decimals", "Japanese yen has no minor unit, so there are no ghost cents.", yen),
            GalleryUi.Card("Single currency", "With one currency the code is shown as static text instead of the menu.", single),
            GalleryUi.Card("No quick add", "An empty QuickAddAmounts list hides the chips.", noChips),
            GalleryUi.Card("Hidden label", "IsLabelVisible = false keeps the label for screen readers only.", hidden),
            GalleryUi.Card("Description and error", "An error replaces the description and turns the shell danger.", GalleryUi.Column(20, described, invalid)),
            GalleryUi.Card("Disabled", null, disabled),
            GalleryUi.Card("German euros", "de-DE puts the symbol after the amount and swaps the separators: 1.234,56 €.", german),
            GalleryUi.Card("Binding and events", "Value is two-way in major units; ValueChanged carries Arc's details and where the change came from (type, paste, key, chip, currency, programmatic).", events));
    }

    private static Control Field(MoneyInput input)
    {
        input.Width = FieldWidth;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        return input;
    }

    private static string Show(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private static string Describe(decimal? value, long? minor, string formatted, string currency)
        => $"Value = {Show(value)}   MinorUnits = {minor?.ToString(CultureInfo.InvariantCulture) ?? "null"}   Formatted = \"{formatted}\"   Currency = {currency}";
}
