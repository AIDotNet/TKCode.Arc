using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class ComboboxPage
{
    public static Control Create()
    {
        var zone = new Combobox
        {
            Label = "Time zone",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Description = "Search by city, zone name or offset.",
            Width = 320,
            ItemsSource = SelectsDemoData.Cities.Select(c => new SelectOption(c.City, c.City)
            {
                Keywords = $"{c.Zone} {SelectsDemoData.Utc(c.Offset)}"
            }).ToArray()
        };
        var chosen = GalleryUi.Text("Value: none", ArcDesignTokenKind.TextSecondary, 13);
        zone.SelectionChanged += (_, _) => chosen.Text = $"Value: {zone.SelectedValue ?? "none"}";

        var framework = new Combobox
        {
            Label = "Framework",
            Width = 280,
            Items =
            {
                new SelectItem { Content = "Avalonia", Keywords = "xaml dotnet" },
                new SelectItem { Content = "AtomUI", Keywords = "ant design" },
                new SelectItem { Content = "MAUI", Keywords = "xamarin", IsEnabled = false },
                new SelectItem { Content = "WinUI" },
                new SelectItem { Content = "Uno Platform" }
            },
            SelectedIndex = 0
        };

        var prefix = new Combobox
        {
            Label = "Starts with",
            Description = "A custom FilterPredicate matches the start of the label only.",
            Width = 280,
            ItemsSource = SelectsDemoData.Cities.Select(c => c.City).ToArray(),
            FilterPredicate = (item, query) => item is string s && s.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)
        };

        var disabled = new Combobox { Label = "Country", Width = 280, IsEnabled = false, ItemsSource = new[] { "Sweden" }, SelectedIndex = 0 };

        return GalleryUi.Column(20,
            GalleryUi.Card("Searchable", "Type to filter; the list springs to its new height. Arrows move the highlight while focus stays in the field, Enter picks, Escape drops the query.",
                GalleryUi.Column(12, zone, chosen)),
            GalleryUi.Card("Inline options", "Disabled options show but cannot be picked; the clear button pops in while something is selected.", GalleryUi.Row(framework)),
            GalleryUi.Card("Custom filter", null, GalleryUi.Row(prefix)),
            GalleryUi.Card("Disabled", null, GalleryUi.Row(disabled)));
    }
}
