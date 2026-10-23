using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CustomerGalaxyPreviewDemo : UserControl
{
    public CustomerGalaxyPreviewDemo()
    {
        InitializeComponent();
        // Seeded sample data: the same 2,400 accounts on every run.
        Galaxy.ItemsSource = CustomerGalaxyDemoData.Accounts;
    }
}
