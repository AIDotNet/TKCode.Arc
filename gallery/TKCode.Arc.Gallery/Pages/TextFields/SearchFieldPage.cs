using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class SearchFieldPage
{
    private static readonly string[] Names =
    [
        "Ada Lovelace", "Alan Turing", "Barbara Liskov", "Claude Shannon", "Donald Knuth",
        "Edsger Dijkstra", "Grace Hopper", "Katherine Johnson", "Margaret Hamilton", "Radia Perlman"
    ];

    public static Control Create()
    {
        var search = new SearchField { Label = "Search people", Placeholder = "Filter by name", Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var list = new StackPanel { Spacing = 4 };
        var submitted = GalleryUi.Text("Press Enter to submit; Escape clears.", ArcDesignTokenKind.TextSecondary);
        void Filter()
        {
            list.Children.Clear();
            var query = search.Text ?? string.Empty;
            foreach (var name in Names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                list.Children.Add(GalleryUi.Text(name));
            }
            if (list.Children.Count == 0)
            {
                list.Children.Add(GalleryUi.Text($"No one matches “{query}”.", ArcDesignTokenKind.TextMuted));
            }
        }
        search.TextChanged += (_, _) => Filter();
        search.SearchSubmitted += (_, e) => submitted.Text = $"Submitted “{e.Query}”";
        Filter();

        return GalleryUi.Column(20,
            GalleryUi.Card("Filter a list", "The glass wakes up while searching; the clear button pops into a reserved slot, so the field never changes width.",
                GalleryUi.Column(12, search, submitted, list)),
            GalleryUi.Card("Sizes", null, GalleryUi.Column(16,
                new SearchField { Label = "Small", Size = ControlSize.Small, Placeholder = "Search", Width = 360, HorizontalAlignment = HorizontalAlignment.Left },
                new SearchField { Label = "Large", Size = ControlSize.Large, Text = "Roadmap", Width = 360, HorizontalAlignment = HorizontalAlignment.Left })));
    }
}
