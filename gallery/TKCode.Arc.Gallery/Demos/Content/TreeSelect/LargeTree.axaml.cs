using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeSelectLargeTreeDemo : UserControl
{
    public TreeSelectLargeTreeDemo()
    {
        InitializeComponent();
        TreeSelectDemoData.Configure(Modules);
        Modules.ItemsSource = TreeSelectDemoData.Monorepo();
        Result.Text = "Selected: 0 files";
        Modules.SelectionChanged += (_, _) => Result.Text = $"Selected: {Modules.SelectedValues.Count:N0} files";
    }
}
