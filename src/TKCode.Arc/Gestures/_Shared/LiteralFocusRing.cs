using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A <see cref="LazyFocusRing"/> for a control whose ring margin is a fixed design value rather than a token (no resource
/// key to hand it): the margin is set on the ring as it is placed, over the unresolved margin resource.
/// </summary>
internal static class LiteralFocusRing
{
    private static readonly object s_noMarginKey = new();

    public static LazyFocusRing Create(Control owner, object radiusKey) => new(owner, s_noMarginKey, radiusKey);

    /// <summary>Places the ring last in <paramref name="host"/> (above the panel's other children) once it is needed.</summary>
    public static void Attach(LazyFocusRing focusRing, Panel? host, Thickness margin)
    {
        if (host is null)
        {
            focusRing.Attach(null, null);
            return;
        }
        focusRing.Attach(ring =>
        {
            ring.Margin = margin;
            host.Children.Add(ring);
        }, ring => host.Children.Remove(ring));
    }
}
