using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KeycapKeyboardPreviewDemo : UserControl
{
    public KeycapKeyboardPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnKeyInvoked(object? sender, KeycapKeyInvokedEventArgs e)
        => Readout.Text = e.Binding is { } binding ? $"{binding.Gesture}: {binding.Label}" : $"{e.KeyName} has no shortcut here.";
}
