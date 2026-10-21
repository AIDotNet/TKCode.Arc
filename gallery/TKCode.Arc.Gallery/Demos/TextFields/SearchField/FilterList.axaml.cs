using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchFieldFilterListDemo : UserControl
{
    private static readonly string[] Names =
    [
        "Ada Lovelace", "Alan Turing", "Barbara Liskov", "Claude Shannon", "Donald Knuth",
        "Edsger Dijkstra", "Grace Hopper", "Katherine Johnson", "Margaret Hamilton", "Radia Perlman"
    ];

    public SearchFieldFilterListDemo()
    {
        InitializeComponent();
        Filter();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e) => Filter();

    private void OnSearchSubmitted(object? sender, SearchSubmittedEventArgs e)
        => Submitted.Text = $"Submitted “{e.Query}”";

    private void Filter()
    {
        var query = Search.Text ?? string.Empty;
        var matches = Names.Where(name => name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        Results.ItemsSource = matches;
        NoMatches.Text = $"No one matches “{query}”.";
        NoMatches.IsVisible = matches.Count == 0;
    }
}
