using Avalonia;

namespace TKCode.Arc;

/// <summary>The pure rules of <see cref="MegaMenuMorph"/>: where the panel goes and how content moves while it morphs.</summary>
internal static class MegaMenuMorphMath
{
    /// <summary>
    /// The panel's rectangle in the menu's coordinates: <paramref name="size"/> centred under the trigger's centre
    /// <paramref name="anchorX"/>, then slid to stay between <paramref name="minX"/> and <paramref name="maxX"/>. A panel
    /// wider than that range starts at <paramref name="minX"/>.
    /// </summary>
    public static Rect PanelRect(double anchorX, Size size, double minX, double maxX)
    {
        var x = anchorX - size.Width / 2;
        if (x + size.Width > maxX)
        {
            x = maxX - size.Width;
        }
        if (x < minX)
        {
            x = minX;
        }
        return new Rect(x, 0, size.Width, size.Height);
    }

    /// <summary>+1 when the pointer travelled right (to a later item), -1 when left, 0 when it stayed.</summary>
    public static int Direction(int from, int to) => from < 0 || to < 0 ? 0 : Math.Sign(to - from);

    /// <summary>
    /// Where the content of the item being shown sits at swap progress <paramref name="progress"/> (0 to 1): it comes in
    /// from the side the pointer travelled to (<paramref name="direction"/> × <paramref name="distance"/>) and fades in
    /// after the first 15% of the swap. Without travel (<paramref name="distance"/> 0) it only fades.
    /// </summary>
    public static (double Offset, double Opacity) Incoming(double progress, int direction, double distance)
    {
        var p = Math.Clamp(progress, 0, 1);
        return (direction * distance * (1 - p), Math.Clamp((p - 0.15) / 0.85, 0, 1));
    }

    /// <summary>
    /// Where the content being replaced sits: it leaves toward the side the pointer came from and is gone by 55% of the
    /// swap, so the two never read as one block.
    /// </summary>
    public static (double Offset, double Opacity) Outgoing(double progress, int direction, double distance)
    {
        var p = Math.Clamp(progress, 0, 1);
        return (-direction * distance * p, 1 - Math.Clamp(p / 0.55, 0, 1));
    }

    /// <summary>The smallest rectangle holding both <paramref name="a"/> and <paramref name="b"/>; an empty one is ignored.</summary>
    public static Rect Union(Rect a, Rect b)
    {
        if (a.Width <= 0 && a.Height <= 0)
        {
            return b;
        }
        if (b.Width <= 0 && b.Height <= 0)
        {
            return a;
        }
        return a.Union(b);
    }
}
