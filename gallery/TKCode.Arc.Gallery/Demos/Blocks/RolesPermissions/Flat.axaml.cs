using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RolesPermissionsFlatDemo : UserControl
{
    public RolesPermissionsFlatDemo()
    {
        InitializeComponent();
        RolesPermissionsDemoData.Fill(Matrix);
        Matrix.SaveHandler = RolesPermissionsDemoData.SimulatedSave;
    }
}
