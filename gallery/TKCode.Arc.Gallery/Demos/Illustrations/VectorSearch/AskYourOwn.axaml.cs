using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class VectorSearchAskYourOwnDemo : UserControl
{
    public VectorSearchAskYourOwnDemo()
    {
        InitializeComponent();
    }

    private void OnSearchSubmitted(object? sender, SearchSubmittedEventArgs e)
    {
        Ask();
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        Ask();
    }

    private void OnResultsChanged(object? sender, EventArgs e)
    {
        Answers.Text = string.Join("  ·  ", Search.Results.Select(r => $"{r.Rank}. {r.Document.Title} ({r.Score:0.00})"));
    }

    private void Ask()
    {
        if (!string.IsNullOrWhiteSpace(Question.Text))
        {
            Search.Search(Question.Text);
        }
    }
}
