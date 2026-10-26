namespace TKCode.Arc.Themes.Corners;

/// <summary>How round the corners of Arc controls are. Only corners change; colors, borders, shadows and motion do not.</summary>
public readonly record struct ArcCornerStyle
{
    // Stored as the distance from 1, so that default(ArcCornerStyle) is the default style and not a square one.
    private readonly double _delta;

    private ArcCornerStyle(double factor) => _delta = factor - 1;

    /// <summary>The corner radii of Arc as shipped: no overrides are written.</summary>
    public static ArcCornerStyle Default { get; } = new(1);

    /// <summary>
    /// Every corner is square, including the pill and circle radii: round avatars, pill buttons and dots become squares.
    /// </summary>
    public static ArcCornerStyle Square { get; } = new(0);

    /// <summary>
    /// Every radius is multiplied by <paramref name="factor"/>: 1 is <see cref="Default"/>, 0 is <see cref="Square"/>, 0.5 halves
    /// the corners, 2 doubles them. Pill and circle radii (999 and up) keep their value unless the factor is 0.
    /// </summary>
    /// <param name="factor">A finite number, zero or more.</param>
    public static ArcCornerStyle Scale(double factor)
    {
        if (!double.IsFinite(factor) || factor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "The factor must be a finite number, zero or more.");
        }
        return new ArcCornerStyle(factor);
    }

    /// <summary>The multiplier of every radius: 1 is <see cref="Default"/>, 0 is <see cref="Square"/>.</summary>
    internal double Factor => 1 + _delta;

    internal bool IsDefault => Factor == 1;

    internal bool IsSquare => Factor == 0;
}
