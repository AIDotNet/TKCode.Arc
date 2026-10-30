using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A finished response with its breakdown open (a real model at its list prices; the usage is invented).</summary>
public partial class GenerationStatsBreakdownDemo : UserControl
{
    public GenerationStatsBreakdownDemo()
    {
        InitializeComponent();
        Stats.Model = GenerationStatsDemoData.Sonnet;
        Stats.Pricing = GenerationStatsDemoData.SonnetPricing;
        Stats.Note = GenerationStatsDemoData.ListPriceNote;
        Stats.StartedAt = DateTimeOffset.Now.AddSeconds(-12);
        Stats.Usage = new GenerationUsage
        {
            InputTokens = 12_480,
            CachedTokens = 9_216,
            OutputTokens = 612,
            ReasoningTokens = 1_024,
            Ttft = TimeSpan.FromMilliseconds(1_140),
            Duration = TimeSpan.FromMilliseconds(8_920),
        };
        Stats.SpeedSamples = [42, 61, 74, 79, 83, 81, 84, 86, 85, 83, 84, 82];
        Stats.Status = GenerationStatus.Done;
    }
}
