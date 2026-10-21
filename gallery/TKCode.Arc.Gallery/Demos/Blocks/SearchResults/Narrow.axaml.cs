using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsNarrowDemo : UserControl
{
    public SearchResultsNarrowDemo()
    {
        InitializeComponent();
        var rooms = new[] { "Living room", "Office", "Bedroom", "Office" };
        var search = (SearchResults)Content!;
        for (var i = 0; i < search.Items.Count; i++)
        {
            search.Items[i].Attributes["room"] = rooms[i];
        }
        search.Refresh();
    }
}
