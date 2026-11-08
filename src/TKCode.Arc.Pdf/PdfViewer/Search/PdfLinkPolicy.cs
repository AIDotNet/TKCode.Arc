namespace TKCode.Arc;

/// <summary>Which links of a document may leave it. A PDF is untrusted input: anything not on this list is never handed to the operating system.</summary>
internal static class PdfLinkPolicy
{
    /// <summary>
    /// True for an absolute <c>http</c>, <c>https</c> or <c>mailto</c> URI. The scheme is compared whole on the parsed URI, in any
    /// case: <c>httpx:</c> or <c>http.evil:</c> only look like the real thing, and a string that did not parse never gets this far.
    /// </summary>
    public static bool IsAllowed(Uri? uri)
    {
        if (uri is not { IsAbsoluteUri: true })
        {
            return false;
        }
        var scheme = uri.Scheme;
        return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || string.Equals(scheme, Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
    }
}
