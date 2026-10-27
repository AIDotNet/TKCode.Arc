using System.Globalization;

namespace TKCode.Arc;

/// <summary>Whether a change is good, bad or flat for its metric (delta colour and arrow).</summary>
internal enum AnalyticsTone
{
    /// <summary>No visible change (rounds to zero at the display precision): TextMuted and a level arrow.</summary>
    Flat,

    /// <summary>Moved the right way: Success.</summary>
    Good,

    /// <summary>Moved the wrong way: Danger.</summary>
    Bad
}

/// <summary>A compact amount split for rolling digits: <c>Scaled</c> shown with <c>Decimals</c> and a unit (k, M, B).</summary>
/// <param name="Scaled">The value divided by the unit, sign kept (2,560,000 → 2.56).</param>
/// <param name="Decimals">Fraction digits to show.</param>
/// <param name="Unit">"k"/"K", "M", "B" or empty.</param>
internal readonly record struct AnalyticsCompactParts(double Scaled, int Decimals, string Unit);

/// <summary>
/// Number formats of the Analytics blocks (en-US by default; pass a culture to change separators). Every negative number
/// uses the true minus sign U+2212 (<see cref="Minus"/>), which every note asks for, and a value that rounds to zero at
/// the display precision carries no sign. Compact formats come in two flavours: significant digits (Revenue explorer
/// "$63.3k", "$2.56M"; Choropleth "$120K", "$1.6M") and fixed decimals (MRR waterfall labels "$412.4k"; Metrics dashboard
/// axis "1.5K" with trailing zeros trimmed).
/// </summary>
internal static class AnalyticsFormat
{
    /// <summary>The true minus sign (U+2212).</summary>
    public const string Minus = "−";

    /// <summary>Shown for a missing value (NaN or infinity).</summary>
    public const string Missing = "—";

    /// <summary>Arc's formatting culture.</summary>
    public static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Grouped number with a true minus: 36,686 · 4.8 · −1,250.</summary>
    public static string Number(double value, int decimals = 0, bool grouping = true, CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        decimals = Math.Clamp(decimals, 0, 15);
        var rounded = Math.Round(Math.Abs(value), decimals, MidpointRounding.AwayFromZero);
        var text = rounded.ToString((grouping ? "N" : "F") + decimals, culture);
        return (value < 0 && rounded > 0 ? Minus : string.Empty) + text;
    }

    /// <summary>
    /// Grouped currency: $412,380 · −$13,800 (sign before the symbol); <paramref name="signed"/> adds "+" to positive values
    /// ("+$55,800").
    /// </summary>
    public static string Currency(double value, int decimals = 0, string symbol = "$", bool signed = false, CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        decimals = Math.Clamp(decimals, 0, 15);
        var rounded = Math.Round(Math.Abs(value), decimals, MidpointRounding.AwayFromZero);
        return Sign(value, rounded, signed) + symbol + rounded.ToString("N" + decimals, culture);
    }

    /// <summary>
    /// The compact split of an amount at <paramref name="significant"/> significant digits (integer digits are always
    /// kept): 63,300 → (63.3, 1, "k"); 2,561,000 → (2.56, 2, "M"); 120,400 at 2 digits → (120, 0, "K"). An amount that
    /// rounds up to 1000 of a unit moves to the next unit (999,960 → 1.00M). Below 1,000 there is no unit.
    /// </summary>
    public static AnalyticsCompactParts CompactParts(double value, int significant = 3, bool upperCaseThousands = false)
    {
        significant = Math.Clamp(significant, 1, 15);
        var abs = Math.Abs(value);
        var units = (ReadOnlySpan<(double Size, string Name)>)[(1e9, "B"), (1e6, "M"), (1e3, upperCaseThousands ? "K" : "k"), (1, "")];
        for (var i = 0; i < units.Length; i++)
        {
            var (size, name) = units[i];
            if (abs < size && size > 1)
            {
                continue;
            }
            var scaled = abs / size;
            var decimals = Math.Max(0, significant - IntegerDigits(scaled));
            var rounded = Math.Round(scaled, decimals, MidpointRounding.AwayFromZero);
            if (IntegerDigits(rounded) > IntegerDigits(scaled))
            {
                // 99.96 at three digits is "100", not "100.0".
                decimals = Math.Max(0, significant - IntegerDigits(rounded));
            }
            if (rounded >= 1000 && i > 0)
            {
                // 999,960 → 1,000.0k: show it in the next unit up.
                var (upSize, upName) = units[i - 1];
                scaled = abs / upSize;
                decimals = Math.Max(0, significant - IntegerDigits(scaled));
                return new AnalyticsCompactParts(Math.Sign(value) * scaled, decimals, upName);
            }
            return new AnalyticsCompactParts(Math.Sign(value) * scaled, decimals, name);
        }
        return new AnalyticsCompactParts(value, 0, string.Empty);
    }

