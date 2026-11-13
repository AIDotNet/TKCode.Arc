using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How a <see cref="SpendAlert"/>'s budget is doing.</summary>
public enum SpendAlertStatus
{
    /// <summary>No alert is crossed and the month's pace stays within the budget.</summary>
    OnTrack,

    /// <summary>No alert is crossed yet, but at the current pace the budget runs out before the period ends.</summary>
    AtRisk,

    /// <summary>Spending has crossed at least one switched-on alert below the budget.</summary>
    Warning,

    /// <summary>Spending is above the budget.</summary>
    Over
}

/// <summary>
/// One alert of a <see cref="SpendAlert"/>: a share of the budget and whether it is switched on. Edits made in the block
/// write back to these objects, and changes made in code reach the block at once.
/// </summary>
public class SpendThreshold : AvaloniaObject
{
    /// <summary>The share of the budget that raises the alert (0.8 = 80%), from 0.01 to 2.</summary>
    public static readonly StyledProperty<double> ShareProperty =
        AvaloniaProperty.Register<SpendThreshold, double>(nameof(Share), 0.8, coerce: (_, v) => SpendAlertMath.ClampShare(v));

    /// <summary>Whether the alert is switched on.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SpendThreshold, bool>(nameof(IsEnabled), true);

    /// <summary>Creates an alert at 80%, switched on.</summary>
    public SpendThreshold()
    {
    }

    /// <summary>Creates an alert at <paramref name="share"/> of the budget.</summary>
    public SpendThreshold(double share, bool isEnabled = true)
    {
        Share = share;
        IsEnabled = isEnabled;
    }

    /// <inheritdoc cref="ShareProperty"/>
    public double Share { get => GetValue(ShareProperty); set => SetValue(ShareProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }
}

/// <summary>Spending crossed an alert of a <see cref="SpendAlert"/>.</summary>
public sealed class SpendThresholdCrossedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SpendThresholdCrossedEventArgs(RoutedEvent routedEvent, SpendThreshold threshold, decimal spent, decimal budget)
        : base(routedEvent)
    {
        Threshold = threshold;
        Spent = spent;
        Budget = budget;
    }

    /// <summary>The highest alert crossed by the change.</summary>
    public SpendThreshold Threshold { get; }

    /// <summary>What has been spent now.</summary>
    public decimal Spent { get; }

    /// <summary>The budget at the time.</summary>
    public decimal Budget { get; }
}

/// <summary>Pure arithmetic of <see cref="SpendAlert"/>: shares, the period's pace and which alerts a change crosses.</summary>
internal static class SpendAlertMath
{
    public const double MinShare = 0.01;
    public const double MaxShare = 2;

    public static double ClampShare(double share) => double.IsNaN(share) ? 0.8 : Math.Clamp(share, MinShare, MaxShare);

    /// <summary>Spent as a share of the budget (0 when there is no budget).</summary>
    public static double Share(decimal spent, decimal budget) => budget > 0 ? (double)(spent / budget) : 0;

    /// <summary>Where spending lands at the end of the period at the pace so far (spent when no day has passed).</summary>
    public static decimal Projected(decimal spent, int daysElapsed, int daysInPeriod)
    {
        if (daysElapsed <= 0 || daysInPeriod <= 0)
        {
            return spent;
        }
        var elapsed = Math.Min(daysElapsed, daysInPeriod);
        return Math.Round(spent / elapsed * daysInPeriod, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The highest switched-on alert that spending passes when it rises from <paramref name="before"/> to <paramref name="after"/>.</summary>
    public static SpendThreshold? Crossed(IEnumerable<SpendThreshold> thresholds, decimal before, decimal after, decimal budget)
    {
        if (budget <= 0 || after <= before)
        {
            return null;
        }
        SpendThreshold? highest = null;
        foreach (var threshold in thresholds)
        {
            if (!threshold.IsEnabled)
            {
                continue;
            }
            var line = (decimal)threshold.Share * budget;
            if (before < line && after >= line && (highest is null || threshold.Share > highest.Share))
            {
                highest = threshold;
            }
        }
        return highest;
    }

    /// <summary>The highest switched-on alert at or below the current spending, or null.</summary>
    public static SpendThreshold? Reached(IEnumerable<SpendThreshold> thresholds, decimal spent, decimal budget)
    {
        if (budget <= 0)
        {
            return null;
        }
        SpendThreshold? highest = null;
        foreach (var threshold in thresholds)
        {
            if (threshold.IsEnabled && spent >= (decimal)threshold.Share * budget && (highest is null || threshold.Share > highest.Share))
            {
                highest = threshold;
            }
        }
        return highest;
    }

    /// <summary>The status of the budget.</summary>
    public static SpendAlertStatus Status(IEnumerable<SpendThreshold> thresholds, decimal spent, decimal budget, decimal projected)
    {
        if (budget <= 0)
        {
            return SpendAlertStatus.OnTrack;
        }
        if (spent > budget)
        {
            return SpendAlertStatus.Over;
        }
        if (Reached(thresholds, spent, budget) is not null)
        {
            return SpendAlertStatus.Warning;
        }
        return projected > budget ? SpendAlertStatus.AtRisk : SpendAlertStatus.OnTrack;
    }
}
