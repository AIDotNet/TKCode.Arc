using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Spring and tween presets measured on the Projects blocks (Arc design reference, fitted from frame captures; see
/// <c>design/components/blocks/projects/</c>). Core <c>Springs.cs</c> is not touched: presets that Core already has are
/// re-exported under the name of the motion they drive, the others are built with <see cref="SpringSpec.FromPhysical"/>.
/// </summary>
internal static class ProjectsSprings
{
    /// <summary>Cards flying between columns, closing up, entering and leaving; progress lines (k 340, c 34 = Gentle).</summary>
    public static readonly SpringSpec Card = ArcMotionPresets.Gentle;

    /// <summary>A highlight gliding between two toggle buttons (k 400, c 40, critically damped; Project board filter).</summary>
    public static readonly SpringSpec Filter = SpringSpec.FromPhysical(400, 40);

    /// <summary>A selected-day fill gliding between day cards (Availability picker: k ≈ 357–393, ζ ≈ .97; Gentle fits best).</summary>
    public static readonly SpringSpec DayHighlight = ArcMotionPresets.Gentle;

    /// <summary>A list sliding in from the side of travel (Availability picker slots: Smooth, k 171, c 26.2).</summary>
    public static readonly SpringSpec ListSlide = ArcMotionPresets.Smooth;

    /// <summary>
    /// Arc's current <c>--ease-spring</c> as a spring: critically damped, ω ≈ 13.8 (k 190, c 27.6). It has no overshoot,
    /// unlike Core's older <c>ArcEasings.CssSpring</c>; used where Arc runs a 580ms CSS spring transition (the release
    /// readiness bar).
    /// </summary>
    public static readonly SpringSpec CssSpring = SpringSpec.FromPhysical(190, 27.6);

    /// <summary>A checkmark or label slot growing in width (critically damped ω 13.75, close to Smooth).</summary>
    public static readonly SpringSpec Width = SpringSpec.FromPhysical(189, 27.5);

    /// <summary>A critically damped spring with natural frequency <paramref name="omega"/> rad/s (k = ω², c = 2ω).</summary>
    public static SpringSpec Critical(double omega)
    {
        if (!(omega > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(omega));
        }
        return SpringSpec.FromPhysical(omega * omega, 2 * omega);
    }

    /// <summary>The 160ms fade Arc pairs with most enter / exit motion (cubic-bezier(.22,1,.36,1)).</summary>
    public static MotionSpec FadeFast(TimeSpan delay = default) => MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay);

    /// <summary>The 120ms fade that reduced motion keeps in place of slides and springs.</summary>
    public static MotionSpec FadeInstant(TimeSpan delay = default) => MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard, delay);
}
