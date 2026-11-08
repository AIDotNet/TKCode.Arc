using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PlanComparisonOwnPlansDemo : UserControl
{
    public PlanComparisonOwnPlansDemo()
    {
        InitializeComponent();
        Comparison.Labels = new PlanComparisonLabels
        {
            Title = "Field notes for every crew size",
            Description = "Every plan works offline in the field. Compare what changes as your survey grows.",
            Footnote = "Prices in EUR per observer and month, VAT excluded."
        };
        Comparison.SelectedPlanChanged += (_, e) =>
            Log.Text = e.NewPlan is null ? "Selection cleared." : $"Selected {e.NewPlan.Name} ({(e.IsUserAction ? "button" : "app")}).";
        Comparison.BillingChanged += (_, e) => Log.Text = $"Billing: {e.OldBilling} to {e.NewBilling}.";
    }
}
