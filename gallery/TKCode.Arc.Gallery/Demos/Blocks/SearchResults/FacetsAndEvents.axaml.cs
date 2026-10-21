using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SearchResultsFacetsAndEventsDemo : UserControl
{
    public SearchResultsFacetsAndEventsDemo()
    {
        InitializeComponent();
        Search.ItemInvoked += (_, e) => Status.Text = $"Opened “{e.Item.Title}” ({e.Item.Id}).";
        Search.PropertyChanged += (_, e) =>
        {
            if (e.Property == SearchResults.ActiveItemProperty && Search.ActiveItem is { } item)
            {
                Status.Text = $"Cursor on “{item.Title}”. Press Enter to open it.";
            }
            else if (e.Property == SearchResults.SelectedKindProperty)
            {
                Status.Text = $"Facet: {Search.SelectedKind ?? "All"}.";
            }
        };
    }
}
