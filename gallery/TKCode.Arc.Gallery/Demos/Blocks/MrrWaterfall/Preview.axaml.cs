using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MrrWaterfallPreviewDemo : UserControl
{
    public MrrWaterfallPreviewDemo()
    {
        InitializeComponent();
        Bridge.Periods.AddRange(MrrWaterfallDemoData.Periods());
    }
}
