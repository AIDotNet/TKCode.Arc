using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class InlineEditPage
{
    public static Control Create()
    {
        var log = GalleryUi.Text("Click the title to rename it.", ArcDesignTokenKind.TextSecondary);
        var failNext = new Switch { Content = GalleryUi.Text("Fail the next save") };

        var title = new InlineEdit
        {
            Label = "Project name",
            Value = "Atlas redesign",
            HeadingLevel = 2,
            Validator = v => v.Length == 0 ? "A project needs a name." : null,
            SaveHandler = async next =>
            {
                await Task.Delay(700);
                if (failNext.IsChecked == true)
                {
                    failNext.IsChecked = false;
                    throw new InvalidOperationException("Network unavailable");
                }
            }
        };
        title.TextCommitted += (_, e) => log.Text = $"Committed “{e.NewValue}” (was “{e.OldValue}”)";
        title.Saved += (_, e) => log.Text = $"Saved “{e.NewValue}”";
        title.SaveFailed += (_, e) => log.Text = $"Save failed: {e.Error.Message}";

        var description = new InlineEdit
        {
            Label = "Description",
            Variant = InlineEditVariant.Body,
            IsMultiline = true,
            PlaceholderText = "Add a description",
            Value = "A calmer home for every project: fewer clicks, clearer status and faster search across docs."
        };

        var empty = new InlineEdit { Label = "Subtitle", Variant = InlineEditVariant.Body, PlaceholderText = "Add a subtitle" };

        return GalleryUi.Column(20,
            GalleryUi.Card("Rename in place", "Enter saves (optimistically, then a check draws), Escape cancels, Tab or clicking away saves. Turn on the switch to see a failed save roll back.",
                GalleryUi.Column(16, title, failNext, log)),
            GalleryUi.Card("Multiline body", "Shift+Enter adds a line; the box height follows on a spring.",
                new Border { Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Child = description }),
            GalleryUi.Card("Empty", "A placeholder keeps the line until there is a value.", empty));
    }
}
