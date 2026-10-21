using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BottomSheetDetentsDemo : UserControl
{
    public BottomSheetDetentsDemo()
    {
        InitializeComponent();
    }

    // The default detents are 45% and 92%; the sheet opens on the first.
    private void OnOpened(object? sender, EventArgs e) => Detent.Text = "Resting at 45%";

    private void OnDetentChanged(object? sender, BottomSheetDetentChangedEventArgs e)
        => Detent.Text = $"Resting at {e.Fraction:P0}";

    private void OnClosed(object? sender, OverlayClosedEventArgs e) => Detent.Text = $"Closed by {e.Reason}";
}
