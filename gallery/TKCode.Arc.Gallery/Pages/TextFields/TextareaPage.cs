using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class TextareaPage
{
    public static Control Create()
    {
        const int limit = 160;
        var bio = new Textarea
        {
            Label = "Bio",
            Placeholder = "A sentence or two about you",
            Description = $"{limit} characters left",
            MaxLength = limit,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        // The count rolls only the digits that change.
        bio.TextChanged += (_, _) => bio.Description = $"{limit - (bio.Text?.Length ?? 0)} characters left";

        var feedback = new Textarea
        {
            Label = "Feedback",
            Text = "Loving the new dashboard, but",
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        feedback.TextChanged += (_, _) => feedback.ErrorMessage = (feedback.Text?.Trim().Length ?? 0) < 40
            ? "Tell us a little more: at least 40 characters."
            : null;
        feedback.ErrorMessage = "Tell us a little more: at least 40 characters.";

        var fixedHeight = new Textarea
        {
            Label = "Notes",
            IsResizable = false,
            Text = "Not resizable, disabled.",
            IsEnabled = false,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Live count", "Enter adds a line. The count in the description rolls its digits; drag the corner grip to make the field taller.", bio),
            GalleryUi.Card("Validation", "The focus ring contracts onto the border; an error turns it danger and opens its row on a spring.", feedback),
            GalleryUi.Card("Disabled", "No grip, faded to 0.6.", fixedHeight));
    }
}
