using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ProgressPage
{
    public static Control Create()
    {
        var upload = new Progress { Label = "Uploading 4 files", IsValueVisible = true, Value = 35 };
        var random = new Random(3);
        var advance = new Button { Content = "Advance", Variant = ButtonVariant.Secondary };
        advance.Click += (_, _) => upload.Value = Math.Min(100, upload.Value + random.Next(8, 30));
        var reset = new Button { Content = "Reset", Variant = ButtonVariant.Ghost };
        reset.Click += (_, _) => upload.Value = 0;
        var complete = new Button { Content = "Complete", Variant = ButtonVariant.Secondary };
        complete.Click += (_, _) => upload.Value = 100;

        var import = new Progress { Label = "Importing contacts", IsValueVisible = true, Maximum = 2_400, Value = 1_392 };
        var plain = new Progress { Value = 62 };
        var label = new Progress { Label = "Preparing export", Value = 20 };
        var swap = new Button { Content = "Next phase", Variant = ButtonVariant.Secondary };
        var phases = new[] { "Preparing export", "Compressing archive", "Uploading archive" };
        var phase = 0;
        swap.Click += (_, _) =>
        {
            phase = (phase + 1) % phases.Length;
            label.Label = phases[phase];
            label.Value = 20 + phase * 35;
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Counted value", "One spring drives the fill and the percentage, so the number always matches the bar; at 100% the fill turns to the success color and a check settles in.",
                GalleryUi.Column(16, upload, GalleryUi.Row(advance, complete, reset))),
            GalleryUi.Card("Custom maximum", "Value 1,392 of 2,400 items reads as 58%.", import),
            GalleryUi.Card("Bar only", "Without a label or value the bar stands alone; its accessible name is \"Progress\".", plain),
            GalleryUi.Card("Changing label", "A new label rises in from a soft blur while the old one lifts away.", GalleryUi.Column(16, label, GalleryUi.Row(swap))));
    }
}
