using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobePreviewDemo : UserControl
{
    public RevenueGlobePreviewDemo()
    {
        InitializeComponent();
        RevenueGlobeSampleDay.Apply(Globe);
    }
}

/// <summary>
/// The invented day every RevenueGlobe demo shows: a mid-sized SaaS with its headquarters in Denver, 40 cities in three
/// regions, and a whole day of payments drawn from a seeded model (the same day on every run). The clock starts at
/// 14:30 UTC.
/// </summary>
public static class RevenueGlobeSampleDay
{
    /// <summary>The day shown: 22 October 2026, 00:00 UTC.</summary>
    public static readonly DateTimeOffset Day = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Where the demo clock starts.</summary>
    public static readonly DateTimeOffset Start = Day.AddHours(14.5);

    private static (City[] Cities, RevenuePayment[] Payments, double[] Forecasts)? s_day;

    private sealed record City(string Name, string Country, string Region, double Latitude, double Longitude, double Utc, double Weight);

    private static readonly string[] s_regions = ["Americas", "EMEA", "APAC"];

    private static readonly City[] s_cities =
    [
        new("Denver", "US", "Americas", 39.74, -104.99, -7, 4.8),
        new("Boston", "US", "Americas", 42.36, -71.06, -5, 8.0),
        new("Atlanta", "US", "Americas", 33.75, -84.39, -5, 5.5),
        new("Houston", "US", "Americas", 29.76, -95.37, -6, 4.2),
        new("Phoenix", "US", "Americas", 33.45, -112.07, -7, 2.4),
        new("Minneapolis", "US", "Americas", 44.98, -93.27, -6, 2.6),
        new("Vancouver", "CA", "Americas", 49.28, -123.12, -8, 3.0),
        new("Montreal", "CA", "Americas", 45.50, -73.57, -5, 3.2),
        new("Guadalajara", "MX", "Americas", 20.67, -103.35, -6, 2.0),
        new("Panama City", "PA", "Americas", 8.98, -79.52, -5, 1.0),
        new("Medellín", "CO", "Americas", 6.24, -75.58, -5, 1.2),
        new("Quito", "EC", "Americas", -0.18, -78.47, -5, 0.7),
        new("Rio de Janeiro", "BR", "Americas", -22.91, -43.17, -3, 3.6),
        new("Montevideo", "UY", "Americas", -34.90, -56.16, -3, 0.8),
        new("Lisbon", "PT", "EMEA", 38.72, -9.14, 0, 2.2),
        new("Copenhagen", "DK", "EMEA", 55.68, 12.57, 1, 3.6),
        new("Warsaw", "PL", "EMEA", 52.23, 21.01, 1, 2.4),
        new("Vienna", "AT", "EMEA", 48.21, 16.37, 1, 3.0),
        new("Zurich", "CH", "EMEA", 47.38, 8.54, 1, 4.4),
        new("Athens", "GR", "EMEA", 37.98, 23.73, 2, 1.6),
        new("Helsinki", "FI", "EMEA", 60.17, 24.94, 2, 2.0),
        new("Brussels", "BE", "EMEA", 50.85, 4.35, 1, 3.0),
        new("Cairo", "EG", "EMEA", 30.04, 31.24, 2, 2.2),
        new("Riyadh", "SA", "EMEA", 24.71, 46.68, 3, 2.8),
        new("Casablanca", "MA", "EMEA", 33.57, -7.59, 1, 1.0),
        new("Accra", "GH", "EMEA", 5.60, -0.19, 0, 0.9),
        new("Johannesburg", "ZA", "EMEA", -26.20, 28.05, 2, 2.6),
        new("Doha", "QA", "EMEA", 25.29, 51.53, 3, 1.8),
        new("Osaka", "JP", "APAC", 34.69, 135.50, 9, 4.0),
        new("Busan", "KR", "APAC", 35.18, 129.08, 9, 3.0),
        new("Taipei", "TW", "APAC", 25.03, 121.57, 8, 2.6),
        new("Bangkok", "TH", "APAC", 13.76, 100.50, 7, 2.4),
        new("Hanoi", "VN", "APAC", 21.03, 105.85, 7, 1.6),
        new("Kuala Lumpur", "MY", "APAC", 3.14, 101.69, 8, 2.0),
        new("Delhi", "IN", "APAC", 28.61, 77.21, 5.5, 4.6),
        new("Chennai", "IN", "APAC", 13.08, 80.27, 5.5, 2.2),
        new("Colombo", "LK", "APAC", 6.93, 79.86, 5.5, 0.9),
        new("Perth", "AU", "APAC", -31.95, 115.86, 8, 2.4),
        new("Brisbane", "AU", "APAC", -27.47, 153.03, 10, 2.2),
        new("Wellington", "NZ", "APAC", -41.29, 174.78, 12, 1.4),
    ];

