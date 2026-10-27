using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SmallMultiplesSharedScaleDemo : UserControl
{
    public SmallMultiplesSharedScaleDemo()
    {
        InitializeComponent();
        Chart.Title = SmallMultiplesDemoData.Title;
        Chart.Description = SmallMultiplesDemoData.Description;
        Chart.Periods.AddRange(SmallMultiplesDemoData.Months());
        Chart.Series.AddRange(SmallMultiplesDemoData.Markets());
    }
}
