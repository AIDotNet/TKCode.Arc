using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How often a <see cref="BillingPlan"/> is charged.</summary>
public enum BillingInterval
{
    /// <summary>Charged every month.</summary>
    Month,

    /// <summary>Charged every year.</summary>
    Year
}

/// <summary>The state of a subscription, shown as a badge next to the plan name.</summary>
public enum BillingPlanStatus
{
    /// <summary>Paid and renewing (success badge).</summary>
    Active,

    /// <summary>In a free trial; the first charge comes when it ends (accent badge).</summary>
    Trialing,

    /// <summary>The last charge failed and is still owed (danger badge).</summary>
    PastDue,

    /// <summary>Canceled; access continues until the end of the paid period (neutral badge).</summary>
    Canceled
}

/// <summary>The state of a <see cref="BillingInvoice"/>.</summary>
public enum BillingInvoiceStatus
{
    /// <summary>Paid in full (success badge).</summary>
    Paid,

    /// <summary>Issued and not yet due or not yet paid (neutral badge).</summary>
    Open,

    /// <summary>Due and unpaid (danger badge).</summary>
    PastDue,

    /// <summary>Paid and refunded (muted neutral badge).</summary>
    Refunded,

    /// <summary>Canceled before payment (muted neutral badge).</summary>
    Void
}

/// <summary>
/// The subscription a <see cref="BillingOverview"/> shows. Writable in XAML; property changes after display update the
/// view (<see cref="INotifyPropertyChanged"/>).
/// </summary>
public class BillingPlan : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private decimal _price;
    private string _currency = "USD";
    private BillingInterval _interval;
    private int? _seats;
    private BillingPlanStatus _status;
    private DateTime? _renewsOn;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The plan's name, such as "Studio".</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? string.Empty);
    }

    /// <summary>The total charged each <see cref="Interval"/>, all seats included.</summary>
    public decimal Price
    {
        get => _price;
        set => Set(ref _price, value);
    }

    /// <summary>ISO 4217 currency code of <see cref="Price"/> (default "USD").</summary>
    public string Currency
    {
        get => _currency;
        set => Set(ref _currency, string.IsNullOrWhiteSpace(value) ? "USD" : value);
    }

    /// <summary>Month or Year: how often <see cref="Price"/> is charged.</summary>
    public BillingInterval Interval
    {
        get => _interval;
        set => Set(ref _interval, value);
    }

    /// <summary>Number of paid seats, or null for plans not priced per seat.</summary>
    public int? Seats
    {
        get => _seats;
        set => Set(ref _seats, value);
    }

    /// <summary>Active, Trialing, PastDue or Canceled.</summary>
    public BillingPlanStatus Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    /// <summary>
    /// The next renewal (the end of the trial while trialing, the end of access once canceled), or null when unknown.
    /// </summary>
    public DateTime? RenewsOn
    {
        get => _renewsOn;
        set => Set(ref _renewsOn, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// The card a <see cref="BillingOverview"/> charges. The block shows the brand as text only (no card logos) and works out
/// whether its expiry month starts within 60 days. Writable in XAML; property changes after display update the view.
/// </summary>
public class BillingPaymentMethod : INotifyPropertyChanged
{
    private string _brand = string.Empty;
    private string _last4 = string.Empty;
    private int _expiryMonth;
    private int _expiryYear;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The card network as text, such as "Visa" or "Mastercard".</summary>
    public string Brand
    {
        get => _brand;
        set => Set(ref _brand, value ?? string.Empty);
    }

    /// <summary>The last four digits of the card number.</summary>
    public string Last4
    {
        get => _last4;
        set => Set(ref _last4, value ?? string.Empty);
    }

    /// <summary>The expiry month, 1 to 12.</summary>
    public int ExpiryMonth
    {
        get => _expiryMonth;
        set => Set(ref _expiryMonth, value);
    }

    /// <summary>The four-digit expiry year.</summary>
    public int ExpiryYear
    {
        get => _expiryYear;
        set => Set(ref _expiryYear, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// One invoice in a <see cref="BillingOverview"/>'s history. Writable in XAML; property changes after display update the
/// view.
/// </summary>
public class BillingInvoice : INotifyPropertyChanged
{
    private string _number = string.Empty;
    private DateTime _date;
    private decimal _amount;
    private string? _currency;
    private BillingInvoiceStatus _status;
    private string? _description;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The invoice number, such as "LS-2026-010".</summary>
    public string Number
    {
        get => _number;
        set => Set(ref _number, value ?? string.Empty);
    }

    /// <summary>The date the invoice was issued; the list is sorted by it, newest first.</summary>
    public DateTime Date
    {
        get => _date;
        set => Set(ref _date, value);
    }

    /// <summary>The invoice total.</summary>
    public decimal Amount
    {
        get => _amount;
        set => Set(ref _amount, value);
    }

    /// <summary>ISO 4217 currency code of <see cref="Amount"/>; null uses the plan's currency.</summary>
    public string? Currency
    {
        get => _currency;
        set => Set(ref _currency, value);
    }

    /// <summary>Paid, Open, PastDue, Refunded or Void.</summary>
    public BillingInvoiceStatus Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    /// <summary>What the invoice was for, such as "Studio plan, 6 seats"; added to the row's accessible name.</summary>
    public string? Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Arguments of <see cref="BillingOverview.InvoiceDownloadRequested"/>: the invoice to download.</summary>
public sealed class BillingInvoiceEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="routedEvent"/> about <paramref name="invoice"/>.</summary>
    public BillingInvoiceEventArgs(RoutedEvent routedEvent, BillingInvoice invoice)
        : base(routedEvent)
    {
        Invoice = invoice;
    }

    /// <summary>The invoice whose download button was pressed.</summary>
    public BillingInvoice Invoice { get; }
}
