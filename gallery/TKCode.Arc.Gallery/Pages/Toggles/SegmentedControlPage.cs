using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class SegmentedControlPage
{
    public static Control Create()
    {
        var range = new SegmentedControl
        {
            ItemsSource = new[] { new SegmentedOption("day", "Day"), new SegmentedOption("week", "Week"), new SegmentedOption("month", "Month") },
            SelectedValue = "week"
        };
        AutomationProperties.SetName(range, "Range");

        var count = GalleryUi.Chip(GalleryUi.Text("3", ArcDesignTokenKind.TextSecondary, 11), ArcDesignTokenKind.Surface, new Thickness(6, 0));
        var inbox = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption("all", "All"),
                new SegmentedOption("unread", "Unread", count),
                new SegmentedOption("archived", "Archived")
            },
            SelectedValue = "all"
        };
        var intent = GalleryUi.Text("Intent: none", ArcDesignTokenKind.TextMuted, 12);
        intent.HorizontalAlignment = HorizontalAlignment.Center;
        inbox.ItemIntent += (_, e) => intent.Text = $"Intent: {e.Value}";

        var many = new SegmentedControl
        {
            MaxWidth = 300,
            ItemsSource = new[] { "Overview", "Activity", "Deployments", "Analytics", "Logs", "Settings" },
            SelectedIndex = 0
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("View switcher", "One pill glides between segments on the morph spring; labels only change color. Arrows wrap; Home and End jump.",
                GalleryUi.Row(range)),
            GalleryUi.Card("Accessory and intent", "Content after the label, such as a count. ItemIntent fires when the pointer or focus reaches a segment, before it is chosen.",
                GalleryUi.Column(10, GalleryUi.Row(inbox), intent)),
            GalleryUi.Card("Overflow", "When the options are wider than the control the row scrolls inside itself, with faded edges on the side that has more.",
                GalleryUi.Row(many)));
    }
}
