using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CustomerGalaxyClustersDemo : UserControl
{
    public CustomerGalaxyClustersDemo()
    {
        InitializeComponent();
        foreach (var grouping in CustomerGalaxyDemoData.Groupings())
        {
            Galaxy.Groupings.Add(grouping);
        }
        Galaxy.ItemsSource = CustomerGalaxyDemoData.Accounts;
    }
}
