using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="LiveCheckout.TotalsChanged"/>.</summary>
public sealed class LiveCheckoutTotalsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LiveCheckoutTotalsChangedEventArgs(RoutedEvent routedEvent, LiveCheckoutTotals totals) : base(routedEvent)
    {
        Totals = totals;
    }

    /// <summary>The new subtotal, discount, tax and total.</summary>
    public LiveCheckoutTotals Totals { get; }
}

/// <summary>Arguments of <see cref="LiveCheckout.CouponApplied"/> and <see cref="LiveCheckout.CouponRemoved"/>.</summary>
public sealed class LiveCheckoutCouponEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LiveCheckoutCouponEventArgs(RoutedEvent routedEvent, string code, decimal percent) : base(routedEvent)
    {
        Code = code;
        Percent = percent;
    }

    /// <summary>The coupon code.</summary>
    public string Code { get; }

    /// <summary>Its discount in percent.</summary>
    public decimal Percent { get; }
}

/// <summary>Arguments of <see cref="LiveCheckout.Paid"/> and <see cref="LiveCheckout.ReceiptRequested"/>.</summary>
public sealed class LiveCheckoutPaidEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LiveCheckoutPaidEventArgs(RoutedEvent routedEvent, LiveCheckoutOrder order, LiveCheckoutReceipt receipt) : base(routedEvent)
    {
        Order = order;
        Receipt = receipt;
    }

    /// <summary>The order that was charged.</summary>
    public LiveCheckoutOrder Order { get; }

    /// <summary>The receipt as shown, with every value filled in.</summary>
    public LiveCheckoutReceipt Receipt { get; }
}

/// <summary>Arguments of <see cref="LiveCheckout.PaymentFailed"/>.</summary>
public sealed class LiveCheckoutPaymentFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LiveCheckoutPaymentFailedEventArgs(RoutedEvent routedEvent, LiveCheckoutOrder order, string message) : base(routedEvent)
    {
        Order = order;
        Message = message;
    }

    /// <summary>The order that could not be charged.</summary>
    public LiveCheckoutOrder Order { get; }

    /// <summary>The message shown above the pay button.</summary>
    public string Message { get; }
}
