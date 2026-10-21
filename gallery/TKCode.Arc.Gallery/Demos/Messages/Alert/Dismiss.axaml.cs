using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AlertDismissDemo : UserControl
{
    public AlertDismissDemo()
    {
        InitializeComponent();
    }

    private void OnShowAgainClick(object? sender, RoutedEventArgs e) => Maintenance.IsOpen = true;
}
