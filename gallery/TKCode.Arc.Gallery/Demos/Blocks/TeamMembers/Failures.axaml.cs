using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamMembersFailuresDemo : UserControl
{
    private int _resends;

    public TeamMembersFailuresDemo()
    {
        InitializeComponent();
        TeamMembersDemoData.Fill(Block);

        // Each handler waits like a server, then refuses some requests by throwing; the block keeps the old state and shows
        // the message (in the line under the field for a send, in the footer for everything else).
        Block.SendInvitesAction = async (request, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            if (request.Emails.FirstOrDefault(e => e.EndsWith("@bounce.example", StringComparison.OrdinalIgnoreCase)) is { } bad)
            {
                throw new InvalidOperationException($"The mail server refused {bad}. Remove it and send again.");
            }
        };
        Block.ResendInviteAction = async (_, cancellation) =>
        {
            await Task.Delay(600, cancellation);
            if (++_resends % 2 == 0)
            {
                throw new TimeoutException();
            }
        };
        Block.RevokeInviteAction = async (invite, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            if (invite.Email?.StartsWith("sara", StringComparison.Ordinal) == true)
            {
                throw new InvalidOperationException("This invite was already accepted.");
            }
        };
        Block.RemoveMemberAction = (_, cancellation) => Task.Delay(500, cancellation);
        Block.ChangeRoleAction = async (change, cancellation) =>
        {
            await Task.Delay(300, cancellation);
            if (change.NewRole == "admin")
            {
                throw new UnauthorizedAccessException();
            }
        };
        Block.ErrorFormatter = exception => exception switch
        {
            UnauthorizedAccessException => "Only the owner can make someone an admin.",
            TimeoutException => "The mail server did not answer. Try the resend again.",
            _ => exception.Message
        };
        Block.ActionFailed += (_, e) => Log.Text = $"{e.Action} failed: {e.Message}";
        Block.InvitesSent += (_, e) => Log.Text = $"Sent {e.Invites.Count} invite(s)";
        Block.InviteResent += (_, e) => Log.Text = $"Resent to {e.Invite.Email}";
    }
}
