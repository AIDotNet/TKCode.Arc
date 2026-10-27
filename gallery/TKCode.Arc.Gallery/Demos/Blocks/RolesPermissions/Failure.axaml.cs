using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RolesPermissionsFailureDemo : UserControl
{
    private int _attempts;

    public RolesPermissionsFailureDemo()
    {
        InitializeComponent();
        RolesPermissionsDemoData.Fill(Matrix);

        // The first attempt fails after the usual wait; the retry goes through.
        Matrix.SaveHandler = async (_, cancellation) =>
        {
            await Task.Delay(900, cancellation);
            _attempts++;
            if (_attempts % 2 == 1)
            {
                throw new InvalidOperationException("The server did not answer. Your changes are still here.");
            }
        };
        Matrix.SaveFailed += (_, e) => Log.Text = $"Attempt {_attempts} failed: {e.Exception.Message}";
        Matrix.Saved += (_, e) => Log.Text = $"Attempt {_attempts} saved {e.Request.ChangeCount} changes";
    }
}
