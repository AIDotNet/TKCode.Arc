using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One measure an <see cref="ActivityTerrain"/> can show (visits, builds, incidents …): a week of hourly values, the
/// week before for comparison, and how to name, color and judge it. Values run Monday 00:00 first, hour by hour, day by
/// day: 168 numbers (7 × 24); missing entries count as zero.
/// </summary>
public class ActivityTerrainMetric : AvaloniaObject
{
    /// <summary>Identifies the metric (<see cref="ActivityTerrain.SelectedMetricKey"/>); defaults to the label.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, string?>(nameof(Key));

    /// <summary>The name on the metric switch and in the summary ("Visits").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, string?>(nameof(Label));

    /// <summary>The unit after a value in the card, the busiest day and announcements ("visits").</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, string?>(nameof(Unit));

    /// <summary>This week's 168 hourly values, Monday 00:00 first (day × 24 + hour).</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>The previous week's values in the same order; without them the change and "Last week" rows are hidden.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> PreviousValuesProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, IReadOnlyList<double>?>(nameof(PreviousValues));

    /// <summary>The color of the bars, the legend and the card's current hour. Default <see cref="ActivityTerrainTone.Accent"/>.</summary>
    public static readonly StyledProperty<ActivityTerrainTone> ToneProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, ActivityTerrainTone>(nameof(Tone));

    /// <summary>Whether a rise is good news (green) or bad news (red, as for errors). Default true.</summary>
    public static readonly StyledProperty<bool> HigherIsBetterProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, bool>(nameof(HigherIsBetter), true);

    /// <summary>The title over the week's total; null uses <see cref="ActivityTerrain.SummaryTitleFormat"/> ("Visits this week").</summary>
    public static readonly StyledProperty<string?> SummaryTitleProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, string?>(nameof(SummaryTitle));

    /// <summary>A .NET number format for values ("N0", "N1", "C0" …); null groups whole numbers (12,345).</summary>
    public static readonly StyledProperty<string?> ValueFormatProperty =
        AvaloniaProperty.Register<ActivityTerrainMetric, string?>(nameof(ValueFormat));

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="UnitProperty"/>
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="PreviousValuesProperty"/>
    public IReadOnlyList<double>? PreviousValues { get => GetValue(PreviousValuesProperty); set => SetValue(PreviousValuesProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public ActivityTerrainTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="HigherIsBetterProperty"/>
    public bool HigherIsBetter { get => GetValue(HigherIsBetterProperty); set => SetValue(HigherIsBetterProperty, value); }

    /// <inheritdoc cref="SummaryTitleProperty"/>
    public string? SummaryTitle { get => GetValue(SummaryTitleProperty); set => SetValue(SummaryTitleProperty, value); }

    /// <inheritdoc cref="ValueFormatProperty"/>
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }

    /// <summary>The key used to select the metric: <see cref="Key"/>, or the label when no key is set.</summary>
    public string? EffectiveKey => Key ?? Label;

    /// <summary>The value at <paramref name="cell"/> (0 when missing or not a number).</summary>
    public double ValueAt(ActivityTerrainCell cell) => At(Values, cell.Index);

    /// <summary>The previous week's value at <paramref name="cell"/>, or null without previous values.</summary>
    public double? PreviousValueAt(ActivityTerrainCell cell) => PreviousValues is { } previous ? At(previous, cell.Index) : null;

    /// <summary>
    /// Flattens a 7 × 24 grid (row = day from Monday, column = hour) into the 168 values <see cref="Values"/> takes.
    /// </summary>
    public static double[] Flatten(double[,] grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var values = new double[ActivityTerrainMath.Cells];
        var days = Math.Min(ActivityTerrainMath.Days, grid.GetLength(0));
        var hours = Math.Min(ActivityTerrainMath.Hours, grid.GetLength(1));
        for (var d = 0; d < days; d++)
        {
            for (var h = 0; h < hours; h++)
            {
                values[d * ActivityTerrainMath.Hours + h] = grid[d, h];
            }
        }
        return values;
    }

    /// <summary>Raised when any property changes (the terrain refreshes what it shows).</summary>
    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal static double At(IReadOnlyList<double>? values, int index)
    {
        if (values is null || index < 0 || index >= values.Count)
        {
            return 0;
        }
        var value = values[index];
        return double.IsFinite(value) ? value : 0;
    }
}
