using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How <see cref="CohortRetention"/> draws its data.</summary>
public enum CohortRetentionView
{
    /// <summary>A triangle of cells, one row per cohort and one column per month, darker where more of the cohort stayed.</summary>
    Heatmap,

    /// <summary>One line per cohort over the months, with the size-weighted average drawn on top.</summary>
    Curves
}

/// <summary>
/// One cell of a <see cref="CohortRetention"/>: a cohort (or the average row) and a month since the cohort started.
/// </summary>
/// <param name="Row">The cohort's index in the selected segment, oldest first (ignored for the average row).</param>
/// <param name="MonthIndex">The month since the start, from 0 (shown as month 1).</param>
/// <param name="IsAverage">True for the size-weighted average row under the cohorts.</param>
public readonly record struct CohortCell(int Row, int MonthIndex, bool IsAverage = false)
{
    /// <summary>The month as people read it (1 for the first month).</summary>
    public int Month => MonthIndex + 1;

    /// <summary>A cell of the average row.</summary>
    public static CohortCell Average(int monthIndex) => new(-1, monthIndex, true);
}

/// <summary>
/// One cohort: people (or accounts, workspaces) who started in the same period, and how many of them were still active in
/// each month after that. The number of values in <see cref="Retained"/> is the number of months measured so far, so
/// newer cohorts have shorter rows and the heatmap becomes a triangle.
/// </summary>
public class RetentionCohort : AvaloniaObject
{
    /// <summary>A stable identifier (events, automation ids); optional.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<RetentionCohort, string?>(nameof(Key));

    /// <summary>The cohort's name in wide layouts ("Jun 2026").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RetentionCohort, string>(nameof(Label), string.Empty);

    /// <summary>The cohort's name in compact layouts ("Jun ’26"); null uses <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> ShortLabelProperty =
        AvaloniaProperty.Register<RetentionCohort, string?>(nameof(ShortLabel));

    /// <summary>How many started in the cohort: the denominator of every rate in its row.</summary>
    public static readonly StyledProperty<int> SizeProperty =
        AvaloniaProperty.Register<RetentionCohort, int>(nameof(Size));

    /// <summary>How many were still active in month 1, 2, … (one value per month measured so far).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> RetainedProperty =
        AvaloniaProperty.Register<RetentionCohort, IReadOnlyList<int>?>(nameof(Retained));

    /// <summary>Anything you want to carry along (it comes back in the events).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<RetentionCohort, object?>(nameof(Tag));

    /// <summary>Creates an empty cohort.</summary>
    public RetentionCohort()
    {
    }

    /// <summary>Creates a cohort with its name, size and retained counts.</summary>
    public RetentionCohort(string label, int size, IReadOnlyList<int> retained, string? shortLabel = null)
    {
        Label = label;
        Size = size;
        Retained = retained;
        ShortLabel = shortLabel;
    }

    /// <summary>Raised when any property of the cohort changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ShortLabelProperty"/>
    public string? ShortLabel { get => GetValue(ShortLabelProperty); set => SetValue(ShortLabelProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public int Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="RetainedProperty"/>
    public IReadOnlyList<int>? Retained { get => GetValue(RetainedProperty); set => SetValue(RetainedProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>Months measured so far.</summary>
    public int MeasuredMonths => Retained?.Count ?? 0;

    /// <summary>The share still active in month <paramref name="monthIndex"/> + 1 (0–1), or null when not measured yet.</summary>
    public double? RateAt(int monthIndex)
    {
        var retained = Retained;
        if (retained is null || monthIndex < 0 || monthIndex >= retained.Count || Size <= 0)
        {
            return null;
        }
        return retained[monthIndex] / (double)Size;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One segment of a <see cref="CohortRetention"/> (all customers, one plan, one channel): its own cohorts. The segment
/// switch in the header lists the segments by <see cref="Label"/>.
/// </summary>
public class RetentionSegment : AvaloniaObject
{
    /// <summary>A stable identifier; optional.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<RetentionSegment, string?>(nameof(Key));

    /// <summary>The segment's name on its switch button ("All", "Starter").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RetentionSegment, string>(nameof(Label), string.Empty);

    /// <summary>What the segment holds, said when it is selected ("Starter-plan spaces"); null uses <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RetentionSegment, string?>(nameof(Description));

    /// <summary>Anything you want to carry along.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<RetentionSegment, object?>(nameof(Tag));

    /// <summary>Creates an empty segment.</summary>
    public RetentionSegment()
    {
        Cohorts.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (RetentionCohort cohort in e.OldItems)
                {
                    cohort.Changed -= OnCohortChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (RetentionCohort cohort in e.NewItems)
                {
                    cohort.Changed += OnCohortChanged;
                }
            }
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                foreach (var cohort in Cohorts)
                {
                    cohort.Changed -= OnCohortChanged;
                    cohort.Changed += OnCohortChanged;
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Creates a segment with a name and its cohorts.</summary>
    public RetentionSegment(string label, IEnumerable<RetentionCohort> cohorts, string? description = null) : this()
    {
        Label = label;
        Description = description;
        Cohorts.AddRange(cohorts);
    }

    /// <summary>Raised when the segment, its list of cohorts or any of its cohorts changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The cohorts, oldest first.</summary>
    [Content]
    public AvaloniaList<RetentionCohort> Cohorts { get; } = new();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnCohortChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>A cell of a <see cref="CohortRetention"/> with its numbers (cursor changes and invoked cells).</summary>
public class CohortCellEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CohortCellEventArgs(RoutedEvent routedEvent, CohortCell? cell, RetentionCohort? cohort, double? rate, long? retained,
        long? size, int coveredCohorts) : base(routedEvent)
    {
        Cell = cell;
        Cohort = cohort;
        Rate = rate;
        Retained = retained;
        Size = size;
        CoveredCohorts = coveredCohorts;
    }

    /// <summary>The cell, or null when the cursor was cleared.</summary>
    public CohortCell? Cell { get; }

    /// <summary>The cell's cohort (null for the average row or no cell).</summary>
    public RetentionCohort? Cohort { get; }

    /// <summary>The share still active (0–1).</summary>
    public double? Rate { get; }

    /// <summary>How many were still active (summed over the covered cohorts for the average row).</summary>
    public long? Retained { get; }

    /// <summary>How many started (summed over the covered cohorts for the average row).</summary>
    public long? Size { get; }

    /// <summary>For the average row: how many cohorts had reached this month (1 for a cohort's own cell).</summary>
    public int CoveredCohorts { get; }
}

/// <summary>Arguments of <see cref="CohortRetention.ViewChanged"/>.</summary>
public class CohortRetentionViewChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CohortRetentionViewChangedEventArgs(RoutedEvent routedEvent, CohortRetentionView oldView, CohortRetentionView newView)
        : base(routedEvent)
    {
        OldView = oldView;
        NewView = newView;
    }

    /// <summary>The view before the change.</summary>
    public CohortRetentionView OldView { get; }

    /// <summary>The view after the change.</summary>
    public CohortRetentionView NewView { get; }
}

/// <summary>Arguments of <see cref="CohortRetention.SegmentChanged"/>.</summary>
public class CohortRetentionSegmentChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CohortRetentionSegmentChangedEventArgs(RoutedEvent routedEvent, RetentionSegment? oldSegment, RetentionSegment? newSegment)
        : base(routedEvent)
    {
        OldSegment = oldSegment;
        NewSegment = newSegment;
    }

    /// <summary>The segment before the change.</summary>
    public RetentionSegment? OldSegment { get; }

    /// <summary>The segment after the change.</summary>
    public RetentionSegment? NewSegment { get; }
}
