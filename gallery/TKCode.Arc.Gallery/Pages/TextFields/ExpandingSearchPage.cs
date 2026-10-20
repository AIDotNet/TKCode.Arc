using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class ExpandingSearchPage
{
    public static Control Create()
    {
        var items = new List<ExpandingSearchItem>
        {
            Item("q3", "Quarterly report", "Doc · Edited 2 hours ago", "Docs", ArcIcons.FileText),
            Item("roadmap", "Roadmap 2026", "Doc · Ada Lovelace", "Docs", ArcIcons.FileText),
            Item("onboarding", "Onboarding checklist", "Doc · 14 items", "Docs", ArcIcons.FileText),
            Item("atlas", "Atlas redesign", "Project · 12 open tasks", "Projects", ArcIcons.Folder),
            Item("reports", "Report automation", "Project · 4 open tasks", "Projects", ArcIcons.Folder),
            Item("billing", "Billing migration", "Project · Paused", "Projects", ArcIcons.Folder, "invoices", "payments"),
            Item("revenue", "Revenue dashboard", "Dashboard · Updated daily", "Dashboards", ArcIcons.ChartBar),
            Item("growth", "Growth funnel", "Dashboard · Weekly", "Dashboards", ArcIcons.ChartBar)
        };
        var recent = new List<ExpandingSearchItem> { items[0], items[3], items[6] };

        var chosen = GalleryUi.Text("Nothing chosen yet.", ArcDesignTokenKind.TextSecondary);
        var header = new ExpandingSearch
        {
            Label = "Search projects and docs",
            ItemsSource = items,
            Suggestions = recent
        };
        header.ItemSelected += (_, e) => chosen.Text = $"Opened “{e.Item.Title}”";

        var bar = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Child = new DockPanel
            {
                Children =
                {
                    Dock(GalleryUi.Text("Workspace", ArcDesignTokenKind.Foreground, 16, Avalonia.Media.FontWeight.Medium), Avalonia.Controls.Dock.Left),
                    header
                }
            }
        };
        ArcTokens.Bind(bar, Border.BorderBrushProperty, ArcDesignTokenKind.Border);

        var start = new ExpandingSearch
        {
            Label = "Find a dashboard",
            Anchor = ExpandingSearchAnchor.Start,
            ExpandedWidth = 300,
            ItemsSource = items.Where(i => i.Group == "Dashboards").ToList()
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Header search", "Click the glass: it morphs into a field and shows recent items. Type to filter; ArrowUp / ArrowDown move the highlight, Enter opens, Escape folds it back.",
                GalleryUi.Column(16, bar, chosen)),
            GalleryUi.Card("Anchored at the start", "The field grows away from the edge the button sits on.", new Border { Width = 420, HorizontalAlignment = HorizontalAlignment.Left, Child = start }));
    }

    private static ExpandingSearchItem Item(string id, string title, string meta, string group, Avalonia.Media.Geometry icon, params string[] keywords)
        => new() { Id = id, Title = title, Meta = meta, Group = group, Icon = icon, Keywords = keywords };

    private static Control Dock(Control control, Avalonia.Controls.Dock dock)
    {
        DockPanel.SetDock(control, dock);
        control.VerticalAlignment = VerticalAlignment.Center;
        control.Margin = new Thickness(4, 0, 16, 0);
        return control;
    }
}
