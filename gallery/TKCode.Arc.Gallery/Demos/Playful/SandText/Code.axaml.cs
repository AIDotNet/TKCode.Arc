using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SandTextCodeDemo : UserControl
{
    public SandTextCodeDemo()
    {
        InitializeComponent();
    }

    private void OnScatter(object? sender, RoutedEventArgs e)
    {
        Status.Text = "Scattering...";
        Sand.Dissolve();
    }

    private void OnGather(object? sender, RoutedEventArgs e)
    {
        Status.Text = "Gathering...";
        Sand.Gather();
    }

    private void OnDissolved(object? sender, RoutedEventArgs e) => Status.Text = "Gone";

    private void OnGathered(object? sender, RoutedEventArgs e) => Status.Text = "Standing";
}
