using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ColorPickerPage
{
    public static Control Create()
    {
        var accent = new ColorPicker { Label = "Accent" };
        var value = GalleryUi.Text("onValueChange: #2F6BFF", ArcDesignTokenKind.TextSecondary, 13);
        accent.ColorChanged += (_, e) => value.Text = $"onValueChange: {e.Hex}";

        var brand = new ColorPicker
        {
            Label = "Brand",
            Color = Color.Parse("#E5484D"),
            Format = ColorPickerFormat.Oklch,
            Swatches =
            [
                ColorSwatch.Create(Color.Parse("#E5484D")),
                ColorSwatch.Create(Color.Parse("#F76B15")),
                ColorSwatch.Create(Color.Parse("#30A46C")),
                ColorSwatch.Create(Color.Parse("#0090FF")),
                ColorSwatch.Create(Color.Parse("#8E4EC6"))
            ]
        };

        var onDark = new ColorPicker
        {
            Label = "Text on dark",
            Color = Color.Parse("#A1A1AA"),
            ContrastBackground = Color.Parse("#111113"),
            Description = "The contrast readout compares against #111113."
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Color field",
                "Click the swatch: it grows into the picker. Drag the area and sliders (they stretch at the edges), use the arrow keys (Shift for tens), or type hex, RGB, HSL or OKLCH and press Enter. Escape closes.",
                GalleryUi.Column(12, accent, value)),
            GalleryUi.Card("Formats and saved swatches",
                "The format button morphs the value between formats. Save the current color with +, drag or Alt + arrow keys to reorder, Delete to remove.",
                brand),
            GalleryUi.Card("Contrast against a background",
                "ContrastBackground sets what the WCAG readout compares with; the ratio counts to the new value and the grade rolls in.",
                onDark));
    }
}
