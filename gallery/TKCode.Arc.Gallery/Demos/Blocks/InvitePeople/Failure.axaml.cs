using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvitePeopleFailureDemo : UserControl
{
    private int _sends;

    public InvitePeopleFailureDemo()
    {
        InitializeComponent();
        var people = InvitePeopleDemoData.Fill(Block);
        Block.SampleText = null;

        // The second, fourth, ... send is refused: the rows are taken back and the chips return to the field.
        Block.SendAction = async (request, token) =>
        {
            await Task.Delay(1000, token);
            if (++_sends % 2 == 0)
            {
                throw new InvalidOperationException("The mail service is busy. Try again in a minute.");
            }
            foreach (var invite in request.Invites)
            {
                Block.MarkDelivered(invite);
            }
        };
        // Revoking Theo's invite fails, so his row comes back.
        Block.RevokeAction = async (invite, token) =>
        {
            await Task.Delay(600, token);
            if (ReferenceEquals(invite.Person, people.Theo))
            {
                throw new InvalidOperationException("Theo's invite was already accepted on another device.");
            }
        };
        Block.UpgradeAction = (_, token) => Task.Delay(500, token);

        Block.SendFailed += (_, e) => Log.Text = $"Send {e.Request.Attempt} failed: {e.Exception.Message}";
        Block.InvitesSent += (_, e) => Log.Text = $"Send {e.Request.Attempt} went through";
        Block.InviteRevoked += (_, e) => Log.Text = $"Revoking {e.Invite.Person?.DisplayName}…";
    }
}
