using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A city on a <see cref="RevenueGlobe"/>: where payments come from (and, for <see cref="RevenueGlobe.Headquarters"/>,
/// where they go). Coordinates are in degrees; the city's local time uses <see cref="UtcOffsetHours"/>.
/// </summary>
public class RevenueCity : AvaloniaObject
{
    /// <summary>Identifies the city; <see cref="RevenuePayment.CityKey"/> refers to it.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<RevenueCity, string>(nameof(Key), string.Empty);

    /// <summary>The name shown on the globe, in the hover card and in the list of top cities.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<RevenueCity, string>(nameof(Name), string.Empty);

    /// <summary>A short country code shown after the name in the list of top cities ("US").</summary>
    public static readonly StyledProperty<string?> CountryCodeProperty =
        AvaloniaProperty.Register<RevenueCity, string?>(nameof(CountryCode));

    /// <summary>The <see cref="RevenueRegion.Key"/> of the region the city counts toward.</summary>
    public static readonly StyledProperty<string?> RegionKeyProperty =
        AvaloniaProperty.Register<RevenueCity, string?>(nameof(RegionKey));

    /// <summary>Latitude in degrees, north positive.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<RevenueCity, double>(nameof(Latitude));

    /// <summary>Longitude in degrees, east positive.</summary>
    public static readonly StyledProperty<double> LongitudeProperty =
        AvaloniaProperty.Register<RevenueCity, double>(nameof(Longitude));

    /// <summary>Offset of the city's local time from UTC in hours (5.5 for India); the hover card shows the local time.</summary>
    public static readonly StyledProperty<double> UtcOffsetHoursProperty =
        AvaloniaProperty.Register<RevenueCity, double>(nameof(UtcOffsetHours));

    /// <summary>A relative size for sample data generators; the control itself does not read it.</summary>
    public static readonly StyledProperty<double> WeightProperty =
        AvaloniaProperty.Register<RevenueCity, double>(nameof(Weight), 1);

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="CountryCodeProperty"/>
    public string? CountryCode { get => GetValue(CountryCodeProperty); set => SetValue(CountryCodeProperty, value); }

    /// <inheritdoc cref="RegionKeyProperty"/>
    public string? RegionKey { get => GetValue(RegionKeyProperty); set => SetValue(RegionKeyProperty, value); }

    /// <inheritdoc cref="LatitudeProperty"/>
    public double Latitude { get => GetValue(LatitudeProperty); set => SetValue(LatitudeProperty, value); }

    /// <inheritdoc cref="LongitudeProperty"/>
    public double Longitude { get => GetValue(LongitudeProperty); set => SetValue(LongitudeProperty, value); }

    /// <inheritdoc cref="UtcOffsetHoursProperty"/>
    public double UtcOffsetHours { get => GetValue(UtcOffsetHoursProperty); set => SetValue(UtcOffsetHoursProperty, value); }

    /// <inheritdoc cref="WeightProperty"/>
    public double Weight { get => GetValue(WeightProperty); set => SetValue(WeightProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A region that groups cities on a <see cref="RevenueGlobe"/>, with the revenue expected from it for the whole day.</summary>
public class RevenueRegion : AvaloniaObject
{
    /// <summary>Identifies the region; <see cref="RevenueCity.RegionKey"/> refers to it.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<RevenueRegion, string>(nameof(Key), string.Empty);

    /// <summary>The name shown in the list of regions and announced when the camera flies there.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<RevenueRegion, string>(nameof(Name), string.Empty);

    /// <summary>Revenue expected from the region for the whole day; its bar shows today's revenue as a share of it.</summary>
    public static readonly StyledProperty<double> ForecastProperty =
        AvaloniaProperty.Register<RevenueRegion, double>(nameof(Forecast));

    /// <summary>Latitude the camera flies to; null uses the revenue-weighted center of the region's cities.</summary>
    public static readonly StyledProperty<double?> CenterLatitudeProperty =
        AvaloniaProperty.Register<RevenueRegion, double?>(nameof(CenterLatitude));

    /// <summary>Longitude the camera flies to; null uses the revenue-weighted center of the region's cities.</summary>
    public static readonly StyledProperty<double?> CenterLongitudeProperty =
        AvaloniaProperty.Register<RevenueRegion, double?>(nameof(CenterLongitude));

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ForecastProperty"/>
    public double Forecast { get => GetValue(ForecastProperty); set => SetValue(ForecastProperty, value); }

    /// <inheritdoc cref="CenterLatitudeProperty"/>
    public double? CenterLatitude { get => GetValue(CenterLatitudeProperty); set => SetValue(CenterLatitudeProperty, value); }

    /// <inheritdoc cref="CenterLongitudeProperty"/>
    public double? CenterLongitude { get => GetValue(CenterLongitudeProperty); set => SetValue(CenterLongitudeProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One payment on a <see cref="RevenueGlobe"/>: when it happened, from which city, and how much.</summary>
/// <param name="Time">When the payment happened.</param>
/// <param name="CityKey">The <see cref="RevenueCity.Key"/> of the city it came from.</param>
/// <param name="Amount">The amount in the globe's currency.</param>
public readonly record struct RevenuePayment(DateTimeOffset Time, string CityKey, double Amount);

/// <summary>Where a <see cref="RevenueGlobe"/>'s current time comes from.</summary>
public enum RevenueGlobeClockMode
{
    /// <summary>The globe moves <see cref="RevenueGlobe.Now"/> on with the real clock, starting from its current value.</summary>
    Internal,

    /// <summary>The app sets <see cref="RevenueGlobe.Now"/> (for example whenever its server reports new payments).</summary>
    External
}

/// <summary>What the timeline of a <see cref="RevenueGlobe"/> is doing.</summary>
public enum RevenueGlobePlaybackState
{
    /// <summary>The playhead follows <see cref="RevenueGlobe.Now"/>.</summary>
    Live,

    /// <summary>Time stands still; arcs in flight still land.</summary>
    Paused,

    /// <summary>The playhead runs toward the present faster than real time, then goes live.</summary>
    Replaying,

    /// <summary>The playhead follows a pointer dragging the timeline.</summary>
    Scrubbing
}

/// <summary>Arguments of <see cref="RevenueGlobe.SelectionChanged"/>: the selected city and region before and after.</summary>
public sealed class RevenueGlobeSelectionChangedEventArgs : RoutedEventArgs
{
    public RevenueGlobeSelectionChangedEventArgs(RoutedEvent routedEvent, RevenueCity? oldCity, RevenueCity? newCity,
                                                 RevenueRegion? oldRegion, RevenueRegion? newRegion)
        : base(routedEvent)
    {
        OldCity = oldCity;
        NewCity = newCity;
        OldRegion = oldRegion;
        NewRegion = newRegion;
    }

    /// <summary>The city selected before the change, or null.</summary>
    public RevenueCity? OldCity { get; }

    /// <summary>The city selected now, or null.</summary>
    public RevenueCity? NewCity { get; }

    /// <summary>The region selected before the change, or null.</summary>
    public RevenueRegion? OldRegion { get; }

    /// <summary>The region selected now, or null.</summary>
    public RevenueRegion? NewRegion { get; }
}

/// <summary>Arguments of <see cref="RevenueGlobe.TimeChanged"/>: the playhead and what the timeline is doing.</summary>
public sealed class RevenueGlobeTimeChangedEventArgs : RoutedEventArgs
{
    public RevenueGlobeTimeChangedEventArgs(RoutedEvent routedEvent, DateTimeOffset time, bool isLive, RevenueGlobePlaybackState playbackState)
        : base(routedEvent)
    {
        Time = time;
        IsLive = isLive;
        PlaybackState = playbackState;
    }

    /// <summary>The playhead.</summary>
    public DateTimeOffset Time { get; }

    /// <summary>The playhead follows the present.</summary>
    public bool IsLive { get; }

    /// <summary>What the timeline is doing.</summary>
    public RevenueGlobePlaybackState PlaybackState { get; }
}

/// <summary>Arguments of <see cref="RevenueGlobe.CityInvoked"/>: the city that was clicked on the globe or in the list.</summary>
public sealed class RevenueGlobeCityEventArgs : RoutedEventArgs
{
    public RevenueGlobeCityEventArgs(RoutedEvent routedEvent, RevenueCity city)
        : base(routedEvent)
    {
        City = city;
    }

    /// <summary>The city.</summary>
    public RevenueCity City { get; }
}
