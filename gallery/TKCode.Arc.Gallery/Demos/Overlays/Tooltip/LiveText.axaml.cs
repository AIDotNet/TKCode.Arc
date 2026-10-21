using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TooltipLiveTextDemo : UserControl
{
    private int _saves;

    public TooltipLiveTextDemo()
    {
        InitializeComponent();
    }

    // Changing the tip while the tooltip is open crossfades the text and springs the bubble to the new size.
    private void OnSaveClick(object? sender, RoutedEventArgs e)
        => Tooltip.SetTip(Save, ++_saves % 2 == 1 ? "Saved just now" : "Save draft");
}
