using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RolesPermissionsNarrowDemo : UserControl
{
    public RolesPermissionsNarrowDemo()
    {
        InitializeComponent();
        RolesPermissionsDemoData.Fill(Matrix);
        Matrix.SaveHandler = RolesPermissionsDemoData.SimulatedSave;
    }
}
