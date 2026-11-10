using System.Globalization;
using Avalonia;

namespace TKCode.Arc;

/// <summary>What a <see cref="TimeDial"/> picks.</summary>
public enum TimeDialMode
{
    /// <summary>A time of day. One turn is twelve hours; going past 12 o'clock switches between AM and PM.</summary>
    Time,

    /// <summary>A length of time from <see cref="TimeDial.Minimum"/> to <see cref="TimeDial.Maximum"/>; one turn is <see cref="TimeDial.TurnLength"/>.</summary>
    Duration
}

/// <summary>The angles, snapping and text of a <see cref="TimeDial"/>, in minutes. Pure functions.</summary>
internal static class TimeDialMath
{
    public const double MinutesPerDay = 24 * 60;

    public const double MinutesPerHalfDay = 12 * 60;

    /// <summary>Degrees clockwise from 12 o'clock of <paramref name="point"/> around <paramref name="center"/>, in [0, 360).</summary>
    public static double AngleOf(Point point, Point center)
    {
        var degrees = Math.Atan2(point.X - center.X, center.Y - point.Y) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    /// <summary>The signed shortest turn from <paramref name="from"/> to <paramref name="to"/> degrees, in (−180, 180].</summary>
    public static double Turn(double from, double to)
    {
        var delta = (to - from) % 360;
        if (delta > 180)
        {
            delta -= 360;
        }
        else if (delta <= -180)
        {
            delta += 360;
        }
        return delta;
    }

    /// <summary>Rounds <paramref name="minutes"/> to the nearest multiple of <paramref name="step"/> (no rounding for a step of zero).</summary>
    public static double Snap(double minutes, double step) => step > 0 ? Math.Round(minutes / step) * step : minutes;

    /// <summary>Wraps a time of day into [0, 24 h).</summary>
    public static double WrapDay(double minutes)
    {
        var wrapped = minutes % MinutesPerDay;
        return wrapped < 0 ? wrapped + MinutesPerDay : wrapped;
    }

    /// <summary>
    /// The value an angle stands for that is closest to <paramref name="current"/>: an angle names a place within one turn,
    /// and the turn it is on is the one nearest the current value. A time of day wraps over midnight; a duration stays
    /// within <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    public static double Nearest(double angle, double current, double turn, double min, double max, bool wrapsDay)
    {
        var within = angle / 360 * turn;
        var laps = Math.Round((current - within) / turn);
        var candidate = within + laps * turn;
        if (wrapsDay)
        {
            return WrapDay(candidate);
        }
        // Take the closest place on the turns that lie inside the range.
        var best = double.NaN;
        for (var k = laps - 1; k <= laps + 1; k++)
        {
            var value = within + k * turn;
            if (value < min - 1e-9 || value > max + 1e-9)
            {
                continue;
            }
            if (double.IsNaN(best) || Math.Abs(value - current) < Math.Abs(best - current))
            {
                best = value;
            }
        }
        return double.IsNaN(best) ? Math.Clamp(candidate, min, max) : best;
    }

    /// <summary>Degrees clockwise from 12 o'clock at which a value is drawn.</summary>
    public static double AngleFor(double minutes, double turn) => turn > 0 ? minutes / turn * 360 : 0;

    /// <summary>The large readout and the small one after it: "7:05" and "PM", "1:25" and "hr", or "25" and "min".</summary>
    public static (string Main, string Unit) Readout(double minutes, TimeDialMode mode, bool is24Hour, string am, string pm,
                                                     string hours, string mins)
    {
        var total = (long)Math.Round(minutes);
        if (mode == TimeDialMode.Time)
        {
            total = (long)WrapDay(total);
            var hour = total / 60;
            var minute = total % 60;
            if (is24Hour)
            {
                return (string.Create(CultureInfo.InvariantCulture, $"{hour:00}:{minute:00}"), string.Empty);
            }
            var shown = hour % 12 == 0 ? 12 : hour % 12;
            return (string.Create(CultureInfo.InvariantCulture, $"{shown}:{minute:00}"), hour < 12 ? am : pm);
        }
        if (total < 60)
        {
            return (total.ToString(CultureInfo.InvariantCulture), mins);
        }
        return (string.Create(CultureInfo.InvariantCulture, $"{total / 60}:{total % 60:00}"), hours);
    }

    /// <summary>The value as words for assistive technology: "7:05 PM", "19:05", "1 hr 25 min" or "25 min".</summary>
    public static string Spoken(double minutes, TimeDialMode mode, bool is24Hour, string am, string pm, string hours, string mins)
    {
        var total = (long)Math.Round(minutes);
        if (mode == TimeDialMode.Time)
        {
            var (main, unit) = Readout(minutes, mode, is24Hour, am, pm, hours, mins);
            return unit.Length > 0 ? main + " " + unit : main;
        }
        if (total < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{total} {mins}");
        }
        return total % 60 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{total / 60} {hours}")
            : string.Create(CultureInfo.InvariantCulture, $"{total / 60} {hours} {total % 60} {mins}");
    }

    /// <summary>The numeral at a quarter turn: 12, 3, 6, 9 for a clock, the minutes or hours of the turn for a duration.</summary>
    public static string QuarterLabel(int quarter, TimeDialMode mode, double turn)
    {
        if (mode == TimeDialMode.Time)
        {
            return quarter switch { 0 => "12", 1 => "3", 2 => "6", _ => "9" };
        }
        var minutes = turn / 4 * quarter;
        if (turn >= 240)
        {
            var h = minutes / 60;
            return h.ToString("0.#", CultureInfo.InvariantCulture) + "h";
        }
        return minutes.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
