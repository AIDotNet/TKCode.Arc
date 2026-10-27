using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The three series of a <see cref="RevenueExplorer"/>.</summary>
public enum RevenueSeries
{
    /// <summary>Revenue from new customers (stacked first, above the zero line).</summary>
    New,

    /// <summary>Revenue added by existing customers (stacked on top of the new series).</summary>
    Expansion,

    /// <summary>Revenue lost (negative, below the zero line).</summary>
    Churn
}

/// <summary>What changed a <see cref="RevenueExplorer"/>'s range.</summary>
public enum RevenueRangeSource
{
    /// <summary>One of the preset buttons.</summary>
    Preset,

    /// <summary>Dragging the window or a handle of the overview strip.</summary>
    Drag,

    /// <summary>The arrow, Home or End keys on the window or a handle.</summary>
    Keyboard,

    /// <summary>A click on the overview strip outside the window.</summary>
    Click,

    /// <summary>Code (the range properties or <see cref="RevenueExplorer.SetRange"/>).</summary>
    Api
}

/// <summary>
/// One month of revenue movement in a <see cref="RevenueExplorer"/>: what new customers added, what existing customers
/// expanded by, what churned (negative), and optionally the month's closing recurring revenue.
/// </summary>
public class RevenueMonth : AvaloniaObject
{
    /// <summary>The month (any day of it; only the year and the month are used).</summary>
    public static readonly StyledProperty<DateOnly> MonthProperty =
        AvaloniaProperty.Register<RevenueMonth, DateOnly>(nameof(Month));

    /// <summary>Recurring revenue added by new customers (0 or more).</summary>
    public static readonly StyledProperty<double> NewProperty =
        AvaloniaProperty.Register<RevenueMonth, double>(nameof(New));

    /// <summary>Recurring revenue added by existing customers (0 or more).</summary>
    public static readonly StyledProperty<double> ExpansionProperty =
        AvaloniaProperty.Register<RevenueMonth, double>(nameof(Expansion));

    /// <summary>Recurring revenue lost (a negative number; a positive one is taken as its negative).</summary>
    public static readonly StyledProperty<double> ChurnProperty =
        AvaloniaProperty.Register<RevenueMonth, double>(nameof(Churn));

    /// <summary>
    /// The month's closing recurring revenue. NaN (default) derives it from the previous month plus its net new, which
    /// needs <see cref="RevenueExplorer.OpeningMrr"/> for the first month.
    /// </summary>
    public static readonly StyledProperty<double> MrrProperty =
        AvaloniaProperty.Register<RevenueMonth, double>(nameof(Mrr), double.NaN);

    /// <summary>Creates an empty month.</summary>
    public RevenueMonth()
    {
    }

    /// <summary>Creates a month with its movements.</summary>
    public RevenueMonth(DateOnly month, double @new, double expansion, double churn, double mrr = double.NaN)
    {
        Month = month;
        New = @new;
        Expansion = expansion;
        Churn = churn;
        Mrr = mrr;
    }

    /// <summary>Raised when any property changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="MonthProperty"/>
    public DateOnly Month { get => GetValue(MonthProperty); set => SetValue(MonthProperty, value); }

    /// <inheritdoc cref="NewProperty"/>
    public double New { get => GetValue(NewProperty); set => SetValue(NewProperty, value); }

    /// <inheritdoc cref="ExpansionProperty"/>
    public double Expansion { get => GetValue(ExpansionProperty); set => SetValue(ExpansionProperty, value); }

    /// <inheritdoc cref="ChurnProperty"/>
    public double Churn { get => GetValue(ChurnProperty); set => SetValue(ChurnProperty, value); }

    /// <inheritdoc cref="MrrProperty"/>
    public double Mrr { get => GetValue(MrrProperty); set => SetValue(MrrProperty, value); }

    /// <summary>New plus expansion plus churn.</summary>
    public double NetNew => New + Expansion + (Churn > 0 ? -Churn : Churn);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>A range button of a <see cref="RevenueExplorer"/>: the last <see cref="Months"/> months, or everything.</summary>
public class RevenueRangePreset : AvaloniaObject
{
    /// <summary>How many of the newest months the button selects; 0 selects every month.</summary>
    public static readonly StyledProperty<int> MonthsProperty =
        AvaloniaProperty.Register<RevenueRangePreset, int>(nameof(Months));

    /// <summary>The button's text ("3M"); null derives it from <see cref="Months"/> ("3M", or "All").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RevenueRangePreset, string?>(nameof(Label));

    /// <summary>The button's accessible name ("Last 3 months"); null derives it from <see cref="Months"/>.</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<RevenueRangePreset, string?>(nameof(AccessibleName));

    /// <summary>Creates a preset.</summary>
    public RevenueRangePreset()
    {
    }

    /// <summary>Creates a preset for the last <paramref name="months"/> months (0 = all).</summary>
    public RevenueRangePreset(int months, string? label = null, string? accessibleName = null)
    {
        Months = months;
        Label = label;
        AccessibleName = accessibleName;
    }

    /// <summary>Raised when any property changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="MonthsProperty"/>
    public int Months { get => GetValue(MonthsProperty); set => SetValue(MonthsProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Arguments of <see cref="RevenueExplorer.RangeChanged"/>.</summary>
public sealed class RevenueRangeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RevenueRangeChangedEventArgs(RoutedEvent routedEvent, int start, int end, RevenueRangeSource source) : base(routedEvent)
    {
        Start = start;
        End = end;
        RangeSource = source;
    }

    /// <summary>Index of the first month of the range.</summary>
    public int Start { get; }

    /// <summary>Index of the last month of the range (included).</summary>
    public int End { get; }

    /// <summary>What changed the range.</summary>
    public RevenueRangeSource RangeSource { get; }
}

/// <summary>Arguments of <see cref="RevenueExplorer.SeriesVisibilityChanged"/>.</summary>
public sealed class RevenueSeriesChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RevenueSeriesChangedEventArgs(RoutedEvent routedEvent, RevenueSeries series, bool isVisible) : base(routedEvent)
    {
        Series = series;
        IsVisible = isVisible;
    }

    /// <summary>The series that was switched.</summary>
    public RevenueSeries Series { get; }

    /// <summary>Whether it is shown now.</summary>
    public bool IsVisible { get; }
}

/// <summary>Arguments of <see cref="RevenueExplorer.CompareChanged"/>.</summary>
public sealed class RevenueCompareChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RevenueCompareChangedEventArgs(RoutedEvent routedEvent, bool isEnabled) : base(routedEvent)
    {
        IsCompared = isEnabled;
    }

    /// <summary>Whether the previous period is drawn now.</summary>
    public bool IsCompared { get; }
}

/// <summary>Arguments of <see cref="RevenueExplorer.ScrubChanged"/>.</summary>
public sealed class RevenueScrubChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RevenueScrubChangedEventArgs(RoutedEvent routedEvent, int? index, RevenueMonth? month) : base(routedEvent)
    {
        Index = index;
        Month = month;
    }

    /// <summary>Index of the inspected month, or null when the readout is closed.</summary>
    public int? Index { get; }

    /// <summary>The inspected month, or null.</summary>
    public RevenueMonth? Month { get; }
}