    /// <summary>
    /// Compact amount at significant digits: <c>Compact(63_300, symbol: "$")</c> → "$63.3k", <c>Compact(2_561_000, symbol:
    /// "$")</c> → "$2.56M", <c>Compact(120_400, 2, "$", upperCaseThousands: true)</c> → "$120K", −25,000 → "−$25.0k".
    /// <paramref name="trimZeros"/> drops trailing fraction zeros ("$25k"); <paramref name="signed"/> adds "+".
    /// </summary>
    public static string Compact(double value, int significant = 3, string symbol = "", bool upperCaseThousands = false,
        bool trimZeros = false, bool signed = false, CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        var parts = CompactParts(value, significant, upperCaseThousands);
        return Join(value, parts, symbol, trimZeros, signed, culture ?? DefaultCulture);
    }

    /// <summary>
    /// Compact amount with a fixed number of decimals once a unit applies: <c>CompactFixed(412_380, 1, "$")</c> →
    /// "$412.4k", <c>CompactFixed(1500, 1, trimZeros: true, upperCaseThousands: true)</c> → "1.5K", 15,000 → "15K".
    /// Amounts below 1,000 use <paramref name="smallDecimals"/> (0).
    /// </summary>
    public static string CompactFixed(double value, int decimals, string symbol = "", bool upperCaseThousands = false,
        bool trimZeros = false, bool signed = false, int smallDecimals = 0, CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        var abs = Math.Abs(value);
        var units = (ReadOnlySpan<(double Size, string Name)>)[(1e9, "B"), (1e6, "M"), (1e3, upperCaseThousands ? "K" : "k")];
        var parts = new AnalyticsCompactParts(value, smallDecimals, string.Empty);
        for (var i = units.Length - 1; i >= 0; i--)
        {
            if (abs < units[i].Size)
            {
                break;
            }
            parts = new AnalyticsCompactParts(value / units[i].Size, decimals, units[i].Name);
            // 999,960 at one decimal is "1,000.0k": the next unit up shows it ("1.0M").
            if (i > 0 && Math.Round(abs / units[i].Size, decimals, MidpointRounding.AwayFromZero) >= 1000)
            {
                parts = new AnalyticsCompactParts(value / units[i - 1].Size, decimals, units[i - 1].Name);
            }
        }
        return Join(value, parts, symbol, trimZeros, signed, culture ?? DefaultCulture);
    }

    /// <summary>
    /// A percentage given in percent units: <c>Percent(4.8, 1)</c> → "4.8%"; <paramref name="signed"/> → "+4.3%" /
    /// "−4.8%" / "0%" (zero after rounding has no sign).
    /// </summary>
    public static string Percent(double percent, int decimals = 1, bool signed = false, CultureInfo? culture = null)
    {
        if (!double.IsFinite(percent))
        {
            return Missing;
        }
        var text = Number(percent, decimals, true, culture) + "%";
        return signed && Round(percent, decimals) > 0 ? "+" + text : text;
    }

