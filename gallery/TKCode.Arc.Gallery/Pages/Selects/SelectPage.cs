using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class SelectPage
{
    public static Control Create()
    {
        var region = new Select
        {
            Label = "Region",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Description = "Where your workspace data is stored.",
            Width = 280,
            Items =
            {
                new SelectItem { Content = "North America", Value = "na" },
                new SelectItem { Content = "Europe", Value = "eu" },
                new SelectItem { Content = "Asia Pacific", Value = "apac" },
                new SelectItem { Content = "South America (soon)", Value = "sa", IsEnabled = false },
                new SelectItem { Content = "Africa", Value = "af" }
            }
        };
        var chosen = GalleryUi.Text("Value: none", ArcDesignTokenKind.TextSecondary, 13);
        region.SelectionChanged += (_, _) => chosen.Text = $"Value: {region.SelectedValue ?? "none"}";

        var sort = new Select
        {
            Label = "Sort by",
            Width = 220,
            ItemsSource = new[]
            {
                new SelectOption("newest", "Newest first"), new SelectOption("oldest", "Oldest first"),
                new SelectOption("updated", "Recently updated"), new SelectOption("az", "Name, A to Z"),
                new SelectOption("za", "Name, Z to A")
            },
            SelectedValue = "updated"
        };

        var zones = new Select
        {
            Label = "Time zone",
            Placeholder = "Choose a time zone",
            Width = 300,
            ItemsSource = SelectsDemoData.Cities,
            TextSelector = item => item is ValueTuple<string, string, int, string> c ? $"{c.Item1} ({SelectsDemoData.Utc(c.Item3)})" : null,
            ValueSelector = item => item is ValueTuple<string, string, int, string> c ? c.Item1 : null
        };

        var many = new Select
        {
            Label = "Build number",
            Description = "5,000 options: only the visible rows are drawn.",
            Width = 260,
            ItemsSource = Enumerable.Range(1, 5000).Select(i => $"Build {i:0000}").ToArray(),
            SelectedIndex = 2499
        };

        var disabled = new Select { Label = "Plan", Width = 220, IsEnabled = false, ItemsSource = new[] { "Free", "Pro", "Team" }, SelectedIndex = 1 };

        return GalleryUi.Column(20,
            GalleryUi.Card("Field", "A compact choice field: the value rolls toward its place in the list. Type a letter on the closed field to jump straight to an option.",
                GalleryUi.Column(12, region, chosen)),
            GalleryUi.Card("Preselected and selectors", "SelectOption records, or any items with TextSelector / ValueSelector.", GalleryUi.Row(sort, zones)),
            GalleryUi.Card("Long lists", "Arrow keys, Home / End and PageUp / PageDown move the gliding highlight; hover the edge arrows to scroll.", GalleryUi.Row(many)),
            GalleryUi.Card("Disabled", null, GalleryUi.Row(disabled)));
    }
}
