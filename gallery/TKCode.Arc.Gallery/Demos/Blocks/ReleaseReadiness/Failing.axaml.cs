using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ReleaseReadinessFailingDemo : UserControl
{
    private int _runs;

    public ReleaseReadinessFailingDemo()
    {
        InitializeComponent();
        Gate.Checks.AddRange(ReleaseReadinessDemoData.RolloutChecks());
        Gate.Owners.AddRange(ReleaseReadinessDemoData.RolloutOwners());

        // The first rollout goes through; the next one is refused, and the one after that (the retry) goes through again.
        Gate.ReleaseAction = async (_, cancellation) =>
        {
            await Task.Delay(900, cancellation);
            _runs++;
            if (_runs % 2 == 0)
            {
                throw new InvalidOperationException(
                    "The rollout could not start: the build is still uploading. Try again in a minute.");
            }
        };

        Gate.StateChanged += (_, e) => Log.Text = $"{e.OldState} → {e.NewState}";
        Gate.ReleaseFailed += (_, e) => Log.Text = $"Attempt {e.Request.Attempt} failed: {e.Exception.GetType().Name}";
        Gate.Released += (_, e) => Log.Text = $"Started on attempt {e.Request.Attempt}, notify: {(e.Notify ? "yes" : "no")}";
    }
}
