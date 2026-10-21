using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ImageCompareBeforeAndAfterDemo : UserControl
{
    public ImageCompareBeforeAndAfterDemo()
    {
        InitializeComponent();
    }

    private void OnPositionChanged(object? sender, ImageComparePositionChangedEventArgs e)
    {
        Readout.Text = $"Position {e.Position:0.#}%";
    }
}
