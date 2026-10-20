using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class MorphSelectPage
{
    public static Control Create()
    {
        var zone = new MorphSelect
        {
            Label = "Time zone",
            ItemsSource = SelectsDemoData.Cities.Select(c => new SelectOption(c.City, c.City)
            {
                Meta = SelectsDemoData.Utc(c.Offset),
                Group = c.Region,
                Keywords = c.Zone,
                Icon = ArcIcons.Globe
            }).ToArray(),
            SelectedValue = "London"
        };
        var chosen = GalleryUi.Text("Value: London", ArcDesignTokenKind.TextSecondary, 13);
        zone.SelectionChanged += (_, _) => chosen.Text = $"Value: {zone.SelectedValue ?? "none"}";

        var status = new MorphSelect
        {
            Label = "Status",
            IsLabelVisible = false,
            Items =
            {
                new SelectItem { Content = "Backlog", Icon = ArcIcons.Clock },
                new SelectItem { Content = "In progress", Icon = ArcIcons.Activity },
                new SelectItem { Content = "In review", Icon = ArcIcons.Eye },
                new SelectItem { Content = "Done", Icon = ArcIcons.Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M9 12l2 2 4-4") },
                new SelectItem { Content = "Canceled", Icon = ArcIcons.CircleX, IsEnabled = false }
            },
            SelectedIndex = 1
        };

        var priority = new MorphSelect
        {
            Label = "Priority",
            PanelAlignment = MorphSelectAlignment.End,
            PanelWidth = 220,
            ItemsSource = new[] { "Urgent", "High", "Medium", "Low", "No priority" }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Grouped with search", "The trigger grows into the list; a highlight glides between options and the chosen label flies back into the trigger. Over eight options a search field takes the trigger's place.",
                GalleryUi.Column(12, zone, chosen)),
            GalleryUi.Card("Compact toolbar", "A hidden label keeps the pill compact. Type a letter to jump; Alt+Up picks and closes.",
                GalleryUi.Row(status, priority)));
    }
}
