using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MobileSheetDetentsDemo : UserControl
{
    public MobileSheetDetentsDemo()
    {
        InitializeComponent();
    }

    private void OnShow(object? sender, RoutedEventArgs e) => PlacesSheet.IsOpen = true;

    private void OnDetentChanged(object? sender, Mobile.SheetDetentChangedEventArgs e)
        => State.Text = e.Index == 0 ? "Peeking" : "Open";
}
