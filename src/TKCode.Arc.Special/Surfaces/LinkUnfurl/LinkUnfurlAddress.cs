using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The address rules of <see cref="LinkUnfurl"/> (Arc Link unfurl, design reference): what counts as a link (an
/// <c>http://</c> or <c>https://</c> address or one starting with <c>www.</c>, whose host has a dot and a top-level domain
/// of at least two letters, so <c>https://x</c> and <c>localhost:3000</c> stay text), the short form a loading link shows
/// (no scheme, no <c>www.</c>) and the plain preview built from an address when no resolver answers with a richer one.
/// </summary>
internal static class LinkUnfurlAddress
{
    /// <summary>
    /// True when <paramref name="text"/> (trimmed) is one address; <paramref name="url"/> is it with <c>https://</c> before a
    /// bare <c>www.</c>.
    /// </summary>
    public static bool TryParse(string? text, out string url)
    {
        url = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var value = text.Trim();
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }
        string rest;
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            rest = value[8..];
        }
        else if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            rest = value[7..];
        }
        else if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            rest = value;
            value = "https://" + value;
        }
        else
        {
            return false;
        }
        var host = HostOf(rest);
        var colon = host.LastIndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }
        var dot = host.LastIndexOf('.');
        if (dot <= 0 || dot == host.Length - 1)
        {
            return false;
        }
        var tld = host[(dot + 1)..];
        if (tld.Length < 2)
        {
            return false;
        }
        foreach (var c in tld)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }
        foreach (var c in host)
        {
            if (!(char.IsLetterOrDigit(c) || c is '.' or '-'))
            {
                return false;
            }
        }
        url = value;
        return true;
    }

    /// <summary>The address without its scheme and without a leading <c>www.</c>: <c>ridgeway.trails/routes/48213907</c>.</summary>
    public static string ShortForm(string url)
    {
        var rest = StripScheme(url);
        if (rest.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[4..];
        }
        return rest;
    }

    /// <summary>The host without its port and without a leading <c>www.</c>, in lower case: <c>ridgeway.trails</c>.</summary>
    public static string Host(string url)
    {
        var host = HostOf(StripScheme(url));
        var colon = host.LastIndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }
        return host.ToLowerInvariant();
    }

    /// <summary>
    /// The title of a plain preview: the last path segment without its extension, with <c>-</c> and <c>_</c> read as
    /// spaces and the first letter in upper case (<c>/guides/my-trip_plan.html</c> → "My trip plan"); the host when the
    /// address has no path.
    /// </summary>
    public static string PlainTitle(string url)
    {
        var rest = StripScheme(url);
        var end = rest.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            rest = rest[..end];
        }
        var slash = rest.IndexOf('/');
        var path = slash >= 0 ? rest[(slash + 1)..] : string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return Host(url);
        }
        var segment = Uri.UnescapeDataString(segments[^1]);
        var dot = segment.LastIndexOf('.');
        if (dot > 0)
        {
            segment = segment[..dot];
        }
        var builder = new StringBuilder(segment.Length);
        foreach (var c in segment)
        {
            builder.Append(c is '-' or '_' ? ' ' : c);
        }
        var title = builder.ToString().Trim();
        if (title.Length == 0)
        {
            return Host(url);
        }
        return char.ToUpperInvariant(title[0]) + title[1..];
    }

    /// <summary>
    /// The preview built from the address alone: site = host, title = <see cref="PlainTitle"/>, description = the short
    /// form (host, path and query), no image and no favicon (the card draws the host's initial).
    /// </summary>
    public static LinkUnfurlPreview PlainPreview(string url) =>
        new(url, Host(url), PlainTitle(url)) { Description = ShortForm(url) };

    /// <summary>Two addresses name the same page when they match without scheme, <c>www.</c> and a trailing slash, ignoring case.</summary>
    public static bool SameAddress(string a, string b) =>
        string.Equals(ShortForm(a).TrimEnd('/'), ShortForm(b).TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static string StripScheme(string url)
    {
        var at = url.IndexOf("://", StringComparison.Ordinal);
        return at >= 0 ? url[(at + 3)..] : url;
    }

    private static string HostOf(string rest)
    {
        var end = rest.IndexOfAny(['/', '?', '#']);
        return end >= 0 ? rest[..end] : rest;
    }
}
