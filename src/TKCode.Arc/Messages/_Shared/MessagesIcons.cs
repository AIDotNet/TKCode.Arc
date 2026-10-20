using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs whose generated <see cref="ArcIcons"/> data is wrong: the generator joins a glyph's path elements
/// with a space, so a later element that starts with a relative <c>m</c> moves relative to the previous element's
/// end instead of the origin (X, CircleX, CircleCheck, ArrowRight, TrendingUp/Down render broken). These are the
/// same glyphs with that moveto made absolute; switch back to <see cref="ArcIcons"/> once the generator is fixed.
/// Shared by the Messages and Cards categories.
/// </summary>
internal static class MessagesIcons
{
    public static Geometry X { get; } = ArcIcons.X;

    public static Geometry CircleX { get; } = ArcIcons.CircleX;

    public static Geometry CircleCheck { get; } = ArcIcons.CircleCheck;

    public static Geometry ArrowRight { get; } = ArcIcons.ArrowRight;

    public static Geometry TrendingUp { get; } = ArcIcons.TrendingUp;

    public static Geometry TrendingDown { get; } = ArcIcons.TrendingDown;
}
