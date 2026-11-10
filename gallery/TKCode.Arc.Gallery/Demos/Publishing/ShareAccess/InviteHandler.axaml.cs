using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareAccessInviteHandlerDemo : UserControl
{
    public ShareAccessInviteHandlerDemo()
    {
        InitializeComponent();
        Share.InviteHandler = SendInvitesAsync;
    }

    // Stands in for a call to your server.
    private static async Task SendInvitesAsync(IReadOnlyList<string> emails, ShareAccessRole role, CancellationToken cancellationToken)
    {
        await Task.Delay(900, cancellationToken);
        if (emails.FirstOrDefault(e => e.Contains("blocked", StringComparison.OrdinalIgnoreCase)) is { } refused)
        {
            throw new InvalidOperationException($"{refused} cannot receive invites.");
        }
    }
}
