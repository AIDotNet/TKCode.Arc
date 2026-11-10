using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SelectionToolbarCustomActionsDemo : UserControl
{
    public SelectionToolbarCustomActionsDemo()
    {
        InitializeComponent();
    }

    private void OnCountClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ContentControl { Content: string text })
        {
            Toolbar.SelectedCount = int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void OnActionInvoked(object? sender, SelectionToolbarActionEventArgs e)
        => Toast.Show(this, $"{e.Action.Label}: {e.SelectedCount} tickets");
}
