using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class OrbitLogosConnectDemo : UserControl
{
    private int _attempts;

    public OrbitLogosConnectDemo()
    {
        InitializeComponent();
        Orbit.ConnectHandler = ConnectAsync;
    }

    private async Task ConnectAsync(OrbitLogo logo)
    {
        // Stand-in for your own sign-in or API call: the first attempt is refused so the Try again state shows.
        await Task.Delay(1200);
        if (_attempts++ == 0)
        {
            throw new InvalidOperationException("Refused for the demo.");
        }
    }

    private void OnOpenParcel(object? sender, RoutedEventArgs e) => Orbit.SelectedId = "parcel";

    private void OnClose(object? sender, RoutedEventArgs e) => Orbit.SelectedId = null;

    private void OnSelectionChanged(object? sender, OrbitLogosSelectionChangedEventArgs e)
    {
        Status.Text = e.Logo is null ? "Closed" : $"Opened {e.Logo.Name} (index {e.Index})";
    }
}
