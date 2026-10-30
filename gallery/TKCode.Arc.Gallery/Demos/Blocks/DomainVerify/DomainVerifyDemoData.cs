namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Domain verify demos' data (all invented): the records a website domain and a sending domain would need, and a
/// stand-in resolver that lets them appear one by one.
/// </summary>
public static class DomainVerifyDemoData
{
    /// <summary>A CNAME to the edge, an ownership TXT and a CAA for a subdomain of a website.</summary>
    public static IReadOnlyList<DomainRecord> WebsiteRecords(string domain)
    {
        var label = domain.Split('.')[0];
        return
        [
            new("route", "CNAME", label, "edge.northwind-sites.net", "Routes traffic to Northwind"),
            new("owner", "TXT", "_northwind", "northwind-verify=7f3a91c0e2b4d658", "Proves you own the domain"),
            new("caa", "CAA", label, "0 issue \"letsencrypt.org\"", "Lets Let's Encrypt issue the certificate"),
        ];
    }

    /// <summary>MX, SPF, DKIM and DMARC for a domain that sends mail.</summary>
    public static IReadOnlyList<DomainRecord> EmailRecords(string domain) =>
    [
        new("mx", "MX", domain, "10 inbound.northwind-mail.net", "Receives replies and bounces"),
        new("spf", "TXT", domain, "v=spf1 include:spf.northwind-mail.net ~all", "Says who may send for the domain"),
        new("dkim", "CNAME", "nw1._domainkey", "nw1.dkim.northwind-mail.net", "Signs every message"),
        new("dmarc", "TXT", "_dmarc", "v=DMARC1; p=quarantine; rua=mailto:dmarc@northwind.dev", "Tells receivers what to do with fakes"),
    ];

    /// <summary>Record i becomes valid on poll i + 1, so the list fills in order; regions follow the number of valid records.</summary>
    public static DomainVerifyResult Propagation(IReadOnlyList<DomainRecord> records, int attempt)
    {
        var checks = records
            .Select((r, i) => new DomainRecordCheck(r.Id, i < attempt ? DomainRecordStatus.Valid : DomainRecordStatus.Missing))
            .ToList();
        var valid = checks.Count(c => c.Status == DomainRecordStatus.Valid);
        return new DomainVerifyResult(checks, new DomainRegions(valid == records.Count ? 6 : Math.Min(5, valid * 2), 6));
    }
}
