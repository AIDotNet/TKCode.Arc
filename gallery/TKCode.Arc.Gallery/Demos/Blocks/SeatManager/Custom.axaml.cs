using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatManagerCustomDemo : UserControl
{
    public SeatManagerCustomDemo()
    {
        InitializeComponent();
        SeatManagerDemoData.ApplyStudio(Card);
        Card.Labels = SeatManagerDemoData.LicenceLabels();
        Reduced.IsCheckedChanged += (_, _) =>
            ArcMotion.SetLevel(Card, Reduced.IsChecked == true ? ArcMotionLevel.Reduced : ArcMotionLevel.Full);
    }
}
