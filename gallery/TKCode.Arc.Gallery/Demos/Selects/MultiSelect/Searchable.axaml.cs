using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiSelectSearchableDemo : UserControl
{
    public MultiSelectSearchableDemo()
    {
        InitializeComponent();
        Cities.ItemsSource = SelectsDemoData.Cities.Select(c => c.City).ToArray();
    }
}
