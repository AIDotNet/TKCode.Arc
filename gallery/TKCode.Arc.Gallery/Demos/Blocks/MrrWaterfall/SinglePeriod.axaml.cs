using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MrrWaterfallSinglePeriodDemo : UserControl
{
    public MrrWaterfallSinglePeriodDemo()
    {
        InitializeComponent();
        Bridge.Periods.AddRange(MrrWaterfallDemoData.ShrinkingPeriods());
        Bridge.SelectedIndex = 5;
    }
}
