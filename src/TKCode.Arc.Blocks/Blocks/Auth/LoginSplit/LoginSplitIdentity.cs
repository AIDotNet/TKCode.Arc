using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Pure rules of <see cref="LoginSplit"/>: how an address becomes an account, how known accounts are matched, and how the
/// reset countdown reads. Kept apart from the control so tests check them against the reference preview's examples.
/// </summary>
internal static class LoginSplitIdentity
{
    private static readonly char[] s_separators = ['.', '_', '+', '-'];

    /// <summary>The address as the block keeps it: trimmed and lower-cased ("  Abc.Def@Example.COM " → "abc.def@example.com").</summary>
    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// A name from the part before the @: split at dots, underscores, plus signs and hyphens, each piece capitalized
    /// ("abcx.iod" → "Abcx Iod", "maya" → "Maya"). Empty parts are skipped; "member" when nothing is left.
    /// </summary>
    public static string NameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        var local = at >= 0 ? email[..at] : email;
        var builder = new StringBuilder();
        foreach (var part in local.Split(s_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }
            builder.Append(char.ToUpper(part[0], CultureInfo.InvariantCulture)).Append(part.AsSpan(1));
        }
        return builder.Length > 0 ? builder.ToString() : "Member";
    }

    /// <summary>The first word of a name ("Rowan Hale" → "Rowan"); the whole text when it has no space.</summary>
    public static string FirstWord(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }

    /// <summary>The listed account whose email matches <paramref name="email"/> (case-insensitive), or a derived one.</summary>
    public static LoginSplitAccount Resolve(string email, IEnumerable<LoginSplitAccount> known)
    {
        var normalized = Normalize(email);
        foreach (var account in known)
        {
            if (string.Equals(Normalize(account.Email), normalized, StringComparison.Ordinal))
            {
                return account;
            }
        }
        return LoginSplitAccount.FromEmail(normalized);
    }

    /// <summary>The countdown as Arc reads it: minutes without padding, seconds in two digits ("0:30", "0:09", "1:05").</summary>
    public static string Countdown(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }
}
