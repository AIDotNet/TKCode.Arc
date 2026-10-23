using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The default story of a <see cref="MixtureOfExperts"/> (invented for TKCode.Arc): eight experts with one-word names and
/// fifteen tokens of a short sentence that mixes a name, a verb, code, symbols, a number and a date. With the default
/// capacity factor of 1, top 2 makes 28 routes with two second choices skipped for capacity, and top 1 lets the final
/// period pass through. The numbers are illustrative, not measurements of a real model.
/// </summary>
public static class MixtureOfExpertsSamples
{
    /// <summary>A new list of the eight default experts (Syntax, Digits, Marks, People, Glue, Actions, Time, Math).</summary>
    public static IReadOnlyList<MixtureOfExpertsExpert> CreateExperts() =>
    [
        new("Syntax", "Brackets, identifiers and language names.", MixtureOfExpertsIcons.Braces),
        new("Digits", "Whole numbers and digits inside dates.", MixtureOfExpertsIcons.Binary),
        new("Marks", "Commas, parentheses and sentence ends.", ArcIcons.Pilcrow),
        new("People", "Names of people, places and products.", MixtureOfExpertsIcons.Contact),
        new("Glue", "Short function words that join a sentence.", ArcIcons.Link),
        new("Actions", "Verbs and things someone does.", MixtureOfExpertsIcons.Rocket),
        new("Time", "Months, weekdays and relative times.", ArcIcons.Clock),
        new("Math", "Operators, variables and equations.", MixtureOfExpertsIcons.SquareFunction),
    ];

    /// <summary>A new list of the fifteen default tokens with their raw gate scores (in the order of the default experts).</summary>
    public static IReadOnlyList<MixtureOfExpertsTokenItem> CreateTokens() =>
    [
        new("Ada", 24, 6, 3, 58, 7, 3, 5, 4),
        new("wrote", 5, 3, 6, 4, 17, 61, 4, 5),
        new("f", 47, 5, 4, 6, 5, 4, 7, 29),
        new("(", 18, 4, 55, 3, 4, 6, 3, 5),
        new("x", 31, 6, 3, 5, 7, 3, 5, 44),
        new(")", 5, 3, 57, 4, 3, 7, 4, 21),
        new("=", 3, 5, 23, 6, 5, 4, 7, 52),
        new("7", 6, 63, 5, 3, 4, 6, 3, 20),
        new("on", 4, 6, 3, 5, 46, 3, 27, 4),
        new("Jan", 5, 3, 6, 22, 3, 7, 56, 5),
        new("3", 3, 41, 4, 6, 5, 4, 36, 3),
        new("in", 6, 4, 5, 3, 50, 19, 3, 5),
        new("Rust", 38, 6, 3, 45, 7, 3, 5, 4),
        new("today", 5, 3, 6, 4, 3, 26, 48, 5),
        new(".", 3, 5, 60, 6, 18, 4, 7, 3),
    ];
}

/// <summary>
/// Lucide glyphs the default experts use that Core <see cref="ArcIcons"/> does not ship (24×24, stroked). Path data is
/// lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c>, parsed once through
/// <see cref="ArcIcons.Parse"/>.
/// </summary>
internal static class MixtureOfExpertsIcons
{
    // Lucide braces, ISC.
    private const string BracesData =
        "M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5c0 1.1.9 2 2 2h1 M16 21h1a2 2 0 0 0 2-2v-5c0-1.1.9-2 2-2a2 2 0 0 1-2-2V5a2 2 0 0 0-2-2h-1";

    // Lucide binary, ISC.
    private const string BinaryData =
        "M16 14h0a2 2 0 0 1 2 2v2a2 2 0 0 1 -2 2h0a2 2 0 0 1 -2 -2v-2a2 2 0 0 1 2 -2z M8 4h0a2 2 0 0 1 2 2v2a2 2 0 0 1 -2 2h0a2 2 0 0 1 -2 -2v-2a2 2 0 0 1 2 -2z M6 20h4 M14 10h4 M6 14h2v6 M14 4h2v6";

    // Lucide contact, ISC.
    private const string ContactData =
        "M16 2v2 M7 21v-2a2 2 0 012-2h6a2 2 0 012 2v2 M8 2v2 M9 10a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z";

    // Lucide rocket, ISC.
    private const string RocketData =
        "M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5 M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09 M9 12a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.4 22.4 0 0 1-4 2z M9 12H4s.55-3.03 2-4c1.62-1.08 5 .05 5 .05";

    // Lucide square-function, ISC.
    private const string SquareFunctionData =
        "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M9 17c2 0 2.8-1 2.8-2.8V10c0-2 1-3.3 3.2-3 M9 11.2h5.7";

    public static Geometry Braces => ArcIcons.Parse(BracesData);

    public static Geometry Binary => ArcIcons.Parse(BinaryData);

    public static Geometry Contact => ArcIcons.Parse(ContactData);

    public static Geometry Rocket => ArcIcons.Parse(RocketData);

    public static Geometry SquareFunction => ArcIcons.Parse(SquareFunctionData);
}
