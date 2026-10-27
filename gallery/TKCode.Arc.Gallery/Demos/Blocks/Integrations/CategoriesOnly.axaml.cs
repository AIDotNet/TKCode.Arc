using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class IntegrationsCategoriesOnlyDemo : UserControl
{
    public IntegrationsCategoriesOnlyDemo()
    {
        InitializeComponent();
        IntegrationsDemoData.Fill(Directory, withSyncOptions: false);
    }
}
