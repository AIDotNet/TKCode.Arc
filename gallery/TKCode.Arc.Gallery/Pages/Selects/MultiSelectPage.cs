using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class MultiSelectPage
{
    private static SelectOption[] Labels() => new[]
    {
        new SelectOption("bug", "Bug"), new SelectOption("feature", "Feature"), new SelectOption("docs", "Documentation"),
        new SelectOption("perf", "Performance"), new SelectOption("design", "Design"), new SelectOption("a11y", "Accessibility"),
        new SelectOption("infra", "Infrastructure", isDisabled: true)
    };

    public static Control Create()
    {
        var labels = new MultiSelect { Label = "Labels", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Description = "Shown on the issue card.", Width = 320, ItemsSource = Labels() };
        var chosen = GalleryUi.Text("Values: none", ArcDesignTokenKind.TextSecondary, 13);
        labels.SelectionChanged += (_, _) =>
            chosen.Text = labels.SelectedValues.Count == 0 ? "Values: none" : $"Values: {string.Join(", ", labels.SelectedValues)}";

        var owners = new MultiSelect { Label = "Owners", MaxVisibleChips = 1, Width = 280, ItemsSource = new[] { "Ada", "Grace", "Linus", "Margaret", "Ken" } };
        owners.Selection.Select(0);
        owners.Selection.Select(1);
        owners.Selection.Select(3);

        var searchable = new MultiSelect
        {
            Label = "Cities",
            Placeholder = "Pick cities",
            IsSearchable = true,
            Width = 320,
            ItemsSource = SelectsDemoData.Cities.Select(c => c.City).ToArray()
        };

        var disabled = new MultiSelect { Label = "Reviewers", Width = 280, IsEnabled = false, ItemsSource = new[] { "Ada", "Grace" } };
        disabled.Selection.Select(0);

        return GalleryUi.Column(20,
            GalleryUi.Card("Chips in the field", "Picked values appear as chips whose slots spring open; past two they fold into a rolling +N. Each check draws itself.",
                GalleryUi.Column(12, labels, chosen)),
            GalleryUi.Card("One visible chip", null, GalleryUi.Row(owners)),
            GalleryUi.Card("Searchable", "IsSearchable adds a search field to the menu (Arc's menu has none).", GalleryUi.Row(searchable)),
            GalleryUi.Card("Disabled", "Disabled hides the clear button.", GalleryUi.Row(disabled)));
    }
}
