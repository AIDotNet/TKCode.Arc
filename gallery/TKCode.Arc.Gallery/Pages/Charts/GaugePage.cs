using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class GaugePage
{
    public static Control Create()
    {
        var disk = new Gauge { Value = 72, Label = "Disk usage", Detail = "360 of 500 GB" };
        disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "Healthy"));
        disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "Filling up"));
        disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "Critical"));
        var random = new Random(3);
        var next = new Button { Content = "New reading", Variant = ButtonVariant.Secondary };
        next.Click += (_, _) =>
        {
            var used = random.Next(40, 500);
            disk.Value = Math.Round(used / 5.0);
            disk.Detail = $"{used} of 500 GB";
        };
        var fill = new Button { Content = "Almost full", Variant = ButtonVariant.Ghost };
        fill.Click += (_, _) =>
        {
            disk.Value = 93;
            disk.Detail = "465 of 500 GB";
        };

        var score = new Gauge { Value = 640, Minimum = 300, Maximum = 850, Label = "Health score", Detail = "640 of 850" };
        var accent = new Gauge { Value = 38, Label = "Build cache" };
        var success = new Gauge { Value = 91, Label = "Uptime", Tone = GaugeTone.Success };
        var warning = new Gauge { Value = 64, Label = "Memory", Tone = GaugeTone.Warning };
        var danger = new Gauge { Value = 97, Label = "Error budget used", Tone = GaugeTone.Danger };

        return GalleryUi.Column(20,
            GalleryUi.Card("Thresholds", "The highest band the value reaches names the state; color and label switch as the count crosses a band.",
                GalleryUi.Column(16, disk, GalleryUi.Row(next, fill))),
            GalleryUi.Card("Custom range", "Minimum and Maximum map any range onto the ring; the number reads as a percentage of it.", score),
            GalleryUi.Card("Tones", "Without thresholds the ring uses Tone: accent, success, warning or danger.",
                GalleryUi.Row(accent, success, warning, danger)));
    }
}
