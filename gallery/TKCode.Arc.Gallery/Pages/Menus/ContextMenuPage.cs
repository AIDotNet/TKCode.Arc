using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ContextMenuPage
{
    private static Border Tile(string name, string meta, Geometry icon)
    {
        var glyph = new ArcGlyph { Data = icon, Size = 22, HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var tile = new Border
        {
            Width = 220,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    glyph,
                    GalleryUi.Text(name, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium),
                    GalleryUi.Text(meta, ArcDesignTokenKind.TextMuted, 12)
                }
            }
        };
        ArcTokens.Bind(tile, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(tile, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return tile;
    }

    public static Control Create()
    {
        var status = GalleryUi.Text("Right-click a file, or focus it and press Shift+F10.", ArcDesignTokenKind.TextSecondary);

        // Arc's usage example: Copy link and Delete file on a file tile.
        var file = new ContextMenu { Label = "Actions for Q3 report.pdf", Content = Tile("Q3 report.pdf", "PDF · 2.4 MB", ArcIcons.FileText) };
        file.Items.Add(new MenuEntry { Header = "Copy link", Icon = ArcIcons.Copy });
        file.Items.Add(new MenuEntry { Header = "Delete file", Icon = ArcIcons.Trash2, IsDestructive = true });
        file.ItemClicked += (_, e) => status.Text = $"Q3 report.pdf: {e.Header}";

        // Rows without icons show a check when checked, and a quiet ellipsis otherwise.
        var pinned = new MenuEntry { Header = "Pinned", IsChecked = true };
        var image = new ContextMenu { Label = "Actions for Launch.png", Content = Tile("Launch.png", "Image · 840 KB", ArcIcons.Image) };
        image.Items.Add(new MenuEntry { Header = "Open", Icon = ArcIcons.ExternalLink });
        image.Items.Add(pinned);
        image.Items.Add(new MenuEntry { Header = "Rename" });
        image.Items.Add(new MenuEntry { Header = "Share", Icon = ArcIcons.Share, IsEnabled = false });
        image.Items.Add(new MenuEntry { Header = "Move to trash", Icon = ArcIcons.Trash2, IsDestructive = true });
        pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
        image.ItemClicked += (_, e) => status.Text = $"Launch.png: {e.Header}";

        // Attached: right-click (or the ContextMenu key) on any control, without wrapping it.
        var attached = new Border
        {
            Width = 220,
            Height = 96,
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Child = GalleryUi.Text("Right-click anywhere here", ArcDesignTokenKind.TextSecondary)
        };
        ((TextBlock)attached.Child).HorizontalAlignment = HorizontalAlignment.Center;
        ((TextBlock)attached.Child).VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(attached, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(attached, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        var surface = new MenuSurface();
        surface.Items.Add(new MenuEntry { Header = "New folder", Icon = ArcIcons.Folder });
        surface.Items.Add(new MenuEntry { Header = "Paste", Icon = ArcIcons.Copy, InputGestureText = "⌘V" });
        surface.Items.Add(new MenuEntry { Header = "Refresh", Icon = ArcIcons.RefreshCw });
        surface.ItemClicked += (_, e) => status.Text = $"Canvas: {e.Header}";
        ContextMenu.SetMenu(attached, surface);

        return GalleryUi.Column(20,
            GalleryUi.Card("File tile", "Right-click opens the menu at the pointer on a snappy spring; a click, tap or Shift+F10 opens it under the tile. Scrolling or resizing closes it.",
                GalleryUi.Column(12, GalleryUi.Row(file, image), status)),
            GalleryUi.Card("Attached menu", "arc:ContextMenu.Menu gives any control a right-click menu without wrapping it.",
                GalleryUi.Row(attached)));
    }
}
