using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatManagerStatesDemo : UserControl
{
    public SeatManagerStatesDemo()
    {
        InitializeComponent();

        SeatManagerSamples.Apply(Adding);
        Adding.Target = 15;

        SeatManagerSamples.Apply(Removing);
        Removing.Purchased = 15;
        Removing.Target = 12;

        SeatManagerSamples.Apply(NoInvites);
        NoInvites.Invites.Clear();
    }
}
