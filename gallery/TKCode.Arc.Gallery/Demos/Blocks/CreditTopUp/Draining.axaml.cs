using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class CreditTopUpDrainingDemo : UserControl
{
    private const long Start = 1_820;
    private readonly DispatcherTimer _usage = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Random _random = new(7);

    public CreditTopUpDrainingDemo()
    {
        InitializeComponent();
        _usage.Tick += (_, _) =>
        {
            if (Running.IsChecked != true || Card.Phase == CreditTopUpPhase.Processing)
            {
                return;
            }
            var spent = _random.Next(30, 81);
            Card.Balance = Math.Max(0, Card.Balance - spent);
            Log.Text = $"Used {spent} credits · balance {Card.Balance:N0} · {(Card.Health == CreditTopUpHealth.RunningLow ? "running low" : "healthy")}";
        };
        Card.AutoTopUpChanged += (_, e) =>
            Log.Text = e.Settings.IsEnabled ? $"Auto refill on below {e.Settings.Threshold:N0}, adds {e.Settings.Credits:N0}" : "Auto refill off";
        Card.Purchased += (_, e) => Log.Text = $"Added {e.Order.Total:N0} credits · balance {e.Balance:N0}";
        // The timer runs only while the demo is on screen.
        AttachedToVisualTree += (_, _) => _usage.Start();
        DetachedFromVisualTree += (_, _) => _usage.Stop();
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        Card.Balance = Start;
        Log.Text = "Balance reset to 1,820";
    }
}
