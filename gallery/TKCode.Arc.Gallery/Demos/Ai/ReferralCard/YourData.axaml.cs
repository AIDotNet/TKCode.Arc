using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card driven by the app: its own list, a send that can fail, handled shares and webhook updates.</summary>
public partial class ReferralCardYourDataDemo : UserControl
{
    private List<Referral> _list = [];
    private int _next;

    public ReferralCardYourDataDemo()
    {
        InitializeComponent();
        Card.InviteHandler = SendAsync;
        Card.InvitesSent += (_, e) =>
        {
            // Your backend would return the new referrals; here they join the app's own list.
            var now = DateTimeOffset.Now;
            _list.InsertRange(0, e.Emails.Select(email => new Referral
            {
                Id = "app-" + ++_next,
                Email = email,
                Stage = ReferralStage.Invited,
                InvitedAt = now
            }));
            Write($"Sent {e.Emails.Count} invite(s).");
        };
        Card.ShareRequested += (_, e) =>
        {
            // A desktop app would open e.Url in the browser or the mail app here.
            e.Handled = true;
            Write($"Share {e.Channel}: {e.Url}");
        };
        Card.LinkCopied += (_, e) => Write("Copied " + e.Link);
        Card.MilestoneReached += (_, e) => Write("Milestone reached: " + e.Milestone.Reward);
        Webhook.Click += (_, _) => Advance();
        Reset.Click += (_, _) => Load();
        Load();
    }

    private void Load()
    {
        _list = ReferralCardDemoData.Halfway(DateTimeOffset.Now);
        Card.Referrals = _list.ToList();
        Write("Events show here.");
    }

    private static async Task SendAsync(IReadOnlyList<string> emails, CancellationToken token)
    {
        await Task.Delay(800, token);
        if (emails.Any(e => e.Contains("bounce", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("One address bounced. Nothing was sent.");
        }
    }

    /// <summary>The oldest referral that is not paid yet moves one stage on.</summary>
    private void Advance()
    {
        for (var i = _list.Count - 1; i >= 0; i--)
        {
            var referral = _list[i];
            if (referral.Stage == ReferralStage.Earned)
            {
                continue;
            }
            var next = referral.WithStage(referral.Stage + 1);
            if (next.Name is null)
            {
                next = next.WithName(NameFrom(next.Email));
            }
            _list[i] = next;
            Card.Referrals = _list.ToList();
            Write($"{next.Email} is now {next.Stage}.");
            return;
        }
        Write("Everyone has paid.");
    }

    private static string NameFrom(string email)
    {
        var local = email.Split('@')[0].Split('.', '-', '_');
        return string.Join(' ', local.Where(p => p.Length > 0).Take(2).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private void Write(string text) => Log.Text = text;
}
