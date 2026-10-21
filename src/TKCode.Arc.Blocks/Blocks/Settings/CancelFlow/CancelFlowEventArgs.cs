using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="CancelFlow.StepChanged"/>.</summary>
public sealed class CancelFlowStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CancelFlowStepChangedEventArgs(RoutedEvent routedEvent, CancelFlowStep oldStep, CancelFlowStep newStep)
        : base(routedEvent)
    {
        OldStep = oldStep;
        NewStep = newStep;
    }

    /// <summary>The step that was shown before.</summary>
    public CancelFlowStep OldStep { get; }

    /// <summary>The step shown now.</summary>
    public CancelFlowStep NewStep { get; }
}

/// <summary>Arguments of <see cref="CancelFlow.OfferAccepted"/>.</summary>
public sealed class CancelFlowOfferEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CancelFlowOfferEventArgs(RoutedEvent routedEvent, CancelFlowReason reason, CancelFlowOffer offer)
        : base(routedEvent)
    {
        Reason = reason;
        Offer = offer;
    }

    /// <summary>The reason the person chose.</summary>
    public CancelFlowReason Reason { get; }

    /// <summary>The offer they accepted.</summary>
    public CancelFlowOffer Offer { get; }
}

/// <summary>Arguments of <see cref="CancelFlow.CancellationConfirmed"/>.</summary>
public sealed class CancelFlowCancelEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CancelFlowCancelEventArgs(RoutedEvent routedEvent, CancelFlowReason reason, string? feedback)
        : base(routedEvent)
    {
        Reason = reason;
        Feedback = feedback;
    }

    /// <summary>The reason the person chose.</summary>
    public CancelFlowReason Reason { get; }

    /// <summary>What they wrote in the feedback field, or null when they left it empty.</summary>
    public string? Feedback { get; }
}
