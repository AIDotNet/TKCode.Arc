using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class HoldToConfirmDefaultDemo : UserControl
{
    public HoldToConfirmDefaultDemo()
    {
        InitializeComponent();
    }

    private void OnHoldingChanged(object? sender, HoldToConfirmHoldingChangedEventArgs e)
    {
        Hint.Text = e.IsHolding
            ? "Keep holding…"
            : Project.IsConfirmed ? "Deleted." : "Let go early and the fill rewinds.";
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Project.IsConfirmed = false;
    }
}
