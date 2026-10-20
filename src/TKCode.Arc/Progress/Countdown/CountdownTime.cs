using System.Globalization;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>Units a <see cref="Countdown"/> shows. The largest unit shown absorbs the rest (hours can pass 24 without days).</summary>
[Flags]
public enum CountdownUnits
{
    None = 0,
    Seconds = 1,
    Minutes = 2,
    Hours = 4,
    Days = 8,
    All = Days | Hours | Minutes | Seconds
}

/// <summary>Large display digits with unit names, or one compact pill for banners and table cells.</summary>
public enum CountdownVariant
{
    Large,
    Compact
}

/// <summary>One unit group of a countdown: its value and how it is labelled.</summary>
internal readonly record struct CountdownPart(CountdownUnits Unit, long Value);

/// <summary>Pure time helpers of <see cref="Countdown"/> (Arc's partsFor / basesFor / toEpoch / spoken text).</summary>
internal static partial class CountdownTime
{
    private static readonly CountdownUnits[] s_order = [CountdownUnits.Days, CountdownUnits.Hours, CountdownUnits.Minutes, CountdownUnits.Seconds];

    public static long SecondsIn(CountdownUnits unit) => unit switch
    {
        CountdownUnits.Days => 86_400,
        CountdownUnits.Hours => 3_600,
        CountdownUnits.Minutes => 60,
        _ => 1
    };

    private static long Limit(CountdownUnits unit) => unit switch
    {
        CountdownUnits.Hours => 24,
        CountdownUnits.Minutes or CountdownUnits.Seconds => 60,
        _ => long.MaxValue
    };

    /// <summary>The units on show, largest first (none selected means all).</summary>
    public static CountdownUnits[] Shown(CountdownUnits units)
    {
        var shown = s_order.Where(u => (units & u) != 0).ToArray();
        return shown.Length > 0 ? shown : s_order;
    }

    /// <summary>Whole seconds left, split across the units on show; the largest absorbs everything above it.</summary>
    public static CountdownPart[] Split(long seconds, CountdownUnits units)
    {
        var shown = Shown(units);
        var parts = new CountdownPart[shown.Length];
        for (var i = 0; i < shown.Length; i++)
        {
            var whole = seconds / SecondsIn(shown[i]);
            parts[i] = new CountdownPart(shown[i], i == 0 ? whole : whole % Limit(shown[i]));
        }
        return parts;
    }

    /// <summary>
    /// Wheel size per digit column: the tens of a two-digit unit below the largest only ever shows 0–5 (minutes,
    /// seconds) or 0–2 (hours), so 00 → 59 is a single step on a six-digit wheel.
    /// </summary>
    public static int BaseFor(CountdownUnits unit, bool largest, int length, int index)
    {
        var tens = index == length - 2 && length == 2 && !largest;
        return tens ? unit == CountdownUnits.Hours ? 3 : 6 : 10;
    }

    /// <summary>Signed shortest turn from <paramref name="from"/> to <paramref name="to"/> on a wheel of <paramref name="baseSize"/>.</summary>
    public static int ShortestTurn(int from, int to, int baseSize)
    {
        var up = ((to - from) % baseSize + baseSize) % baseSize;
        var down = baseSize - up;
        return down <= up ? -down : up;
    }

    public static string Name(CountdownUnits unit, bool compact) => unit switch
    {
        CountdownUnits.Days => compact ? "d" : "Days",
        CountdownUnits.Hours => compact ? "h" : "Hours",
        CountdownUnits.Minutes => compact ? "m" : "Minutes",
        _ => compact ? "s" : "Seconds"
    };

    private static string Singular(CountdownUnits unit) => unit switch
    {
        CountdownUnits.Days => "day",
        CountdownUnits.Hours => "hour",
        CountdownUnits.Minutes => "minute",
        _ => "second"
    };

    /// <summary>Remaining time in words; seconds are left out while more than a minute remains.</summary>
    public static string Spoken(long seconds, CountdownUnits units)
    {
        var parts = Split(seconds, units);
        var words = new List<string>();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Value <= 0 && i != parts.Length - 1)
            {
                continue;
            }
            if (seconds >= 60 && part.Unit == CountdownUnits.Seconds)
            {
                continue;
            }
            var name = Singular(part.Unit);
            words.Add($"{part.Value.ToString(CultureInfo.InvariantCulture)} {(part.Value == 1 ? name : name + "s")}");
        }
        return string.Join(", ", words);
    }

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex WallTime();

    /// <summary>
    /// Resolves a target string without reading the device's time zone: ISO text with <c>Z</c> or an offset is exact;
    /// wall time without an offset is read in <paramref name="timeZone"/> (daylight saving resolved for that date), or UTC.
    /// </summary>
    public static DateTimeOffset Resolve(string text, TimeZoneInfo? timeZone)
    {
        var trimmed = text.Trim();
        var wall = WallTime().Match(trimmed);
        if (!wall.Success)
        {
            return DateTimeOffset.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        }
        int Group(int i) => wall.Groups[i].Success ? int.Parse(wall.Groups[i].Value, CultureInfo.InvariantCulture) : 0;
        var local = new DateTime(Group(1), Group(2), Group(3), Group(4), Group(5), Group(6), DateTimeKind.Unspecified);
        if (timeZone is null)
        {
            return new DateTimeOffset(local, TimeSpan.Zero);
        }
        if (timeZone.IsInvalidTime(local))
        {
            // A wall time skipped by a daylight-saving jump resolves forward, like Arc's two-pass offset.
            local = local.AddHours(1);
        }
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
