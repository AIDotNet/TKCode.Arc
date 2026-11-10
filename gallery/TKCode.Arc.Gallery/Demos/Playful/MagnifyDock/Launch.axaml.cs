using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MagnifyDockLaunchDemo : UserControl
{
    public MagnifyDockLaunchDemo()
    {
        InitializeComponent();
    }

    private void OnItemLaunched(object? sender, MagnifyDockItemEventArgs e) =>
        Readout.Text = $"Opened {e.Container.Label}";

    private void OnQuitAll(object? sender, RoutedEventArgs e)
    {
        foreach (var item in Dock.Items)
        {
            if (item is MagnifyDockItem icon)
            {
                icon.IsRunning = false;
            }
        }
        Readout.Text = "Nothing open";
    }
}
