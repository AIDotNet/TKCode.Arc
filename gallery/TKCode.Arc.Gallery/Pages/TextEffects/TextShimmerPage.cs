using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class TextShimmerPage
{
    public static Control Create()
    {
        var thinking = new TextShimmer { Text = "Thinking" };
        var summary = new TextShimmer { Text = "Generating summary", FontSize = 22 };
        var steps = new[] { "Reading 14 files", "Drafting a plan", "Writing the summary" };
        var index = 0;
        var next = new Button { Content = "Next step", Variant = ButtonVariant.Secondary };
        next.Click += (_, _) =>
        {
            summary.IsActive = true;
            summary.Text = steps[index = (index + 1) % steps.Length];
        };
        var done = new Button { Content = "Finish", Variant = ButtonVariant.Secondary };
        done.Click += (_, _) =>
        {
            summary.Text = "Summary ready";
            summary.IsActive = false;
        };
        var slow = new TextShimmer { Text = "Indexing the workspace", SweepDuration = TimeSpan.FromSeconds(3) };

        return GalleryUi.Column(20,
            GalleryUi.Card("Status line", "A calm band of the text color sweeps across a muted base, then rests briefly before the next pass.", thinking),
            GalleryUi.Card("Changing and finishing", "A new label rises in place while the line springs to its width; finishing lets the band glide off and the text settle to solid.",
                GalleryUi.Column(16, summary, GalleryUi.Row(next, done))),
            GalleryUi.Card("Slower sweep", "SweepDuration sets one pass (default 1.8s).", slow));
    }
}
