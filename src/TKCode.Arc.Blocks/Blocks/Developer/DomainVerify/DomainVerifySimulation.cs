using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The domain rules and the stand-in server of <see cref="DomainVerify"/>: turning what was typed into a bare host name,
/// the records a domain needs, and a scripted check that runs while no <see cref="DomainVerify.VerifyAction"/> is set.
/// Pure functions, so they are tested without a window.
/// </summary>
internal static class DomainVerifySimulation
{
    /// <summary>
    /// Reduces typed text to a host name: trims, lowers, drops a protocol, credentials, path, query, port and trailing
    /// dots. Returns an empty string for empty input.
    /// </summary>
    public static string Normalize(string? text)
    {
        var s = (text ?? string.Empty).Trim().ToLowerInvariant();
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            s = s[(scheme + 3)..];
        }
        var end = s.IndexOfAny(['/', '?', '#']);
        if (end >= 0)
        {
            s = s[..end];
        }
        var at = s.LastIndexOf('@');
        if (at >= 0)
        {
            s = s[(at + 1)..];
        }
        var colon = s.LastIndexOf(':');
        if (colon >= 0 && s[(colon + 1)..].All(char.IsAsciiDigit))
        {
            s = s[..colon];
        }
        return s.TrimEnd('.');
    }

    /// <summary>Whether <paramref name="host"/> (already normalized) is a name with at least two labels and a letter-only top level.</summary>
    public static bool IsValid(string host)
    {
        if (host.Length is 0 or > 253)
        {
            return false;
        }
        var labels = host.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }
        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63 || label[0] == '-' || label[^1] == '-'
                || !label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }
        }
        return labels[^1].Length >= 2 && labels[^1].All(char.IsAsciiLetter);
    }

    /// <summary>Whether the host is a registrable name itself (two labels) rather than a subdomain.</summary>
    public static bool IsApex(string host) => host.Split('.').Length == 2;

    /// <summary>The records a custom domain needs: where it points, who owns it and who may issue its certificate.</summary>
    public static IReadOnlyList<DomainRecord> DefaultRecords(string host)
    {
        var apex = IsApex(host);
        return
        [
            apex
                ? new DomainRecord("route", "A", "@", "203.0.113.10", "Routes traffic to your workspace")
                : new DomainRecord("route", "CNAME", host.Split('.')[0], "edge.arc-sample.net", "Routes traffic to your workspace"),
            new DomainRecord("owner", "TXT", "_arc-verify", $"arc-verify={Token(host)}", "Proves you own the domain"),
            new DomainRecord("caa", "CAA", apex ? "@" : host.Split('.')[0], "0 issue \"letsencrypt.org\"",
                "Lets Let's Encrypt issue the certificate"),
        ];
    }

    /// <summary>A stable 16-character hex token for a host (FNV-1a, so the same host always gets the same value).</summary>
    public static string Token(string host)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(host))
        {
            hash = (hash ^ b) * 1099511628211UL;
        }
        return hash.ToString("x16");
    }

    /// <summary>
    /// The scripted check. Run 1: the first record is valid at once, the others are missing; on the second poll the middle
    /// ones are valid and the last record (with three or more records) holds a conflicting value, which stops the run.
    /// Run 2 and later: everything is valid.
    /// </summary>
    public static DomainVerifyResult Check(IReadOnlyList<DomainRecord> records, DomainVerifyContext context)
    {
        var checks = new List<DomainRecordCheck>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var last = i == records.Count - 1 && records.Count >= 3;
            if (context.Run >= 2 || i == 0 || (context.Attempt >= 2 && !last))
            {
                checks.Add(new DomainRecordCheck(record.Id, DomainRecordStatus.Valid));
            }
            else if (context.Attempt >= 2 && last)
            {
                checks.Add(new DomainRecordCheck(record.Id, DomainRecordStatus.Invalid,
                    Found: "0 issue \"other-ca.example\"",
                    Fix: $"Replace the {record.Type} value with the one above so the certificate can be issued."));
            }
            else
            {
                checks.Add(new DomainRecordCheck(record.Id, DomainRecordStatus.Missing));
            }
        }
        var valid = checks.Count(c => c.Status == DomainRecordStatus.Valid);
        var regions = context.Run >= 2 ? 6 : Math.Min(5, valid * 2);
        return new DomainVerifyResult(checks, new DomainRegions(regions, 6));
    }
}
