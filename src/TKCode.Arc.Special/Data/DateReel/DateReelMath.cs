using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>The order of the columns of a <see cref="DateReel"/>.</summary>
public enum DateReelOrder
{
    /// <summary>Month, day, year (March 14 2026).</summary>
    MonthDayYear,

    /// <summary>Day, month, year (14 March 2026).</summary>
    DayMonthYear,

    /// <summary>Year, month, day (2026 March 14).</summary>
    YearMonthDay
}

/// <summary>Which part of a date a <see cref="DateReel"/> column turns.</summary>
internal enum DateReelPart
{
    Day,
    Month,
    Year
}

/// <summary>
/// The wheel arithmetic of a <see cref="DateReel"/> column, in rows: a position is a row index that may be fractional
/// while the wheel turns, and on a looping wheel it may run past either end. Pure functions.
/// </summary>
internal static class DateReelMath
{
    /// <summary>Wraps a row index into [0, <paramref name="count"/>).</summary>
    public static int Wrap(long index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        var wrapped = index % count;
        return (int)(wrapped < 0 ? wrapped + count : wrapped);
    }

    /// <summary>The row a position rests on: the nearest one, wrapped on a looping wheel, clamped on a plain one.</summary>
    public static int IndexAt(double position, int count, bool loops)
    {
        var nearest = (long)Math.Round(position);
        return loops ? Wrap(nearest, count) : (int)Math.Clamp(nearest, 0, Math.Max(0, count - 1));
    }

    /// <summary>
    /// The position that shows <paramref name="index"/> closest to <paramref name="position"/>: on a looping wheel the short
    /// way round (11 → 0 turns one row on), on a plain wheel the index itself.
    /// </summary>
    public static double PositionFor(double position, int index, int count, bool loops)
    {
        if (!loops || count <= 0)
        {
            return index;
        }
        var current = Wrap((long)Math.Round(position), count);
        var delta = index - current;
        if (delta > count / 2.0)
        {
            delta -= count;
        }
        else if (delta < -count / 2.0)
        {
            delta += count;
        }
        return Math.Round(position) + delta;
    }

    /// <summary>
    /// Where a flick released at <paramref name="position"/> with <paramref name="velocity"/> (rows per second) comes to
    /// rest: the momentum projection of Core <see cref="MomentumProjector"/>, rounded to a row and, on a plain wheel, kept
    /// on the wheel.
    /// </summary>
    public static double Settle(double position, double velocity, int count, bool loops, double decay)
    {
        var projected = MomentumProjector.ProjectedPosition(position, velocity, SnapProjection.Decay(decay));
        var row = Math.Round(projected);
        return loops ? row : Math.Clamp(row, 0, Math.Max(0, count - 1));
    }

    /// <summary>The angle step between rows on the drum, for a drum showing <paramref name="visibleRows"/> rows.</summary>
    public static double Step(int visibleRows) => 2.0 / Math.Max(1, visibleRows);

    /// <summary>
    /// Where a row <paramref name="delta"/> rows from the middle is drawn on a drum of radius <paramref name="radius"/>:
    /// its offset from the middle, how much it is squashed (1 at the middle) and whether it is on the visible half.
    /// </summary>
    public static (double Offset, double Scale, double Angle, bool Visible) Project(double delta, double step, double radius)
    {
        var angle = delta * step;
        if (Math.Abs(angle) >= Math.PI / 2)
        {
            return (0, 0, angle, false);
        }
        return (radius * Math.Sin(angle), Math.Cos(angle), angle, true);
    }

    /// <summary>The row offset from the middle that a point <paramref name="offset"/> px from the middle shows.</summary>
    public static double DeltaAt(double offset, double step, double radius)
    {
        var ratio = Math.Clamp(offset / Math.Max(1, radius), -0.999, 0.999);
        return Math.Asin(ratio) / step;
    }
}
