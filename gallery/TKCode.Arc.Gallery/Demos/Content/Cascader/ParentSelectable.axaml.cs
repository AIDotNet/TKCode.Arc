using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderParentSelectableDemo : UserControl
{
    public CascaderParentSelectableDemo()
    {
        InitializeComponent();
        CascaderDemoData.Configure(Category);
        Category.ItemsSource = CascaderDemoData.Categories();
        Category.SelectedValue = "phones";
    }
}
