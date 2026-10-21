using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ColorPickerColorFieldDemo : UserControl
{
    public ColorPickerColorFieldDemo()
    {
        InitializeComponent();
    }

    private void OnColorChanged(object? sender, ColorPickerColorChangedEventArgs e)
        => Status.Text = $"onValueChange: {e.Hex}";
}
