using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>How a shared element follows its counterpart while a <see cref="SharedElementScope"/> flies.</summary>
public enum SharedElementMode
{
    /// <summary>Position and size: the box scales onto its counterpart's box (Motion <c>layout</c>).</summary>
    Bounds,

    /// <summary>Position only: the box keeps its own size and its top-left follows (Motion <c>layout="position"</c>).</summary>
    Position
}

/// <summary>
/// Attached properties that mark the pieces a <see cref="SharedElementScope"/> flies between two containers (Motion's
/// <c>layoutId</c>). Give the matching pieces on both sides the same <see cref="KeyProperty"/>:
/// <code>
/// &lt;Border arc:SharedElement.Key="media" /&gt;                       &lt;!-- in the card --&gt;
/// &lt;Border arc:SharedElement.Key="media" /&gt;                       &lt;!-- in the quick look --&gt;
/// &lt;TextBlock arc:SharedElement.Key="byline" arc:SharedElement.Mode="Position" /&gt;
/// </code>
/// </summary>
public sealed class SharedElement
{
    private SharedElement()
    {
    }

    /// <summary>Identifies a piece; pieces with the same key in the scope's source and target fly between each other.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<SharedElement, Visual, string?>("Key");

    /// <summary>Whether the piece scales onto its counterpart (<see cref="SharedElementMode.Bounds"/>, default) or only moves.</summary>
    public static readonly AttachedProperty<SharedElementMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<SharedElement, Visual, SharedElementMode>("Mode");

    public static string? GetKey(Visual element) => element.GetValue(KeyProperty);

    public static void SetKey(Visual element, string? value) => element.SetValue(KeyProperty, value);

    public static SharedElementMode GetMode(Visual element) => element.GetValue(ModeProperty);

    public static void SetMode(Visual element, SharedElementMode value) => element.SetValue(ModeProperty, value);
}

/// <summary>Pure geometry of a shared-element flight (allocation free, unit tested).</summary>
public static class SharedElementMath
{
    public static double Lerp(double from, double to, double t) => from + (to - from) * t;

    public static Point Lerp(Point from, Point to, double t) => new(Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t));

    public static Rect Lerp(Rect from, Rect to, double t) =>
        new(Lerp(from.X, to.X, t), Lerp(from.Y, to.Y, t), Math.Max(0, Lerp(from.Width, to.Width, t)), Math.Max(0, Lerp(from.Height, to.Height, t)));

    public static CornerRadius Lerp(CornerRadius from, CornerRadius to, double t) =>
        new(Math.Max(0, Lerp(from.TopLeft, to.TopLeft, t)), Math.Max(0, Lerp(from.TopRight, to.TopRight, t)),
            Math.Max(0, Lerp(from.BottomRight, to.BottomRight, t)), Math.Max(0, Lerp(from.BottomLeft, to.BottomLeft, t)));

    /// <summary>
    /// Where a piece is drawn at <paramref name="progress"/> (0 = on its counterpart <paramref name="source"/>, 1 = at its
    /// own <paramref name="natural"/> box). <see cref="SharedElementMode.Position"/> keeps the natural size.
    /// </summary>
    public static Rect FlightRect(SharedElementMode mode, Rect source, Rect natural, double progress) => mode == SharedElementMode.Position
        ? new Rect(Lerp(source.Position, natural.Position, progress), natural.Size)
        : Lerp(source, natural, progress);

    /// <summary>
    /// The render transform (origin at the top-left) that draws a box of <paramref name="size"/>, whose local space maps
    /// to the root through <paramref name="chain"/> (its layout offset and every ancestor transform, not its own),
    /// exactly onto <paramref name="target"/> in root space. Identity when the chain cannot be inverted.
    /// </summary>
    public static Matrix Place(Size size, Matrix chain, Rect target)
    {
        if (size.Width <= 0 || size.Height <= 0 || !chain.TryInvert(out var inverse))
        {
            return Matrix.Identity;
        }
        var map = Matrix.CreateScale(target.Width / size.Width, target.Height / size.Height) *
                  Matrix.CreateTranslation(target.X, target.Y);
        return map * inverse;
    }

    /// <summary>
    /// Local corner radii that read as a circular <paramref name="radius"/> once the box is scaled by
    /// (<paramref name="scaleX"/>, <paramref name="scaleY"/>): Motion's border-radius scale correction.
    /// </summary>
    public static Vector CompensateRadius(double radius, double scaleX, double scaleY) =>
        new(Math.Max(0, radius) / Math.Max(scaleX, 1e-4), Math.Max(0, radius) / Math.Max(scaleY, 1e-4));
}
