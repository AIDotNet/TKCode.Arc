using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MrrWaterfallPercentDemo : UserControl
{
    public MrrWaterfallPercentDemo()
    {
        InitializeComponent();
        Bridge.Periods.AddRange(MrrWaterfallDemoData.Periods());
    }
}
