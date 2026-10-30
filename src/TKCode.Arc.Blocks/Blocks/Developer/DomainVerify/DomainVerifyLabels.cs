namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="DomainVerify"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; formatted texts are functions so that word order can change.
/// </summary>
public sealed class DomainVerifyLabels
{
    /// <summary>The label of the domain field.</summary>
    public string DomainLabel { get; init; } = "Domain";

    /// <summary>The domain field's placeholder.</summary>
    public string DomainPlaceholder { get; init; } = "app.example.com";

    /// <summary>The heading of the add area.</summary>
    public string AddHeading { get; init; } = "Add a domain";

    /// <summary>The line under <see cref="AddHeading"/>.</summary>
    public string AddHint { get; init; } = "Use a domain or subdomain you own";

    /// <summary>The add button.</summary>
    public string AddDomain { get; init; } = "Add domain";

    /// <summary>The add button while the records are fetched.</summary>
    public string Adding { get; init; } = "Adding";

    /// <summary>The error under a domain that is not valid.</summary>
    public string DomainInvalid { get; init; } = "Enter a domain like app.example.com.";

    /// <summary>The error when fetching the records failed and the exception has no message.</summary>
    public string AddFailed { get; init; } = "The records could not be loaded. Try again.";

    /// <summary>The accessible name of the records list.</summary>
    public string ListName { get; init; } = "DNS records";

    /// <summary>The column headers of the records.</summary>
    public string TypeHeader { get; init; } = "Type";

    /// <summary>The name column header.</summary>
    public string NameHeader { get; init; } = "Name";

    /// <summary>The value column header.</summary>
    public string ValueHeader { get; init; } = "Value";

    /// <summary>The status column header.</summary>
    public string StatusHeader { get; init; } = "Status";

    /// <summary>The text over the placeholder rows.</summary>
    public string EmptyState { get; init; } = "DNS records appear here after you add a domain";

    /// <summary>A record that no check has looked at.</summary>
    public string NotChecked { get; init; } = "Not checked";

    /// <summary>A record whose check is running.</summary>
    public string Checking { get; init; } = "Checking";

    /// <summary>A record that holds the expected value.</summary>
    public string Valid { get; init; } = "Valid";

    /// <summary>A record that was not found.</summary>
    public string NotFound { get; init; } = "Not found";

    /// <summary>A record with a conflicting value.</summary>
    public string Misconfigured { get; init; } = "Misconfigured";

    /// <summary>The label before the conflicting value of a record.</summary>
    public string FoundLabel { get; init; } = "Found";

    /// <summary>The label before the fix of a record.</summary>
    public string FixLabel { get; init; } = "Fix";

    /// <summary>The accessible name of a record's copy button; the argument is the record type.</summary>
    public Func<string, string> CopyName { get; init; } = type => $"Copy {type} value";

    /// <summary>The copy button's text while it shows the copied state.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The line naming the DNS host; the argument is its name.</summary>
    public Func<string, string> ProviderLine { get; init; } = name => $"DNS hosted at {name}";

    /// <summary>The shorter provider line of a narrow card.</summary>
    public Func<string, string> ProviderLineShort { get; init; } = name => $"DNS at {name}";

    /// <summary>The domain row's status before a check ran.</summary>
    public string DomainWaiting { get; init; } = "Waiting for DNS records";

    /// <summary>The domain row's status while checking.</summary>
    public string DomainChecking { get; init; } = "Checking DNS";

    /// <summary>The domain row's status when a record needs a fix.</summary>
    public string DomainNeedsFix { get; init; } = "Needs a fix";

    /// <summary>The domain row's status once verified.</summary>
    public string DomainVerified { get; init; } = "Verified";

    /// <summary>The domain row's detail while the certificate is issued.</summary>
    public string IssuingCertificate { get; init; } = "Issuing SSL certificate";

    /// <summary>The domain row's detail once it is issued; the arguments are the issuer and the renewal text.</summary>
    public Func<string, string, string> CertificateIssued { get; init; } =
        (issuer, renews) => $"SSL by {issuer} · renews {renews}";

    /// <summary>The narrow form of <see cref="CertificateIssued"/>; the argument is the issuer.</summary>
    public Func<string, string> CertificateIssuedShort { get; init; } = issuer => $"SSL by {issuer}";

    /// <summary>The footer text in the records phase.</summary>
    public string FooterRecords { get; init; } = "Add these records at your DNS provider, then check";

    /// <summary>The footer text while checking; the arguments are the regions that propagated and the total (0 when unknown).</summary>
    public Func<int, int, string> FooterChecking { get; init; } = (done, total) =>
        total > 0 ? $"Checking records · {done} of {total} regions propagated" : "Checking records";

    /// <summary>The footer text when a record needs a fix.</summary>
    public string FooterInvalid { get; init; } = "Fix the marked record, then check again";

    /// <summary>The footer text once verified.</summary>
    public string FooterVerified { get; init; } = "Your domain is live";

    /// <summary>The footer button that starts a check.</summary>
    public string CheckRecords { get; init; } = "Check records";

    /// <summary>The footer button after a record was found misconfigured.</summary>
    public string CheckAgain { get; init; } = "Check again";

    /// <summary>The footer button once verified.</summary>
    public string AddAnother { get; init; } = "Add another";

    /// <summary>The accessible name of the remove button; the argument is the domain.</summary>
    public Func<string, string> RemoveName { get; init; } = domain => $"Remove {domain}";

    /// <summary>The announcement of a simulated flow, added to each step.</summary>
    public string SimulatedNote { get; init; } = "Simulated, no DNS was queried";

    /// <summary>The announcement after the domain was added; the argument is the domain.</summary>
    public Func<string, string> AddedAnnouncement { get; init; } = domain => $"{domain} added. DNS records are listed.";

    /// <summary>The announcement when a check starts.</summary>
    public string CheckingAnnouncement { get; init; } = "Checking DNS records";

    /// <summary>The announcement when a record needs a fix; the argument is the record's type.</summary>
    public Func<string, string> NeedsFixAnnouncement { get; init; } = type => $"The {type} record needs a fix";

    /// <summary>The announcement when every record is valid; the argument is the domain.</summary>
    public Func<string, string> VerifiedAnnouncement { get; init; } = domain => $"{domain} is verified";

    /// <summary>The announcement when the certificate was issued; the argument is the issuer.</summary>
    public Func<string, string> CertificateAnnouncement { get; init; } = issuer => $"SSL certificate issued by {issuer}";

    /// <summary>The announcement when a handler failed; the argument is the message.</summary>
    public Func<string, string> FailedAnnouncement { get; init; } = message => $"Check failed. {message}";

    /// <summary>The announcement after the domain was removed.</summary>
    public string RemovedAnnouncement { get; init; } = "Domain removed";

    /// <summary>The message shown when a handler threw without a message.</summary>
    public string CheckFailed { get; init; } = "The check could not run. Try again.";
}
