using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamMembersNarrowDemo : UserControl
{
    public TeamMembersNarrowDemo()
    {
        InitializeComponent();
        TeamMembersDemoData.Fill(Block);
        TeamMembersDemoData.Simulate(Block);
    }
}
