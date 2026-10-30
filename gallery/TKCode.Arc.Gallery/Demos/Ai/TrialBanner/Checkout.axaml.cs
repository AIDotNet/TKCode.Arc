using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An app's own checkout handler (simulated here), with the events the banner raises.</summary>
public partial class TrialBannerCheckoutDemo : UserControl
{
    public TrialBannerCheckoutDemo()
    {
        InitializeComponent();
        TrialBannerDemoData.Apply(Banner);
        Banner.AddPaymentHandler = AddPaymentAsync;
        Banner.ComparePlansRequested += (_, e) =>
        {
            // A real app navigates to its plans page here.
            e.Handled = true;
            Log.Text = "Compare plans: the app opens its plans page.";
        };
        Banner.ManageBillingRequested += (_, e) =>
        {
            e.Handled = true;
            Log.Text = "Manage billing: the app opens its billing portal.";
        };
        Banner.Snoozed += (_, e) => Log.Text = $"Snoozed ({e.Option}) until {e.Until:MMM d}. Persist this and hide the banner until then.";
        Banner.Dismissed += (_, _) => Log.Text = "Dismissed. Persist this.";
    }

    /// <summary>Stands in for a call to your billing service: 1.2s, then success or a declined card.</summary>
    private async Task AddPaymentAsync(CancellationToken cancellation)
    {
        Log.Text = "Opening checkout…";
        await Task.Delay(TimeSpan.FromMilliseconds(1200), cancellation);
        if (Decline.IsChecked == true)
        {
            Log.Text = "The card was declined (sample).";
            throw new InvalidOperationException("The card was declined.");
        }
        Log.Text = "Payment method on file.";
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        Banner.IsUpgraded = false;
        Banner.IsCollapsed = false;
        Log.Text = "Nothing yet.";
    }
}
