using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvitePeoplePreviewDemo : UserControl
{
    public InvitePeoplePreviewDemo()
    {
        InitializeComponent();
        InvitePeopleDemoData.Fill(Block);
        _ = new InvitePeopleSimulation(Block);
    }
}