    /// <summary>
    /// A relative change given as a fraction: <c>Change(0.117)</c> → "11.7%" (unsigned: the arrow carries the
    /// direction, Metrics dashboard), <c>Change(−0.034, signed: true)</c> → "−3.4%".
    /// </summary>
    public static string Change(double fraction, int decimals = 1, bool signed = false, CultureInfo? culture = null)
    {
        if (!double.IsFinite(fraction))
        {
            return Missing;
        }
        var percent = fraction * 100;
        return signed ? Percent(percent, decimals, true, culture) : Number(Math.Abs(percent), decimals, true, culture) + "%";
    }

    /// <summary>
    /// A change in percentage points: <c>Points(0.1)</c> → "0.1 pts" (unsigned), <c>Points(−0.25, 2, signed: true)</c> →
    /// "−0.25 pts". The unit text is <paramref name="unit"/> (" pts").
    /// </summary>
    public static string Points(double points, int decimals = 1, bool signed = false, string unit = " pts", CultureInfo? culture = null)
    {
        if (!double.IsFinite(points))
        {
            return Missing;
        }
        var text = signed ? Number(points, decimals, true, culture) : Number(Math.Abs(points), decimals, true, culture);
        if (signed && Round(points, decimals) > 0)
        {
            text = "+" + text;
        }
        return text + unit;
    }

