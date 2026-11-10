using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MagicLinkSignInPreviewDemo : UserControl
{
    public MagicLinkSignInPreviewDemo()
    {
        InitializeComponent();
    }

    // A real app emails the link here; the gallery only says what it would send.
    private void OnLinkRequested(object? sender, MagicLinkSignInLinkEventArgs e)
        => Toast.Show(this, e.IsResend ? $"New link sent to {e.Email}" : $"Link sent to {e.Email}");
}
