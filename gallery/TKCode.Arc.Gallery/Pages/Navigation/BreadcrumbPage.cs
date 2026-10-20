using System.Collections.ObjectModel;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class BreadcrumbPage
{
    public static Control Create()
    {
        var status = GalleryUi.Text("Choose a crumb", ArcDesignTokenKind.TextMuted, 12);
        var basic = new Breadcrumb();
        basic.Items.Add(new BreadcrumbItem { Content = "Workspace" });
        basic.Items.Add(new BreadcrumbItem { Content = "Settings" });
        basic.Items.Add(new BreadcrumbItem { Content = "Billing" });
        basic.ItemClick += (_, e) => status.Text = $"Navigate to {((BreadcrumbItem)e.Item!).Content} (index {e.Index})";

        // A file browser: the path lives in local state, crumbs truncate it and folders extend it.
        var path = new ObservableCollection<string> { "Home", "Projects" };
        var files = new Breadcrumb { ItemsSource = path };
        files.ItemClick += (_, e) =>
        {
            while (path.Count > e.Index + 1)
            {
                path.RemoveAt(path.Count - 1);
            }
        };
        var folders = new[] { "Arc", "src", "components", "breadcrumb" };
        var open = new Button { Content = "Open next folder", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        open.Click += (_, _) =>
        {
            var next = folders[(path.Count - 2) % folders.Length];
            path.Add(next);
        };
        var up = new Button { Content = "Up one level", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        up.Click += (_, _) =>
        {
            if (path.Count > 1)
            {
                path.RemoveAt(path.Count - 1);
            }
        };

        var longPath = new Breadcrumb
        {
            MaxVisibleItems = 4,
            ItemsSource = new[] { "Acme", "Engineering", "Platform", "Services", "Billing", "Invoices", "2026" }
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Breadcrumb", "Chevrons separate the crumbs; the last one is the current page. Every crumb reserves its medium-weight width, so the path never shifts.",
                GalleryUi.Column(12, basic, status)),
            GalleryUi.Card("Client-side path", "Opening a folder slides a new crumb in from the path before it; choosing a crumb truncates the path and the removed crumbs slide back as they fade.",
                GalleryUi.Column(12, files, GalleryUi.Row(open, up))),
            GalleryUi.Card("Collapsed path", "MaxVisibleItems folds the middle of a long path into an overflow menu (an addition to Arc, which never collapses).", longPath));
    }
}
