using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CustomerGalaxyNarrowDemo : UserControl
{
    public CustomerGalaxyNarrowDemo()
    {
        InitializeComponent();
        foreach (var grouping in CustomerGalaxyDemoData.Groupings())
        {
            Galaxy.Groupings.Add(grouping);
        }
        Galaxy.ItemsSource = CustomerGalaxyDemoData.Accounts;
    }
}
