using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExpandingSearchHeaderSearchDemo : UserControl
{
    public ExpandingSearchHeaderSearchDemo()
    {
        InitializeComponent();
        List<ExpandingSearchItem> items =
        [
            Item("q3", "Quarterly report", "Doc · Edited 2 hours ago", "Docs", ArcIcons.FileText),
            Item("roadmap", "Roadmap 2026", "Doc · Ada Lovelace", "Docs", ArcIcons.FileText),
            Item("onboarding", "Onboarding checklist", "Doc · 14 items", "Docs", ArcIcons.FileText),
            Item("atlas", "Atlas redesign", "Project · 12 open tasks", "Projects", ArcIcons.Folder),
            Item("reports", "Report automation", "Project · 4 open tasks", "Projects", ArcIcons.Folder),
            Item("billing", "Billing migration", "Project · Paused", "Projects", ArcIcons.Folder, "invoices", "payments"),
            Item("revenue", "Revenue dashboard", "Dashboard · Updated daily", "Dashboards", ArcIcons.ChartBar),
            Item("growth", "Growth funnel", "Dashboard · Weekly", "Dashboards", ArcIcons.ChartBar)
        ];
        Header.ItemsSource = items;
        // Shown before anything is typed.
        Header.Suggestions = new[] { items[0], items[3], items[6] };
    }

    private void OnItemSelected(object? sender, ExpandingSearchItemSelectedEventArgs e)
        => Chosen.Text = $"Opened “{e.Item.Title}”";

    private static ExpandingSearchItem Item(string id, string title, string meta, string group, Geometry icon, params string[] keywords)
        => new() { Id = id, Title = title, Meta = meta, Group = group, Icon = icon, Keywords = keywords };
}
