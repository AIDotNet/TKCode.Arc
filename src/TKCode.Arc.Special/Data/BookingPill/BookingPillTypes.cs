using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A segment of a <see cref="BookingPill"/>.</summary>
public enum BookingPillPart
{
    /// <summary>No segment is open.</summary>
    None,

    /// <summary>Where: the place.</summary>
    Place,

    /// <summary>When: the check-in and check-out dates.</summary>
    Dates,

    /// <summary>Who: the guests.</summary>
    Guests
}

/// <summary>A place offered in the place picker of a <see cref="BookingPill"/>.</summary>
public class BookingPlace : AvaloniaObject
{
    /// <summary>The place's name, which the pill shows once it is picked.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<BookingPlace, string>(nameof(Name), string.Empty);

    /// <summary>A second line under the name, such as the region or country.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<BookingPlace, string?>(nameof(Detail));

    /// <inheritdoc cref="NameProperty"/>
    public string Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }
}

/// <summary>Event data of <see cref="BookingPill.SearchRequested"/>: everything the pill holds.</summary>
public sealed class BookingPillSearchEventArgs : RoutedEventArgs
{
    internal BookingPillSearchEventArgs(RoutedEvent routedEvent, string? place, DateOnly? checkIn, DateOnly? checkOut,
                                        int adults, int children, int infants) : base(routedEvent)
    {
        Place = place;
        CheckIn = checkIn;
        CheckOut = checkOut;
        Adults = adults;
        Children = children;
        Infants = infants;
    }

    /// <summary>The picked place, or null for anywhere.</summary>
    public string? Place { get; }

    /// <summary>The first night, or null.</summary>
    public DateOnly? CheckIn { get; }

    /// <summary>The day of leaving, or null.</summary>
    public DateOnly? CheckOut { get; }

    /// <summary>Adults.</summary>
    public int Adults { get; }

    /// <summary>Children.</summary>
    public int Children { get; }

    /// <summary>Infants.</summary>
    public int Infants { get; }
}
