using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class ComboboxCustomFilterDemo : UserControl
{
    public ComboboxCustomFilterDemo()
    {
        InitializeComponent();
        Prefix.ItemsSource = SelectsDemoData.Cities.Select(c => c.City).ToArray();
        Prefix.FilterPredicate = (item, query) =>
            item is string label && label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase);
    }
}
