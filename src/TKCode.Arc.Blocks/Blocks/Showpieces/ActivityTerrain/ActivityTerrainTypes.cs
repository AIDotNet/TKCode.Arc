using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>One hour of the week in an <see cref="ActivityTerrain"/>: a day (0 = Monday … 6 = Sunday) and an hour (0–23).</summary>
/// <param name="Day">The day of the week, 0 (Monday) to 6 (Sunday).</param>
/// <param name="Hour">The hour of the day, 0 (00:00 to 01:00) to 23.</param>
public readonly record struct ActivityTerrainCell(int Day, int Hour)
{
    /// <summary>The index of the cell in a week of values (day × 24 + hour).</summary>
    public int Index => Day * ActivityTerrainMath.Hours + Hour;

    /// <summary>True when the day is 0–6 and the hour 0–23.</summary>
    public bool IsValid => Day is >= 0 and < ActivityTerrainMath.Days && Hour is >= 0 and < ActivityTerrainMath.Hours;

    /// <summary>The cell at <paramref name="index"/> (day × 24 + hour).</summary>
    public static ActivityTerrainCell FromIndex(int index) => new(index / ActivityTerrainMath.Hours, index % ActivityTerrainMath.Hours);
}

/// <summary>How an <see cref="ActivityTerrain"/> shows the week.</summary>
public enum ActivityTerrainViewMode
{
    /// <summary>Bars standing on a floor, seen from above and to the side; drag to orbit.</summary>
    Perspective,

    /// <summary>The bars pressed flat and seen straight from above: a heat map of the week.</summary>
    Top
}

/// <summary>The color a metric's bars, legend and change take.</summary>
public enum ActivityTerrainTone
{
    /// <summary>The theme's accent (gray with the default neutral accent).</summary>
    Accent,

    /// <summary>A gray that ignores the accent.</summary>
    Neutral,

    /// <summary>The success color (green).</summary>
    Success,

    /// <summary>The warning color (amber).</summary>
    Warning,

    /// <summary>The danger color (red): errors, incidents.</summary>
    Danger
}

/// <summary>
/// The hours that count as working hours for the "in working hours" share of the busiest day: from
/// <see cref="FirstDay"/> to <see cref="LastDay"/> and from <see cref="FirstHour"/>:00 to the end of
/// <see cref="LastHour"/> (default Monday to Friday, 09:00 to 17:59).
/// </summary>
public sealed class ActivityTerrainWorkingHours
{
    /// <summary>First working day, 0 (Monday) to 6. Default 0.</summary>
    public int FirstDay { get; set; }

    /// <summary>Last working day, 0 (Monday) to 6. Default 4 (Friday).</summary>
    public int LastDay { get; set; } = 4;

    /// <summary>First working hour, 0–23. Default 9.</summary>
    public int FirstHour { get; set; } = 9;

    /// <summary>Last working hour (counted whole), 0–23. Default 17 (until 17:59).</summary>
    public int LastHour { get; set; } = 17;

    /// <summary>True when <paramref name="cell"/> falls in the working hours.</summary>
    public bool Contains(ActivityTerrainCell cell) =>
        cell.Day >= FirstDay && cell.Day <= LastDay && cell.Hour >= FirstHour && cell.Hour <= LastHour;
}

/// <summary>Arguments of <see cref="ActivityTerrain.CellHovered"/> and <see cref="ActivityTerrain.CellFocused"/>.</summary>
public sealed class ActivityTerrainCellEventArgs : RoutedEventArgs
{
    public ActivityTerrainCellEventArgs(RoutedEvent routedEvent, ActivityTerrainCell? cell, double? value) : base(routedEvent)
    {
        Cell = cell;
        Value = value;
    }

    /// <summary>The cell, or null when the highlight cleared or the view returned to the whole week.</summary>
    public ActivityTerrainCell? Cell { get; }

    /// <summary>The selected metric's value at <see cref="Cell"/>, when there is one.</summary>
    public double? Value { get; }
}

/// <summary>Arguments of <see cref="ActivityTerrain.ViewModeChanged"/>.</summary>
public sealed class ActivityTerrainViewModeChangedEventArgs : RoutedEventArgs
{
    public ActivityTerrainViewModeChangedEventArgs(RoutedEvent routedEvent, ActivityTerrainViewMode viewMode) : base(routedEvent)
    {
        ViewMode = viewMode;
    }

    /// <summary>The view now shown.</summary>
    public ActivityTerrainViewMode ViewMode { get; }
}
