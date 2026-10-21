using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class SelectSelectorsDemo : UserControl
{
    public SelectSelectorsDemo()
    {
        InitializeComponent();

        Sort.ItemsSource = new[]
        {
            new SelectOption("newest", "Newest first"),
            new SelectOption("oldest", "Oldest first"),
            new SelectOption("updated", "Recently updated"),
            new SelectOption("az", "Name, A to Z"),
            new SelectOption("za", "Name, Z to A")
        };
        Sort.SelectedValue = "updated";

        // Any items work when the select is told how to read them.
        Zones.ItemsSource = SelectsDemoData.Cities;
        Zones.TextSelector = item => item is ValueTuple<string, string, int, string>(var city, _, var offset, _)
            ? $"{city} ({SelectsDemoData.Utc(offset)})"
            : null;
        Zones.ValueSelector = item => item is ValueTuple<string, string, int, string>(var city, _, _, _) ? city : null;
    }
}
