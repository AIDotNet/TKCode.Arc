using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RolesPermissionsPreviewDemo : UserControl
{
    public RolesPermissionsPreviewDemo()
    {
        InitializeComponent();
        RolesPermissionsDemoData.Fill(Matrix);

        // Store the changes somewhere; here the save only waits, so the spinner and the "saved" state are visible.
        Matrix.SaveHandler = RolesPermissionsDemoData.SimulatedSave;
    }
}
