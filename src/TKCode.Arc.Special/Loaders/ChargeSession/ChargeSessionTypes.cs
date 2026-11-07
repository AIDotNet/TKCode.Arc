using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ChargeSession"/> is.</summary>
public enum ChargeSessionState
{
    /// <summary>Power flows and the pack fills toward the limit.</summary>
    Charging,

    /// <summary>Charging was stopped; the flow drains and the session waits for Resume.</summary>
    Stopped,

    /// <summary>The pack reached the limit (or the limit was set at or under the charge).</summary>
    Complete,
}

/// <summary>The session's figures at one moment: what <see cref="ChargeSession.StateChanged"/> reports.</summary>
/// <param name="Charge">State of charge in percent.</param>
/// <param name="EnergyAdded">Energy added this session in kWh.</param>
/// <param name="Cost">Cost of the energy added, in the session currency.</param>
/// <param name="Limit">The charge limit in percent.</param>
public sealed record ChargeSessionDetail(double Charge, double EnergyAdded, decimal Cost, double Limit);

/// <summary>Arguments of <see cref="ChargeSession.StateChanged"/>.</summary>
public sealed class ChargeSessionStateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChargeSessionStateChangedEventArgs(RoutedEvent routedEvent, ChargeSessionState state, ChargeSessionState previousState,
        bool isNewSession, ChargeSessionDetail detail)
        : base(routedEvent)
    {
        State = state;
        PreviousState = previousState;
        IsNewSession = isNewSession;
        Detail = detail;
    }

    /// <summary>The new state.</summary>
    public ChargeSessionState State { get; }

    /// <summary>The state before the change.</summary>
    public ChargeSessionState PreviousState { get; }

    /// <summary>Whether a new session started (charge back to the start, energy and cost back to zero).</summary>
    public bool IsNewSession { get; }

    /// <summary>Charge, energy, cost and limit at the moment of the change.</summary>
    public ChargeSessionDetail Detail { get; }
}

/// <summary>Arguments of <see cref="ChargeSession.LimitChanged"/>.</summary>
public sealed class ChargeSessionLimitChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ChargeSessionLimitChangedEventArgs(RoutedEvent routedEvent, double limit)
        : base(routedEvent)
    {
        Limit = limit;
    }

    /// <summary>The settled limit in percent.</summary>
    public double Limit { get; }
}
