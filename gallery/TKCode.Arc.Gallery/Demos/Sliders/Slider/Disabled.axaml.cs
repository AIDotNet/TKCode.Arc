using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SliderDisabledDemo : UserControl
{
    public SliderDisabledDemo()
    {
        InitializeComponent();
        OpacitySlider.ValueFormatter = v => $"{v:0}%";
    }
}
