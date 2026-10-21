using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsNoResultsDemo : UserControl
{
    public SearchResultsNoResultsDemo()
    {
        InitializeComponent();
        // The ticked type hides every result for "tide": the empty state says so and offers to clear the filters.
        // Suggestions are queries to try; choosing one fills the field.
        Search.Suggestions = ["mooring", "passage", "harbour"];
    }
}
