using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class SliderPage
{
    public static Control Create()
    {
        var volume = new Slider
        {
            Label = "Volume",
            Value = 64,
            Width = 360,
            StartContent = GalleryUi.Glyph(ArcIcons.Minus),
            EndContent = GalleryUi.Glyph(ArcIcons.Plus)
        };

        var committed = GalleryUi.Text("Committed: $200 – $800", ArcDesignTokenKind.TextMuted, 12);
        committed.HorizontalAlignment = HorizontalAlignment.Center;
        var price = new Slider
        {
            Label = "Price",
            IsRange = true,
            Minimum = 0,
            Maximum = 1000,
            SmallChange = 10,
            LowerValue = 200,
            UpperValue = 800,
            MinStepsBetweenThumbs = 5,
            LowerThumbName = "Minimum price",
            UpperThumbName = "Maximum price",
            ValueFormatter = v => $"${v:N0}",
            Width = 360
        };
        price.ValueCommitted += (_, e) => committed.Text = $"Committed: ${e.LowerValue:N0} – ${e.UpperValue:N0}";

        var speed = new Slider { Label = "Playback speed", Minimum = 0.5, Maximum = 2, SmallChange = 0.25, Value = 1, ValueFormatter = v => $"{v:0.##}×", Width = 360 };
        foreach (var mark in new[] { 0.5, 1, 1.5, 2 })
        {
            speed.Marks.Add(new SliderMark(mark, $"{mark:0.#}×"));
        }
        speed.Marks.Add(0.75);
        speed.Marks.Add(1.25);
        speed.Marks.Add(1.75);

        return GalleryUi.Column(20,
            GalleryUi.Card("Single value", "Drag 1:1 and release with momentum; press the track and the thumb springs there. The bubble rolls its digits while dragging or keyboard focused.",
                GalleryUi.Row(volume)),
            GalleryUi.Card("Range", "Two thumbs kept 5 steps apart, formatted as prices. ValueCommitted fires once per release or key.",
                GalleryUi.Column(16, GalleryUi.Row(price), committed)),
            GalleryUi.Card("Steps and marks", "Ticks inside the fill take the on color and labeled marks are clickable. At a limit a key makes the thumb strain toward the press.",
                GalleryUi.Row(speed)),
            GalleryUi.Card("Disabled", "A disabled slider leaves the tab order.",
                GalleryUi.Row(new Slider { Label = "Opacity", Value = 40, IsEnabled = false, Width = 360, ValueFormatter = v => $"{v:0}%" })));
    }
}
