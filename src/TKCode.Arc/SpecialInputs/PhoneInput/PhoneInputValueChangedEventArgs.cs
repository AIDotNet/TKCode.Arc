using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Arguments of <see cref="PhoneInput.ValueChanged"/>: the E.164 value before and after the change plus Arc's details
/// (the country, the number as shown in the field, its status and whether it is valid).
/// </summary>
public sealed class PhoneInputValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PhoneInputValueChangedEventArgs(RoutedEvent routedEvent, string oldValue, string newValue, PhoneCountry country,
                                           string formatted, PhoneNumberStatus status)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
        Country = country;
        Formatted = formatted;
        Status = status;
    }

    /// <summary>The previous E.164 value ("" when there were no digits).</summary>
    public string OldValue { get; }

    /// <summary>The new E.164 value, such as "+14155550132" ("" when there are no digits).</summary>
    public string NewValue { get; }

    /// <summary>The selected country.</summary>
    public PhoneCountry Country { get; }

    /// <summary>The number as shown in the field, without the calling code (a typed trunk prefix is kept).</summary>
    public string Formatted { get; }

    /// <summary>Where the number stands against the country's expected lengths.</summary>
    public PhoneNumberStatus Status { get; }

    /// <summary>True when <see cref="Status"/> is <see cref="PhoneNumberStatus.Valid"/>.</summary>
    public bool IsValid => Status == PhoneNumberStatus.Valid;
}
