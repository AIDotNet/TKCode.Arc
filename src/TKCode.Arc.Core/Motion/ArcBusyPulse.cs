namespace TKCode.Arc.Motion;

/// <summary>
/// What a busy indicator (spinner, loader, running segment) shows under reduced motion instead of its travel, scale,
/// rotation or blur: its resting frame breathes in opacity, 1 → <see cref="MinOpacity"/> → 1 every
/// <see cref="Period"/> seconds on a sine ease in and out, so the user can still tell that work is in
/// progress (reduced motion keeps short opacity changes and drops movement). The owner runs it on a
/// <see cref="LiveLoop"/> with <see cref="LiveLoop.MinimumLevel"/> <see cref="ArcMotionLevel.Reduced"/> and keeps the
/// phase with <see cref="Advance"/>; with motion off the loop parks and the frame rests at full opacity.
/// </summary>
public static class ArcBusyPulse
{
    /// <summary>One breath in seconds.</summary>
    public const double Period = 1.8;

    /// <summary>The opacity at the bottom of a breath.</summary>
    public const double MinOpacity = 0.45;

    /// <summary>The phase <paramref name="seconds"/> after <paramref name="phase"/>, kept within one period.</summary>
    public static double Advance(double phase, double seconds)
    {
        var next = (phase + Math.Max(0, seconds)) % Period;
        return double.IsFinite(next) ? next : 0;
    }

    /// <summary>The opacity <paramref name="seconds"/> into the pulse: 1 at 0, <see cref="MinOpacity"/> half a period in.</summary>
    public static double Opacity(double seconds)
    {
        if (!(seconds > 0) || !double.IsFinite(seconds))
        {
            return 1;
        }
        var phase = seconds % Period / Period;
        var half = phase < 0.5 ? phase * 2 : (1 - phase) * 2;
        // A closed-form ease (no shared easing instance), so it is pure and safe to call from any thread.
        return 1 - (1 - MinOpacity) * (1 - Math.Cos(Math.PI * half)) / 2;
    }
}
