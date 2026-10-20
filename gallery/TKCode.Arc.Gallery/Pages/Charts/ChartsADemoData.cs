namespace TKCode.Arc.Gallery.Pages;

/// <summary>Deterministic sample data for the chart demos (team A).</summary>
internal static class ChartsADemoData
{
    public static readonly DateTime Today = new(2026, 9, 30);

    /// <summary>A smooth daily series with weekly rhythm and gentle noise.</summary>
    public static double[] Daily(int days, double baseline, double swing, int seed, double trend = 0)
    {
        var random = new Random(seed);
        var values = new double[days];
        for (var i = 0; i < days; i++)
        {
            var weekly = Math.Sin(i / 7.0 * Math.PI * 2) * swing * 0.4;
            var drift = trend * i;
            values[i] = Math.Round(Math.Max(0, baseline + weekly + drift + (random.NextDouble() - 0.5) * swing));
        }
        return values;
    }

    public static DateTime[] Days(int count)
    {
        var days = new DateTime[count];
        for (var i = 0; i < count; i++)
        {
            days[i] = Today.AddDays(i - count + 1);
        }
        return days;
    }
}
