using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class InboxSidebarPreviewDemo : UserControl
{
    public InboxSidebarPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnCompose(object? sender, RoutedEventArgs e) => Toast.Show(this, "New message");

    private void OnCreateLabel(object? sender, RoutedEventArgs e) => Toast.Show(this, "Name your new label");
}
