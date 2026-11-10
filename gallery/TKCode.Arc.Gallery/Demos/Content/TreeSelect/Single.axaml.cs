using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeSelectSingleDemo : UserControl
{
    public TreeSelectSingleDemo()
    {
        InitializeComponent();
        TreeSelectDemoData.Configure(Destination);
        Destination.ItemsSource = TreeSelectDemoData.Folders();
        Destination.SelectedValue = "Projects/Website/Assets";
        Result.Text = $"SelectedValue: {Destination.SelectedValue}";
        Destination.SelectionChanged += (_, _) => Result.Text = $"SelectedValue: {Destination.SelectedValue ?? "none"}";
    }
}
