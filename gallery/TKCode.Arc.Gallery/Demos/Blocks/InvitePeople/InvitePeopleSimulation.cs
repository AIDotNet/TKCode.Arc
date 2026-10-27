namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The pretend server behind the InvitePeople demos: each invite is delivered 680 ms after Send and the next ones 160 ms
/// apart, and the first invite of the first send is accepted 3.55 s after it went out (only once until the block is
/// reset). Everything stops when the block is reset, because the block cancels the token it passes.
/// </summary>
public sealed class InvitePeopleSimulation
{
    private readonly InvitePeople _block;
    private bool _accepted;

    public InvitePeopleSimulation(InvitePeople block)
    {
        _block = block;
        block.SendAction = SendAsync;
        block.ResetPerformed += (_, _) => _accepted = false;
    }

    /// <summary>When false nobody accepts (the narrow and flat demos keep their lists).</summary>
    public bool AcceptsOnce { get; set; } = true;

    private async Task SendAsync(InvitePeopleSendRequest request, CancellationToken token)
    {
        if (AcceptsOnce && !_accepted && request.Invites.Count > 0)
        {
            _accepted = true;
            _ = AcceptLaterAsync(request.Invites[0], token);
        }
        for (var i = 0; i < request.Invites.Count; i++)
        {
            await Task.Delay(i == 0 ? 680 : 160, token);
            _block.MarkDelivered(request.Invites[i]);
        }
    }

    private async Task AcceptLaterAsync(InvitePeoplePendingInvite invite, CancellationToken token)
    {
        try
        {
            await Task.Delay(3550, token);
            _block.MarkJoined(invite);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
