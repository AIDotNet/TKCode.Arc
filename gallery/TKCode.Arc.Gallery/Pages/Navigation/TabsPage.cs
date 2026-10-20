using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc.Gallery.Pages;

public static class TabsPage
{
    public static Control Create()
    {
        var basic = new Tabs();
        basic.Items.Add(new TabItem
        {
            Header = "Overview",
            Content = GalleryUi.Text("Revenue is up 12% this quarter. Three new regions went live and churn held steady.", ArcDesignTokenKind.TextSecondary)
        });
        basic.Items.Add(new TabItem
        {
            Header = "Activity",
            Content = GalleryUi.Column(8,
                GalleryUi.Text("Maya merged “Billing v2” · 2h ago", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Leo opened 3 issues · 5h ago", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Ana deployed to production · yesterday", ArcDesignTokenKind.TextSecondary),
                GalleryUi.Text("Kai rotated the API keys · 2 days ago", ArcDesignTokenKind.TextSecondary))
        });
        basic.Items.Add(new TabItem
        {
            Header = "Settings",
            Content = GalleryUi.Column(10,
                GalleryUi.Text("Workspace name, members and billing live here.", ArcDesignTokenKind.TextSecondary),
                new Switch { Content = "Email digests", IsChecked = true })
        });
        basic.Items.Add(new TabItem { Header = "Archived", IsEnabled = false, Content = GalleryUi.Text("Nothing archived.") });

        var status = GalleryUi.Text("Selected: Inbox", ArcDesignTokenKind.TextMuted, 12);
        var bound = new Tabs
        {
            ItemsSource = new[] { "Inbox", "Drafts", "Sent", "Scheduled", "Spam", "Trash", "Archive", "Templates", "Snoozed", "Important" },
            ContentTemplate = new FuncDataTemplate<string>((name, _) => GalleryUi.Text($"{name}: nothing new since this morning.", ArcDesignTokenKind.TextSecondary)),
            MaxWidth = 460,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        bound.SelectionChanged += (_, _) => status.Text = $"Selected: {bound.SelectedItem}";

        var manual = new Tabs { ActivationMode = TabsActivationMode.Manual };
        foreach (var name in new[] { "Day", "Week", "Month", "Year" })
        {
            manual.Items.Add(new TabItem { Header = name, Content = GalleryUi.Text($"{name} view", ArcDesignTokenKind.TextSecondary) });
        }

        return GalleryUi.Column(20,
            GalleryUi.Card("Tabs", "The pill glides between triggers; the new panel slides in from the side of travel and the area springs to its height. Arrow keys move and select, Home/End jump, Tab moves into the panel. Archived is disabled.", basic),
            GalleryUi.Card("Overflow and ItemsSource", "Ten tabs in a 460px frame: the list scrolls with soft edges and edge buttons, and the selected tab is always scrolled into view.",
                GalleryUi.Column(12, bound, status)),
            GalleryUi.Card("Manual activation", "Arrow keys only move focus; Enter or Space selects.", manual));
    }
}
