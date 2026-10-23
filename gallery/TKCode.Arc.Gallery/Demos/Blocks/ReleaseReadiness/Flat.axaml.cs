using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReleaseReadinessFlatDemo : UserControl
{
    public ReleaseReadinessFlatDemo()
    {
        InitializeComponent();
        Gate.Checks.AddRange(ReleaseReadinessDemoData.RolloutChecks());
        Gate.Owners.AddRange(ReleaseReadinessDemoData.RolloutOwners());
        Gate.ReleaseAction = (_, cancellation) => Task.Delay(800, cancellation);
    }
}
