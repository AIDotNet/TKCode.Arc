using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ElasticSliderFineStepsDemo : UserControl
{
    public ElasticSliderFineStepsDemo()
    {
        InitializeComponent();
        Zoom.ValueFormatter = v => $"{v:0.0}×";
    }
}
