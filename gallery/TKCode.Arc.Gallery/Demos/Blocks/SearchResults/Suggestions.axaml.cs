using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsSuggestionsDemo : UserControl
{
    public SearchResultsSuggestionsDemo()
    {
        InitializeComponent();
        // Recent searches are recorded by the control when a query is submitted; seed them (or save them) yourself.
        Search.Recent.AddRange(["export", "invite", "password reset"]);
        Search.Suggestions = ["billing", "single sign-on", "audit log", "api keys"];
        Search.QuerySubmitted += (_, e) => Status.Text = $"Submitted “{e.Query}”. Recent: {string.Join(", ", Search.Recent)}.";
    }
}
