using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ExpandableTextControlledDemo : UserControl
{
    public ExpandableTextControlledDemo()
    {
        InitializeComponent();
    }

    private void OnExpandedChanged(object? sender, ExpandableTextExpandedChangedEventArgs e)
        => State.Text = e.IsExpanded ? "Expanded" : "Collapsed";

    private void OnToggleClick(object? sender, RoutedEventArgs e) => Bio.IsExpanded = !Bio.IsExpanded;
}
