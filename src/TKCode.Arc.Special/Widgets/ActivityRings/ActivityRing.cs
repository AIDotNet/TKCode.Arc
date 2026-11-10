using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One daily goal of an <see cref="ActivityRings"/>: what has been done (<see cref="Value"/>) against what was planned
/// (<see cref="Goal"/>). The ring is full at the goal and keeps going round past it.
/// </summary>
public class ActivityRing : AvaloniaObject
{
    /// <summary>Name of the goal, shown in the legend and read by assistive technology, such as "Move".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActivityRing, string>(nameof(Label), string.Empty);

    /// <summary>What has been done so far, in the goal's unit. Negative values count as zero.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ActivityRing, double>(nameof(Value));

    /// <summary>The amount that fills the ring once. Zero or less leaves the ring empty.</summary>
    public static readonly StyledProperty<double> GoalProperty =
        AvaloniaProperty.Register<ActivityRing, double>(nameof(Goal), 100);

    /// <summary>Unit written after the goal in the legend, such as "kcal" or "min". Null writes none.</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ActivityRing, string?>(nameof(Unit));

    /// <summary>Color of the ring. Null takes the next series color of the theme (Series1 for the outer ring).</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<ActivityRing, IBrush?>(nameof(Brush));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="GoalProperty"/>
    public double Goal
    {
        get => GetValue(GoalProperty);
        set => SetValue(GoalProperty, value);
    }

    /// <inheritdoc cref="UnitProperty"/>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <inheritdoc cref="BrushProperty"/>
    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>
    /// How far round the ring is: <see cref="Value"/> divided by <see cref="Goal"/>, so 1 is a full ring and 1.4 a full ring
    /// with 40 percent overlapping it. Zero for a goal of zero or less; at most <see cref="ActivityRings.MaxProgress"/>.
    /// </summary>
    public double Progress => ActivityRings.ProgressOf(Value, Goal);
}
