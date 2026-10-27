using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MrrWaterfallNarrowDemo : UserControl
{
    public MrrWaterfallNarrowDemo()
    {
        InitializeComponent();
        Bridge.Periods.AddRange(MrrWaterfallDemoData.Periods());
    }
}
