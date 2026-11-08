using Avalonia;

namespace TKCode.Arc;

/// <summary>Links from what arc-pdf.js's <c>readLinks</c> flattened.</summary>
internal static class PdfJsLinks
{
    private const int Stride = 13;

    /// <param name="uris">Per link, the address; empty for an internal one.</param>
    /// <param name="values">Per link, its rectangle in page space (four numbers) and its destination (<see cref="PdfJsGeometry.Destination"/>).</param>
    /// <param name="view">The view of the page the links are on.</param>
    /// <param name="pageCount">How many pages the document has.</param>
    public static IReadOnlyList<PdfLink> Build(string[] uris, ReadOnlySpan<double> values, ReadOnlySpan<double> view, int pageCount)
    {
        var links = new List<PdfLink>(uris.Length);
        for (var i = 0; i < uris.Length && (i + 1) * Stride <= values.Length; i++)
        {
            var link = values.Slice(i * Stride, Stride);
            var uri = Uri.TryCreate(uris[i].Trim(), UriKind.Absolute, out var parsed) ? parsed : null;
            var destination = uri is null ? PdfJsGeometry.Destination(link[4..], pageCount) : null;
            if (uri is not null || destination is not null)
            {
                links.Add(new PdfLink(PdfJsGeometry.MapRect(view, link[0], link[1], link[2], link[3]), destination, uri));
            }
        }
        return links;
    }
}
