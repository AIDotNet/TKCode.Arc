using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SettingsCommandInlineDemo : UserControl
{
    public SettingsCommandInlineDemo()
    {
        InitializeComponent();
    }

    // Toggles and choices change in place; actions are the app's to run, here with a toast.
    private void OnItemInvoked(object? sender, SettingsCommandItemEventArgs e)
        => Toast.Show(this, $"Opening {e.Item.Header}", ToastKind.Info);
}
