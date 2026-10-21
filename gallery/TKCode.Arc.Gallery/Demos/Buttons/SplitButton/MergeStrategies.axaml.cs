using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SplitButtonMergeStrategiesDemo : UserControl
{
    public SplitButtonMergeStrategiesDemo()
    {
        InitializeComponent();
    }

    private void OnMergeClick(object? sender, RoutedEventArgs e)
    {
        Log.Text = "Merged with a merge commit.";
    }

    private void OnItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        Log.Text = $"Menu: {e.Header}";
    }
}
