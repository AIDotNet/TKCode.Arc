using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeSelectPreviewDemo : UserControl
{
    public TreeSelectPreviewDemo()
    {
        InitializeComponent();
        TreeSelectDemoData.Configure(Files);
        Files.ItemsSource = TreeSelectDemoData.Project();
        Files.SelectedValues = ["app/page.tsx", "components/Button.tsx"];
        Result.Text = TreeSelectDemoData.Describe(Files.SelectedValues);
        Files.SelectionChanged += (_, _) => Result.Text = TreeSelectDemoData.Describe(Files.SelectedValues);
    }
}
