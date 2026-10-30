using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app's own models, presets and cache rate, with the workload and the pick reported back.</summary>
public partial class LlmCostCalculatorYourPricesDemo : UserControl
{
    public LlmCostCalculatorYourPricesDemo()
    {
        InitializeComponent();
        // The models and their list prices (US dollars per 1M tokens) come from your price list; here, the gallery's catalog.
        Calculator.Models = LlmCostCalculatorDemoData.ApprovedModels;
        Calculator.Presets = LlmCostCalculatorDemoData.TeamPresets;
        Calculator.Description = $"Approved models, list prices per 1M tokens, checked {AiModelCatalog.CheckedOn}";
        Calculator.Note = LlmCostCalculatorDemoData.ApprovedNote;
        Calculator.Usage = new LlmUsage(6_000, 400, 20_000, true);
        // SelectedCost follows every change, whether it came from a slider, a preset, the switch or a picked row.
        Calculator.PropertyChanged += (_, e) =>
        {
            if (e.Property == LlmCostCalculator.SelectedCostProperty || e.Property == LlmCostCalculator.SelectedModelProperty)
            {
                Report();
            }
        };
        Calculator.SelectedModelChanged += (_, e) => Picked.Text = $"Picked {e.Model.Name} at ${e.Cost.Total:N2} a month.";
    }

    private void Report()
    {
        var usage = Calculator.Usage;
        var model = Calculator.SelectedModel;
        var cost = Calculator.SelectedCost;
        Log.Text = model is null
            ? string.Empty
            : $"{model.Name}: {usage.InputTokens:N0} in, {usage.OutputTokens:N0} out, {usage.RequestsPerDay:N0} a day"
              + $"{(usage.Cached ? ", cached" : string.Empty)}: ${cost.Total:N2} a month, ${cost.PerRequest:N4} a request.";
    }
}
