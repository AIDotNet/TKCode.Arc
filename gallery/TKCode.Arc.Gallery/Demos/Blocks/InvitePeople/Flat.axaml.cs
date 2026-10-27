using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvitePeopleFlatDemo : UserControl
{
    public InvitePeopleFlatDemo()
    {
        InitializeComponent();
        InvitePeopleDemoData.Fill(Block);
        Block.SampleText = null;
        _ = new InvitePeopleSimulation(Block) { AcceptsOnce = false };
    }
}
