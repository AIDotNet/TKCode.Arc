using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where the period's spend stands against the spending limit (<see cref="UsageBilling.LimitStatus"/>).</summary>
public enum UsageBillingLimitStatus
{
    /// <summary>No limit applies: it is turned off or has no positive amount.</summary>
    None,

    /// <summary>The spend is below the alert threshold.</summary>
    Ok,

    /// <summary>The spend has passed the alert threshold but not the limit.</summary>
    Near,

    /// <summary>The spend has reached or passed the limit.</summary>
    Reached
}

/// <summary>
/// Arguments of <see cref="UsageBilling.SpendLimitChanged"/>: the limit before and after the change and whether the
/// limit is turned on.
/// </summary>
public sealed class UsageBillingLimitChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsageBillingLimitChangedEventArgs(RoutedEvent routedEvent, object? source, decimal? oldLimit, decimal? newLimit,
                                             bool isEnabled)
        : base(routedEvent, source)
    {
        OldLimit = oldLimit;
        NewLimit = newLimit;
        IsEnabled = isEnabled;
    }

    /// <summary>The limit amount before the change (null when none was set).</summary>
    public decimal? OldLimit { get; }

    /// <summary>The limit amount after the change (null when none is set).</summary>
    public decimal? NewLimit { get; }

    /// <summary>Whether the limit is turned on after the change.</summary>
    public bool IsEnabled { get; }
}
