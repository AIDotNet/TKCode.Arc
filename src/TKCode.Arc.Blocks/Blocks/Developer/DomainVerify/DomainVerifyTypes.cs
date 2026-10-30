using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="DomainVerify"/> card is in its flow.</summary>
public enum DomainVerifyPhase
{
    /// <summary>The domain field and the "Add domain" button; placeholder rows behind an empty state.</summary>
    Enter,

    /// <summary>The records of the domain are being fetched.</summary>
    Adding,

    /// <summary>The records are listed and wait for the person to copy them and start a check.</summary>
    Records,

    /// <summary>The resolvers are being polled.</summary>
    Checking,

    /// <summary>A record holds a conflicting value; its row shows what was found and how to fix it.</summary>
    Invalid,

    /// <summary>Every record is valid; the certificate is being issued or was issued.</summary>
    Verified,
}

/// <summary>The outcome of one record in a check.</summary>
public enum DomainRecordStatus
{
    /// <summary>The record holds the expected value.</summary>
    Valid,

    /// <summary>The record is not there (yet); the card keeps polling while it propagates.</summary>
    Missing,

    /// <summary>The record exists with a conflicting value; polling stops.</summary>
    Invalid,
}

/// <summary>A DNS record the customer has to create.</summary>
/// <param name="Id">Identifies the record in a <see cref="DomainRecordCheck"/>.</param>
/// <param name="Type">The record type shown in the first column ("CNAME", "TXT", "CAA", "MX").</param>
/// <param name="Name">The host name (the record's name field at the DNS provider).</param>
/// <param name="Value">The value to copy; the copy button always copies all of it.</param>
/// <param name="Purpose">A short line under the record saying what it is for; null leaves it out.</param>
public sealed record DomainRecord(string Id, string Type, string Name, string Value, string? Purpose = null);

/// <summary>What one check found for one record.</summary>
/// <param name="Id">The <see cref="DomainRecord.Id"/> it is about.</param>
/// <param name="Status">Valid, still missing or conflicting.</param>
/// <param name="Detail">An optional one-line note shown under the record.</param>
/// <param name="Found">The conflicting value that was found (for <see cref="DomainRecordStatus.Invalid"/>).</param>
/// <param name="Fix">A one-sentence fix (for <see cref="DomainRecordStatus.Invalid"/>).</param>
public sealed record DomainRecordCheck(string Id, DomainRecordStatus Status, string? Detail = null, string? Found = null,
    string? Fix = null);

/// <summary>How far a record has spread over the resolvers that were asked.</summary>
/// <param name="Propagated">Resolvers that answer with the new value.</param>
/// <param name="Total">Resolvers that were asked.</param>
public readonly record struct DomainRegions(int Propagated, int Total);

/// <summary>The result of one check: a verdict per record and the propagation count.</summary>
/// <param name="Records">One entry per record; a record without an entry counts as missing.</param>
/// <param name="Regions">The propagation count shown in the footer.</param>
public sealed record DomainVerifyResult(IReadOnlyList<DomainRecordCheck> Records, DomainRegions Regions);

/// <summary>Which check <see cref="DomainVerify.VerifyAction"/> is running.</summary>
/// <param name="Attempt">The poll within the run, from 1.</param>
/// <param name="Run">The run (a press of the check button), from 1.</param>
public readonly record struct DomainVerifyContext(int Attempt, int Run);

/// <summary>The certificate that was issued once every record was valid.</summary>
/// <param name="Issuer">The certificate authority ("Let's Encrypt").</param>
/// <param name="Renews">When it renews, as text ("in 60 days").</param>
public sealed record DomainCertificate(string Issuer, string Renews);

/// <summary>Who hosts the domain's DNS, shown as "DNS hosted at {name}" with a small mark.</summary>
/// <param name="Name">The provider's name.</param>
/// <param name="Mark">A 14px glyph (24x24 stroke geometry) drawn before the name; null shows the name alone.</param>
public sealed record DomainProvider(string Name, Geometry? Mark = null);

/// <summary>Arguments of <see cref="DomainVerify.PhaseChanged"/>.</summary>
public class DomainVerifyPhaseChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DomainVerifyPhaseChangedEventArgs(RoutedEvent routedEvent, object source, DomainVerifyPhase oldPhase,
        DomainVerifyPhase newPhase)
        : base(routedEvent, source)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }

    /// <summary>The phase before.</summary>
    public DomainVerifyPhase OldPhase { get; }

    /// <summary>The phase now.</summary>
    public DomainVerifyPhase NewPhase { get; }
}

/// <summary>Arguments of <see cref="DomainVerify.Verified"/>.</summary>
public class DomainVerifiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DomainVerifiedEventArgs(RoutedEvent routedEvent, object source, string domain, DomainCertificate certificate)
        : base(routedEvent, source)
    {
        Domain = domain;
        Certificate = certificate;
    }

    /// <summary>The verified domain.</summary>
    public string Domain { get; }

    /// <summary>The certificate that was issued.</summary>
    public DomainCertificate Certificate { get; }
}

/// <summary>Arguments of <see cref="DomainVerify.Removed"/>.</summary>
public class DomainRemovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DomainRemovedEventArgs(RoutedEvent routedEvent, object source, string domain)
        : base(routedEvent, source)
    {
        Domain = domain;
    }

    /// <summary>The domain that was removed.</summary>
    public string Domain { get; }
}

/// <summary>Arguments of <see cref="DomainVerify.Failed"/>.</summary>
public class DomainVerifyFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public DomainVerifyFailedEventArgs(RoutedEvent routedEvent, object source, string domain, Exception exception)
        : base(routedEvent, source)
    {
        Domain = domain;
        Exception = exception;
    }

    /// <summary>The domain being set up.</summary>
    public string Domain { get; }

    /// <summary>What a handler threw.</summary>
    public Exception Exception { get; }
}
