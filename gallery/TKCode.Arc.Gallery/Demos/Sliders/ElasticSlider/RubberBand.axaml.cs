using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ElasticSliderRubberBandDemo : UserControl
{
    public ElasticSliderRubberBandDemo()
    {
        InitializeComponent();
        Volume.ValueFormatter = v => $"{v:0}%";
    }
}
