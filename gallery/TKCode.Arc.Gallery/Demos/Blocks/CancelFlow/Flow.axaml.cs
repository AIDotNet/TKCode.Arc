using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CancelFlowFlowDemo : UserControl
{
    public CancelFlowFlowDemo()
    {
        InitializeComponent();
        // Access runs to the end of the paid month: the renewal date of the Studio plan.
        Flow.AccessEndsOn = new DateTime(2026, 11, 1);
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => Flow.Reset();

    // On a real page this closes the flow and returns to billing; here it confirms and starts over.
    private void OnKeepPlanRequested(object? sender, RoutedEventArgs e)
    {
        Toast.Show(this, "Your Studio plan stays active", ToastKind.Success);
        Flow.Reset();
    }
}
