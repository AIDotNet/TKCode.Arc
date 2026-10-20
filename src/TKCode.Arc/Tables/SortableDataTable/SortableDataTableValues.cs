using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>Value helpers of <see cref="SortableDataTable"/>: Arc's comparison, emptiness and number detection.</summary>
internal static class SortableDataTableValues
{
    private static readonly CompareInfo s_compare = CultureInfo.GetCultureInfo("en").CompareInfo;
    private static readonly CompareOptions s_options = ProbeOptions();

    /// <summary>Arc: <c>value == null || value === ""</c>.</summary>
    public static bool IsEmpty(object? value) => value is null || value is DBNull || value is string { Length: 0 };

    public static bool IsNumber(object? value) => value is double or float or decimal or int or long or short or byte
        or sbyte or uint or ulong or ushort;

    private static double ToDouble(object value) => value switch
    {
        double d => d,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        sbyte sb => sb,
        uint ui => ui,
        ulong ul => ul,
        ushort us => us,
        _ => double.NaN
    };

    /// <summary>
    /// Ascending comparison of two non-empty values: numbers numerically, dates and other comparable values of
    /// one type with their own order, everything else as text with Arc's collator (numeric, base sensitivity).
    /// </summary>
    public static int Compare(object left, object right, IComparer? comparer)
    {
        if (comparer is not null)
        {
            return comparer.Compare(left, right);
        }
        if (IsNumber(left) && IsNumber(right))
        {
            return ToDouble(left).CompareTo(ToDouble(right));
        }
        if (left.GetType() == right.GetType() && left is IComparable comparable && left is not string)
        {
            return comparable.CompareTo(right);
        }
        return CompareNatural(Text(left), Text(right));
    }

    private static string Text(object value) => value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Natural text order: digit runs compare as numbers, other runs ignore case and accents.</summary>
    public static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            var digitA = char.IsAsciiDigit(a[i]);
            var digitB = char.IsAsciiDigit(b[j]);
            if (digitA && digitB)
            {
                var si = i;
                var sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i]))
                {
                    i++;
                }
                while (j < b.Length && char.IsAsciiDigit(b[j]))
                {
                    j++;
                }
                var runA = a.AsSpan(si, i - si).TrimStart('0');
                var runB = b.AsSpan(sj, j - sj).TrimStart('0');
                if (runA.Length != runB.Length)
                {
                    return runA.Length.CompareTo(runB.Length);
                }
                var digits = runA.SequenceCompareTo(runB);
                if (digits != 0)
                {
                    return Math.Sign(digits);
                }
                continue;
            }
            if (digitA != digitB)
            {
                // Digits sort before letters, as in the ICU root collation.
                return digitA ? -1 : 1;
            }
            var startA = i;
            var startB = j;
            while (i < a.Length && !char.IsAsciiDigit(a[i]))
            {
                i++;
            }
            while (j < b.Length && !char.IsAsciiDigit(b[j]))
            {
                j++;
            }
            var text = s_compare.Compare(a, startA, i - startA, b, startB, j - startB, s_options);
            if (text != 0)
            {
                return Math.Sign(text);
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }

    private static CompareOptions ProbeOptions()
    {
        // Invariant globalization (common in AOT builds) may not support linguistic options.
        try
        {
            _ = CultureInfo.GetCultureInfo("en").CompareInfo.Compare("a", "B", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            return CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        }
        catch (Exception)
        {
            return CompareOptions.OrdinalIgnoreCase;
        }
    }

    /// <summary>Formats a cell value: Formatter, then StringFormat, then the value's text; null shows Arc's en dash.</summary>
    public static string Format(DataTableColumn column, object? value)
    {
        if (column.Formatter is { } formatter)
        {
            return formatter(value);
        }
        if (value is null || value is DBNull)
        {
            return "–";
        }
        if (column.StringFormat is { Length: > 0 } format)
        {
            if (format.Contains('{'))
            {
                return string.Format(CultureInfo.CurrentCulture, format, value);
            }
            if (value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.CurrentCulture);
            }
        }
        return value as string ?? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }
}

/// <summary>Evaluates a column binding against row items without putting them in the tree.</summary>
internal sealed class SortableDataTableBindingProbe : StyledElement
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<SortableDataTableBindingProbe, object?>("Value");

    public SortableDataTableBindingProbe(BindingBase binding)
    {
        Bind(ValueProperty, binding);
    }

    public object? Evaluate(object item)
    {
        DataContext = item;
        return GetValue(ValueProperty);
    }

    public void Release() => DataContext = null;
}
