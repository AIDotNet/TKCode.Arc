using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class PhoneInputPage
{
    private const double FieldWidth = 360;

    public static Control Create()
    {
        // Arc's default demo: a labeled field on the United States, the guide showing the expected shape.
        var basic = Field(new PhoneInput { Label = "Phone number" });

        var preset = Field(new PhoneInput { Label = "Mobile", Value = "+447400123456" });

        var restricted = Field(new PhoneInput
        {
            Label = "Phone number",
            Countries = new[] { "GB", "IE", "FR", "DE", "ES", "IT", "NL", "PT" },
            PreferredCountries = new[] { "GB", "IE" },
            Country = "GB"
        });

        var noPreferred = Field(new PhoneInput { Label = "Phone number", PreferredCountries = Array.Empty<string>() });

        var messages = GalleryUi.Column(16,
            Field(new PhoneInput { Label = "Phone number", Description = "We text a sign-in code to this number." }),
            Field(new PhoneInput { Label = "Phone number", Value = "+1201555", ErrorMessage = "Enter the full number to get a code." }));

        var validationOff = Field(new PhoneInput
        {
            Label = "Phone number",
            IsValidationEnabled = false,
            Description = "Validation is off: incomplete numbers show no message after blur."
        });

        var states = GalleryUi.Column(16,
            Field(new PhoneInput { Label = "Disabled", Value = "+14155550132", IsEnabled = false }),
            Field(new PhoneInput { Label = "Phone number", IsLabelVisible = false, Value = "+33612345678" }));

        var events = PhoneInputEvents();

        return GalleryUi.Column(20,
            GalleryUi.Card("Phone input",
                "Formats as you type in the country's shape, with a faint guide for the rest. Type \"+\" to search calling codes; paste \"+44 (0)7911 123456\" to switch countries.",
                basic),
            GalleryUi.Card("Preset value", "An E.164 value picks its country and formats: +447400123456 reads United Kingdom, 7400 123456.", preset),
            GalleryUi.Card("Restricted countries", "Countries limits the picker; PreferredCountries pins a Suggested group above the full list.", restricted),
            GalleryUi.Card("No preferred countries", "An empty PreferredCountries drops the Suggested group: one list sorted by name.", noPreferred),
            GalleryUi.Card("Description and error", "The description sits under the field; an error replaces it and the built-in validation message.", messages),
            GalleryUi.Card("Validation off", "Arc validates after blur by default (\"Numbers in United States have 10 digits\"); IsValidationEnabled turns it off.", validationOff),
            GalleryUi.Card("Disabled and hidden label", "Disabled fades the shell; a hidden label still names the number for assistive technology.", states),
            GalleryUi.Card("Keyboard", "On the country button: ArrowDown / ArrowUp open, a letter opens a search for it. In the list: arrows, PageUp / PageDown, Home / End, Enter picks, Escape returns to the button. Backspace on a separator deletes the digit beside it.",
                Field(new PhoneInput { Label = "Phone number", Country = "DE" })),
            GalleryUi.Card("Value changed", "ValueChanged reports the E.164 value with the formatted number and its status on every edit.", events));
    }

    private static PhoneInput Field(PhoneInput input)
    {
        input.Width = FieldWidth;
        input.HorizontalAlignment = HorizontalAlignment.Left;
        return input;
    }

    private static Control PhoneInputEvents()
    {
        var input = Field(new PhoneInput { Label = "Phone number" });
        var e164 = GalleryUi.Mono("E.164: —");
        var formatted = GalleryUi.Mono("Formatted: —");
        var status = GalleryUi.Mono("Status: Empty");
        input.ValueChanged += (_, e) =>
        {
            e164.Text = $"E.164: {(e.NewValue.Length > 0 ? e.NewValue : "—")}";
            formatted.Text = $"Formatted: {(e.Formatted.Length > 0 ? $"+{e.Country.Dial} {e.Formatted}" : "—")}";
            status.Text = $"Status: {e.Status}{(e.IsValid ? " (valid)" : string.Empty)}";
        };
        var readout = GalleryUi.Column(4, e164, formatted, status);
        return GalleryUi.Column(12, input, readout, GalleryUi.Text("Try +14155550132, or pick Canada with a number typed.", ArcDesignTokenKind.TextMuted, 13));
    }
}
