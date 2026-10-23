using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReleaseReadinessPreviewDemo : UserControl
{
    public ReleaseReadinessPreviewDemo()
    {
        InitializeComponent();
        Gate.Checks.AddRange(ReleaseReadinessDemoData.RolloutChecks());
        Gate.Owners.AddRange(ReleaseReadinessDemoData.RolloutOwners());

        // Start the rollout somewhere; here it only waits, so the button's busy state is visible for a moment.
        Gate.ReleaseAction = (_, cancellation) => Task.Delay(1200, cancellation);
    }
}
