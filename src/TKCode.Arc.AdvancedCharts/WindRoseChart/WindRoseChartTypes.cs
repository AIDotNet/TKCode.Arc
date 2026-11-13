using System.ComponentModel;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>What the length of a <see cref="WindRoseChart"/> petal measures.</summary>
public enum WindRoseRadialAxis
{
    /// <summary>Percent of all observations, calm included.</summary>
    Percent,

    /// <summary>The amounts as given, such as observation counts.</summary>
    Count
}

/// <summary>
/// One direction of a <see cref="WindRoseChart"/> with an amount per speed bin (the wide form of the data). Several rows
/// for the same sector add up.
/// </summary>
public sealed class WindRoseRow
{
    /// <summary>Creates an empty row (for XAML).</summary>
    public WindRoseRow()
    {
    }

    /// <summary>Creates a row for a direction in degrees clockwise from north.</summary>
    public WindRoseRow(double direction, params double[] values)
    {
        Direction = direction;
        Values.AddRange(values);
    }

    /// <summary>Creates a row for a direction name, such as "NNE".</summary>
    public WindRoseRow(string direction, params double[] values)
    {
        Direction = direction;
        Values.AddRange(values);
    }

    /// <summary>
    /// The direction: a number is degrees clockwise from north (360 and 0 are north); a string is a number, a compass name
    /// such as "NNE" or "NbE", the compass name in the current language, or one of <see cref="WindRoseChart.DirectionLabels"/>.
    /// </summary>
    public object? Direction { get; set; }

    /// <summary>The amounts per speed bin, inner to outer (in XAML: <c>Values="1.2 3.4 0.8"</c>, spaces or commas). NaN and negative amounts count as zero.</summary>
    [TypeConverter(typeof(WindRoseValuesConverter))]
    public AvaloniaList<double> Values { get; set; } = new();
}

/// <summary>Reads <see cref="WindRoseRow.Values"/> from XAML text: numbers separated by spaces, commas or semicolons, in the invariant culture.</summary>
public sealed class WindRoseValuesConverter : TypeConverter
{
    private static readonly char[] s_separators = [' ', ',', ';', '\t', '\r', '\n'];

    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => Parse(value as string ?? string.Empty);

    /// <summary>Parses "1.2 3.4 0.8" or "1.2, 3.4, 0.8" into a list of amounts.</summary>
    public static AvaloniaList<double> Parse(string text)
    {
        var values = new AvaloniaList<double>();
        foreach (var part in text.Split(s_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"'{part}' is not a number.");
            }
            values.Add(value);
        }
        return values;
    }
}

/// <summary>One amount of a <see cref="WindRoseChart"/>: a direction, a speed bin and how much (the long form of the data).</summary>
public sealed class WindRoseRecord
{
    /// <summary>Creates an empty record (for XAML).</summary>
    public WindRoseRecord()
    {
    }

    /// <summary>Creates a record.</summary>
    public WindRoseRecord(object? direction, int bin, double value)
    {
        Direction = direction;
        Bin = bin;
        Value = value;
    }

    /// <summary>The direction, read like <see cref="WindRoseRow.Direction"/>.</summary>
    public object? Direction { get; set; }

    /// <summary>Index of the speed bin in <see cref="WindRoseChart.Bins"/>; other indexes are ignored.</summary>
    public int Bin { get; set; }

    /// <summary>The amount; NaN and negative amounts count as zero.</summary>
    public double Value { get; set; }
}

/// <summary>One speed bin of a <see cref="WindRoseChart"/>: a band of each petal, from its minimum up to the next bin's minimum.</summary>
public sealed class WindRoseBin
{
    /// <summary>Creates an empty bin (for XAML).</summary>
    public WindRoseBin()
    {
    }

    /// <summary>Creates a bin.</summary>
    public WindRoseBin(double minimum, string? label = null, Color? color = null)
    {
        Minimum = minimum;
        Label = label;
        Color = color;
    }

    /// <summary>The lowest speed in the bin (inclusive). The next bin's minimum is its upper bound; the last bin is open.</summary>
    public double Minimum { get; set; }

    /// <summary>Name in the legend and the tooltip; null gives "0.5–2 m/s", or "≥10 m/s" for the last bin, with the chart's unit.</summary>
    public string? Label { get; set; }

    /// <summary>Fixed color of the bin; null takes the bin's step of the chart's sequential ramp.</summary>
    public Color? Color { get; set; }
}

/// <summary>A direction sector of a <see cref="WindRoseChart"/> as drawn: what the tooltip, the reading and the events report.</summary>
public sealed class WindRoseSector
{
    internal WindRoseSector(int index, string label, double direction, double width, double[] values, double total, double share)
    {
        Index = index;
        Label = label;
        Direction = direction;
        From = Normalize(direction - width / 2);
        To = Normalize(direction + width / 2);
        Values = values;
        Total = total;
        Share = share;
    }

    private static double Normalize(double degrees) => ((degrees % 360) + 360) % 360;

    /// <summary>Position from sector 0.</summary>
    public int Index { get; }

    /// <summary>The direction label, such as "NNE".</summary>
    public string Label { get; }

    /// <summary>Centre direction in degrees clockwise from north.</summary>
    public double Direction { get; }

    /// <summary>Where the sector starts, in degrees clockwise from north (inclusive).</summary>
    public double From { get; }

    /// <summary>Where the sector ends, in degrees clockwise from north (exclusive).</summary>
    public double To { get; }

    /// <summary>The amounts per speed bin, as given (before the percent scale).</summary>
    public IReadOnlyList<double> Values { get; }

    /// <summary>Sum of <see cref="Values"/>.</summary>
    public double Total { get; }

    /// <summary>The sector's fraction of all observations, calm included (0 to 1).</summary>
    public double Share { get; }
}

/// <summary>Arguments of <see cref="WindRoseChart.SelectionChanged"/>.</summary>
public sealed class WindRoseSelectionChangedEventArgs : RoutedEventArgs
{
    internal WindRoseSelectionChangedEventArgs(int oldIndex, int newIndex, WindRoseSector? oldItem, WindRoseSector? newItem)
        : base(WindRoseChart.SelectionChangedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        OldItem = oldItem;
        NewItem = newItem;
    }

    /// <summary>The sector selected before, or -1.</summary>
    public int OldIndex { get; }

    /// <summary>The sector selected now, or -1.</summary>
    public int NewIndex { get; }

    /// <summary>The sector selected before, or null.</summary>
    public WindRoseSector? OldItem { get; }

    /// <summary>The sector selected now, or null.</summary>
    public WindRoseSector? NewItem { get; }
}

/// <summary>Arguments of <see cref="WindRoseChart.ItemInvoked"/>.</summary>
public sealed class WindRoseItemInvokedEventArgs : RoutedEventArgs
{
    internal WindRoseItemInvokedEventArgs(int index, WindRoseSector item, int bin)
        : base(WindRoseChart.ItemInvokedEvent)
    {
        Index = index;
        Item = item;
        Bin = bin;
    }

    /// <summary>The sector that was chosen.</summary>
    public int Index { get; }

    /// <summary>The sector that was chosen.</summary>
    public WindRoseSector Item { get; }

    /// <summary>The speed bin under the pointer, or -1 when the sector was chosen as a whole (keyboard, or outside its petal).</summary>
    public int Bin { get; }
}
