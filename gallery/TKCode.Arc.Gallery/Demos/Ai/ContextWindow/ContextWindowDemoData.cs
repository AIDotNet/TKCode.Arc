using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An invented session of a documentation assistant, for the ContextWindow demos (made-up models, files and numbers).</summary>
internal static class ContextWindowDemoData
{
    private static readonly IBrush s_heronMark = new ImmutableSolidColorBrush(Color.Parse("#009E84"));

    public static IEnumerable<ContextWindowModel> Models() =>
    [
        new ContextWindowModel("heron-2", "Heron 2", 256_000, 16_000, ArcIcons.BookOpen, s_heronMark),
        new ContextWindowModel("heron-2-mini", "Heron 2 Mini", 128_000, 8_000, ArcIcons.BookOpen)
    ];

    public static IEnumerable<ContextWindowItem> Items() =>
    [
        new ContextWindowItem(ContextWindowCategory.System, "Style guide and instructions", 2_400, "system"),
        new ContextWindowItem(ContextWindowCategory.Tools, "search_handbook", 3_100, "tool-search"),
        new ContextWindowItem(ContextWindowCategory.Tools, "open_page", 1_900, "tool-open"),
        new ContextWindowItem(ContextWindowCategory.Files, "handbook/billing.md", 14_600, "file-billing"),
        new ContextWindowItem(ContextWindowCategory.Files, "handbook/refunds.md", 9_800, "file-refunds"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Outline the refunds page", 8_200, "m1"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Add the partial refund rules", 11_400, "m2"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Check the wording against the style guide", 6_900, "m3"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Draft the FAQ section", 12_700, "m4"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Link the billing page", 5_300, "m5"),
        new ContextWindowItem(ContextWindowCategory.Messages, "Shorten the introduction", 7_600, "m6")
    ];

    public static IEnumerable<ContextWindowTurn> Turns() =>
    [
        new ContextWindowTurn("Write the cancellation section", 14_800),
        new ContextWindowTurn("Turn the table into steps", 9_600),
        new ContextWindowTurn("Find every mention of store credit", 17_200),
        new ContextWindowTurn("Proofread the whole page", 21_500)
    ];

    public static IEnumerable<ContextWindowTurn> Files() =>
    [
        new ContextWindowTurn("handbook/invoices.md", 18_300),
        new ContextWindowTurn("exports/tickets-march.csv", 26_400)
    ];

    /// <summary>Replies of different sizes for the demo that adds its own items.</summary>
    public static readonly (string Label, long Tokens)[] Replies =
    [
        ("Summarize the open tickets", 9_400),
        ("Group the tickets by cause", 13_800),
        ("Draft a reply for the top issue", 7_200),
        ("Compare with last month", 16_500)
    ];
}
