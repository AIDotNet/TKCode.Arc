using Avalonia;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The part of a visual that its ancestors leave visible, in the visual's own coordinates: the same calculation
/// Avalonia uses for <see cref="Avalonia.Layout.Layoutable.EffectiveViewportChanged"/> (clipping ancestors intersect,
/// bounds translate, render transforms invert). Avalonia only reports changes after a listener subscribes, so code
/// that starts watching a control that is already laid out reads its first viewport here.
/// </summary>
public static class ArcViewport
{
    /// <summary>The effective viewport of <paramref name="target"/>, or an empty rect when it is not in a visual tree.</summary>
    public static Rect Calculate(Visual target)
    {
        if (!target.IsAttachedToVisualTree())
        {
            return default;
        }
        var viewport = default(Rect);
        return Accumulate(target, target, ref viewport) ? viewport : default;
    }

    private static bool Accumulate(Visual target, Visual control, ref Rect viewport)
    {
        if (control.GetVisualParent() is { } parent)
        {
            if (!Accumulate(target, parent, ref viewport))
            {
                return false;
            }
        }
        else
        {
            viewport = new Rect(control.Bounds.Size);
        }
        // The target's own clip is not applied: a control clipped to its bounds would otherwise only ever see itself.
        if (!ReferenceEquals(control, target) && control.ClipToBounds)
        {
            viewport = control.Bounds.Intersect(viewport);
        }
        viewport = viewport.Translate(-(Vector)control.Bounds.Position);
        if (!ReferenceEquals(control, target) && control.RenderTransform is { } transform)
        {
            var origin = control.RenderTransformOrigin.ToPixels(control.Bounds.Size);
            var offset = Matrix.CreateTranslation(origin);
            if (!transform.Value.TryInvert(out var inverse))
            {
                return false;
            }
            viewport = viewport.TransformToAABB(-offset * inverse * offset);
        }
        return true;
    }
}
