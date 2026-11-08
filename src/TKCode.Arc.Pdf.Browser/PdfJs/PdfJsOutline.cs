namespace TKCode.Arc;

/// <summary>The outline from what arc-pdf.js's <c>readOutline</c> flattened.</summary>
internal static class PdfJsOutline
{
    /// <summary>As in the PDFium backend: a document is untrusted, and its outline can be absurdly large or deep.</summary>
    public const int MaxNodes = 10_000;

    /// <inheritdoc cref="MaxNodes"/>
    public const int MaxDepth = 64;

    private const int Stride = 10;

    /// <param name="titles">The titles in reading order.</param>
    /// <param name="values">Per entry, its depth and its destination (<see cref="PdfJsGeometry.Destination"/>).</param>
    /// <param name="pageCount">How many pages the document has.</param>
    public static IReadOnlyList<PdfOutlineItem> Build(string[] titles, ReadOnlySpan<double> values, int pageCount)
    {
        var root = new List<PdfOutlineItem>();
        // Who takes the children at each depth; deeper entries of a depth that is not there join the nearest one above.
        var levels = new List<List<PdfOutlineItem>> { root };
        var nodes = 0;
        for (var i = 0; i < titles.Length && (i + 1) * Stride <= values.Length && nodes < MaxNodes; i++)
        {
            var entry = values.Slice(i * Stride, Stride);
            var depth = (int)entry[0];
            if (depth < 0 || depth >= MaxDepth)
            {
                continue;
            }
            depth = Math.Min(depth, levels.Count - 1);
            levels.RemoveRange(depth + 1, levels.Count - depth - 1);
            var children = new List<PdfOutlineItem>();
            levels[depth].Add(new PdfOutlineItem(titles[i], PdfJsGeometry.Destination(entry[1..], pageCount), children));
            levels.Add(children);
            nodes++;
        }
        return root;
    }
}