    /// <summary>Fills a globe with the sample day: cities, regions, HQ, payments, the day and the 14:30 clock.</summary>
    public static void Apply(RevenueGlobe globe)
    {
        var (cities, payments, forecasts) = s_day ??= Generate();
        var regions = new RevenueRegion[s_regions.Length];
        for (var i = 0; i < regions.Length; i++)
        {
            regions[i] = new RevenueRegion { Key = s_regions[i], Name = s_regions[i], Forecast = forecasts[i] };
        }
        var models = cities.Select(c => new RevenueCity
        {
            Key = c.Name,
            Name = c.Name,
            CountryCode = c.Country,
            RegionKey = c.Region,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            UtcOffsetHours = c.Utc,
            Weight = c.Weight
        }).ToArray();
        globe.Regions.AddRange(regions);
        globe.Cities.AddRange(models);
        globe.Headquarters = models[0];
        globe.DayStart = Day;
        globe.Now = Start;
        globe.Payments = payments;
    }

    // The model: payments per minute λ(h) = k·(62 + 50·g(h, 13, 6.5) + 14·g(h, 8.5, 2.4) + 10·g(h, 18.5, 3)) with
    // g(x, μ, w) = exp(−((x − μ)/w)²); ±8% jitter per 15 minutes, a 4% chance of a 1.3–1.9× burst and a 2.3× spike at
    // 11:15. Each payment picks a city by weight × daylight (0.35 at night, 1 by day in the city's own time zone);
    // amounts are log-normal around $17 with a 1.2% chance of a 15–45× order. mulberry32, seed 20261022: the same day
    // every time.
    private const double Scale = 1.6;

    private static (City[], RevenuePayment[], double[]) Generate()
    {
        var random = new Mulberry32(20261022);
        var cities = s_cities;
        var weights = new double[cities.Length];
        for (var c = 0; c < cities.Length; c++)
        {
            weights[c] = cities[c].Weight * (0.85 + 0.3 * random.Next());
        }
        var payments = new List<RevenuePayment>(220_000);
        var expected = new double[s_regions.Length];
        var share = new double[cities.Length];
        var seconds = new List<(double Second, int City, double Amount)>(512);
        var burst = 1.0;
        for (var minute = 0; minute < 1440; minute++)
        {
            var h = minute / 60.0;
            if (minute % 15 == 0)
            {
                // Each quarter hour jitters a little, now and then one bursts, and at 11:15 a newsletter goes out.
                burst = (1 + 0.08 * Gaussian(random)) * (random.Next() < 0.04 ? 1.3 + 0.6 * random.Next() : 1);
                if (minute == 11 * 60 + 15)
                {
                    burst *= 2.3;
                }
            }
            var lambda = Scale * (62 + 50 * G(h, 13, 6.5) + 14 * G(h, 8.5, 2.4) + 10 * G(h, 18.5, 3)) * burst;
            var total = 0.0;
            for (var c = 0; c < cities.Length; c++)
            {
                share[c] = weights[c] * Daylight(h, cities[c].Utc);
                total += share[c];
            }
            for (var c = 0; c < cities.Length; c++)
            {
                // Expected revenue (mean amount ≈ $31) by region, for the forecasts.
                expected[Array.IndexOf(s_regions, cities[c].Region)] += lambda / burst * share[c] / total * 31;
            }
            var count = Poisson(random, lambda);
            seconds.Clear();
            for (var i = 0; i < count; i++)
            {
                var pick = random.Next() * total;
                var city = 0;
                while (city < cities.Length - 1 && pick >= share[city])
                {
                    pick -= share[city];
                    city++;
                }
                var amount = Math.Exp(Math.Log(17) + 0.8 * Gaussian(random));
                if (random.Next() < 0.012)
                {
                    amount *= 15 + 30 * random.Next();
                }
                seconds.Add((random.Next() * 60, city, Math.Max(1, Math.Round(amount))));
            }
            seconds.Sort((a, b) => a.Second.CompareTo(b.Second));
            foreach (var (second, city, amount) in seconds)
            {
                payments.Add(new RevenuePayment(Day.AddSeconds(minute * 60 + second), cities[city].Name, amount));
            }
        }
        // Forecast: the region's expected day × 1.08, rounded to 10,000.
        var forecasts = expected.Select(e => Math.Round(e * 1.08 / 10_000) * 10_000).ToArray();
        return (cities, payments.ToArray(), forecasts);
    }

    private static double G(double x, double mu, double width) => Math.Exp(-Math.Pow((x - mu) / width, 2));

    /// <summary>0.35 at night, rising 06–09 and falling 21–24 local time.</summary>
    private static double Daylight(double utcHour, double offset)
    {
        var local = ((utcHour + offset) % 24 + 24) % 24;
        return 0.35 + 0.65 * Smoothstep(6, 9, local) * (1 - Smoothstep(21, 24, local));
    }

    private static double Smoothstep(double a, double b, double x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static int Poisson(Mulberry32 random, double lambda)
    {
        // Inversion by sequential search (λ stays under 300 here).
        var u = random.Next();
        var p = Math.Exp(-lambda);
        var cumulative = p;
        var k = 0;
        while (u > cumulative && k < 2000)
        {
            k++;
            p *= lambda / k;
            cumulative += p;
            if (p < 1e-300)
            {
                break;
            }
        }
        return k;
    }

    private static double Gaussian(Mulberry32 random)
    {
        var u = Math.Max(1e-12, random.Next());
        var v = random.Next();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    /// <summary>mulberry32: a tiny seeded generator, the same numbers on every platform.</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
