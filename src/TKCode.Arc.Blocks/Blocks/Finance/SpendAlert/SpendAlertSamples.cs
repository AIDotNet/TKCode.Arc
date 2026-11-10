namespace TKCode.Arc;

/// <summary>
/// Invented sample content for the <see cref="SpendAlert"/> preview: a dining budget eighteen days into a thirty-day month
/// with alerts at 50%, 80% and 100%. Replace it with your own data; none of it is required.
/// </summary>
public static class SpendAlertSamples
{
    /// <summary>Fills <paramref name="alert"/> with the sample budget, spending and alerts.</summary>
    public static void Apply(SpendAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        alert.Title = "Dining out";
        alert.Period = "November · day 18 of 30";
        alert.Budget = 600m;
        alert.Spent = 412.80m;
        alert.DaysElapsed = 18;
        alert.DaysInPeriod = 30;
        alert.Thresholds.Clear();
        alert.Thresholds.Add(new SpendThreshold(0.5));
        alert.Thresholds.Add(new SpendThreshold(0.8));
        alert.Thresholds.Add(new SpendThreshold(1.0));
    }

    /// <summary>Invented purchases the preview can add one by one: (place, amount).</summary>
    public static IReadOnlyList<(string Place, decimal Amount)> Purchases { get; } =
    [
        ("Saffron Table", 38.40m),
        ("Lantern Coffee", 6.40m),
        ("Noodle Harbor", 24.90m),
        ("Olive & Ember", 61.20m),
        ("Corner Taqueria", 17.35m),
    ];
}
