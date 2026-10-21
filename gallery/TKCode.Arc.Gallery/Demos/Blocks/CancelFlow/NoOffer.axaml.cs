using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CancelFlowNoOfferDemo : UserControl
{
    public CancelFlowNoOfferDemo()
    {
        InitializeComponent();
        Flow.AccessEndsOn = new DateTime(2026, 11, 1);
        // No Reasons, so the flow offers the five default reasons from its labels; start with one chosen and answered.
        Flow.SelectedReason = Flow.Labels.DefaultReasons[2];
        Flow.Feedback = "Two-way sync with our shared calendar.";
    }
}
