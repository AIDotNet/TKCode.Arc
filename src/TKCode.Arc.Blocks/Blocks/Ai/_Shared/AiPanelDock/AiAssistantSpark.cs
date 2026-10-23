using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The assistant's default mark when the host gives none: a neutral four-pointed spark with concave sides in a 24 × 24
/// box, filled (drawn by <see cref="ModelMark"/> in Accent). Our own shape; Arc's preview uses a brand mark, which we do
/// not copy (spec 10.3).
/// </summary>
internal static class AiAssistantSpark
{
    /// <summary>The spark geometry.</summary>
    public static Geometry Geometry { get; } = StreamGeometry.Parse(
        "M12 1.5 C12.7 7.3 16.7 11.3 22.5 12 C16.7 12.7 12.7 16.7 12 22.5 C11.3 16.7 7.3 12.7 1.5 12 C7.3 11.3 11.3 7.3 12 1.5 Z");

    /// <summary>The mark to draw: <paramref name="mark"/>, or the spark when it is null.</summary>
    public static object Resolve(object? mark) => mark ?? Geometry;
}
