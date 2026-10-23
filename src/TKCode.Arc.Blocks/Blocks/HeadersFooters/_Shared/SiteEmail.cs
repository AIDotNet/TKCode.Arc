// Ported from Arc's free site-footer / newsletter-signup blocks (MIT, Copyright (c) 2026 Elia Kuratli): the email pattern.
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>Email address check shared by the footer newsletter and the newsletter signup (Arc's <c>EMAIL</c> pattern).</summary>
internal static partial class SiteEmail
{
    /// <summary>True when the trimmed text looks like <c>name@domain.tld</c> (no spaces, one <c>@</c>, a top level domain of two or more characters).</summary>
    public static bool IsValid(string? text) => !string.IsNullOrWhiteSpace(text) && Pattern().IsMatch(text.Trim());

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
