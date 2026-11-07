using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The measured geometry of a pull (Arc Stretch refresh, design reference): the resistance curve, the sheet's peel, the
/// indicator's fade and centring, the fill, the row spread and the running segment. Pure functions of the pull; the
/// control writes their results to transforms on every frame.
/// </summary>
internal static class StretchRefreshMath
{
    /// <summary>The stretch the sheet approaches however far the pointer goes (280).</summary>
    public const double ResistanceLimit = 280;

    /// <summary>The resistance factor (0.7): with <see cref="ResistanceLimit"/> this is y = 280·d / (d + 400).</summary>
    public const double ResistanceFactor = 0.7;

    /// <summary>A pointer pull starts once the pointer has gone more than 4 px down (5 starts, 4 does not).</summary>
    public const double StartSlop = 4;

    /// <summary>Where the sheet rests while refreshing (60).</summary>
    public const double RestOffset = 60;

    /// <summary>The pull at which the sheet's top corners reach their full radius (40).</summary>
    public const double PeelDistance = 40;

    /// <summary>The sheet's top corner radius at full peel (26).</summary>
    public const double PeelRadius = 26;

    /// <summary>Each row moves this fraction of the tension per index (0.05).</summary>
    public const double SpreadPerRow = 0.05;

    /// <summary>The height of the indicator's slot (34): it is centred in the gap above the sheet.</summary>
    public const double AnchorHeight = 34;

    /// <summary>The track over-stretch once armed (1.12) and its size when it gives way to the result (0.4).</summary>
    public const double ArmedStretch = 1.12;

    public const double ShrunkStretch = 0.4;

    /// <summary>The running segment: 40% of the track, travelling 0 → 150% of its own width, 700 ms each way.</summary>
    public const double RunWidth = 0.4;

    public const double RunTravel = 1.5;

    public const double RunLegSeconds = 0.7;

    /// <summary>The wheel pull ends this long after the last wheel event (150 ms).</summary>
    public static readonly TimeSpan WheelIdle = TimeSpan.FromMilliseconds(150);

    /// <summary>Pixels of pull per wheel unit (Avalonia's scroll step: one notch scrolls 50 px).</summary>
    public const double WheelPixelsPerUnit = 50;

    /// <summary>After the result shows, the sheet springs back at 1210 ms and the cycle ends at 1860 ms (reduced: both at 1200 ms).</summary>
    public static readonly TimeSpan SheetReturnDelay = TimeSpan.FromMilliseconds(1210);

    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(1860);

    public static readonly TimeSpan ReducedSettleDelay = TimeSpan.FromMilliseconds(1200);

    /// <summary>The sheet's displayed pull for a raw pointer distance: 280·d/(d+400), 0 for d ≤ 0.</summary>
    public static double Resist(double distance)
        => distance <= 0 || double.IsNaN(distance) ? 0 : RubberBand.Apply(distance, ResistanceLimit, ResistanceFactor);

    /// <summary>The raw distance that gives a displayed pull (to catch a sheet mid-flight without a jump).</summary>
    public static double Unresist(double pull)
        => pull <= 0 || double.IsNaN(pull) ? 0 : RubberBand.Invert(pull, ResistanceLimit, ResistanceFactor);

    /// <summary>How far the sheet's top corners have peeled: min(1, y / 40).</summary>
    public static double Peel(double pull) => Math.Clamp(pull / PeelDistance, 0, 1);

    /// <summary>The indicator's opacity: clamp((y − 4) / 24, 0, 1).</summary>
    public static double AnchorOpacity(double pull) => Math.Clamp((pull - 4) / 24, 0, 1);

    /// <summary>The indicator's vertical shift: −17 + y / 2, which centres its 34 px slot in the gap.</summary>
    public static double AnchorOffset(double pull) => -AnchorHeight / 2 + pull / 2;

    /// <summary>The fill's horizontal scale (from the centre): it reaches the full track exactly at the threshold.</summary>
    public static double FillScale(double pull, double threshold)
        => threshold <= 0 ? 1 : Math.Clamp(pull / threshold, 0, 1);

    /// <summary>The extra shift of row <paramref name="index"/> for a tension: index · tension · 0.05.</summary>
    public static double RowShift(int index, double tension) => index * tension * SpreadPerRow;

    /// <summary>
    /// The running segment's position, in segment widths from the track's left end (0 … 1.5), after
    /// <paramref name="seconds"/> of running: there and back on (.65, 0, .35, 1), 700 ms each way.
    /// </summary>
    public static double RunPosition(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds))
        {
            return 0;
        }
        var cycle = seconds % (2 * RunLegSeconds);
        var forward = cycle < RunLegSeconds;
        var p = (forward ? cycle : cycle - RunLegSeconds) / RunLegSeconds;
        var eased = ArcEasings.InOut.Ease(p);
        return RunTravel * (forward ? eased : 1 - eased);
    }

    /// <summary>The result line when a refresh handler returns nothing (or only white space).</summary>
    public static string ResultText(string? result, StretchRefreshLabels labels)
        => string.IsNullOrWhiteSpace(result) ? labels.Done : result!;
}
