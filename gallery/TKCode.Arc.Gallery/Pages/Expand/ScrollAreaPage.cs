using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ScrollAreaPage
{
    private static readonly string[] Releases =
    [
        "v2.4 · Overlay scrollbars", "v2.3 · Faster cold starts", "v2.2 · Keyboard shortcuts", "v2.1 · Shared workspaces",
        "v2.0 · New editor", "v1.9 · Offline mode", "v1.8 · Audit log", "v1.7 · Webhooks", "v1.6 · SSO", "v1.5 · Dark mode",
        "v1.4 · Billing portal", "v1.3 · Import from CSV", "v1.2 · Comments", "v1.1 · Mentions", "v1.0 · Launch"
    ];

    public static Control Create()
    {
        var list = new StackPanel { Spacing = 0 };
        foreach (var release in Releases)
        {
            var row = new Border { Padding = new Thickness(16, 12), BorderThickness = new Thickness(0, 0, 0, 1) };
            ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            row.Child = GalleryUi.Text(release, ArcDesignTokenKind.Foreground);
            list.Children.Add(row);
        }
        var edges = GalleryUi.Text("Edges: bottom", ArcDesignTokenKind.TextMuted, 12);
        var vertical = new ScrollArea { Height = 260, Width = 360, Content = list, Label = "Release notes", HorizontalAlignment = HorizontalAlignment.Left };
        vertical.EdgesChanged += (_, e) => edges.Text = "Edges: " + string.Join(", ",
            new[] { e.Edges.Top ? "top" : null, e.Edges.Bottom ? "bottom" : null }.Where(s => s is not null).DefaultIfEmpty("none"));
        Frame(vertical);

        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(16) };
        for (var i = 1; i <= 12; i++)
        {
            var card = new Border { Width = 160, Height = 100, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1), Padding = new Thickness(14) };
            ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            card.Child = GalleryUi.Text($"Card {i}", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
            strip.Children.Add(card);
        }
        var horizontal = new ScrollArea
        {
            Orientation = ScrollAreaOrientation.Horizontal,
            Content = strip,
            HorizontalSnapPointsType = SnapPointsType.Mandatory,
            Label = "Cards"
        };
        Frame(horizontal);

        var grid = new Grid { Width = 900, Height = 600 };
        for (var r = 0; r < 12; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }
        for (var c = 0; c < 18; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }
        for (var r = 0; r < 12; r++)
        {
            for (var c = 0; c < 18; c++)
            {
                var cell = GalleryUi.Text($"{(char)('A' + c)}{r + 1}", ArcDesignTokenKind.TextSecondary, 12);
                cell.HorizontalAlignment = HorizontalAlignment.Center;
                cell.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        var both = new ScrollArea
        {
            Orientation = ScrollAreaOrientation.Both,
            OverlayScrollbarMode = ScrollAreaScrollbarMode.Always,
            Height = 240,
            Content = grid
        };
        Frame(both);

        return GalleryUi.Column(20,
            GalleryUi.Card("Vertical", "Thin overlay scrollbars appear while scrolling or hovering and fade after 900ms; the edges fade only where more content continues. Focus the area and use the arrow keys, Page Up/Down, Home/End or Space.",
                GalleryUi.Column(12, Wrap(vertical), edges)),
            GalleryUi.Card("Horizontal strip", "A vertical wheel scrolls the strip sideways while it can, then hands the wheel back to the page. Cards snap.", Wrap(horizontal)),
            GalleryUi.Card("Both axes, always visible", "OverlayScrollbarMode = Always keeps the bars while content overflows; the two tracks stop short of the corner.", Wrap(both)));
    }

    private static void Frame(ScrollArea area) => area.CornerRadius = new CornerRadius(18);

    private static Border Wrap(Control content)
    {
        var frame = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = content, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return frame;
    }
}
