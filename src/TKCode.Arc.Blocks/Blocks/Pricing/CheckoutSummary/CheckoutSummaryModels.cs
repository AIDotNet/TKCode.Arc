using Avalonia;

namespace TKCode.Arc;

/// <summary>Where the pay button of a <see cref="CheckoutSummary"/> is in its life.</summary>
public enum CheckoutSummaryPayState
{
    /// <summary>Ready to pay: the lock glyph and the pay label with the total.</summary>
    Idle,

    /// <summary>The payment is running: a spinner and "Processing"; the button and the promo code are locked.</summary>
    Processing,

    /// <summary>The payment went through: a check and "Paid" on a green button. This state is final until reset.</summary>
    Paid,

    /// <summary>The payment failed: an error line under the button and "Try again", which starts a new attempt.</summary>
    Error
}

/// <summary>The product a <see cref="CheckoutSummary"/> is for: name, description, price and billing period.</summary>
public class CheckoutSummaryItem : AvaloniaObject
{
    /// <summary>Product name, kept on one line (cut with an ellipsis when it does not fit).</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<CheckoutSummaryItem, string?>(nameof(Name));

    /// <summary>One short sentence under the name; wraps on narrow cards.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<CheckoutSummaryItem, string?>(nameof(Description));

    /// <summary>Price before adjustments, codes and tax; it is also the subtotal.</summary>
    public static readonly StyledProperty<decimal> PriceProperty =
        AvaloniaProperty.Register<CheckoutSummaryItem, decimal>(nameof(Price));

    /// <summary>Small text under the price, such as "billed yearly".</summary>
    public static readonly StyledProperty<string?> PeriodProperty =
        AvaloniaProperty.Register<CheckoutSummaryItem, string?>(nameof(Period));

    /// <summary>Optional 44 px thumbnail before the name: an image, a glyph or any control.</summary>
    public static readonly StyledProperty<object?> MediaProperty =
        AvaloniaProperty.Register<CheckoutSummaryItem, object?>(nameof(Media));

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="PriceProperty"/>
    public decimal Price { get => GetValue(PriceProperty); set => SetValue(PriceProperty, value); }

    /// <inheritdoc cref="PeriodProperty"/>
    public string? Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    /// <inheritdoc cref="MediaProperty"/>
    public object? Media { get => GetValue(MediaProperty); set => SetValue(MediaProperty, value); }
}

/// <summary>
/// An automatic line of a <see cref="CheckoutSummary"/> between the subtotal and the tax (a plan discount, a credit, a
/// fee). Negative amounts are discounts and show in green.
/// </summary>
public class CheckoutSummaryLine : AvaloniaObject
{
    /// <summary>Stable key of the line, so a changed list animates only the lines that came or went ("coupon" is reserved).</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<CheckoutSummaryLine, string?>(nameof(Id));

    /// <summary>What the line is, such as "Annual plan".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryLine, string?>(nameof(Label));

    /// <summary>Signed amount added to the subtotal; negative is a discount.</summary>
    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<CheckoutSummaryLine, decimal>(nameof(Amount));

    /// <summary>Optional small text under the label, such as "Three months free".</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<CheckoutSummaryLine, string?>(nameof(Note));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="AmountProperty"/>
    public decimal Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
}

/// <summary>The tax line of a <see cref="CheckoutSummary"/>.</summary>
public class CheckoutSummaryTax : AvaloniaObject
{
    /// <summary>Name of the tax; the rate is added in brackets when it is known: "Sales tax (7.5%)".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryTax, string?>(nameof(Label), "Tax");

    /// <summary>Rate as a fraction (0.075 = 7.5%). Without a rate the line shows <see cref="Note"/> and no amount, and the total leaves tax out.</summary>
    public static readonly StyledProperty<decimal?> RateProperty =
        AvaloniaProperty.Register<CheckoutSummaryTax, decimal?>(nameof(Rate));

    /// <summary>Text shown in place of the amount when there is no rate (the labels' "Calculated at checkout" when empty).</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<CheckoutSummaryTax, string?>(nameof(Note));

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="RateProperty"/>
    public decimal? Rate { get => GetValue(RateProperty); set => SetValue(RateProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
}

/// <summary>What a promo code check answered: a valid code with its discount, or a message saying why not.</summary>
public sealed class CheckoutSummaryCouponResult
{
    private CheckoutSummaryCouponResult(bool isValid, string? code, string? label, decimal amount, decimal? percent, string? message)
    {
        IsValid = isValid;
        Code = code;
        Label = label;
        Amount = amount;
        Percent = percent;
        Message = message;
    }

    /// <summary>True when the code was accepted.</summary>
    public bool IsValid { get; }

    /// <summary>The code as it should be shown on the chip (normally upper case).</summary>
    public string? Code { get; }

    /// <summary>Label of the discount line, such as "Hello, 15% off"; the code itself when empty.</summary>
    public string? Label { get; }

    /// <summary>Fixed discount (positive, subtracted) when <see cref="Percent"/> is not set.</summary>
    public decimal Amount { get; }

    /// <summary>Discount as a fraction of the adjusted subtotal (0.15 = 15%), recomputed when the lines change.</summary>
    public decimal? Percent { get; }

    /// <summary>Why the code was refused; shown under the field.</summary>
    public string? Message { get; }

    /// <summary>A valid code that takes a fixed <paramref name="amount"/> off.</summary>
    public static CheckoutSummaryCouponResult Valid(string code, decimal amount, string? label = null)
        => new(true, code, label, Math.Max(0m, amount), null, null);

    /// <summary>A valid code that takes <paramref name="percent"/> (0.15 = 15%) of the adjusted subtotal off.</summary>
    public static CheckoutSummaryCouponResult ValidPercent(string code, decimal percent, string? label = null)
        => new(true, code, label, 0m, Math.Clamp(percent, 0m, 1m), null);

    /// <summary>A refused code with the <paramref name="message"/> to show.</summary>
    public static CheckoutSummaryCouponResult Invalid(string message)
        => new(false, null, null, 0m, null, message);
}
