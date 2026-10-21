using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Lucide glyphs <see cref="BillingOverview"/> needs that <see cref="ArcIcons"/> does not include.</summary>
internal static class BillingOverviewGlyphs
{
    /// <summary>Lucide <c>receipt-text</c>: the empty invoice list.</summary>
    public static Geometry ReceiptText =>
        ArcIcons.Parse("M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z M14 8H8 M16 12H8 M13 16H8");
}
