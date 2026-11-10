using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RangeHistogramSliderDisabledDemo : UserControl
{
    public RangeHistogramSliderDisabledDemo()
    {
        InitializeComponent();
        Sizes.Data = [3, 4, 5, 5, 6, 8, 8, 9, 12, 14, 15, 15, 18, 22, 24, 26, 30, 34, 38, 41, 44, 48, 55, 60];
    }
}
