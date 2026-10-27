using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvitePeopleNarrowDemo : UserControl
{
    public InvitePeopleNarrowDemo()
    {
        InitializeComponent();
        InvitePeopleDemoData.Fill(Block);
        _ = new InvitePeopleSimulation(Block) { AcceptsOnce = false };
    }
}
