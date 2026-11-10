using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeSelectCheckStrategyDemo : UserControl
{
    public TreeSelectCheckStrategyDemo()
    {
        InitializeComponent();
        TreeSelectDemoData.Configure(People, icons: false);
        People.ItemsSource = TreeSelectDemoData.Teams();
        People.SelectedValues = ["engineering/web", "design/ava"];
        Show();
        People.SelectionChanged += (_, _) => Show();
        Strategy.SelectionChanged += (_, _) =>
        {
            People.CheckStrategy = Strategy.SelectedValue switch
            {
                "Parents" => TreeSelectCheckStrategy.Parents,
                "All" => TreeSelectCheckStrategy.All,
                _ => TreeSelectCheckStrategy.Leaves
            };
            Show();
        };
    }

    private void Show() => Result.Text = TreeSelectDemoData.Describe(People.SelectedValues);
}
