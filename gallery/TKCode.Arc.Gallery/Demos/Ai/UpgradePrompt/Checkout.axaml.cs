using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UpgradePromptCheckoutDemo : UserControl
{
    private int _attempts;

    public UpgradePromptCheckoutDemo()
    {
        InitializeComponent();
        Prompt.UpgradeHandler = UpgradeAsync;
        Prompt.IntervalChanged += (_, e) => Log.Text = $"Billing: {e.Interval}.";
        Prompt.UpgradeFailed += (_, e) => Log.Text = $"Failed: {e.Error.Message}";
        Prompt.Upgraded += (_, e) =>
            Log.Text = $"Upgraded to {e.Selection.Plan.Name}, {e.Selection.Interval}, {e.Selection.Amount:0.##} {e.Selection.Currency} today (sample).";
        Prompt.Dismissed += (_, _) => Log.Text = "Snoozed until the limit resets.";
        Prompt.ContinueRequested += OnContinue;
    }

    // Your server charges the plan and interval here; this sample only waits and declines the first attempt.
    private async Task UpgradeAsync(UpgradePromptSelection selection, CancellationToken cancellationToken)
    {
        await Task.Delay(900, cancellationToken);
        if (_attempts++ == 0)
        {
            throw new InvalidOperationException("Your card was declined. Try another card.");
        }
    }

    private void OnContinue(object? sender, RoutedEventArgs e)
    {
        // A real app closes the prompt and retries the blocked action; the sample starts over.
        Log.Text = "Continue: close the prompt and retry the blocked action.";
        _attempts = 0;
    }
}
