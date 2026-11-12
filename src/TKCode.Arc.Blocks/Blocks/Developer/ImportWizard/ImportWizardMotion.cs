using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Springs, curves and timings measured on Arc's Import wizard (design reference, ADR 0045; frame captures and the
/// browser's own animation data, 2026-11-13). See <c>design/components/blocks/developer/import-wizard.md</c> section 4.
/// </summary>
internal static class ImportWizardMotion
{
    /// <summary>k 171.3, c 26.18 (visualDuration 0.4, no bounce): card height, step slide, connectors, meters, rows, pill.</summary>
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);

    /// <summary>k 405.5, c 40.27 (visualDuration 0.26, no bounce): rolling digits, check pop-in, blur-ins, button exit.</summary>
    public static readonly SpringSpec Quick = SpringSpec.FromVisual(0.26, 0);

    /// <summary>Every color transition: cubic-bezier(0.22, 1, 0.36, 1).</summary>
    public static readonly Easing EaseOut = new SplineEasing(0.22, 1, 0.36, 1);

    /// <summary>The number glyph and "Matching" fading out: cubic-bezier(0.25, 0.1, 0.35, 1) over 300ms.</summary>
    public static readonly Easing FadeOut = new SplineEasing(0.25, 0.1, 0.35, 1);

    /// <summary>Rows leaving a filtered view: CSS ease-out, cubic-bezier(0, 0, 0.58, 1) over 200ms.</summary>
    public static readonly Easing RowExit = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>The "Matching" pulse: each half cubic-bezier(0.65, 0, 0.35, 1).</summary>
    public static readonly Easing Pulse = new SplineEasing(0.65, 0, 0.35, 1);

    /// <summary>The summary check drawing itself.</summary>
    public static readonly Easing CheckDraw = new SplineEasing(0.18, 1, 0.5, 0.97);

    public static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan RowExitDuration = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan PulsePeriod = TimeSpan.FromMilliseconds(1000);

    /// <summary>Step transitions: travel ±28, blur 4.</summary>
    public const double StepTravel = 28;
    public const double StepBlur = 4;

    /// <summary>The 2px reading line runs 420ms ease-out before the preview shows.</summary>
    public static readonly TimeSpan Reading = TimeSpan.FromMilliseconds(420);

    /// <summary>Preview rows stagger 40ms apart, rising 6px.</summary>
    public static readonly TimeSpan PreviewStagger = TimeSpan.FromMilliseconds(40);

    /// <summary>The first column resolves 270ms after the mapping step opens, then one every 117ms.</summary>
    public static readonly TimeSpan MatchFirst = TimeSpan.FromMilliseconds(270);
    public static readonly TimeSpan MatchInterval = TimeSpan.FromMilliseconds(117);

    /// <summary>Clean-up fixes one cell every 90ms (the first at once).</summary>
    public static readonly TimeSpan FixInterval = TimeSpan.FromMilliseconds(90);

    /// <summary>A fixed cell glows for 900ms before its row leaves the "to fix" view.</summary>
    public static readonly TimeSpan FixGlow = TimeSpan.FromMilliseconds(900);

    /// <summary>Rows leave a filtered view moving 24px to the right.</summary>
    public const double RowExitTravel = 24;

    /// <summary>A simulated batch takes 388ms (62ms with reduced motion); the bar holds 263ms at 100%.</summary>
    public static readonly TimeSpan Batch = TimeSpan.FromMilliseconds(388);
    public static readonly TimeSpan ReducedBatch = TimeSpan.FromMilliseconds(62);
    public static readonly TimeSpan DoneHold = TimeSpan.FromMilliseconds(263);

    /// <summary>The summary check starts drawing 150ms after the summary shows and takes 350ms.</summary>
    public static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan CheckDuration = TimeSpan.FromMilliseconds(350);

    /// <summary>The summary numbers count up once from zero over about 650ms.</summary>
    public static readonly TimeSpan CountUp = TimeSpan.FromMilliseconds(650);
    public static readonly Easing CountUpEasing = new SplineEasing(0.3, 0.03, 0.35, 1);
}
