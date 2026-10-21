using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardStackPhotosDemo : UserControl
{
    public CardStackPhotosDemo()
    {
        InitializeComponent();
        Photographed.LabelSelector = item => ((CardStackDestination)item!).Name;
    }
}
