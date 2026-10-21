using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CancelFlowInDialogDemo : UserControl
{
    private const string Active = "Studio plan · renews on Nov 1, 2026";

    public CancelFlowInDialogDemo()
    {
        InitializeComponent();
        Flow.AccessEndsOn = new DateTime(2026, 11, 1);
    }

    // Every opening starts from the first step, unless the plan was just canceled.
    private void OnOpened(object? sender, EventArgs e)
    {
        if (Flow.Step != CancelFlowStep.Canceled)
        {
            Flow.Reset();
        }
    }

    private void OnKeepPlanRequested(object? sender, RoutedEventArgs e) => CancelDialog.Close();

    private void OnOfferAccepted(object? sender, CancelFlowOfferEventArgs e) =>
        Status.Text = "Studio plan · free month added, renews on Dec 1, 2026";

    private void OnCancellationConfirmed(object? sender, CancelFlowCancelEventArgs e) =>
        Status.Text = $"Studio plan · canceled ({e.Reason.Title.ToLowerInvariant()}), ends on Nov 1, 2026";

    // The flow resets itself after this event; the page only puts the plan back.
    private void OnReactivateRequested(object? sender, RoutedEventArgs e)
    {
        Status.Text = Active;
        CancelDialog.Close();
    }
}
