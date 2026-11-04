using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Demos;

public partial class ArcWindowBasicDemo : UserControl
{
    public ArcWindowBasicDemo()
    {
        InitializeComponent();
        // The browser has a single view and no windows to open.
        OpenButton.IsVisible = !OperatingSystem.IsBrowser();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        var window = new ArcWindow
        {
            Title = "Untitled document",
            Width = 720,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = "Drag the title bar, double-click it, or resize from the edges.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
