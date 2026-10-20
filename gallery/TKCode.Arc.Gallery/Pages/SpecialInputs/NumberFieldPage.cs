using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class NumberFieldPage
{
    public static Control Create()
    {
        // Arc's default demo: a bounded count whose unit follows the value.
        var seats = new NumberField
        {
            Label = "Seats",
            Minimum = 1,
            Maximum = 10,
            Value = 3,
            Description = "Up to 10 per team.",
            SuffixFormatter = n => n == 1 ? " seat" : " seats"
        };

        var price = new NumberField { Label = "Price", Prefix = "$", Step = 0.5m, Maximum = 999, Value = 24.5m };
        var cents = new NumberField { Label = "Unit cost", Prefix = "$", Step = 0.01m, Maximum = 100, Value = 4.99m };

        var sizes = GalleryUi.Row(
            new NumberField { Label = "Small", Size = ControlSize.Small, Value = 2, Maximum = 99 },
            new NumberField { Label = "Medium", Value = 12, Maximum = 99 },
            new NumberField { Label = "Large", Size = ControlSize.Large, Value = 48, Maximum = 99 });

        var scrub = GalleryUi.Row(
            new NumberField { Label = "Width", IsScrubEnabled = true, Suffix = " px", Maximum = 640, Value = 320, Step = 4 },
            new NumberField { Label = "Opacity", IsScrubEnabled = true, Suffix = "%", Maximum = 100, Value = 80, Step = 5 });

        var states = GalleryUi.Row(
            new NumberField { Label = "Disabled", Value = 4, Maximum = 10, IsEnabled = false, Suffix = " seats" },
            new NumberField { Label = "Quantity", Value = 0, Maximum = 5, ErrorMessage = "Add at least one item." });

        var hint = new NumberField
        {
            Label = "Guests",
            Minimum = 1,
            Maximum = 8,
            Value = 8,
            Description = "Press + to meet the custom note.",
            LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum ? $"Room holds {limit}" : $"At least {limit}"
        };

        var temperature = new NumberField
        {
            Label = "Temperature",
            Minimum = -20,
            Maximum = 40,
            Value = 21,
            Suffix = "°C",
            Description = "Type a minus sign for values below zero."
        };

        // Two-way binding: the slider-free readout and a second field share one value.
        var bound = new NumberField { Label = "Bound", Maximum = 50, Value = 7 };
        var mirror = new NumberField { Label = "Mirror", Maximum = 50 };
        // Property-to-property two-way binding (no reflection, AOT safe).
        mirror[!!NumberField.ValueProperty] = bound[!!NumberField.ValueProperty];
        var readout = GalleryUi.Mono("Value 7");
        bound.ValueChanged += (_, e) => readout.Text = $"Value {e.NewValue} (was {e.OldValue}, {e.ChangeSource})";
        var binding = GalleryUi.Column(16, GalleryUi.Row(bound, mirror), Centered(readout));

        var keyboard = GalleryUi.Column(12,
            GalleryUi.Row(new NumberField { Label = "Items", Maximum = 1000, Value = 120, LargeStep = 50 }),
            Centered(GalleryUi.Text("ArrowUp / ArrowDown step (hold to repeat), Shift or PageUp / PageDown take the large step, " +
                                    "Home / End jump to the limits, Enter commits a draft or selects the number, Escape restores it. " +
                                    "The wheel steps while the field is focused.", ArcDesignTokenKind.TextSecondary, 13)));

        return GalleryUi.Column(20,
            GalleryUi.Card("Default", "A bounded count: the digits roll, a held stepper repeats and speeds up, and a press at a limit strains and names it.", GalleryUi.Row(seats)),
            GalleryUi.Card("Price", "A prefix travels with the number; the step's precision sets the fraction digits.", GalleryUi.Row(price, cents)),
            GalleryUi.Card("Sizes", "Small, Medium and Large: 164 / 196 / 228px wide on ControlSizeSM / MD / LG.", sizes),
            GalleryUi.Card("Scrub label", "Drag the label sideways: one step every 6px; past a limit the value follows with resistance.", scrub),
            GalleryUi.Card("States", "Disabled, and an error message from the field foundation.", states),
            GalleryUi.Card("Custom limit hint", "LimitHintFormatter writes the note beside the label.", GalleryUi.Row(hint)),
            GalleryUi.Card("Negative range", "A minimum below zero accepts the minus sign.", GalleryUi.Row(temperature)),
            GalleryUi.Card("Binding and events", "Value is two-way; ValueChanged reports the old and new value and what changed it.", binding),
            GalleryUi.Card("Keyboard", null, keyboard));
    }

    private static Control Centered(Control child)
    {
        child.HorizontalAlignment = HorizontalAlignment.Center;
        return child;
    }
}
