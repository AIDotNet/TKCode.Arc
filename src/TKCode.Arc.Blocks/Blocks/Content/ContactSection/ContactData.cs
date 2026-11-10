// Ported from Arc's free contact-section block (MIT, Copyright (c) 2026 Elia Kuratli): the data shapes, validation and office hours of
// contact-section.tsx. The sample channels, offices and copy are this library's own.

using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The layout of a <see cref="ContactSection"/>.</summary>
public enum ContactSectionVariant
{
    /// <summary>A validated form whose card springs into a confirmation.</summary>
    Form,

    /// <summary>A list of ways to reach you beside a detail panel that morphs to each one.</summary>
    Channels,

    /// <summary>Office cards with live local times and open status.</summary>
    Offices
}

/// <summary>A valid message from the form of a <see cref="ContactSection"/>.</summary>
public sealed class ContactMessage
{
    /// <summary>The sender's name, trimmed.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The sender's email address, trimmed.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>The chosen topic.</summary>
    public string Topic { get; init; } = string.Empty;

    /// <summary>The message, trimmed.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>One way to reach you in the channels layout.</summary>
public sealed class ContactChannel : AvaloniaObject
{
    /// <summary>The channel's stable value.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ContactChannel, string>(nameof(Value), string.Empty);

    /// <inheritdoc cref="ValueProperty"/>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The channel's name in the list.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ContactChannel, string>(nameof(Label), string.Empty);

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>One line under the label, such as a response time. It hides when the list runs sideways.</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<ContactChannel, string?>(nameof(Meta));

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>A 24 by 24 stroke icon for the list.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ContactChannel, Geometry?>(nameof(Icon));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>What the panel shows while the channel is chosen: text or any control.</summary>
    public static readonly StyledProperty<object?> DetailProperty =
        AvaloniaProperty.Register<ContactChannel, object?>(nameof(Detail));

    /// <inheritdoc cref="DetailProperty"/>
    public object? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
}

/// <summary>One office in the offices layout.</summary>
public sealed class ContactOffice : AvaloniaObject
{
    /// <summary>Creates the object with its empty lists.</summary>
    public ContactOffice()
    {
        Address = new();
    }

    /// <summary>The office's city.</summary>
    public static readonly StyledProperty<string> CityProperty =
        AvaloniaProperty.Register<ContactOffice, string>(nameof(City), string.Empty);

    /// <inheritdoc cref="CityProperty"/>
    public string City { get => GetValue(CityProperty); set => SetValue(CityProperty, value); }

    /// <summary>The time zone id used for the live time and the open status.</summary>
    public static readonly StyledProperty<string> TimeZoneProperty =
        AvaloniaProperty.Register<ContactOffice, string>(nameof(TimeZone), "UTC");

    /// <inheritdoc cref="TimeZoneProperty"/>
    public string TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }

    /// <summary>The address lines.</summary>
    public static readonly StyledProperty<AvaloniaList<string>> AddressProperty =
        AvaloniaProperty.Register<ContactOffice, AvaloniaList<string>>(nameof(Address));

    /// <inheritdoc cref="AddressProperty"/>
    public AvaloniaList<string> Address { get => GetValue(AddressProperty); set => SetValue(AddressProperty, value); }

    /// <summary>The office's email address, shown as a link.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<ContactOffice, string?>(nameof(Email));

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <summary>The hour the office opens, in local 24 hour time.</summary>
    public static readonly StyledProperty<int> OpenHourProperty =
        AvaloniaProperty.Register<ContactOffice, int>(nameof(OpenHour), 9);

    /// <inheritdoc cref="OpenHourProperty"/>
    public int OpenHour { get => GetValue(OpenHourProperty); set => SetValue(OpenHourProperty, value); }

    /// <summary>The hour the office closes, in local 24 hour time.</summary>
    public static readonly StyledProperty<int> CloseHourProperty =
        AvaloniaProperty.Register<ContactOffice, int>(nameof(CloseHour), 18);

    /// <inheritdoc cref="CloseHourProperty"/>
    public int CloseHour { get => GetValue(CloseHourProperty); set => SetValue(CloseHourProperty, value); }
}

/// <summary>The rules of the form and the state of an office.</summary>
internal static class ContactRules
{
    public const int MaxMessage = 500;
    public const int MinMessage = 20;

    /// <summary>The same shape as Arc's pattern: no spaces, one @, a dot in the domain and at least two characters after the last dot.</summary>
    public static bool IsEmail(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || value.Any(char.IsWhiteSpace))
        {
            return false;
        }
        var dot = value.LastIndexOf('.');
        return dot > at + 1 && value.Length - dot - 1 >= 2;
    }

    public static string? ValidateName(string name) => string.IsNullOrWhiteSpace(name) ? "Enter your name" : null;

    public static string? ValidateEmail(string email)
    {
        var trimmed = email.Trim();
        return trimmed.Length == 0 ? "Enter your email address" : IsEmail(trimmed) ? null : "Enter an email like name@example.com";
    }

    public static string? ValidateMessage(string message)
    {
        var trimmed = message.Trim();
        return trimmed.Length >= MinMessage ? null : trimmed.Length > 0 ? "Add a little more detail, at least 20 characters" : "Tell us how we can help";
    }

    public static string HourLabel(int hour) => $"{(hour % 12 == 0 ? 12 : hour % 12)}{(hour < 12 || hour == 24 ? "am" : "pm")}";

    /// <summary>The office's local time as text, and whether it is open; open is null when its time zone is unknown here.</summary>
    public static (string Time, bool? IsOpen) OfficeState(ContactOffice office, DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(office.TimeZone);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or System.Security.SecurityException)
        {
            return ("--:--", null);
        }
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var weekend = local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var open = !weekend && local.Hour >= office.OpenHour && local.Hour < office.CloseHour;
        return (local.ToString("h:mm tt", CultureInfo.InvariantCulture), open);
    }
}
