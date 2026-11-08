using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsagePricingLiveDemo : UserControl
{
    private readonly List<string> _lines = new();

    public UsagePricingLiveDemo()
    {
        InitializeComponent();
        Pricing.SeatStops = UsagePricingDemoData.EditorStops;
        Pricing.UsageStops = UsagePricingDemoData.StorageStops;
        Pricing.SeatIndex = 6;
        Pricing.UsageIndex = 9;
        Pricing.UsageFormatter = UsagePricingDemoData.FormatStorage;
        Pricing.OverageUnit = 100;
        Pricing.Labels = UsagePricingDemoData.StorageLabels;
        foreach (var plan in UsagePricingDemoData.StoragePlans())
        {
            Pricing.Plans.Add(plan);
        }
        Pricing.TrialHandler = UsagePricingSamples.SimulatedTrial(TimeSpan.FromSeconds(0.9), r => r.Plan.Name == "Harbor");

        Pricing.RecommendationChanged += (_, e) =>
            Add($"recommend {e.NewPlan?.Name} ({e.Reason})" + (ReferenceEquals(e.OldPlan, e.NewPlan) ? "" : $", was {e.OldPlan?.Name}"));
        Pricing.SelectionChanged += (_, e) =>
            Add($"selection {e.Seats} editors, {UsagePricingDemoData.FormatStorage(e.Usage)}, {e.Period} -> {e.Total:0} USD");
        Pricing.TrialStarted += (_, e) => Add($"trial started on {e.Plan?.Name}");
        Pricing.TrialUndone += (_, e) => Add($"trial off ({e.Plan?.Name})");
        Pricing.TrialFailed += (_, e) => Add($"trial refused for {e.Plan?.Name}");
        Add("ready");
    }

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 6)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
