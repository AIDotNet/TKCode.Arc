using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>The pure logic of <see cref="ReferralCard"/>: addresses, amounts, the link's parts, share URLs and counts.</summary>
internal static class ReferralCardFormat
{
    /// <summary>What separates addresses when typed or pasted: commas, semicolons, spaces, tabs and new lines.</summary>
    public static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Whether <paramref name="text"/> reads as an email address: one @, a local part, a domain with a dot and a top level of
    /// at least two letters, no spaces, no empty labels. Deliberately simple: the server has the last word.
    /// </summary>
    public static bool IsEmail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 254)
        {
            return false;
        }
        var at = text.IndexOf('@');
        if (at <= 0 || at != text.LastIndexOf('@') || at == text.Length - 1)
        {
            return false;
        }
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c is '<' or '>' or '(' or ')' or '[' or ']' or '\\' or '"' or ',' or ';' or ':')
            {
                return false;
            }
        }
        var local = text[..at];
        var domain = text[(at + 1)..];
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }
        var labels = domain.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }
        foreach (var label in labels)
        {
            if (label.Length == 0 || label.StartsWith('-') || label.EndsWith('-'))
            {
                return false;
            }
        }
        var top = labels[^1];
        if (top.Length < 2)
        {
            return false;
        }
        foreach (var c in top)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Splits typed or pasted text into trimmed, non-empty pieces.</summary>
    public static List<string> Split(string? text)
    {
        var pieces = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return pieces;
        }
        foreach (var piece in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var clean = piece.Trim('<', '>', '"', '\'');
            if (clean.Length > 0)
            {
                pieces.Add(clean);
            }
        }
        return pieces;
    }

    /// <summary>The symbol written before an amount for an ISO 4217 code, or null when it is written after as the code.</summary>
    public static string? CurrencySymbol(string? currency) => (currency ?? "USD").ToUpperInvariant() switch
    {
        "USD" or "AUD" or "CAD" or "NZD" or "SGD" or "HKD" or "MXN" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" or "CNY" => "¥",
        "INR" => "₹",
        "KRW" => "₩",
        "BRL" => "R$",
        "CHF" => "CHF ",
        _ => null
    };

    /// <summary>A whole amount in a currency: "$20", "€1,240", "20 SEK".</summary>
    public static string Money(long amount, string? currency)
    {
        var digits = amount.ToString("#,0", s_en);
        var symbol = CurrencySymbol(currency);
        return symbol is not null ? symbol + digits : digits + " " + (currency ?? string.Empty).ToUpperInvariant();
    }

    /// <summary>The prefix and suffix around the rolling digits of an amount.</summary>
    public static (string? Prefix, string? Suffix) MoneyAffixes(string? currency)
    {
        var symbol = CurrencySymbol(currency);
        return symbol is not null ? (symbol, null) : (null, " " + (currency ?? string.Empty).ToUpperInvariant());
    }

    /// <summary>
    /// The link as shown: the part up to and including the last slash without the scheme (muted), then the code (full color).
    /// "https://app.example.com/r/emma20" reads "app.example.com/r/" and "emma20".
    /// </summary>
    public static (string Domain, string Code) SplitLink(string? link)
    {
        var text = (link ?? string.Empty).Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            text = text[(scheme + 3)..];
        }
        var slash = text.TrimEnd('/').LastIndexOf('/');
        return slash < 0 ? (string.Empty, text) : (text[..(slash + 1)], text[(slash + 1)..]);
    }

    /// <summary>
    /// The share URL for a channel. Email builds a <c>mailto:</c> link whose body is the message and the link. Post and
    /// Network fill <paramref name="urlFormat"/> (<c>{url}</c> and <c>{text}</c> are replaced, escaped); without a format
    /// they return the link itself.
    /// </summary>
    public static string ShareUrl(ReferralShareChannel channel, string link, string? message, string? urlFormat, string? subject)
    {
        link ??= string.Empty;
        if (channel == ReferralShareChannel.Email)
        {
            var body = string.IsNullOrWhiteSpace(message) ? link : message.Trim() + "\n\n" + link;
            var builder = new StringBuilder("mailto:?");
            if (!string.IsNullOrWhiteSpace(subject))
            {
                builder.Append("subject=").Append(Uri.EscapeDataString(subject.Trim())).Append('&');
            }
            builder.Append("body=").Append(Uri.EscapeDataString(body));
            return builder.ToString();
        }
        if (string.IsNullOrWhiteSpace(urlFormat))
        {
            return link;
        }
        return urlFormat
            .Replace("{url}", Uri.EscapeDataString(link), StringComparison.Ordinal)
            .Replace("{text}", Uri.EscapeDataString(message ?? string.Empty), StringComparison.Ordinal);
    }

    /// <summary>The funnel: everyone invited, those who signed up (paid ones included) and those who paid.</summary>
    public static (int Invited, int SignedUp, int Earned) Count(IReadOnlyList<Referral> referrals)
    {
        var signed = 0;
        var earned = 0;
        foreach (var referral in referrals)
        {
            if (referral.Stage >= ReferralStage.SignedUp)
            {
                signed++;
            }
            if (referral.Stage == ReferralStage.Earned)
            {
                earned++;
            }
        }
        return (referrals.Count, signed, earned);
    }

    /// <summary>The most segments the milestone bar draws.</summary>
    public const int MaxSegments = 12;

    /// <summary>How many segments a goal draws (1 to 12).</summary>
    public static int Segments(int goal) => Math.Clamp(goal, 1, MaxSegments);

    /// <summary>The filled share of the segments (0 to the segment count, fractional for a goal above 12).</summary>
    public static double Filled(int paid, int goal)
    {
        var safeGoal = Math.Max(1, goal);
        var segments = Segments(safeGoal);
        return Math.Clamp(paid, 0, safeGoal) * (double)segments / safeGoal;
    }

    /// <summary>The name shown for a referral: its name, or the address.</summary>
    public static string Who(Referral referral) =>
        string.IsNullOrWhiteSpace(referral.Name) ? referral.Email : referral.Name.Trim();

    /// <summary>The name the avatar's initials come from: the name, or the address's local part split at dots and dashes.</summary>
    public static string AvatarName(Referral referral)
    {
        if (!string.IsNullOrWhiteSpace(referral.Name))
        {
            return referral.Name.Trim();
        }
        var email = referral.Email ?? string.Empty;
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        return local.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Replace('+', ' ');
    }
}
