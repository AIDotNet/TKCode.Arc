using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class ComboboxSearchableDemo : UserControl
{
    public ComboboxSearchableDemo()
    {
        InitializeComponent();
        // Keywords are matched by the search but not shown.
        Zone.ItemsSource = SelectsDemoData.Cities.Select(c => new SelectOption(c.City, c.City)
        {
            Keywords = $"{c.Zone} {SelectsDemoData.Utc(c.Offset)}"
        }).ToArray();
    }
}
