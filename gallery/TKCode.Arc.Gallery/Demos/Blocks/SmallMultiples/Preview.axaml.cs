using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SmallMultiplesPreviewDemo : UserControl
{
    public SmallMultiplesPreviewDemo()
    {
        InitializeComponent();
        Chart.Title = SmallMultiplesDemoData.Title;
        Chart.Description = SmallMultiplesDemoData.Description;
        Chart.Periods.AddRange(SmallMultiplesDemoData.Months());
        Chart.Series.AddRange(SmallMultiplesDemoData.Markets());
    }
}
