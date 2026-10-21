using Avalonia.Controls;
using Avalonia.Interactivity;
using StreamMark.Avalonia.Selection;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// An outline sidebar and a find bar built from the public host integration API of <see cref="Markdown"/>:
/// <c>GetOutline</c>, <c>ScrollToPoint</c>, <c>Find</c>, <c>FindNext</c> and <c>FindPrevious</c>.
/// </summary>
public partial class MarkdownOutlineDemo : UserControl
{
    private const string Body = """
        # A field guide to Arc

        Arc is a component library. This page shows how a host can build its own navigation and search around the
        rendered document without reaching into the template.

        ## Navigation

        An outline lists every heading in reading order. Each item carries the level, the text and the point the
        document scrolls to, so a click is one `ScrollToPoint` call.

        ### Anchors

        Links that point inside the document, such as [the search section](#search), scroll to their heading by
        themselves. `ScrollToAnchor` does the same for your own links and returns `false` when the heading is missing.

        ## Search

        `Find` returns the number of matches and highlights all of them. `FindNext` and `FindPrevious` move the current
        match and scroll to it, wrapping around at the ends.

        ## Selection

        `SelectAll`, `GetSelectedText`, `GetSelectedMarkdown` and `CopySelectionAsync` work on the same selection the
        reader makes with the mouse or the keyboard.

        ## Timing

        Parsing happens on a worker thread. Wait for `DocumentReady` before you read the outline for the first time,
        or accept that it is empty until the first snapshot lands.
        """;

    private IReadOnlyList<OutlineItem> _outline = Array.Empty<OutlineItem>();

    public MarkdownOutlineDemo()
    {
        InitializeComponent();
        Article.Text = Body;
    }

    private void OnDocumentReady(object? sender, EventArgs e)
    {
        _outline = Article.GetOutline();
        Outline.ItemsSource = _outline.Select(o => $"{new string(' ', (o.Level - 1) * 2)}{o.Text}").ToList();
        Status.Text = $"{_outline.Count} headings";
    }

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Outline.SelectedIndex >= 0 && Outline.SelectedIndex < _outline.Count)
        {
            Article.ScrollToPoint(_outline[Outline.SelectedIndex].Target);
        }
    }

    private void OnFindTextChanged(object? sender, TextChangedEventArgs e) => Update(FindBox.Text);

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        Article.FindNext();
        ShowCurrent();
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        Article.FindPrevious();
        ShowCurrent();
    }

    private void Update(string? query)
    {
        var count = Article.Find(query);
        if (string.IsNullOrEmpty(query))
        {
            Status.Text = "No search";
            return;
        }
        Status.Text = count == 0 ? "No matches" : $"{Article.CurrentMatchIndex + 1} of {count}";
    }

    private void ShowCurrent()
    {
        if (Article.MatchCount > 0)
        {
            Status.Text = $"{Article.CurrentMatchIndex + 1} of {Article.MatchCount}";
        }
    }
}
