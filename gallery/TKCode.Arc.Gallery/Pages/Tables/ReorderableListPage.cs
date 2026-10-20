using System.Collections.ObjectModel;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Pages;

public static class ReorderableListPage
{
    public static Control Create()
    {
        var priorities = new ObservableCollection<ReorderableListEntry>
        {
            new("pricing", "Ship pricing page", "Marketing · due Friday"),
            new("onboarding", "Review onboarding flow", "Design · 3 open comments"),
            new("billing", "Migrate billing webhooks", "Platform"),
            new("notes", "Write launch notes", "Docs · draft ready")
        };
        var status = GalleryUi.Text("Drag a grip, or focus one and press ↑ / ↓. Space picks a row up, arrows move it, Space drops, Escape cancels.",
            ArcDesignTokenKind.TextSecondary);
        var list = new ReorderableList { ItemsSource = priorities, Width = 520, HorizontalAlignment = HorizontalAlignment.Left };
        list.Reordered += (_, e) => status.Text = $"Moved “{(e.Item as ReorderableListEntry)?.Title}” from {e.OldIndex + 1} to {e.NewIndex + 1}. Order: " +
                                                  string.Join(", ", priorities.Select(p => p.Id));

        var tracks = new ObservableCollection<ReorderableListEntry>();
        for (var i = 1; i <= 24; i++)
        {
            tracks.Add(new ReorderableListEntry($"t{i}", $"Track {i:00}", $"{2 + i % 4}:{(i * 7) % 60:00}"));
        }
        var longList = new ReorderableList { ItemsSource = tracks };
        var scroller = new ScrollViewer
        {
            Height = 340,
            Width = 528,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new Border { Padding = new Thickness(4, 8), Child = longList }
        };

        var colors = new ObservableCollection<string> { "Coral", "Saffron", "Mint", "Azure", "Iris" };
        var custom = new ReorderableList
        {
            ItemsSource = colors,
            Width = 520,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemTemplate = new FuncDataTemplate<string>((name, _) => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new Border
                    {
                        Width = 14,
                        Height = 14,
                        CornerRadius = new CornerRadius(7),
                        VerticalAlignment = VerticalAlignment.Center,
                        Background = new SolidColorBrush(Swatch(name))
                    },
                    new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center }
                }
            })
        };

        return GalleryUi.Column(20,
            GalleryUi.Card("Priorities", "Rows lift when their grip is pressed, follow the pointer, and the rows they pass slide aside on springs. The position number rolls in the direction a row travelled.",
                GalleryUi.Column(14, list, status)),
            GalleryUi.Card("Auto-scroll", "Hold a dragged row near the top or bottom edge of the scrolling area to scroll it.", scroller),
            GalleryUi.Card("Custom item template", "ItemTemplate replaces the title and detail; an ObservableCollection<string> is reordered in place.", custom));
    }

    private static Color Swatch(string? name) => name switch
    {
        "Coral" => Color.Parse("#F07167"),
        "Saffron" => Color.Parse("#F4B942"),
        "Mint" => Color.Parse("#57CC99"),
        "Azure" => Color.Parse("#4EA8DE"),
        _ => Color.Parse("#9381FF")
    };
}
