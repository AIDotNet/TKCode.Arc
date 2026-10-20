namespace TKCode.Arc;

/// <summary>Heading level of a text effect; H1–H3 set <c>AutomationProperties.HeadingLevel</c> 1–3.</summary>
public enum TextLevel
{
    H1,
    H2,
    H3,

    /// <summary>A paragraph: body type and a softer blur.</summary>
    Paragraph
}

/// <summary>Timing helpers shared by the staggered text reveals (P20).</summary>
internal static class TextEffectsTiming
{
    /// <summary>Eased progress of a track that starts after <paramref name="delay"/> and lasts <paramref name="duration"/>.</summary>
    public static double Track(double elapsed, double delay, double duration, Avalonia.Animation.Easings.Easing easing)
    {
        var p = (elapsed - delay) / Math.Max(duration, 1e-6);
        if (p <= 0)
        {
            return 0;
        }
        return p >= 1 ? 1 : easing.Ease(p);
    }
}
