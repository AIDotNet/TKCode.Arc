using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReleaseReadinessHandoffDemo : UserControl
{
    public ReleaseReadinessHandoffDemo()
    {
        InitializeComponent();
        Gate.Checks.AddRange(ReleaseReadinessDemoData.HandoffChecks());
        Gate.Owners.AddRange(ReleaseReadinessDemoData.HandoffOwners());
        Gate.ReleaseAction = (_, cancellation) => Task.Delay(700, cancellation);

        Gate.CheckChanged += (_, e) =>
            Log.Text = $"{e.Check.Title}: {(e.NewValue ? "done" : "open")} · {e.DoneCount} of {e.TotalCount} required";
        Gate.Released += (_, e) =>
            Log.Text = e.Request.SkippedChecks.Count == 0
                ? "Handed over with every check done"
                : $"Handed over; skipped: {string.Join(", ", e.Request.SkippedChecks.Select(c => c.Title))}";
    }
}
