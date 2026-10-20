using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ChipGroupPage
{
    private static readonly string[] Topics =
    {
        "Design", "Engineering", "Research", "Marketing", "Sales", "Support", "Finance", "Legal", "Operations", "People"
    };

    public static Control Create()
    {
        var topics = new ChipGroup { Label = "Topics", ItemsSource = Topics, MaxWidth = 560, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        topics.Selection.Select(1);
        var chosen = GalleryUi.Text("Values: Engineering", ArcDesignTokenKind.TextSecondary, 13);
        topics.SelectionChanged += (_, _) =>
            chosen.Text = topics.SelectedValues.Count == 0 ? "Values: none" : $"Values: {string.Join(", ", topics.SelectedValues)}";

        var folded = new ChipGroup { Label = "Topics", ItemsSource = Topics, MaxVisibleItems = 5, MaxWidth = 560, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        folded.Selection.Select(7);

        var single = new ChipGroup
        {
            Label = "Status",
            IsMultiple = false,
            Items =
            {
                new SelectItem { Content = "Open", Value = "open" },
                new SelectItem { Content = "In progress", Value = "progress" },
                new SelectItem { Content = "Closed", Value = "closed" }
            }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Filter chips", "Picking a chip grows a check and slides the label while its neighbours glide, even across lines. Arrows move between chips; Space or Enter toggles.",
                GalleryUi.Column(12, topics, chosen)),
            GalleryUi.Card("Folded", "Past five chips the rest fold behind +N more; a selected chip always stays in view.", folded),
            GalleryUi.Card("Single choice", "IsMultiple false: one chip at a time, and the chosen chip can still be cleared.", single));
    }
}
