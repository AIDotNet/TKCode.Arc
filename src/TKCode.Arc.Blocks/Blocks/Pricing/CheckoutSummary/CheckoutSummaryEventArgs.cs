using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of the promo code events of <see cref="CheckoutSummary"/>.</summary>
public sealed class CheckoutSummaryCouponEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CheckoutSummaryCouponEventArgs(RoutedEvent routedEvent, string code, CheckoutSummaryCouponResult? result)
        : base(routedEvent)
    {
        Code = code;
        Result = result;
    }

    /// <summary>The code: as typed for a refused code, as shown on the chip for an applied or removed one.</summary>
    public string Code { get; }

    /// <summary>The check's answer (null for <see cref="CheckoutSummary.CouponRemovedEvent"/>).</summary>
    public CheckoutSummaryCouponResult? Result { get; }

    /// <summary>The refusal message, when the code was refused.</summary>
    public string? Message => Result is { IsValid: false } ? Result.Message : null;
}

/// <summary>Arguments of <see cref="CheckoutSummary.PayRequestedEvent"/>; set <see cref="Cancel"/> to stop the payment.</summary>
public sealed class CheckoutSummaryPayRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CheckoutSummaryPayRequestedEventArgs(RoutedEvent routedEvent, decimal total, string formattedTotal)
        : base(routedEvent)
    {
        Total = total;
        FormattedTotal = formattedTotal;
    }

    /// <summary>The amount to charge.</summary>
    public decimal Total { get; }

    /// <summary>The amount as shown on the button, such as "$96.75".</summary>
    public string FormattedTotal { get; }

    /// <summary>Set to true to keep the button idle and not start the payment.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <see cref="CheckoutSummary.PayStateChangedEvent"/>.</summary>
public sealed class CheckoutSummaryPayStateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CheckoutSummaryPayStateChangedEventArgs(RoutedEvent routedEvent, CheckoutSummaryPayState oldState, CheckoutSummaryPayState newState, Exception? error)
        : base(routedEvent)
    {
        OldState = oldState;
        NewState = newState;
        Error = error;
    }

    /// <summary>The state before the change.</summary>
    public CheckoutSummaryPayState OldState { get; }

    /// <summary>The state now.</summary>
    public CheckoutSummaryPayState NewState { get; }

    /// <summary>The exception thrown by <see cref="CheckoutSummary.PayAsync"/>, when the new state is <see cref="CheckoutSummaryPayState.Error"/> because of it.</summary>
    public Exception? Error { get; }
}
