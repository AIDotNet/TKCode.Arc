using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented data for the UsagePricing demos: a storage product priced per editor and per 100 GB.</summary>
public static class UsagePricingDemoData
{
    /// <summary>Editor seats: 1–5 one by one, then wider steps up to 120.</summary>
    public static IReadOnlyList<int> EditorStops { get; } = [1, 2, 3, 4, 5, 8, 10, 15, 20, 30, 40, 60, 80, 120];

    /// <summary>Stored gigabytes a month, from 50 GB to 20 TB.</summary>
    public static IReadOnlyList<double> StorageStops { get; } =
        [50, 100, 200, 300, 500, 750, 1_000, 1_500, 2_000, 3_000, 4_000, 5_000, 7_500, 10_000, 15_000, 20_000];

    /// <summary>"750 GB", "1.5 TB".</summary>
    public static string FormatStorage(double gigabytes)
        => gigabytes >= 1_000
            ? (gigabytes / 1_000).ToString("0.##", CultureInfo.InvariantCulture) + " TB"
            : gigabytes.ToString("0", CultureInfo.InvariantCulture) + " GB";

    /// <summary>Three storage plans: Pocket, Crew and Harbor.</summary>
    public static List<UsagePricingPlan> StoragePlans()
    {
        var pocket = new UsagePricingPlan("Pocket", 8m, 1m, 200, 4m, 3, 500);
        pocket.Features.AddRange(
        [
            new UsagePricingFeature("versions", "Version history", "14 days"),
            new UsagePricingFeature("share", "Shared links", "Read only")
        ]);
        var crew = new UsagePricingPlan("Crew", 15m, 3m, 1_500, 3m, 20, 4_000);
        crew.Features.AddRange(
        [
            new UsagePricingFeature("review", "Review comments"),
            new UsagePricingFeature("versions", "Version history", "90 days"),
            new UsagePricingFeature("share", "Shared links", "With passwords")
        ]);
        var harbor = new UsagePricingPlan("Harbor", 24m, 4m, 8_000, 2m, null, null);
        harbor.Features.AddRange(
        [
            new UsagePricingFeature("region", "Data region", "Your choice"),
            new UsagePricingFeature("review", "Review comments"),
            new UsagePricingFeature("versions", "Version history", "1 year"),
            new UsagePricingFeature("share", "Shared links", "Expiring")
        ]);
        return [pocket, crew, harbor];
    }

    /// <summary>Wording for the storage product.</summary>
    public static UsagePricingLabels StorageLabels { get; } = new()
    {
        Title = "Price your workspace",
        Description = "Pay for each editor. Storage beyond what your plan holds costs a flat rate per extra 100 GB.",
        SeatsLabel = "Editors",
        UsageLabel = "Storage",
        UsageRow = "Storage",
        SeatsRow = plan => $"{plan} editors",
        UsageBundled = (used, included) => $"{used} of {included} held",
        UsageOver = (used, extra, rate) => $"{used} stored, {extra} extra at {rate} per 100 GB",
        BundledValue = "Held",
        ReasonWithin = (seats, usage) => $"Holds up to {seats} editors and {usage}",
        ReasonSeats = seats => $"For more than {seats} editors",
        ReasonUsage = usage => $"For more than {usage} of storage",
        ReasonBoth = (seats, usage) => $"For more than {seats} editors and {usage}",
        BilledMonthly = "Charged monthly, storage included",
        BilledYearly = amount => $"{amount} per year for editors, storage monthly",
        StartWith = plan => $"Start {plan} free",
        TrialNote = "30 days free, no card needed"
    };

    /// <summary>German number formats, English sentences (amounts read €1.234).</summary>
    public static UsagePricingLabels GermanFormatLabels { get; } = new()
    {
        Culture = CultureInfo.GetCultureInfo("de-DE"),
        Title = "Estimate in euros",
        Saving = amount => $"Annual billing saves {amount}",
        Yearly = "Yearly",
        TotalDetail = "Monthly, VAT not included"
    };
}
