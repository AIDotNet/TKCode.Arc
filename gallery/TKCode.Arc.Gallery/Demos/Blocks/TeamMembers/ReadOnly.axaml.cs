using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamMembersReadOnlyDemo : UserControl
{
    public TeamMembersReadOnlyDemo()
    {
        InitializeComponent();
        TeamMembersDemoData.Fill(Block);
        Block.Description = "Everyone with access to the Northwind Lab workspace.";
    }
}