    /// <summary>Replaces ASCII hyphen-minus signs before digits or a currency symbol with U+2212 ("-$5" → "−$5").</summary>
    public static string WithTrueMinus(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('-') < 0)
        {
            return text;
        }
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] == '-' && (i == 0 || !char.IsLetterOrDigit(chars[i - 1])) && !char.IsWhiteSpace(chars[i + 1]))
            {
                chars[i] = '−';
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// The tone of a change: flat when it rounds to zero at <paramref name="decimals"/> (of the displayed number, e.g. the
    /// percent or pts value), otherwise good when it moved the right way (<paramref name="lowerIsBetter"/> flips it).
    /// </summary>
    public static AnalyticsTone Tone(double displayedChange, bool lowerIsBetter = false, int decimals = 1)
    {
        if (!double.IsFinite(displayedChange) || Round(displayedChange, decimals) == 0)
        {
            return AnalyticsTone.Flat;
        }
        var up = displayedChange > 0;
        return up != lowerIsBetter ? AnalyticsTone.Good : AnalyticsTone.Bad;
    }

    /// <summary>English ordinal: 1st, 2nd, 3rd, 4th, 11th, 21st (Choropleth ranks).</summary>
    public static string Ordinal(int n)
    {
        var mod100 = Math.Abs(n) % 100;
        var suffix = mod100 is >= 11 and <= 13 ? "th" : (Math.Abs(n) % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n.ToString(DefaultCulture) + suffix;
    }

    /// <summary>A date in Arc's short form ("MMM d": "Oct 18") or any .NET pattern, en-US.</summary>
    public static string Date(DateTime date, string pattern = "MMM d", CultureInfo? culture = null)
        => date.ToString(pattern, culture ?? DefaultCulture);

    /// <summary>A <see cref="DateOnly"/> in Arc's short form ("MMM d") or any .NET pattern, en-US.</summary>
    public static string Date(DateOnly date, string pattern = "MMM d", CultureInfo? culture = null)
        => date.ToString(pattern, culture ?? DefaultCulture);

    private static string Join(double value, AnalyticsCompactParts parts, string symbol, bool trimZeros, bool signed, CultureInfo culture)
    {
        var rounded = Math.Round(Math.Abs(parts.Scaled), parts.Decimals, MidpointRounding.AwayFromZero);
        var number = rounded.ToString("N" + parts.Decimals, culture);
        if (trimZeros && parts.Decimals > 0)
        {
            var separator = culture.NumberFormat.NumberDecimalSeparator;
            number = number.TrimEnd('0');
            if (number.EndsWith(separator, StringComparison.Ordinal))
            {
                number = number[..^separator.Length];
            }
        }
        return Sign(value, rounded, signed) + symbol + number + parts.Unit;
    }

    private static string Sign(double value, double roundedAbs, bool signed)
    {
        if (roundedAbs == 0)
        {
            return string.Empty;
        }
        return value < 0 ? Minus : signed ? "+" : string.Empty;
    }

    private static double Round(double value, int decimals) => Math.Round(Math.Abs(value), Math.Clamp(decimals, 0, 15), MidpointRounding.AwayFromZero) * Math.Sign(value);

    private static int IntegerDigits(double abs) => abs < 1 ? 1 : (int)Math.Floor(Math.Log10(abs)) + 1;
}

/// <summary>Which <see cref="AnalyticsFormat"/> rule an <see cref="AnalyticsNumberFormat"/> applies.</summary>
internal enum AnalyticsNumberKind
{
    /// <summary>Grouped number (36,686; 4.8).</summary>
    Number,

    /// <summary>Grouped currency ($412,380).</summary>
    Currency,

    /// <summary>Compact at significant digits ($63.3k, $2.56M; 2 digits uppercase: $120K).</summary>
    Compact,

    /// <summary>Compact with fixed decimals ($412.4k; trimmed: 1.5K).</summary>
    CompactFixed,

    /// <summary>A value in percent units (4.8%).</summary>
    Percent,

    /// <summary>A value in percentage points (0.1 pts).</summary>
    Points
}

/// <summary>
/// One number format, usable both as a string (<see cref="Format"/>: axes, readouts, tables, announcements) and as rolling
/// digit parts (<see cref="Parts"/>: <see cref="AnalyticsRollingNumber"/>), so a KPI that rolls and the screen-reader
/// text of the same KPI always agree. Build with the static factories.
/// </summary>
/// <param name="Kind">The rule.</param>
/// <param name="Decimals">Fraction digits (Number, Currency, CompactFixed, Percent, Points).</param>
/// <param name="Symbol">Currency symbol placed after the sign ("$").</param>
/// <param name="Significant">Significant digits (Compact).</param>
/// <param name="UpperCaseThousands">"K" instead of "k" (Compact, CompactFixed).</param>
/// <param name="TrimZeros">Drop trailing fraction zeros (Compact, CompactFixed).</param>
/// <param name="Signed">"+" on positive values (zero after rounding stays unsigned).</param>
/// <param name="Suffix">Text after the number ("ms", " seats"); Points uses it as its unit (default " pts").</param>
internal readonly record struct AnalyticsNumberFormat(
    AnalyticsNumberKind Kind,
    int Decimals = 0,
    string Symbol = "",
    int Significant = 3,
    bool UpperCaseThousands = false,
    bool TrimZeros = false,
    bool Signed = false,
    string? Suffix = null)
{
    /// <summary>Grouped integer (the default format).</summary>
    public static AnalyticsNumberFormat Integer => new(AnalyticsNumberKind.Number);

    /// <summary>Grouped number with <paramref name="decimals"/>.</summary>
    public static AnalyticsNumberFormat Number(int decimals = 0, string? suffix = null) => new(AnalyticsNumberKind.Number, decimals, Suffix: suffix);

    /// <summary>Grouped currency.</summary>
    public static AnalyticsNumberFormat Currency(int decimals = 0, string symbol = "$", bool signed = false)
        => new(AnalyticsNumberKind.Currency, decimals, symbol, Signed: signed);

    /// <summary>Compact at significant digits ("$63.3k").</summary>
    public static AnalyticsNumberFormat Compact(int significant = 3, string symbol = "", bool upperCaseThousands = false, bool signed = false)
        => new(AnalyticsNumberKind.Compact, 0, symbol, significant, upperCaseThousands, Signed: signed);

    /// <summary>Compact with fixed decimals ("$412.4k").</summary>
    public static AnalyticsNumberFormat CompactFixed(int decimals = 1, string symbol = "", bool upperCaseThousands = false, bool trimZeros = false, bool signed = false)
        => new(AnalyticsNumberKind.CompactFixed, decimals, symbol, 3, upperCaseThousands, trimZeros, signed);

    /// <summary>Percent units ("4.8%").</summary>
    public static AnalyticsNumberFormat Percent(int decimals = 1, bool signed = false) => new(AnalyticsNumberKind.Percent, decimals, Signed: signed);

    /// <summary>Percentage points ("0.1 pts").</summary>
    public static AnalyticsNumberFormat Points(int decimals = 1, bool signed = false, string unit = " pts")
        => new(AnalyticsNumberKind.Points, decimals, Signed: signed, Suffix: unit);

    /// <summary>The value as text (true minus).</summary>
    public string Format(double value) => Kind switch
    {
        AnalyticsNumberKind.Currency => AnalyticsFormat.Currency(value, Decimals, Symbol, Signed) + Suffix,
        AnalyticsNumberKind.Compact => AnalyticsFormat.Compact(value, Significant, Symbol, UpperCaseThousands, TrimZeros, Signed) + Suffix,
        AnalyticsNumberKind.CompactFixed => AnalyticsFormat.CompactFixed(value, Decimals, Symbol, UpperCaseThousands, TrimZeros, Signed) + Suffix,
        AnalyticsNumberKind.Percent => AnalyticsFormat.Percent(value, Decimals, Signed) + Suffix,
        AnalyticsNumberKind.Points => AnalyticsFormat.Points(value, Decimals, Signed, Suffix ?? " pts"),
        _ => SignedNumber(value) + Suffix
    };

    /// <summary>
    /// The value split for rolling digits: a static prefix (sign and symbol), the unsigned number that rolls with its
    /// decimals, and a static suffix (unit, "%", " pts"). Trimmed zeros do not apply (wheels keep their columns).
    /// </summary>
    public (string Prefix, double Value, int Decimals, string Suffix) Parts(double value)
    {
        if (!double.IsFinite(value))
        {
            return (string.Empty, 0, 0, string.Empty);
        }
        switch (Kind)
        {
            case AnalyticsNumberKind.Compact:
            {
                var parts = AnalyticsFormat.CompactParts(value, Significant, UpperCaseThousands);
                var abs = Math.Round(Math.Abs(parts.Scaled), parts.Decimals, MidpointRounding.AwayFromZero);
                return (SignOf(value, abs) + Symbol, abs, parts.Decimals, parts.Unit + Suffix);
            }
            case AnalyticsNumberKind.CompactFixed:
            {
                var text = AnalyticsFormat.CompactFixed(Math.Abs(value), Decimals, string.Empty, UpperCaseThousands);
                var unit = text.Length > 0 && char.IsLetter(text[^1]) ? text[^1].ToString() : string.Empty;
                var scale = unit switch { "k" or "K" => 1e3, "M" => 1e6, "B" => 1e9, _ => 1.0 };
                var decimals = unit.Length == 0 ? 0 : Decimals;
                var abs = Math.Round(Math.Abs(value) / scale, decimals, MidpointRounding.AwayFromZero);
                return (SignOf(value, abs) + Symbol, abs, decimals, unit + Suffix);
            }
            case AnalyticsNumberKind.Percent:
            {
                var abs = Math.Round(Math.Abs(value), Decimals, MidpointRounding.AwayFromZero);
                return (SignOf(value, abs), abs, Decimals, "%" + Suffix);
            }
            case AnalyticsNumberKind.Points:
            {
                var abs = Math.Round(Math.Abs(value), Decimals, MidpointRounding.AwayFromZero);
                return (SignOf(value, abs), abs, Decimals, Suffix ?? " pts");
            }
            default:
            {
                var abs = Math.Round(Math.Abs(value), Decimals, MidpointRounding.AwayFromZero);
                return (SignOf(value, abs) + Symbol, abs, Decimals, Suffix ?? string.Empty);
            }
        }
    }

    private string SignedNumber(double value)
    {
        var text = AnalyticsFormat.Number(value, Decimals);
        var abs = Math.Round(Math.Abs(value), Math.Clamp(Decimals, 0, 15), MidpointRounding.AwayFromZero);
        return Signed && value > 0 && abs > 0 ? "+" + text : text;
    }

    private string SignOf(double value, double roundedAbs)
        => roundedAbs == 0 ? string.Empty : value < 0 ? AnalyticsFormat.Minus : Signed ? "+" : string.Empty;
}
