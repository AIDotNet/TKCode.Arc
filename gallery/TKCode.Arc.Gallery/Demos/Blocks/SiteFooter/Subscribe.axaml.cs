using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteFooterSubscribeDemo : UserControl
{
    private int _sends;

    public SiteFooterSubscribeDemo()
    {
        InitializeComponent();
        Footer.SubscribeAsync = SendAsync;
        Footer.Subscribed += (_, e) => Log.Text = $"Subscribed: {e.Email}";
    }

    // A real app would call its mailing list here; throwing is how a send fails.
    private async Task SendAsync(string email, CancellationToken cancellationToken)
    {
        await Task.Delay(1200, cancellationToken);
        if (++_sends % 2 == 1)
        {
            Log.Text = $"Send {_sends} failed for {email}";
            throw new InvalidOperationException("The list is unavailable.");
        }
    }
}
