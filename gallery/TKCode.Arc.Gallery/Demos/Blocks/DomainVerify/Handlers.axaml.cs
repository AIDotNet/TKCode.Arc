using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DomainVerifyHandlersDemo : UserControl
{
    public DomainVerifyHandlersDemo()
    {
        InitializeComponent();

        Block.GetRecordsAction = async (domain, cancellation) =>
        {
            await Task.Delay(600, cancellation);
            return DomainVerifyDemoData.WebsiteRecords(domain);
        };

        // The resolver pool is busy during the first run, so that check throws; the second run sees the records
        // spread one by one over three polls.
        Block.VerifyAction = async (domain, records, context, cancellation) =>
        {
            await Task.Delay(500, cancellation);
            if (context.Run == 1)
            {
                throw new InvalidOperationException("The resolver pool is busy right now. Check again in a moment.");
            }
            return DomainVerifyDemoData.Propagation(records, context.Attempt);
        };

        Block.IssueCertificateAction = async (domain, cancellation) =>
        {
            await Task.Delay(1200, cancellation);
            return new DomainCertificate("Let's Encrypt", "in 60 days");
        };

        Block.PollInterval = TimeSpan.FromMilliseconds(900);
        Block.PhaseChanged += (_, e) => Log.Text = $"Phase: {e.OldPhase} → {e.NewPhase}";
        Block.Failed += (_, e) => Log.Text = $"{e.Domain}: {e.Exception.Message}";
        Block.Verified += (_, e) => Log.Text = $"{e.Domain} is live, certificate by {e.Certificate.Issuer}, renews {e.Certificate.Renews}.";
        Block.Removed += (_, e) => Log.Text = $"{e.Domain} was removed.";
    }
}
