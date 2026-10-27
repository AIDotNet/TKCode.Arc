using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamMembersPreviewDemo : UserControl
{
    public TeamMembersPreviewDemo()
    {
        InitializeComponent();
        TeamMembersDemoData.Fill(Block);
        // Connect these to your API; here they only wait, so the busy buttons and the flight are visible.
        TeamMembersDemoData.Simulate(Block);
    }
}
