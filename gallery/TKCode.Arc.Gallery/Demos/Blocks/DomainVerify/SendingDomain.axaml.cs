using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DomainVerifySendingDemo : UserControl
{
    public DomainVerifySendingDemo()
    {
        InitializeComponent();

        // An email domain needs MX, SPF, DKIM and DMARC instead of a CNAME and a CAA, and no SSL certificate: the last step
        // switches delivery on, so the card's words change with Labels.
        Block.Labels = new DomainVerifyLabels
        {
            AddHeading = "Add a sending domain",
            AddHint = "Mail is sent from addresses at this domain",
            FooterRecords = "Add these records, then check that mail can be signed",
            FooterVerified = "Delivery is on",
            IssuingCertificate = "Turning on delivery",
            CertificateIssued = (issuer, renews) => $"Delivery on · signed by {issuer}",
            CertificateIssuedShort = issuer => $"Signed by {issuer}",
            CertificateAnnouncement = issuer => $"Delivery is on, signed by {issuer}",
        };

        Block.GetRecordsAction = async (domain, cancellation) =>
        {
            await Task.Delay(500, cancellation);
            return DomainVerifyDemoData.EmailRecords(domain);
        };
        Block.VerifyAction = async (domain, records, context, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            return DomainVerifyDemoData.Propagation(records, context.Attempt + 1);
        };
        Block.IssueCertificateAction = async (domain, cancellation) =>
        {
            await Task.Delay(900, cancellation);
            return new DomainCertificate("Northwind Mail", "yearly");
        };
        Block.PollInterval = TimeSpan.FromMilliseconds(700);
    }
}
