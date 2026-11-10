using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MagicLinkSignInExpiredDemo : UserControl
{
    private bool _fail;

    public MagicLinkSignInExpiredDemo()
    {
        InitializeComponent();
        // The sender runs for a moment, so the button shows its spinner; it fails on request.
        SignIn.LinkSender = async _ =>
        {
            await Task.Delay(700);
            return !_fail;
        };
    }

    private void OnExpire(object? sender, RoutedEventArgs e) => SignIn.ShowExpired();

    private void OnFail(object? sender, RoutedEventArgs e)
    {
        _fail = !_fail;
        Toast.Show(this, _fail ? "Sending will fail" : "Sending works again");
    }
}
