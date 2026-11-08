using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SeatManagerBackendDemo : UserControl
{
    private readonly List<string> _lines = new();

    public SeatManagerBackendDemo()
    {
        InitializeComponent();
        SeatManagerSamples.Apply(Card);

        // A real app posts to its billing API here; the card shows the spinner while the task runs and the exception's
        // message (with Try again) when it throws.
        Card.UpdateSeatsAsync = async (quote, cancellation) =>
        {
            Write($"updateSeats: {quote.Current} → {quote.Target}, {quote.DueToday:0.00} due");
            await Task.Delay(1200, cancellation);
            if (Decline.IsChecked == true)
            {
                Decline.IsChecked = false;
                throw new InvalidOperationException(SeatManagerSamples.DeclinedMessage);
            }
        };
        Card.ResendInviteAsync = async (invite, cancellation) =>
        {
            Write($"resendInvite: {invite.Email}");
            await Task.Delay(700, cancellation);
        };
        Card.RevokeInviteAsync = (invite, _) =>
        {
            Write($"revokeInvite: {invite.Email}");
            return Task.CompletedTask;
        };

        Card.PhaseChanged += (_, e) => Write($"phase: {e.OldPhase} → {e.NewPhase}");
        Card.SeatsChanged += (_, e) => Write($"seats now {e.Seats}");
        Card.InviteRestored += (_, e) => Write($"restored {e.Invite.Email}");
    }

    private void Write(string line)
    {
        _lines.Add(line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(0);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
