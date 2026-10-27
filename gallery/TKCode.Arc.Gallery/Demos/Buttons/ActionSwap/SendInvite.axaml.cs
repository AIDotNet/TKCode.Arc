using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActionSwapSendInviteDemo : UserControl
{
    public ActionSwapSendInviteDemo()
    {
        InitializeComponent();
        // The task holds the working phase ("Sending") until the invite is sent; "Sent" then holds for two seconds.
        Invite.AsyncAction = async (next, _) =>
        {
            if (next.Id == "sent")
            {
                await Task.Delay(900);
            }
        };
    }
}
