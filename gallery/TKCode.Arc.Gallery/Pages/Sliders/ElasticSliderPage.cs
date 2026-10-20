using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ElasticSliderPage
{
    public static Control Create()
    {
        var volume = new ElasticSlider
        {
            Label = "Volume",
            Value = 40,
            Width = 360,
            StartIcon = GalleryUi.Glyph(ArcIcons.Minus),
            EndIcon = GalleryUi.Glyph(ArcIcons.Volume2),
            ValueFormatter = v => $"{v:0}%"
        };

        var brightness = new ElasticSlider
        {
            Label = "Brightness",
            Value = 50,
            Width = 360,
            StartIcon = GalleryUi.Glyph(ArcIcons.Moon),
            EndIcon = GalleryUi.Glyph(ArcIcons.Sparkles)
        };
        brightness.Marks.Add(new SliderMark(0, "Off"));
        brightness.Marks.Add(new SliderMark(25, "25"));
        brightness.Marks.Add(new SliderMark(50, "50"));
        brightness.Marks.Add(new SliderMark(75, "75"));
        brightness.Marks.Add(new SliderMark(100, "Max"));

        var zoom = new ElasticSlider
        {
            Label = "Zoom",
            Minimum = 0.5,
            Maximum = 3,
            SmallChange = 0.1,
            Value = 1,
            Width = 360,
            StartIcon = GalleryUi.Glyph(ArcIcons.ZoomOut),
            EndIcon = GalleryUi.Glyph(ArcIcons.ZoomIn),
            ValueFormatter = v => $"{v:0.0}×"
        };
        zoom.Marks.Add(1.0);
        zoom.Marks.Add(2.0);

        return GalleryUi.Column(20,
            GalleryUi.Card("Rubber band", "Pull past either end: the track stretches like rubber (up to 26px), pushes the icon outward and springs back with your release. The thumb swells to 1.28× while held.",
                GalleryUi.Row(volume)),
            GalleryUi.Card("Detents", "Marks are detents the thumb clicks into within 7px, and their ticks pop. The value tag sways with the thumb's speed.",
                GalleryUi.Row(brightness)),
            GalleryUi.Card("Fine steps", "Home and End at a limit hit the wall and bounce.", GalleryUi.Row(zoom)),
            GalleryUi.Card("Disabled", "A disabled slider leaves the tab order.",
                GalleryUi.Row(new ElasticSlider { Label = "Contrast", Value = 30, IsEnabled = false, Width = 360 })));
    }
}
