using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProfilePageSaveErrorDemo : UserControl
{
    private int _attempts;

    public ProfilePageSaveErrorDemo()
    {
        InitializeComponent();
        ProfilePageDemoData.Fill(Page);

        // Your server call goes here. Throwing keeps the sheet open and shows the message above its buttons.
        Page.SaveHandler = async profile =>
        {
            await Task.Delay(900);
            if (++_attempts == 1)
            {
                throw new InvalidOperationException("We could not reach the server. Your changes are still here; try again.");
            }
        };
        // A clipboard that refuses: Share shows Failed for a moment.
        Page.CopyHandler = _ => Task.FromResult(false);

        Page.SaveFailed += (_, e) => Log.Text = $"SaveFailed: {e.Error.Message}";
        Page.Saved += (_, e) => Log.Text = $"Saved on attempt {_attempts}: {e.ChangedFields}";
        Page.Shared += (_, e) => Log.Text = $"Shared: {(e.Succeeded ? "copied" : "failed")}";
    }
}
