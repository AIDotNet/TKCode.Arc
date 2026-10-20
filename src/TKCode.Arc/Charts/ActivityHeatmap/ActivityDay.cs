namespace TKCode.Arc;

/// <summary>One day of an <see cref="ActivityHeatmap"/>.</summary>
public sealed class ActivityDay
{
    /// <summary>Creates an empty day (for XAML).</summary>
    public ActivityDay()
    {
    }

    /// <summary>Creates a day.</summary>
    public ActivityDay(DateOnly date, double count)
    {
        Date = date;
        Count = count;
    }

    /// <summary>Calendar day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Activity on that day; negatives count as zero.</summary>
    public double Count { get; set; }
}

/// <summary>Arguments of <see cref="ActivityHeatmap.DateSelected"/>.</summary>
public sealed class ActivityHeatmapDateSelectedEventArgs : EventArgs
{
    internal ActivityHeatmapDateSelectedEventArgs(DateOnly date, double count, int level)
    {
        Date = date;
        Count = count;
        Level = level;
    }

    /// <summary>The selected day.</summary>
    public DateOnly Date { get; }

    /// <summary>The day's total.</summary>
    public double Count { get; }

    /// <summary>The day's color level, 0 (none) to 4 (busiest).</summary>
    public int Level { get; }
}

/// <summary>The resolved calendar behind an <see cref="ActivityHeatmap"/>.</summary>
internal sealed class ActivityHeatmapModel
{
    public DateOnly Start;
    public int Length;
    public int Lead;
    public int Weeks;
    public double Total;
    public double[] Counts = [];
    public int[] Levels = [];
    public double[] Thresholds = [1, 2, 3];
    public readonly List<(int Month, int Column, string Label)> Months = new();
    public readonly int[] PerLevel = new int[5];

    public static ActivityHeatmapModel Build(IReadOnlyList<ActivityDay> days, DayOfWeek firstDay, IReadOnlyList<double>? thresholds,
                                             System.Globalization.CultureInfo culture)
    {
        var model = new ActivityHeatmapModel();
        if (days.Count == 0)
        {
            model.Start = new DateOnly(2025, 1, 1);
        }
        else
        {
            var min = days.Min(d => d.Date);
            var max = days.Max(d => d.Date);
            model.Start = min;
            model.Length = max.DayNumber - min.DayNumber + 1;
        }
        model.Counts = new double[model.Length];
        foreach (var day in days)
        {
            var index = day.Date.DayNumber - model.Start.DayNumber;
            if (index >= 0 && index < model.Length)
            {
                model.Counts[index] += Math.Max(0, day.Count);
            }
        }
        var busiest = model.Counts.Length == 0 ? 0 : model.Counts.Max();
        model.Thresholds = thresholds is { Count: >= 3 }
            ? [thresholds[0], thresholds[1], thresholds[2]]
            : [Math.Max(1, Math.Ceiling(busiest * 0.25)), Math.Max(2, Math.Ceiling(busiest * 0.5)), Math.Max(3, Math.Ceiling(busiest * 0.75))];
        model.Levels = new int[model.Length];
        for (var i = 0; i < model.Length; i++)
        {
            var count = model.Counts[i];
            var level = count <= 0 ? 0 : count <= model.Thresholds[0] ? 1 : count <= model.Thresholds[1] ? 2 : count <= model.Thresholds[2] ? 3 : 4;
            model.Levels[i] = level;
            model.PerLevel[level]++;
        }
        model.Total = model.Counts.Sum();
        model.Lead = ((int)model.Start.DayOfWeek - (int)firstDay + 7) % 7;
        model.Weeks = Math.Max(1, (int)Math.Ceiling((model.Lead + model.Length) / 7.0));
        for (var i = 0; i < model.Length; i++)
        {
            var date = model.Start.AddDays(i);
            if (i == 0 || date.Day == 1)
            {
                model.Months.Add((date.Year * 12 + date.Month - 1, (model.Lead + i) / 7,
                    culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month)));
            }
        }
        // A partial first month keeps its label only when there is room before the next one.
        if (model.Months.Count > 1 && model.Months[1].Column - model.Months[0].Column < 3)
        {
            model.Months.RemoveAt(0);
        }
        return model;
    }

    public DateOnly DateAt(int index) => Start.AddDays(index);

    /// <summary>Day index at a grid position, or -1 outside the range.</summary>
    public int IndexAt(int column, int row)
    {
        var index = column * 7 + row - Lead;
        return index >= 0 && index < Length ? index : -1;
    }

    public (int Column, int Row) PositionOf(int index) => ((index + Lead) / 7, (index + Lead) % 7);
}
